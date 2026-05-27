using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using Nymora.Core.Data;
using Nymora.Core.Enums;
using Nymora.Core.ScriptableObjects;
using Nymora.Network.Backend;
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

        [Tooltip("Sorting layer du perso (corps + ombre). Doit etre AU-DESSUS de 'Default' dans " +
                 "la liste des Sorting Layers, et exclu des Target Sorting Layers des Torch Lights " +
                 "(seule la Global Light doit le cibler). Permet au perso de ne plus etre lave par " +
                 "les torches tout en s'iso-triant avec le sprite torche (meme layer, meme base).")]
        [SerializeField] private string _hubSortingLayer = "Personnages";

        [Header("Class visuals (5.3.g.bis — drag les 5 NymoraClassDefinition.asset)")]
        [SerializeField] private NymoraClassDefinition[] _classDefinitions;

        [Header("Cosmetic skins (5.5.e — drag les CosmeticSkinDefinition.asset)")]
        [SerializeField] private CosmeticSkinDefinition[] _skinDefinitions;
        [Tooltip("Pour fetch l'inventaire et résoudre le skin équipé (avatar local uniquement).")]
        [SerializeField] private NymoraBackendSettings _backendSettings;

        [Header("Emotes (E2 — drag EmoteCatalog.asset ; assigné aussi par Import Emotes)")]
        [Tooltip("Résout l'émote reçue par RPC (emoteId -> sprite) pour afficher la bulle sur cet avatar.")]
        [SerializeField] private EmoteCatalog _emoteCatalog;

        // 5.3.g.bis — SceneSpriteAnimator auto-add pour anim Idle pixel art (frames Sprite[]
        // extraites de l'AnimatorController Stage0_SE et stockees dans NymoraClassDefinition.IdleFrames).
        private SceneSpriteAnimator _spriteAnimator;

        // 5.3.g.bis — Facing 4 directions iso. Switch entre IdleFrames (SE) et IdleFramesNE (NE) +
        // flipX selon delta de mouvement. NW = NE mirror, SW = SE mirror.
        private enum HubFacing { SE, NE, SW, NW }
        private HubFacing _currentFacing = HubFacing.SE;
        // Index de la direction courante (0=SE, 1=NE, 2=SW, 3=NW). Utilisé par le tuner de
        // placement du familier pour surligner le bloc actif. Outil dev uniquement.
        public int CurrentFacingIndex => (int)_currentFacing;
        private NymoraClassDefinition _currentClassDef;
        // 5.5.e — skin equipe pour la classe courante (local only). Si non-null, l'avatar joue
        // SES frames au lieu de celles de _currentClassDef.
        private CosmeticSkinDefinition _currentSkinDef;
        private NymoraApiClient _skinApi;
        // 5.10 (B4) — familier équipé + sa vue (suiveur). Résolu via PetCatalog (Resources).
        private PetDefinition _currentPetDef;
        private HubPetView _petView;
        // Dernier facing avec lequel le familier a été piloté. Sert à détecter un pivot sur place :
        // si le facing change alors que l'avatar est à l'arrêt, le familier se TÉLÉPORTE à sa
        // nouvelle ancre (sinon il "marchait" pour aller de l'autre côté). En marche -> traîne.
        private int _petLastFacingIndex = int.MinValue;
        private static PetCatalog _petCatalog;
        private static bool _petCatalogLoaded;
        // Ancre du familier près de l'avatar : offset face (SE/SW) vs dos (NE/NW) lu dans
        // PetPlacementConfig (réglable en Play Mode via HubPetPlacementTuner). Rendu devant
        // (ownerOrder+1 dans HubPetView).
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

        // 5.5.f — Skin cosmetique equipe (cosmeticId backend), sync entre clients pour que les
        // remotes voient le skin sur l'avatar. "" = aucun skin (frames de classe de base).
        // _64 car les ids sont longs (ex "skin_soulrender_ashen_sovereign" = 31 chars).
        // AJOUT DE CE [Networked] FIELD => regen prefab + scene + rebuild standalone obligatoire
        // (cf feedback-networked-field-regen-protocol, sinon InvalidOperationException Invalid Length).
        [Networked, OnChangedRender(nameof(OnSkinIdChanged))] public NetworkString<_64> NetSkinId { get; set; }

        // 5.10 (B4) — Familier équipé (cosmeticId backend), sync entre clients pour que les remotes
        // voient le familier accompagner l'avatar. "" = aucun familier. _64 (ids courts type "pet_*").
        // AJOUT DE CE [Networked] FIELD => regen prefab + scene + rebuild standalone obligatoire
        // (cf feedback-networked-field-regen-protocol, sinon InvalidOperationException Invalid Length).
        [Networked, OnChangedRender(nameof(OnPetIdChanged))] public NetworkString<_64> NetPetId { get; set; }

        // Titre cosmetique equipe (texte deja "propre", ex "l'Inebranlable"), sync entre clients
        // pour la 3e ligne du HubAvatarHoverTooltip (clan / pseudo / titre). "" = aucun titre.
        // Resolu depuis l'inventaire dans RefreshEquippedSkinAsync (cosmetique type "title" equipe).
        // _64 chars (les titres sont courts mais on garde de la marge).
        // AJOUT DE CE [Networked] FIELD => regen prefab + scene + rebuild standalone obligatoire
        // (cf feedback-networked-field-regen-protocol, sinon InvalidOperationException Invalid Length).
        [Networked] public NetworkString<_64> NetTitle { get; set; }

        // Bannière cosmétique (emblème) équipée, SYNC entre clients pour que les remotes voient
        // l'emblème au-dessus de la plaque dans HubAvatarHoverTooltip (avant : local-only via un
        // static LocalEquippedBannerId, donc invisible pour les autres). Résolue depuis l'inventaire
        // dans RefreshEquippedSkinAsync (avatar local / State Authority). "" = aucune bannière.
        // _64 chars (les cosmeticId de bannières sont courts, ex "banner_couronne", marge gardée).
        // AJOUT DE CE [Networked] FIELD => regen prefab + scene + rebuild standalone obligatoire
        // (cf feedback-networked-field-regen-protocol, sinon InvalidOperationException Invalid Length).
        [Networked] public NetworkString<_64> NetBanner { get; set; }

        // 5.3.g.bis hotfix multi (17 mai nuit) — Facing sync direct State Auth -> remotes.
        // Sans ce field, le remote calculait son facing depuis le delta NetGridX/Y, mais
        // NetGridX/Y n'est pousse qu'au END-of-step (cf HubMovementController.Update ligne 56),
        // donc le pivot remote arrivait ~1 tile (~250ms a 4 t/s) APRES le debut du walk.
        // Maintenant la State Auth pousse NetFacing des que _currentFacing change (dans
        // TrackGridPositionForFacing, declenche par SetWorldPositionInterpolated des le 1er
        // frame du step) -> OnNetFacingChanged cote remote applique le pivot instantanement.
        // byte == cast direct de HubFacing (4 valeurs, SE=0/NE=1/SW=2/NW=3).
        [Networked, OnChangedRender(nameof(OnNetFacingChanged))] public byte NetFacing { get; set; }

        // 19 mai — Nom du clan du joueur, sync entre clients pour que les remotes voient le clan
        // sur le tooltip d'avatar hover. State Auth pousse depuis HubClanPanel.MyClanName au Spawn,
        // re-push si l'event OnClanStateChanged trigger (join/quit clan en cours de session).
        // Vide ("") = pas de clan. _32 chars max (suffisant : noms clans plus longs sont rares).
        [Networked] public NetworkString<_32> NetClanName { get; set; }

        // POLISH-7 (20 mai) — Pseudo officiel (Profile.displayName) sync entre clients pour que
        // les remotes voient le bon pseudo dans HubAvatarHoverTooltip. State Auth pousse depuis
        // HubChatClient.MyDisplayName au Spawn (ou via OnWelcome retarde si WELCOME pas encore recu).
        // _32 chars max (largement suffisant pour un pseudo unique en DB).
        [Networked] public NetworkString<_32> NetDisplayName { get; set; }

        // E1 — bulle d'emote (View) posee lazy sur le root avatar. Pas de [Networked] ici : en E1
        // c'est purement local (la diffusion reseau aux remotes viendra en E2 via RPC Fusion).
        private EmoteBubbleView _emoteBubble;

        // C1 — bulle de chat (View) posee lazy. Alimentee par HubChatBubbleRouter sur reception
        // d'un CHANNEL_MESSAGE global (deja diffuse a tous par le backend -> pas de RPC).
        private ChatBubbleView _chatBubble;

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

        // C1 — Registre de TOUS les avatars hub (local + distants), pour router une bulle de chat
        // vers le bon perso via le displayName de l'expediteur. Peuple au Spawn, retire au Despawn.
        public static readonly List<HubAvatar> All = new List<HubAvatar>();

        /// <summary>Pseudo officiel sync (NetDisplayName) — sert au matching des bulles de chat.</summary>
        public string DisplayName => NetDisplayName.ToString();

        public override void Spawned()
        {
            // SpriteRenderer peut etre sur le root (prefab legacy) OU sur le child "Visual"
            // (prefab restructure via RestructureHubAvatarPrefabTool pour pouvoir appliquer
            // Y offset + Scale per-class sans toucher au transform root tile-anchor).
            _sr = GetComponentInChildren<SpriteRenderer>(true);
            _visualTransform = _sr != null ? _sr.transform : transform;
            ApplyHubSortingLayer();
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
                // 19 mai — push le nom du clan pour les remotes (tooltip hover). Re-push automatique
                // via HubClanPanel.OnClanStateChanged si Lorenzo rejoint/quitte un clan en cours.
                PushClanName();
                if (HubClanPanel.Instance != null) HubClanPanel.Instance.OnClanStateChanged += PushClanName;
                Debug.Log($"[HubAvatar] Local spawned at ({_spawnGridX},{_spawnGridY}) sub='{NetSub}' class='{NetClassId}' facing='{_currentFacing}' clan='{NetClanName}'");
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

            // 5.3.g.bis — Apply class visual (local + remote) maintenant que NetClassId est sync.
            // 5.5.f — remote : applique le skin deja sync via NetSkinId. Local : NetSkinId vide
            // au spawn, RefreshEquippedSkin fetch l'inventaire et push NetSkinId juste apres.
            _currentSkinDef = FindSkinDef(NetSkinId.ToString());
            ApplyClassVisual();
            RefreshEquippedSkin();

            // 5.10 (B5) — applique le familier déjà sync via NetPetId. Indispensable pour les
            // avatars DISTANTS : OnPetIdChanged (OnChangedRender) ne se déclenche qu'au CHANGEMENT,
            // pas à l'arrivée -> sans ça l'observateur ne voit jamais le familier d'un joueur déjà
            // équipé. Local : NetPetId vide au spawn -> no-op, RefreshEquippedSkin le posera juste après.
            _currentPetDef = FindPetDef(NetPetId.ToString());
            ApplyPetVisual();

            // C1 — inscrit l'avatar au registre (local + distants) pour le routage des bulles de chat.
            if (!All.Contains(this)) All.Add(this);
        }

        /// <summary>
        /// Place TOUS les SpriteRenderers du perso (corps "Visual" + "ShadowBlob") sur le
        /// sorting layer dedie. Le perso quitte ainsi "Default" -> n'est plus eclaire par les
        /// Torch Lights (qui ne ciblent que Default), seulement par la Global Light. L'iso-tri
        /// avec le sprite torche reste correct car IsoDepthSort pose le sprite torche sur le
        /// MEME layer avec la meme base order. View-only, aucun impact sim.
        /// </summary>
        private void ApplyHubSortingLayer()
        {
            if (string.IsNullOrEmpty(_hubSortingLayer)) return;
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r != null) r.sortingLayerName = _hubSortingLayer;
            }
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

            // 5.5.e — un skin equipe pour une AUTRE classe ne s'applique pas a la classe courante.
            if (_currentSkinDef != null && _currentSkinDef.ClassId != cls) _currentSkinDef = null;

            // Port calibration combat -> hub : Y offset + Scale appliques au child Visual
            // (jamais au transform root, qui est l'ancre tile pilotee par HubMovementController).
            // Fallback no-op si on tourne sur l'ancien prefab mono-GO (Visual == root) pour
            // eviter de bouger l'avatar entier hors de sa tile. Le skin override la calibration.
            if (_visualTransform != null && _visualTransform != transform)
            {
                float rawScale = _currentSkinDef != null ? _currentSkinDef.HubVisualScale : def.HubVisualScale;
                float yOff = _currentSkinDef != null ? _currentSkinDef.HubVisualYOffset : def.HubVisualYOffset;
                float scale = rawScale > 0f ? rawScale : 1f;
                Vector3 lp = _visualTransform.localPosition;
                _visualTransform.localPosition = new Vector3(lp.x, yOff, lp.z);
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

            // 5.5.e — source des frames : le skin equipe override la classe.
            bool useSkin = _currentSkinDef != null;
            Sprite[] idleSE = useSkin ? _currentSkinDef.IdleFrames : _currentClassDef.IdleFrames;
            Sprite[] idleNE = useSkin ? _currentSkinDef.IdleFramesNE : _currentClassDef.IdleFramesNE;
            Sprite[] walkSE = useSkin ? _currentSkinDef.WalkFrames : _currentClassDef.WalkFrames;
            Sprite[] walkNE = useSkin ? _currentSkinDef.WalkFramesNE : _currentClassDef.WalkFramesNE;
            float idleFpsV = useSkin ? _currentSkinDef.IdleFps : _currentClassDef.IdleFps;
            float walkFpsV = useSkin ? _currentSkinDef.WalkFps : _currentClassDef.WalkFps;

            Sprite[] frames;
            float fps;
            if (_isWalking)
            {
                frames = useNE ? walkNE : walkSE;
                fps = walkFpsV;
                // Fallback Idle si Walk pas peuple.
                if (frames == null || frames.Length == 0)
                {
                    frames = useNE ? idleNE : idleSE;
                    fps = idleFpsV;
                }
            }
            else
            {
                frames = useNE ? idleNE : idleSE;
                fps = idleFpsV;
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
            // 19 mai — Poll clan sync (1x/sec) si State Auth. Robuste a la race condition
            // HubClanPanel.Instance null au Spawned() ou fetch async HandleWelcome pas fini.
            if (Object != null && Object.HasStateAuthority && Time.frameCount % 60 == 0)
            {
                SyncClanNameIfChanged();
            }

            // 5.10 (B4) — le familier suit l'avatar (indépendant de la classe).
            DrivePet();

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
        /// 5.5.f — OnChanged callback NetSkinId. Cote remote : resout le skin depuis l'id reseau
        /// et re-applique le visuel. Skip si State Authority (deja applique localement dans
        /// RefreshEquippedSkinAsync). FindSkinDef("") -> null -> retour aux frames de classe.
        /// </summary>
        private void OnSkinIdChanged()
        {
            if (Object != null && Object.HasStateAuthority) return;
            _currentSkinDef = FindSkinDef(NetSkinId.ToString());
            ApplyClassVisual();
        }

        /// <summary>
        /// 5.10 (B4) — OnChanged callback NetPetId. Côté remote : résout le familier depuis l'id
        /// réseau et (dé)spawn la vue suiveuse. Skip si State Authority (déjà appliqué dans
        /// RefreshEquippedSkinAsync). FindPetDef("") -> null -> familier retiré.
        /// </summary>
        private void OnPetIdChanged()
        {
            if (Object != null && Object.HasStateAuthority) return;
            _currentPetDef = FindPetDef(NetPetId.ToString());
            ApplyPetVisual();
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
                RefreshEquippedSkin(); // re-resoudre le skin equipe pour la nouvelle classe
            }
        }

        /// <summary>
        /// 5.5.e/f — Resout le skin equipe pour la classe active et le pousse sur NetSkinId
        /// (sync reseau -> les remotes l'appliquent via OnSkinIdChanged). Appele : au Spawn
        /// local, au changement de classe, et apres equiper/desequiper (HubProfilePanel).
        /// State Authority uniquement (les remotes recoivent via NetSkinId).
        /// </summary>
        public void RefreshEquippedSkin()
        {
            if (Object == null || !Object.HasStateAuthority) return;
            RefreshEquippedSkinAsync().Forget();
        }

        private async UniTaskVoid RefreshEquippedSkinAsync()
        {
            if (_backendSettings == null) return;
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token)) return;
            if (_skinApi == null) _skinApi = new NymoraApiClient(_backendSettings);
            _skinApi.SetBearerToken(token);

            string cls = NetClassId.ToString();
            // activeClass -> le familier équipé renvoyé est celui CHOISI pour cette classe (par-classe).
            var res = await _skinApi.GetInventoryAsync(cls);
            if (!res.IsSuccess) return;

            string equippedId = "";
            string equippedTitle = "";
            string equippedBanner = "";
            string equippedPet = "";
            if (res.Data.items != null)
            {
                foreach (var it in res.Data.items)
                {
                    if (it == null || !it.equipped) continue;
                    if (it.type == "skin" && it.classLock == cls && string.IsNullOrEmpty(equippedId))
                        equippedId = it.id;
                    // Titre : pas de class-lock, un seul equipe a la fois. Texte "propre" extrait du nom.
                    else if (it.type == "title" && string.IsNullOrEmpty(equippedTitle))
                        equippedTitle = ExtractTitleText(it.name);
                    // Banniere : pas de class-lock, un seul equipe a la fois (cosmeticId brut = cle asset).
                    else if (it.type == "banner" && string.IsNullOrEmpty(equippedBanner))
                        equippedBanner = it.id;
                    // 5.10 (B4) — Familier : pas de class-lock, un seul equipe (cosmeticId = cle asset).
                    else if (it.type == "pet" && string.IsNullOrEmpty(equippedPet))
                        equippedPet = it.id;
                }
            }
            // 5.5.f — push reseau (les remotes recoivent OnSkinIdChanged) + applique en local.
            if (Object != null && Object.HasStateAuthority)
            {
                NetSkinId = equippedId;
                NetTitle = equippedTitle; // sync titre pour le tooltip (3e ligne)
                // Sync l'emblème de bannière : les remotes le voient désormais sur le tooltip
                // (avant c'était un static local-only invisible pour les autres).
                NetBanner = equippedBanner;
                NetPetId = equippedPet; // 5.10 (B4) — familier sync aux remotes
            }
            _currentSkinDef = FindSkinDef(equippedId);
            ApplyClassVisual();

            // 5.10 (B4) — familier équipé : résout + (dé)spawn la vue suiveuse.
            _currentPetDef = FindPetDef(equippedPet);
            ApplyPetVisual();
            // Pont hub → combat : familier local pour l'affichage en combat (B5).
            Nymora.Core.Data.CombatCosmeticsContext.LocalPetId = equippedPet;

            // 5.10 (A2) — Pont hub → combat : mémorise le skin équipé local pour que le combat
            // (qui ne référence pas le réseau) l'applique au combattant local au spawn.
            Nymora.Core.Data.CombatCosmeticsContext.LocalSkinId = equippedId;
        }

        private CosmeticSkinDefinition FindSkinDef(string cosmeticId)
        {
            if (_skinDefinitions == null) return null;
            foreach (var s in _skinDefinitions)
                if (s != null && s.CosmeticId == cosmeticId) return s;
            return null;
        }

        /// <summary>5.10 (B4) — résout un familier via le PetCatalog (Resources, chargé une fois).</summary>
        private static PetDefinition FindPetDef(string cosmeticId)
        {
            if (string.IsNullOrEmpty(cosmeticId)) return null;
            if (!_petCatalogLoaded)
            {
                _petCatalog = Resources.Load<PetCatalog>("Cosmetics/PetCatalog");
                _petCatalogLoaded = true;
            }
            return _petCatalog != null ? _petCatalog.Resolve(cosmeticId) : null;
        }

        /// <summary>
        /// 5.10 (B4) — Crée/met à jour ou retire la vue du familier selon _currentPetDef.
        /// La vue est un GameObject autonome (non parenté) piloté dans Update via DrivePet.
        /// </summary>
        private void ApplyPetVisual()
        {
            if (_currentPetDef == null)
            {
                if (_petView != null) { Destroy(_petView.gameObject); _petView = null; }
                return;
            }
            if (_petView == null)
            {
                var go = new GameObject($"HubPet_{_currentPetDef.CosmeticId}");
                _petView = go.AddComponent<HubPetView>();
            }
            Vector2 startOffset = PetPlacementConfig.Instance.OffsetForFacing((int)_currentFacing);
            _petView.Init(_currentPetDef, _hubSortingLayer,
                          transform.position + new Vector3(startOffset.x, startOffset.y, 0f));
        }

        /// <summary>5.10 (B4) — pousse la position/anim du familier chaque frame (suit l'avatar).</summary>
        private void DrivePet()
        {
            if (_petView == null) return;
            bool useNE = (_currentFacing == HubFacing.NE || _currentFacing == HubFacing.NW);
            bool flipX = (_currentFacing == HubFacing.SW || _currentFacing == HubFacing.NW);
            // Offset par direction (SE/SW/NE/NW), réglable via HubPetPlacementTuner, + décalage Y
            // propre au familier.
            Vector2 off = PetPlacementConfig.Instance.OffsetForFacing((int)_currentFacing);
            Vector3 anchor = transform.position + new Vector3(off.x, off.y, 0f)
                             + new Vector3(0f, _currentPetDef != null ? _currentPetDef.VisualYOffset : 0f, 0f);
            int ownerOrder = _sr != null ? _sr.sortingOrder : _baseSortingOrder;
            // Pivot sur place (facing change alors qu'on ne marche pas) -> snap instantané.
            int facingIndex = (int)_currentFacing;
            bool snap = !_isWalking && _petLastFacingIndex != int.MinValue
                        && _petLastFacingIndex != facingIndex;
            _petLastFacingIndex = facingIndex;
            _petView.Drive(anchor, useNE, flipX, ownerOrder, Time.deltaTime, snap);
        }

        /// <summary>
        /// Extrait le texte propre d'un nom de titre catalogue. Les titres sont nommes
        /// « Titre : « l'Inebranlable » » cote backend -> on ne garde que le contenu entre
        /// les guillemets « ». Fallback = nom complet si pas de guillemets.
        /// </summary>
        private static string ExtractTitleText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            int a = raw.IndexOf('«'); // «
            int b = raw.LastIndexOf('»'); // »
            if (a >= 0 && b > a + 1) return raw.Substring(a + 1, b - a - 1).Trim();
            return raw;
        }

        /// <summary>
        /// E2 — Point d'entree appele par le popup d'emote sur HubAvatar.Local. L'avatar local a la
        /// State Authority (Shared Mode) -> emet une RPC vers TOUS (InvokeLocal inclus) pour que la
        /// bulle s'affiche chez soi ET chez les autres joueurs du hub. Pas de [Networked] field donc
        /// aucune regen (les RPC ne changent pas le layout reseau).
        /// </summary>
        public void PlayEmote(string emoteId)
        {
            if (string.IsNullOrEmpty(emoteId)) return;
            if (Object != null && Object.HasStateAuthority)
                RpcPlayEmote(emoteId);
            else
                ShowEmoteById(emoteId); // garde-fou (le popup ne cible que Local de toute facon)
        }

        // RPC : seul le proprietaire (State Authority en Shared Mode) declenche son emote ; tous les
        // clients executent le corps (InvokeLocal=true par defaut) -> bulle synchronisee partout.
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcPlayEmote(NetworkString<_32> emoteId)
        {
            ShowEmoteById(emoteId.ToString());
        }

        /// <summary>Resout l'emote par id via le catalogue puis affiche la bulle sur cet avatar.</summary>
        private void ShowEmoteById(string emoteId)
        {
            var sprite = _emoteCatalog != null ? _emoteCatalog.GetSprite(emoteId) : null;
            if (sprite != null) ShowEmoteBubble(sprite);
        }

        /// <summary>
        /// Affiche une bulle d'emote au-dessus de CET avatar (couche d'affichage bas niveau). Le
        /// composant EmoteBubbleView est cree a la volee (aucun setup prefab). Appele localement ET
        /// via la RPC RpcPlayEmote sur chaque client.
        /// </summary>
        public void ShowEmoteBubble(Sprite emoteSprite)
        {
            if (emoteSprite == null) return;
            if (_emoteBubble == null) _emoteBubble = gameObject.AddComponent<EmoteBubbleView>();
            _emoteBubble.Show(emoteSprite);
        }

        /// <summary>
        /// C1 — Affiche une bulle de chat (texte) au-dessus de cet avatar. Appele par
        /// HubChatBubbleRouter pour l'avatar dont le NetDisplayName matche l'expediteur. Marche pour
        /// l'avatar local ET les distants (le message est deja recu par tous via le backend).
        /// </summary>
        public void ShowChatBubble(string text, TMPro.TMP_FontAsset font)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (_chatBubble == null) _chatBubble = gameObject.AddComponent<ChatBubbleView>();
            _chatBubble.Show(text, font);
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
            if (HubClanPanel.Instance != null) HubClanPanel.Instance.OnClanStateChanged -= PushClanName;
            All.Remove(this);
            if (Local == this) Local = null;
            // 5.10 (B4) — détruit la vue du familier (GameObject autonome non parenté).
            if (_petView != null) { Destroy(_petView.gameObject); _petView = null; }
        }

        /// <summary>
        /// 19 mai — Push NetClanName depuis HubClanPanel.MyClanName si State Auth (= avatar
        /// local). No-op si pas d'instance HubClanPanel (panel pas encore spawn / scene
        /// chargee). Appele : (1) au Spawned local, (2) sur OnClanStateChanged si subscribe
        /// reussi, (3) tous les 60 frames via Update poll (robust fallback race condition).
        /// </summary>
        private void PushClanName()
        {
            if (Object == null || !Object.HasStateAuthority) return;
            string clanName = (HubClanPanel.Instance != null && HubClanPanel.Instance.HasClan)
                ? (HubClanPanel.Instance.MyClanName ?? "")
                : "";
            NetClanName = clanName;
        }

        /// <summary>
        /// Version idempotente : push uniquement si change vs NetClanName actuel. Appelee
        /// chaque ~1s via Update poll pour rattraper les race conditions du fetch clan async.
        /// </summary>
        private void SyncClanNameIfChanged()
        {
            string target = (HubClanPanel.Instance != null && HubClanPanel.Instance.HasClan)
                ? (HubClanPanel.Instance.MyClanName ?? "")
                : "";
            if (NetClanName.ToString() != target)
            {
                NetClanName = target;
            }
        }

        private void AssignSubFromChatClient()
        {
            var client = HubChatClient.Instance;
            if (client == null) return;
            if (!string.IsNullOrEmpty(client.MyUserId))
            {
                NetSub = client.MyUserId;
                // POLISH-7 (20 mai) : push aussi le pseudo officiel au Spawn (NetDisplayName).
                // MyDisplayName est set par le meme WELCOME que MyUserId donc dispo en meme temps.
                if (!string.IsNullOrEmpty(client.MyDisplayName)) NetDisplayName = client.MyDisplayName;
                return;
            }
            // WELCOME pas encore arrivé : on s'abonne pour push retardé (NetSub + NetDisplayName)
            client.OnWelcome += HandleWelcomePostSpawn;
        }

        private void HandleWelcomePostSpawn(string sub, string email, string displayName)
        {
            if (HubChatClient.Instance != null) HubChatClient.Instance.OnWelcome -= HandleWelcomePostSpawn;
            if (Object != null && Object.HasStateAuthority && !string.IsNullOrEmpty(sub))
            {
                NetSub = sub;
                // POLISH-7 (20 mai) : push aussi le displayName officiel sur le reseau pour
                // que les remotes voient le bon pseudo dans leur tooltip avatar hover.
                if (!string.IsNullOrEmpty(displayName)) NetDisplayName = displayName;
                Debug.Log($"[HubAvatar] NetSub/NetDisplayName assignes post-spawn via WELCOME : sub='{sub}' displayName='{displayName}'");
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
