using Fusion;
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
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class HubAvatar : NetworkBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Sprite _avatarSprite;
        [SerializeField] private int _baseSortingOrder = 100;

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

        private SpriteRenderer _sr;
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
            _sr = GetComponent<SpriteRenderer>();
            if (_avatarSprite != null) _sr.sprite = _avatarSprite;
            _sr.color = ColorForPlayer(Object.InputAuthority);

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
                Debug.Log($"[HubAvatar] Local spawned at ({_spawnGridX},{_spawnGridY}) sub='{NetSub}'");
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
                Debug.Log($"[HubAvatar] Remote avatar spawned (player {Object.InputAuthority}) at ({NetGridX},{NetGridY}) sub='{NetSub}'");
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
        }

        public void SetWorldPositionInterpolated(int gx, int gy, Vector3 worldPos)
        {
            // Local lerp (State Authority) : on ne pousse PAS NetGridX/Y a chaque frame, seulement
            // au end-of-step via SetGridPosition (trafic reseau leger : 4 updates/sec a 4 tiles/sec).
            _gridX = gx;
            _gridY = gy;
            transform.position = worldPos;
            if (_sr != null) _sr.sortingOrder = IsoProjection.SortingOrderFor(_gridX, _gridY, _baseSortingOrder);
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
