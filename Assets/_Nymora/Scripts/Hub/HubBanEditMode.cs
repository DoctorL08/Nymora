using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.X — Mode dev permettant de bannir/débannir des tiles directement
    /// en Play Mode via clic gauche dans le Game view.
    ///
    /// - Touche B (configurable) : toggle Edit Mode ON/OFF
    /// - Edit Mode ON : HubInputController désactivé (pas de pathfind), clic = toggle ban
    /// - Edit Mode OFF : retour au comportement normal
    /// - Save automatique dans le ScriptableObject HubGridBanList (en Editor seulement)
    /// - UI indicators : bannière "BAN EDIT MODE" en haut + coords (gx, gy) sous le curseur
    ///
    /// Note : la persistence sur disque est `#if UNITY_EDITOR` only. En Build standalone,
    /// le toggle est silencieusement no-op (cf Debug.LogWarning). C'est un outil dev.
    /// </summary>
    public sealed class HubBanEditMode : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private HubGridRenderer _grid;
        [SerializeField] private HubInputController _input;
        [SerializeField] private HubGridBanList _banList;
        [SerializeField] private KeyCode _toggleKey = KeyCode.B;
        [SerializeField] private bool _logToggles = true;

        [Header("UI indicators (cleanup)")]
        [SerializeField] private GameObject _modeIndicatorRoot;
        [SerializeField] private TextMeshProUGUI _hoverCoordsText;
        [SerializeField] private RectTransform _hoverCoordsRect;
        [SerializeField] private Vector2 _hoverCoordsOffset = new Vector2(20f, 20f);

        private bool _editMode;

        public bool IsActive => _editMode;

        private void Reset()
        {
            _camera = Camera.main;
        }

        private void Start()
        {
            // Cache l'UI au start (sera affichée au toggle Edit Mode)
            if (_modeIndicatorRoot != null) _modeIndicatorRoot.SetActive(false);
            if (_hoverCoordsRect != null) _hoverCoordsRect.gameObject.SetActive(false);
        }

        private void Update()
        {
#if UNITY_EDITOR
            // Bind 'B' actif uniquement en Éditeur : en build alpha un joueur ne doit
            // jamais pouvoir basculer ce mode dev (qui couperait son propre input).
            if (Input.GetKeyDown(_toggleKey))
            {
                SetEditMode(!_editMode);
            }
#endif

            if (!_editMode) return;

            // Update hover coords overlay
            if (_camera != null && _grid != null)
            {
                Vector3 mouseWorld = _camera.ScreenToWorldPoint(Input.mousePosition);
                mouseWorld.z = 0f;
                (int hx, int hy) = IsoProjection.WorldToGrid(mouseWorld, _grid.TileWorldWidth, _grid.TileWorldHeight, _grid.CenterOffset);
                if (_hoverCoordsText != null)
                {
                    bool inGrid = hx >= 0 && hx < _grid.Width && hy >= 0 && hy < _grid.Height;
                    bool banned = inGrid && _banList != null && _banList.IsBanned(hx, hy);
                    if (inGrid) _hoverCoordsText.text = $"({hx}, {hy}) {(banned ? "✕ BANNED" : "walkable")}";
                    else _hoverCoordsText.text = "hors grille";
                }
                if (_hoverCoordsRect != null)
                {
                    _hoverCoordsRect.position = (Vector2)Input.mousePosition + _hoverCoordsOffset;
                }
            }

            if (!Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (_camera == null || _grid == null || _banList == null) return;

            Vector3 mouseWorldClick = _camera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldClick.z = 0f;
            (int gx, int gy) = IsoProjection.WorldToGrid(mouseWorldClick, _grid.TileWorldWidth, _grid.TileWorldHeight, _grid.CenterOffset);

            if (gx < 0 || gx >= _grid.Width || gy < 0 || gy >= _grid.Height)
            {
                if (_logToggles) Debug.Log($"[HubBanEditMode] Clic hors grille ({gx},{gy})");
                return;
            }
            ToggleBanAt(gx, gy);
        }

        private void SetEditMode(bool enabled)
        {
            _editMode = enabled;
            if (_input != null) _input.enabled = !_editMode;
            if (_modeIndicatorRoot != null) _modeIndicatorRoot.SetActive(_editMode);
            if (_hoverCoordsRect != null) _hoverCoordsRect.gameObject.SetActive(_editMode);
            if (_logToggles) Debug.Log($"[HubBanEditMode] {(_editMode ? "ON — clic = toggle ban" : "OFF — clic = pathfind normal")}");
        }

        private void ToggleBanAt(int gx, int gy)
        {
#if UNITY_EDITOR
            bool currentlyBanned = _banList.IsBanned(gx, gy);
            _banList.EditorSetBanned(gx, gy, !currentlyBanned);
            UnityEditor.AssetDatabase.SaveAssets();
            if (_grid != null) _grid.RefreshBanColors();
            if (_logToggles) Debug.Log($"[HubBanEditMode] Tile ({gx},{gy}) -> {(!currentlyBanned ? "BANNED" : "walkable")}");
#else
            Debug.LogWarning("[HubBanEditMode] Toggle ban disponible uniquement en mode Editor (l'asset ScriptableObject ne peut pas être modifié en runtime build).");
#endif
        }
    }
}
