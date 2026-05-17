using Fusion;
using Nymora.Core.Data;
using Nymora.Core.Enums;
using Nymora.Core.ScriptableObjects;
using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.3.c + 4.4.a — Avatar joueur networked Fusion (Shared Mode).
    ///
    /// Replication position (4.4.a) :
    /// - State Authority : HubMovementController fait le lerp local tile-par-tile.
    ///   Au end-of-step, SetGridPosition() pousse NetGridX/Y au reseau.
    /// - Non-State Authority : Render() interpole transform.position vers le world
    ///   calcule depuis NetGridX/Y (lerp smooth chaque frame).
    ///
    /// Brique 5.3.g.bis — Visual classe :
    /// - State Authority : init NetClassId depuis SelectedClassPreferences (PlayerPrefs)
    ///   au Spawn, et expose RefreshClassVisual() pour update live (Class Selector confirm)
    /// - Tous : ApplyClassVisual via NetClassId Changed callback (sync sprite + Animator
    ///   IdleAnimator de NymoraClassDefinition).
    /// </summary>
    public sealed class HubAvatar : NetworkBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Sprite _avatarSprite;
        [SerializeField] private int _baseSortingOrder = 100;

        [Header("Class visuals (5.3.g.bis — drag les 5 NymoraClassDefinition.asset)")]
        [SerializeField] private NymoraClassDefinition[] _classDefinitions;

        // 5.3.g.bis — SceneSpriteAnimator auto-add pour anim Idle pixel art (frames Sprite[]
        // extraites de l'AnimatorController Stage0_SE et stockees dans NymoraClassDefinition.IdleFrames).
        private SceneSpriteAnimator _spriteAnimator;

        // 5.3.g.bis — Facing 4 directions iso. Switch entre IdleFrames (SE) et IdleFramesNE (NE) +
        // flipX selon delta de mouvement. NW = NE mirror, SW = SE mirror.
        private enum HubFacing { SE, NE, SW, NW }
        private HubFacing _currentFacing = HubFacing.SE;
        private NymoraClassDefinition _currentClassDef;
        private int _prevGridXForFacing = int.MinValue;
        private int _prevGridYForFacing = int.MinValue;

        // Walk anim — detection mouvement via delta transform.position. Uniforme pour local
        // (lerp HubMovementController) et remote (interp NetWorldPos). _isWalking flippe quand
        // la position n'a pas bouge depuis _idleThresholdSec → ApplyFacingVisual re-Play
        // les frames Walk ou Idle.
        private const float _idleThresholdSec = 0.08f;
        private const float _moveSqrEpsilon = 0.0001f;
        private Vector3 _lastTrackedWorldPos;
        private float _lastMoveTime;
        private bool _isWalking;

        [Header("Spawn (State Authority only)")]
        [SerializeField] private int _spawnGridX = 10;
        [SerializeField] private int _spawnGridY = 10;

        [Header("Remote interpolation (non-State Authority)")]
        // 4.10.polish v4 — interval estimé entre 2 snapshots reçus (auto-mesuré au runtime).
        // Default 0.05s = 20 Hz. Sera écrasé dynamiquement par la mesure réelle.
        [SerializeField, Range(0.01f, 0.5f)] private float _fallbackSnapshotInterval = 0.05f;

        [Networked] public int NetGridX { get; set; }
        [Networked] public int NetGridY { get; set; }
        // 4.10.polish v3 — position world continue (push 60fps côté State Auth).
        // Source de vérité pour le mouvement remote. NetGridX/Y conservé pour sorting order
        // et sémantique tile (cliquable via HubInputController).
        [Networked] public Vector3 NetWorldPos { get; set; }
        // 4.8.b — sub backend pousse par State Auth au Spawn (depuis HubChatClient.MyUserId)
        // 4.11 hotfix — passe en _64 car les UUID (36 chars) etaient tronques a 16 par _16,
        // ce qui faisait echouer SEND_FRIEND_REQUEST et POST /clans/invite par UUID.
        [Networked] public NetworkString<_64> NetSub { get; set; }
        // 5.3.g.bis — Classe selectionnee (sync entre clients pour que les remotes voient
        // le bon sprite). _16 suffit pour "Soulrender"/"Nightseer"/"Colossar"/"Necram"/"Ghostra".
        [Networked, OnChangedRender(nameof(OnClassIdChanged))] public NetworkString<_16> NetClassId { get; set; }

        // 5.3.g.bis hotfix multi (17 mai nuit) — Facing sync direct State Auth -> remotes.
        // Sans ce field, le remote calculait son facing depuis le delta NetGridX/Y, mais
        // NetGridX/Y n'est pousse qu'au END-of-step (cf HubMovementController.Update ligne 56),
        // donc le pivot remote arrivait ~1 tile (~250ms a 4 t/s) APRES le debut du walk.
        // Maintenant la State Auth pousse NetFacing des que _currentFacing change (dans
        // TrackGridPositionForFacing, declenche par SetWorldPositionInterpolated des le 1er
        // frame du step) -> OnNetFacingChanged cote remote applique le pivot instantanement.
        // byte == cast direct de HubFacing (4 valeurs, SE=0/NE=1/SW=2/NW=3).
        [Networked, OnChangedRender(nameof(OnNetFacingChanged))] public byte NetFacing { get; set; }

        private SpriteRenderer _sr;
        // Transform du child "Visual" (cree par RestructureHubAvatarPrefabTool). Recoit le
        // Scale + Y offset per-class via ApplyClassVisual. Fallback sur transform root si
        // le prefab n'a pas encore ete restructure (ancien prefab mono-GO).
        private Transform _visualTransform;
        private HubGridRenderer _grid;
        private int _gridX;
        private int _gridY;

        // 4.10.polish v4 — snapshot interpolation manuelle (les [Networked] Vector3 sont sync au tickrate
        // Fusion ~30 Hz, donc Lerp asymptotique génère du stop-and-go entre 2 snapshots).
        // On tracke les 2 derniers snapshots reçus + leur timing pour interpoler LINÉAIREMENT.
        private Vector3 _prevNetWorldPos;
        private Vector3 _currentNetWorldPos;
        private float _lastSnapshotTime;
        private float _measuredSnapshotInterval = -1f;

        public int GridX => _gridX;
        public int GridY => _gridY;
        public HubGridRenderer Grid => _grid;
        public string Sub => NetSub.ToString();

        public static HubAvatar Local { get; private set; }

        public override void Spawned()
        {
            // SpriteRenderer peut etre sur le root (prefab legacy) OU sur le child "Visual"
            // (prefab restructure via RestructureHubAvatarPrefabTool pour pouvoir appliquer
            // Y offset + Scale per-class sans toucher au transform root tile-anchor).
            _sr = GetComponentInChildren<SpriteRenderer>(true);
            _visualTransform = _sr != null ? _sr.transform : transform;
            if (_sr != null && _avatarSprite != null) _sr.sprite = _avatarSprite;
            // 5.3.g.bis — reset color blanche pour pas teinter le sprite classe (Color tint
            // par player rendait tout rouge/HSV-flashy quand on cherche a voir le vrai
            // sprite de classe).
            if (_sr != null) _sr.color = Color.white;

            // 5.3.g.bis — SceneSpriteAnimator pour anim live des frames Idle de la classe
            // (les Animators combat ciblent un child path "Visual", pas le root).
            _spriteAnimator = GetComponent<SceneSpriteAnimator>();
            if (_spriteAnimator == null) _spriteAnimator = gameObject.AddComponent<SceneSpriteAnimator>();

            _grid = FindFirstObjectByType<HubGridRenderer>();
            if (_grid == null)
            {
                Debug.LogError("[HubAvatar] HubGridRenderer introuvable dans la scene.");
                return;
            }

            if (Object.HasStateAuthority)
            {
                Local = this;
                NetGridX = _spawnGridX;
                NetGridY = _spawnGridY;
                // 4.8.b — push notre sub backend pour que les remotes puissent nous adresser.
                // Race : si WELCOME backend pas encore recu, on bind OnWelcome pour push retardé.
                AssignSubFromChatClient();
                SetGridPosition(_spawnGridX, _spawnGridY);
                // 4.10.polish v3 — init NetWorldPos pour que les remotes se positionnent direct au bon endroit.
                NetWorldPos = transform.position;
                // 5.3.g.bis — push la classe choisie via PlayerPrefs (sync vers remotes pour leur sprite)
                NetClassId = SelectedClassPreferences.Get();
                // 5.3.g.bis hotfix multi — init NetFacing explicite (SE=0 par defaut, evite
                // d'avoir un remote qui lit 0 par defaut sans qu'on ait jamais set la valeur).
                NetFacing = (byte)_currentFacing;
                Debug.Log($"[HubAvatar] Local spawned at ({_spawnGridX},{_spawnGridY}) sub='{NetSub}' class='{NetClassId}' facing='{_currentFacing}'");
            }
            else
            {
                // Init transform depuis Networked
                _gridX = NetGridX;
                _gridY = NetGridY;
                if (NetWorldPos != Vector3.zero)
                {
                    transform.position = NetWorldPos;
                }
                else
                {
                    ApplyTransform();
                }
                // 4.10.polish v4 — init snapshot interp buffers à la position de spawn
                _prevNetWorldPos = transform.position;
                _currentNetWorldPos = transform.position;
                _lastSnapshotTime = Time.time;
                if (_sr != null) _sr.sortingOrder = IsoProjection.SortingOrderFor(_gridX, _gridY, _baseSortingOrder);
                // 5.3.g.bis hotfix multi — restituer le facing courant de la State Auth au
                // moment du spawn remote (un joueur qui rejoint en cours doit voir le bon
                // facing initial des autres). ApplyClassVisual plus bas applique ApplyFacingVisual.
                _currentFacing = (HubFacing)NetFacing;
                Debug.Log($"[HubAvatar] Remote avatar spawned (player {Object.InputAuthority}) at ({NetGridX},{NetGridY}) sub='{NetSub}' class='{NetClassId}' facing='{_currentFacing}'");
            }

            // Init walk-detection : marque la position de spawn comme "deja vue" et fait
            // remonter _lastMoveTime loin dans le passe pour eviter un flicker walk -> idle
            // dans les ~80ms qui suivent le Spawn.
            _lastTrackedWorldPos = transform.position;
            _lastMoveTime = -1000f;
            _isWalking = false;

            // 5.3.g.bis — Apply class visual (local + remote) maintenant que NetClassId est sync
            ApplyClassVisual();
        }

        /// <summary>
        /// 5.3.g.bis — Refresh visuel de la classe : lookup NymoraClassDefinition + apply
        /// facing courant (frames SE/NE + flipX selon direction). Reset color blanche pour
        /// pas teinter. Called from Spawned + OnChangedRender(NetClassId).
        /// </summary>
        private void ApplyClassVisual()
        {
            if (_sr == null)
            {
                _sr = GetComponentInChildren<SpriteRenderer>(true);
                if (_sr != null && _visualTransform == null) _visualTransform = _sr.transform;
            }
            if (_sr != null) _sr.color = Color.white;
            if (_classDefinitions == null || _classDefinitions.Length == 0) return;

            string classIdStr = NetClassId.ToString();
            if (string.IsNullOrEmpty(classIdStr)) return;
            if (!System.Enum.TryParse(classIdStr, out NymoraClass cls)) return;

            NymoraClassDefinition def = null;
            foreach (var d in _classDefinitions)
            {
                if (d != null && d.ClassId == cls) { def = d; break; }
            }
            if (def == null) return;
            _currentClassDef = def;

            // Port calibration combat -> hub : Y offset + Scale appliques au child Visual
            // (jamais au transform root, qui est l'ancre tile pilotee par HubMovementController).
            // Fallback no-op si on tourne sur l'ancien prefab mono-GO (Visual == root) pour
            // eviter de bouger l'avatar entier hors de sa tile.
            if (_visualTransform != null && _visualTransform != transform)
            {
                float scale = def.HubVisualScale > 0f ? def.HubVisualScale : 1f;
                Vector3 lp = _visualTransform.localPosition;
                _visualTransform.localPosition = new Vector3(lp.x, def.HubVisualYOffset, lp.z);
                _visualTransform.localScale = new Vector3(scale, scale, 1f);
            }

            ApplyFacingVisual();
        }

        /// <summary>
        /// 5.3.g.bis — Lance l'anim selon _currentFacing : frames SE (face) ou NE (dos),
        /// + flipX si direction-mirror (NW = NE mirror, SW = SE mirror).
        /// Selection Walk vs Idle frames selon _isWalking (detection mouvement via delta
        /// transform.position dans Update). Fallback Idle si WalkFrames pas peuples.
        /// </summary>
        private void ApplyFacingVisual()
        {
            if (_currentClassDef == null) return;
            if (_sr == null) _sr = GetComponentInChildren<SpriteRenderer>(true);
            if (_sr == null) return;

            // Mapping iso classique :
            //   SE = frames SE no flip   |  NE = frames NE no flip
            //   SW = frames SE flipX     |  NW = frames NE flipX
            bool useNE = (_currentFacing == HubFacing.NE || _currentFacing == HubFacing.NW);
            bool flipX = (_currentFacing == HubFacing.SW || _currentFacing == HubFacing.NW);

            Sprite[] frames;
            float fps;
            if (_isWalking)
            {
                frames = useNE ? _currentClassDef.WalkFramesNE : _currentClassDef.WalkFrames;
                fps = _currentClassDef.WalkFps;
                // Fallback Idle si Walk pas peuple (asset designer pas encore livre).
                if (frames == null || frames.Length == 0)
                {
                    frames = useNE ? _currentClassDef.IdleFramesNE : _currentClassDef.IdleFrames;
                    fps = _currentClassDef.IdleFps;
                }
            }
            else
            {
                frames = useNE ? _currentClassDef.IdleFramesNE : _currentClassDef.IdleFrames;
                fps = _currentClassDef.IdleFps;
            }

            if (frames != null && frames.Length > 0 && _spriteAnimator != null)
            {
                _spriteAnimator.Play(_sr, frames, fps);
            }
            else if (_currentClassDef.PortraitSprite != null)
            {
                if (_spriteAnimator != null) _spriteAnimator.Stop();
                _sr.sprite = _currentClassDef.PortraitSprite;
            }
            _sr.flipX = flipX;
        }

        /// <summary>
        /// 5.3.g.bis — Compute facing depuis le delta grid (dx, dy). Iso 4 directions,
        /// convention IDENTIQUE a Quantum.FacingHelpers.FacingFromGridDelta (combat) :
        /// projection world (dxWorld = dx-dy, dyWorld = dx+dy) -> classement quadrant.
        ///   (+1, 0) -> NE   (-1, 0) -> SW   (0, +1) -> NW   (0, -1) -> SE.
        /// Le pathfinder hub est 4-connexite donc pas de diagonales en pratique, mais
        /// la formule gere les diagonales proprement aussi (axe dominant via signe).
        /// </summary>
        private static HubFacing ComputeFacingFromDelta(int dx, int dy)
        {
            int dxWorld = dx - dy;
            int dyWorld = dx + dy;
            bool east = dxWorld >= 0;
            bool north = dyWorld >= 0;
            if (east && north) return HubFacing.NE;
            if (east && !north) return HubFacing.SE;
            if (!east && north) return HubFacing.NW;
            return HubFacing.SW;
        }

        /// <summary>
        /// Apply un nouveau facing : update _currentFacing, replay anim, et push NetFacing
        /// si State Authority pour sync immediat les remotes (cf [[project-hub-multi-instance-test-pending]]).
        /// </summary>
        private void ApplyAndPushFacing(HubFacing newFacing)
        {
            if (newFacing == _currentFacing) return;
            _currentFacing = newFacing;
            ApplyFacingVisual();
            // 5.3.g.bis hotfix multi — push facing reseau cote State Auth des le moment du
            // flip (1er frame du step ou avant via PrimeFacingForNextStep). Le remote recoit
            // OnNetFacingChanged et pivote instant.
            if (Object != null && Object.HasStateAuthority)
            {
                NetFacing = (byte)newFacing;
            }
        }

        private void TrackGridPositionForFacing()
        {
            if (_prevGridXForFacing == int.MinValue)
            {
                _prevGridXForFacing = _gridX;
                _prevGridYForFacing = _gridY;
                return;
            }
            int dx = _gridX - _prevGridXForFacing;
            int dy = _gridY - _prevGridYForFacing;
            if (dx == 0 && dy == 0) return;
            _prevGridXForFacing = _gridX;
            _prevGridYForFacing = _gridY;

            ApplyAndPushFacing(ComputeFacingFromDelta(dx, dy));
        }

        /// <summary>
        /// 5.3.g.bis polish multi (17 mai nuit suite) — Push le facing AU MOMENT DU CLIC,
        /// avant meme que HubMovementController.BeginNextStep se declenche (frame suivante).
        /// Appele par HubInputController apres avoir calcule le path. Gain ~16-50ms cote
        /// remote car NetFacing peut maintenant rattraper le prochain tick Fusion meme s'il
        /// vient juste de partir, au lieu d'attendre le 1er frame du step.
        /// </summary>
        public void PrimeFacingForNextStep(int nextGx, int nextGy)
        {
            int dx = nextGx - _gridX;
            int dy = nextGy - _gridY;
            if (dx == 0 && dy == 0) return;
            ApplyAndPushFacing(ComputeFacingFromDelta(dx, dy));
        }

        /// <summary>
        /// 5.3.g.bis hotfix multi — Callback OnChangedRender NetFacing. Set par State Auth
        /// dans TrackGridPositionForFacing au moment du flip. Cote remote, on applique direct
        /// le facing recu (skip si HasStateAuthority puisque deja applique localement).
        /// </summary>
        private void OnNetFacingChanged()
        {
            if (Object != null && Object.HasStateAuthority) return;
            var f = (HubFacing)NetFacing;
            if (f == _currentFacing) return;
            _currentFacing = f;
            ApplyFacingVisual();
        }

        /// <summary>
        /// Detection mouvement via delta transform.position chaque frame. Uniforme local
        /// (lerp HubMovementController.Update) + remote (interp NetWorldPos dans Render).
        /// Quand l'avatar n'a pas bouge depuis _idleThresholdSec, on flippe _isWalking
        /// vers false → ApplyFacingVisual swap vers IdleFrames.
        /// </summary>
        private void Update()
        {
            if (_currentClassDef == null) return;
            Vector3 curPos = transform.position;
            if (Vector3.SqrMagnitude(curPos - _lastTrackedWorldPos) > _moveSqrEpsilon)
            {
                _lastMoveTime = Time.time;
                _lastTrackedWorldPos = curPos;
            }
            bool walking = (Time.time - _lastMoveTime) < _idleThresholdSec;
            if (walking != _isWalking)
            {
                _isWalking = walking;
                ApplyFacingVisual();
            }
        }

        /// <summary>OnChanged callback Networked NetClassId : re-apply visual sur tous les clients.</summary>
        private void OnClassIdChanged()
        {
            ApplyClassVisual();
        }

        /// <summary>
        /// 5.3.g.bis — API publique pour changer la classe locale (appele depuis le
        /// HubClassSelectorPanel.ConfirmSelection). Save PlayerPrefs + push NetClassId
        /// (qui re-sync les remotes via OnChangedRender).
        /// </summary>
        public void SetClassFromLocalChoice(string classId)
        {
            if (string.IsNullOrEmpty(classId)) return;
            SelectedClassPreferences.Set(classId);
            if (Object != null && Object.HasStateAuthority)
            {
                NetClassId = classId;
                ApplyClassVisual(); // local immediate (au cas où OnChangedRender n'est pas déclenchée pour soi)
            }
        }

        // 4.4.b — Couleur deterministe par joueur pour distinguer self/other en multi-instance.
        // HSV based sur hash(InputAuthority) -> teinte unique stable cote A et cote B.
        private static Color ColorForPlayer(PlayerRef player)
        {
            int hash = player.RawEncoded;
            float h = (Mathf.Abs(hash) % 360) / 360f;
            return Color.HSVToRGB(h, 0.7f, 0.95f);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (HubChatClient.Instance != null) HubChatClient.Instance.OnWelcome -= HandleWelcomePostSpawn;
            if (Local == this) Local = null;
        }

        private void AssignSubFromChatClient()
        {
            var client = HubChatClient.Instance;
            if (client == null) return;
            if (!string.IsNullOrEmpty(client.MyUserId))
            {
                NetSub = client.MyUserId;
                return;
            }
            // WELCOME pas encore arrivé : on s'abonne pour push retardé
            client.OnWelcome += HandleWelcomePostSpawn;
        }

        private void HandleWelcomePostSpawn(string sub, string email)
        {
            if (HubChatClient.Instance != null) HubChatClient.Instance.OnWelcome -= HandleWelcomePostSpawn;
            if (Object != null && Object.HasStateAuthority && !string.IsNullOrEmpty(sub))
            {
                NetSub = sub;
                Debug.Log($"[HubAvatar] NetSub assigne post-spawn via WELCOME : '{sub}'");
            }
        }

        public override void Render()
        {
            if (Object == null || _grid == null) return;

            if (Object.HasStateAuthority)
            {
                // 4.10.polish v3 — push notre world position à chaque frame.
                // (Fusion ne sync au réseau qu'au tickrate ~30 Hz, mais ça suffit avec
                // l'interpolation snapshot côté remote.)
                NetWorldPos = transform.position;
                return;
            }

            // Remote — snapshot interpolation manuelle (4.10.polish v4)
            // Détection nouveau snapshot reçu
            if (NetWorldPos != _currentNetWorldPos)
            {
                float now = Time.time;
                // 4.10.polish v4.1 — Au tout 1er snapshot après spawn (interval encore inconnu),
                // on SNAP direct au lieu de lerp depuis la position de spawn (sinon glissade
                // initiale visible). Les snapshots suivants utilisent l'interp linéaire normale.
                if (_measuredSnapshotInterval < 0f)
                {
                    _prevNetWorldPos = NetWorldPos;
                    _currentNetWorldPos = NetWorldPos;
                    transform.position = NetWorldPos;
                    // On note un interval initial probable (≈ 1 tick Fusion Shared Mode 30 Hz)
                    _measuredSnapshotInterval = 0.033f;
                }
                else
                {
                    _prevNetWorldPos = _currentNetWorldPos;
                    _currentNetWorldPos = NetWorldPos;
                    var measured = now - _lastSnapshotTime;
                    // Filtrage doux (running average 70/30) pour absorber le jitter
                    _measuredSnapshotInterval = _measuredSnapshotInterval * 0.7f + measured * 0.3f;
                }
                _lastSnapshotTime = now;
            }

            // Interpolation linéaire entre prev et current selon le temps écoulé
            float interval = _measuredSnapshotInterval > 0f ? _measuredSnapshotInterval : _fallbackSnapshotInterval;
            float elapsed = Time.time - _lastSnapshotTime;
            float t = Mathf.Clamp01(elapsed / interval);
            transform.position = Vector3.Lerp(_prevNetWorldPos, _currentNetWorldPos, t);

            _gridX = NetGridX;
            _gridY = NetGridY;
            if (_sr != null) _sr.sortingOrder = IsoProjection.SortingOrderFor(_gridX, _gridY, _baseSortingOrder);
            // 5.3.g.bis hotfix multi — pas de TrackGridPositionForFacing cote remote :
            // NetGridX/Y push au end-of-step donnerait un facing en retard d'~1 tile.
            // Le facing remote vient de OnNetFacingChanged (push State Auth des le 1er frame).
            // On garde quand meme _prev*ForFacing a jour pour eviter un fake-delta si jamais
            // le tracker etait reactive plus tard (defensif).
            _prevGridXForFacing = _gridX;
            _prevGridYForFacing = _gridY;
        }

        public void SetGridPosition(int gx, int gy)
        {
            _gridX = gx;
            _gridY = gy;
            if (Object != null && Object.HasStateAuthority)
            {
                NetGridX = gx;
                NetGridY = gy;
            }
            ApplyTransform();
            TrackGridPositionForFacing();
        }

        public void SetWorldPositionInterpolated(int gx, int gy, Vector3 worldPos)
        {
            // Local lerp (State Authority) : on ne pousse PAS NetGridX/Y a chaque frame, seulement
            // au end-of-step via SetGridPosition (trafic reseau leger : 4 updates/sec a 4 tiles/sec).
            _gridX = gx;
            _gridY = gy;
            transform.position = worldPos;
            if (_sr != null) _sr.sortingOrder = IsoProjection.SortingOrderFor(_gridX, _gridY, _baseSortingOrder);
            TrackGridPositionForFacing();
        }

        private void ApplyTransform()
        {
            if (_grid == null) return;
            Vector3 worldPos = IsoProjection.GridToWorld(_gridX, _gridY, _grid.TileWorldWidth, _grid.TileWorldHeight) + _grid.CenterOffset;
            transform.position = worldPos;
            if (_sr != null) _sr.sortingOrder = IsoProjection.SortingOrderFor(_gridX, _gridY, _baseSortingOrder);
        }
    }
}
