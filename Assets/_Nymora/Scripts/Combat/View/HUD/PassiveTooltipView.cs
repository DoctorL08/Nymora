using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Tooltip UI Canvas pour le panel passif HUD combat. Auto-init complet (Canvas overlay +
    /// Panel + Text avec layout group / size fitter). Drive par PassivePanelView au hover sur
    /// l'icone passif (milieu-gauche du HUD).
    ///
    /// Position : ancre a un RectTransform reference (anchorRect) — typiquement le panel
    /// passif. Affiche le tooltip a droite de l'anchor (cote interieur du HUD) avec un
    /// petit gap. Largeur fixe, hauteur auto-fit.
    /// </summary>
    public class PassiveTooltipView : MonoBehaviour
    {
        private static PassiveTooltipView _instance;

        /// <summary>
        /// Lazy singleton : auto-cree au premier acces si pas encore instancie. Robuste a
        /// l'ordre Awake / SceneLoad / AutoCreate. Evite la race condition observee le 19 mai
        /// (RuntimeInitializeOnLoadMethod parfois pas appele a temps).
        /// </summary>
        public static PassiveTooltipView Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("PassiveTooltipView");
                    _instance = go.AddComponent<PassiveTooltipView>();
                }
                return _instance;
            }
        }

        [Header("Style (re-skin DA hub)")]
        [SerializeField] private float _fixedWidth = 420f;
        [SerializeField] private int _panelPadding = 14;
        [SerializeField] private float _panelSpacing = 6f;
        [SerializeField] private int _fontSize = 16;
        [SerializeField] private Vector2 _anchorOffset = new Vector2(20f, 0f);
        [SerializeField] private Color _bgColor = new Color(0.10f, 0.105f, 0.12f, 0.96f);
        [SerializeField] private int _sortingOrder = 5000;
        [Tooltip("Optionnel : police Ari (DA hub), câblée par 'Restyle Combat HUD' sur l'instance de scène. Défaut TMP sinon.")]
        [SerializeField] private TMP_FontAsset _font;

        private Canvas _canvas;
        private RectTransform _panel;
        private TMP_Text _text;
        private bool _visible;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            EnsureUI();
            Hide();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void EnsureUI()
        {
            var canvasGo = new GameObject("PassiveTooltipCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = _sortingOrder;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var panelGo = new GameObject("Panel",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGo.transform.SetParent(canvasGo.transform, false);
            _panel = panelGo.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0f, 0f);
            _panel.anchorMax = new Vector2(0f, 0f);
            _panel.pivot = new Vector2(0f, 0f);
            _panel.sizeDelta = new Vector2(_fixedWidth, 0f);
            var panelImg = panelGo.GetComponent<Image>();
            panelImg.color = _bgColor;
            CombatUiKit.ApplyRounded(panelImg, CombatUiKit.CornerRadius); // coins arrondis DA hub

            var vlg = panelGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(_panelPadding, _panelPadding, _panelPadding, _panelPadding);
            vlg.spacing = _panelSpacing;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = panelGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(panelGo.transform, false);
            _text = textGo.AddComponent<TextMeshProUGUI>();
            if (_font != null) _text.font = _font;
            _text.fontSize = _fontSize;
            _text.color = CombatUiKit.TextPrimary;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.enableWordWrapping = true;
            _text.richText = true;
            _text.raycastTarget = false;

            // 26 mai — Masquer le panneau DÈS sa création. Sinon il reste actif à vide
            // (texte vide -> hauteur = padding seul = ~28px) parqué à sa position par défaut
            // (0,0) = coin bas-gauche -> "bout de cadre noir" parasite en combat. Hide() en
            // Awake ne suffisait pas : son garde `if (_visible)` est false au démarrage et
            // n'appelait donc jamais SetActive(false). On aligne l'état (_visible=false) sur
            // un panneau inactif dès le départ.
            panelGo.SetActive(false);
        }

        /// <summary>
        /// Affiche le tooltip avec le contenu donne, ancre a droite du RectTransform anchor.
        /// </summary>
        public void Show(string richText, RectTransform anchor)
        {
            if (_panel == null || _text == null) return;
            _text.text = richText;
            // Restore pivot bottom-left (cas par defaut Show, opposite de ShowAbove qui set right)
            _panel.pivot = new Vector2(0f, 0f);
            // Position : a droite du anchor (cote interieur du HUD), aligne en bas.
            if (anchor != null)
            {
                Vector3[] worldCorners = new Vector3[4];
                anchor.GetWorldCorners(worldCorners);
                // worldCorners[2] = top-right, worldCorners[3] = top-right inverse... en realite :
                // [0] bottom-left, [1] top-left, [2] top-right, [3] bottom-right
                // On veut a droite du bottom-right = +x, meme y bottom
                Vector3 bottomRight = worldCorners[3];
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, bottomRight);
                _panel.anchoredPosition = screenPos + _anchorOffset;
            }
            else
            {
                _panel.anchoredPosition = new Vector2(20f, 20f);
            }
            _panel.gameObject.SetActive(true);
            _visible = true;
            Debug.Log($"[PassiveTooltipView] Show at anchored={_panel.anchoredPosition} | text length={richText?.Length}");
        }

        /// <summary>
        /// Variante : positionne le tooltip AU-DESSUS de l'anchor avec pivot DROIT (le
        /// tooltip s'etend vers la GAUCHE depuis le coin haut-droite du slot). Utilise par
        /// TimelineView (anchor en bas-droite -> tooltip pousse vers le haut sans deborder
        /// le bord droit de l'ecran).
        /// </summary>
        public void ShowAbove(string richText, RectTransform anchor)
        {
            if (_panel == null || _text == null) return;
            _text.text = richText;
            // Pivot right-bottom -> tooltip s'etend vers la gauche depuis le pivot
            _panel.pivot = new Vector2(1f, 0f);
            if (anchor != null)
            {
                Vector3[] worldCorners = new Vector3[4];
                anchor.GetWorldCorners(worldCorners);
                // worldCorners : [0] bottom-left, [1] top-left, [2] top-right, [3] bottom-right
                Vector3 topRight = worldCorners[2];
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, topRight);
                _panel.anchoredPosition = new Vector2(screenPos.x, screenPos.y + 10f);
            }
            else
            {
                _panel.anchoredPosition = new Vector2(Screen.width - 20f, 20f);
            }
            _panel.gameObject.SetActive(true);
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
    }
}
