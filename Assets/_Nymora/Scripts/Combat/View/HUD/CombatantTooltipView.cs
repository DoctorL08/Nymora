using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// POLISH-5d (17 mai) — Tooltip flottant qui affiche le HP d'un combatant survole
    /// par la souris. Drive par TileHoverView (Show/Hide).
    ///
    /// Auto-init complet : si attache a un GameObject vide, cree son propre Canvas
    /// (overlay) + Panel + Text au Awake. Lorenzo n'a qu'a poser le component sur un
    /// GameObject dans la scene combat (sans rien d'autre a configurer).
    ///
    /// Position : suit la souris en screen-space avec un offset (+15, +15) pour ne pas
    /// se retrouver sous le curseur.
    /// </summary>
    public class CombatantTooltipView : MonoBehaviour
    {
        public static CombatantTooltipView Instance { get; private set; }

        [Header("Optionnel (auto-cree si null)")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private TMP_Text _text;

        [Header("Style")]
        [SerializeField] private Vector2 _mouseOffset = new Vector2(12f, 12f);
        [SerializeField] private Vector2 _panelSize = new Vector2(95f, 24f);
        [SerializeField] private Color _bgColor = new Color(0f, 0f, 0f, 0.78f);
        [SerializeField] private int _fontSize = 13;
        [SerializeField] private int _sortingOrder = 5000; // au-dessus de tout

        private bool _visible;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureUI();
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void EnsureUI()
        {
            if (_canvas == null)
            {
                var canvasGO = new GameObject("CombatantTooltipCanvas");
                canvasGO.transform.SetParent(transform, false);
                _canvas = canvasGO.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = _sortingOrder;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }
            if (_panel == null)
            {
                var panelGO = new GameObject("Panel");
                panelGO.transform.SetParent(_canvas.transform, false);
                _panel = panelGO.AddComponent<RectTransform>();
                _panel.anchorMin = new Vector2(0f, 0f);
                _panel.anchorMax = new Vector2(0f, 0f);
                _panel.pivot = new Vector2(0f, 0f);
                _panel.sizeDelta = _panelSize;
                var bg = panelGO.AddComponent<Image>();
                bg.color = _bgColor;
                // Petit padding via outline pour la lisibilite contre fond clair.
                var outline = panelGO.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
            if (_text == null)
            {
                var textGO = new GameObject("Text");
                textGO.transform.SetParent(_panel, false);
                var rect = textGO.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(6f, 3f);
                rect.offsetMax = new Vector2(-6f, -3f);
                _text = textGO.AddComponent<TextMeshProUGUI>();
                _text.fontSize = _fontSize;
                _text.color = Color.white;
                _text.alignment = TextAlignmentOptions.MidlineLeft;
                _text.text = "HP: -/-";
                _text.raycastTarget = false;
            }
        }

        public void Show(int hpCurrent, int hpMax)
        {
            if (_text != null)
            {
                _text.text = $"HP: {hpCurrent} / {hpMax}";
            }
            if (_panel != null && !_visible)
            {
                _panel.gameObject.SetActive(true);
            }
            _visible = true;
        }

        public void Hide()
        {
            if (_panel != null && _visible)
            {
                _panel.gameObject.SetActive(false);
            }
            _visible = false;
        }

        private void Update()
        {
            if (!_visible || _panel == null) return;
            // Position screen-space basee sur la souris + offset, clamp dans l'ecran.
            Vector2 mouse = UnityEngine.Input.mousePosition;
            Vector2 pos = mouse + _mouseOffset;
            Vector2 size = _panel.sizeDelta;
            // Clamp pour eviter de sortir a droite/haut.
            if (pos.x + size.x > Screen.width) pos.x = Screen.width - size.x;
            if (pos.y + size.y > Screen.height) pos.y = Screen.height - size.y;
            _panel.anchoredPosition = pos;
        }
    }
}
