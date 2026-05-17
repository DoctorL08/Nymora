using System.Collections.Generic;
using Nymora.Core.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 5.3.f — Panel Class Selector overlay en mode CAROUSEL.
    ///
    /// Affichage :
    ///   - 1 classe centrale en grand (sprite + nom + lore + passif + signature)
    ///   - 2 classes laterales en fade (preview prev/next)
    ///   - Fleches gauche/droite pour naviguer le carousel
    ///   - Click sur la card centrale = confirme la selection (close + SwitchClass)
    ///
    /// Le panel script spawn dynamiquement toute la structure carousel
    /// dans _carouselContainer (fourni par le tool, vide).
    /// </summary>
    public sealed class HubClassSelectorPanel : MonoBehaviour
    {
        [Header("Data — 5 ClassDefinition.asset (drag-drop)")]
        [SerializeField] private NymoraClassDefinition[] _classDefinitions;

        [Header("Root")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _closeButton;

        [Header("Carousel container (zone fullscreen vide, le script spawn dedans)")]
        [SerializeField] private RectTransform _carouselContainer;

        [Header("Visuels")]
        [SerializeField] private Color _arrowColor = new Color(0.30f, 0.32f, 0.40f, 1f);
        [SerializeField] private Color _confirmHintColor = new Color(0.30f, 0.55f, 0.35f, 1f);
        [SerializeField] private float _sideCardAlpha = 0.30f;
        [Tooltip("Largeur de la card centrale en px.")]
        [SerializeField] private float _centerCardWidth = 760f;
        [Tooltip("Largeur des cards laterales en px.")]
        [SerializeField] private float _sideCardWidth = 700f;
        [Tooltip("Taille du sprite Idle dans la card centrale.")]
        [SerializeField] private float _centerSpriteSize = 180f;
        [Tooltip("Taille du sprite Idle dans les cards laterales.")]
        [SerializeField] private float _sideSpriteSize = 140f;

        public static HubClassSelectorPanel Instance { get; private set; }

        private int _activeIndex;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveAllListeners();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open()
        {
            if (_panelRoot == null) return;
            _panelRoot.SetActive(true);
            // Initialise l'index sur la classe courante du DeckBuilder si possible
            if (HubDeckBuilderPanel.Instance != null && _classDefinitions != null)
            {
                string cur = HubDeckBuilderPanel.Instance.CurrentClassId;
                for (int i = 0; i < _classDefinitions.Length; i++)
                {
                    if (_classDefinitions[i] != null && _classDefinitions[i].ClassId.ToString() == cur)
                    {
                        _activeIndex = i;
                        break;
                    }
                }
            }
            RenderCarousel();
        }

        public void Close()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private void NavigateLeft()
        {
            if (_classDefinitions == null || _classDefinitions.Length == 0) return;
            _activeIndex = (_activeIndex - 1 + _classDefinitions.Length) % _classDefinitions.Length;
            RenderCarousel();
        }

        private void NavigateRight()
        {
            if (_classDefinitions == null || _classDefinitions.Length == 0) return;
            _activeIndex = (_activeIndex + 1) % _classDefinitions.Length;
            RenderCarousel();
        }

        private void ConfirmSelection()
        {
            if (_classDefinitions == null || _classDefinitions.Length == 0) return;
            var def = _classDefinitions[_activeIndex];
            if (def == null) return;
            string classId = def.ClassId.ToString();
            Close();
            // 5.3.g.bis — Push la classe sur l'avatar hub (sprite/Animator update + sync remotes).
            if (HubAvatar.Local != null) HubAvatar.Local.SetClassFromLocalChoice(classId);
            if (HubDeckBuilderPanel.Instance != null) HubDeckBuilderPanel.Instance.SwitchClass(classId);
        }

        // -----------------------------------------------------------------------------
        // Rendering carousel : 3 cards (prev / center / next) + 2 arrows + hint.
        // -----------------------------------------------------------------------------
        private void RenderCarousel()
        {
            ClearSpawned();
            if (_carouselContainer == null || _classDefinitions == null || _classDefinitions.Length == 0) return;

            int count = _classDefinitions.Length;
            int prevIdx = (_activeIndex - 1 + count) % count;
            int nextIdx = (_activeIndex + 1) % count;

            // Side card LEFT (preview prev, fade)
            SpawnCard(_classDefinitions[prevIdx], anchorX: 0.16f, isCenter: false);

            // Side card RIGHT (preview next, fade)
            SpawnCard(_classDefinitions[nextIdx], anchorX: 0.84f, isCenter: false);

            // Center card (big, full alpha, clickable to confirm)
            SpawnCard(_classDefinitions[_activeIndex], anchorX: 0.5f, isCenter: true);

            // Left arrow
            SpawnArrow(anchorX: 0.04f, label: "<", onClick: NavigateLeft);

            // Right arrow
            SpawnArrow(anchorX: 0.96f, label: ">", onClick: NavigateRight);

            // Hint "Click sur la classe centrale pour confirmer"
            SpawnConfirmHint();
        }

        private void SpawnCard(NymoraClassDefinition def, float anchorX, bool isCenter)
        {
            if (def == null) return;

            float width = isCenter ? _centerCardWidth : _sideCardWidth;
            float spriteSize = isCenter ? _centerSpriteSize : _sideSpriteSize;
            float alpha = isCenter ? 1f : _sideCardAlpha;

            // Card stretch vertical avec marge bas 100 pour laisser de la place au hint global.
            var card = new GameObject($"Card_{def.ClassId}_{(isCenter ? "center" : "side")}",
                typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            card.transform.SetParent(_carouselContainer, false);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(anchorX, 0f);
            rt.anchorMax = new Vector2(anchorX, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, -160f); // height = container - 160 (30 top + 130 bottom pour hint)
            rt.anchoredPosition = new Vector2(0f, 35f); // remonte la card pour laisser place hint en bas
            card.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.15f, 1f);
            var cg = card.GetComponent<CanvasGroup>();
            cg.alpha = alpha;
            cg.blocksRaycasts = isCenter;
            cg.interactable = isCenter;

            if (isCenter)
            {
                var btn = card.AddComponent<Button>();
                btn.targetGraphic = card.GetComponent<Image>();
                btn.onClick.AddListener(ConfirmSelection);
            }

            // Layout vertical fixe (du haut vers le bas) avec zones distinctes :
            //   - Band coloree (top)
            //   - Nom classe (font 60 center, 40 side) — AU-DESSUS du sprite
            //   - SpriteBg (sprite Idle anime)
            //   - Lore (zone dediee, no chevauchement) — center-only
            //   - Icones Passif + Signature (bas) — center-only

            float bandH = isCenter ? 12f : 8f;
            float nameH = isCenter ? 52f : 42f;
            float nameFont = isCenter ? 40f : 28f;
            float nameTopOffset = bandH + 8f; // top de la card -> top du nom

            // Bandeau couleur en haut
            var band = MakeChild("Band", card.transform, typeof(Image));
            var bandRt = band.GetComponent<RectTransform>();
            bandRt.anchorMin = new Vector2(0f, 1f);
            bandRt.anchorMax = new Vector2(1f, 1f);
            bandRt.pivot = new Vector2(0.5f, 1f);
            bandRt.sizeDelta = new Vector2(0f, bandH);
            band.GetComponent<Image>().color = def.AccentColor;

            // Nom classe AU-DESSUS du sprite
            var nameGo = MakeChild("Name", card.transform, typeof(TextMeshProUGUI));
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 1f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0.5f, 1f);
            nameRt.anchoredPosition = new Vector2(0f, -nameTopOffset);
            nameRt.sizeDelta = new Vector2(-30f, nameH);
            var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
            nameTmp.text = def.DisplayName;
            nameTmp.fontSize = nameFont;
            nameTmp.color = def.AccentColor;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.fontStyle = FontStyles.Bold;

            // SpriteBg (sous le nom)
            float spriteTopOffset = nameTopOffset + nameH + 4f;
            var spriteBgGo = MakeChild("SpriteBg", card.transform, typeof(Image));
            var spriteBgRt = spriteBgGo.GetComponent<RectTransform>();
            spriteBgRt.anchorMin = new Vector2(0.5f, 1f);
            spriteBgRt.anchorMax = new Vector2(0.5f, 1f);
            spriteBgRt.pivot = new Vector2(0.5f, 1f);
            spriteBgRt.anchoredPosition = new Vector2(0f, -spriteTopOffset);
            spriteBgRt.sizeDelta = new Vector2(spriteSize, spriteSize);
            spriteBgGo.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.10f, 1f);

            var spriteGo = MakeChild("Sprite", spriteBgGo.transform, typeof(Image));
            var spriteRt = spriteGo.GetComponent<RectTransform>();
            spriteRt.anchorMin = Vector2.zero;
            spriteRt.anchorMax = Vector2.one;
            spriteRt.offsetMin = new Vector2(16f, 16f);
            spriteRt.offsetMax = new Vector2(-16f, -16f);
            var spriteImg = spriteGo.GetComponent<Image>();
            spriteImg.preserveAspect = true;
            bool hasFrames = def.IdleFrames != null && def.IdleFrames.Length > 0;
            if (hasFrames)
            {
                spriteImg.sprite = def.IdleFrames[0];
                var animUI = spriteGo.AddComponent<UISpriteAnimator>();
                animUI.Play(spriteImg, def.IdleFrames, def.IdleFps);
            }
            else if (def.PortraitSprite != null) spriteImg.sprite = def.PortraitSprite;
            else if (def.IconSprite != null) spriteImg.sprite = def.IconSprite;
            else spriteImg.color = new Color(0.18f, 0.18f, 0.22f, 1f);

            if (!isCenter) { _spawned.Add(card); return; }

            // === Center-only : Lore + Icones — positionnement TOP-sequentiel (zero overlap) ===

            float loreTopOffset = spriteTopOffset + spriteSize + 10f;
            float loreH = 95f;
            var loreGo = MakeChild("Lore", card.transform, typeof(TextMeshProUGUI));
            var loreRt = loreGo.GetComponent<RectTransform>();
            loreRt.anchorMin = new Vector2(0f, 1f);
            loreRt.anchorMax = new Vector2(1f, 1f);
            loreRt.pivot = new Vector2(0.5f, 1f);
            loreRt.anchoredPosition = new Vector2(0f, -loreTopOffset);
            loreRt.sizeDelta = new Vector2(-50f, loreH);
            var loreTmp = loreGo.GetComponent<TextMeshProUGUI>();
            loreTmp.text = string.IsNullOrEmpty(def.Lore) ? "<i>(Lore a remplir)</i>" : def.Lore;
            loreTmp.fontSize = 18f;
            loreTmp.color = new Color(0.92f, 0.92f, 0.96f);
            loreTmp.alignment = TextAlignmentOptions.Center;
            loreTmp.enableWordWrapping = true;
            loreTmp.richText = true;

            // Icones Passif/Signature en TOP-sequentiel (juste sous le lore)
            float iconsTopOffset = loreTopOffset + loreH + 8f;
            string passifTooltip =
                $"<size=24><color=#ffe28a><b>Passif : {def.PassiveName}</b></color></size>\n\n" +
                $"<size=18>{def.PassiveDescription}</size>";
            SpawnInfoIconTop(card.transform, "P", new Color(0.85f, 0.70f, 0.30f, 1f),
                xOffset: -80f, topOffset: iconsTopOffset, tooltipText: passifTooltip, labelOverlay: "Passif");

            string signatureTooltip =
                $"<size=24><color=#ffb066><b>Signature : {def.SignatureName}</b></color></size>\n\n" +
                $"<size=18>Chargement : {def.ResourceCap} {def.ResourceKind}</size>";
            SpawnInfoIconTop(card.transform, "S", new Color(0.85f, 0.50f, 0.30f, 1f),
                xOffset: 80f, topOffset: iconsTopOffset, tooltipText: signatureTooltip, labelOverlay: "Signature");

            _spawned.Add(card);
        }

        /// <summary>
        /// Variante TOP-anchored de SpawnInfoIcon : position depuis le top de la card
        /// (zero overlap garanti, comme tout le reste du layout center-card).
        /// </summary>
        private void SpawnInfoIconTop(Transform parent, string letter, Color color, float xOffset, float topOffset, string tooltipText, string labelOverlay)
        {
            var icon = new GameObject($"Icon_{letter}", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(parent, false);
            var rt = icon.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(xOffset, -topOffset);
            rt.sizeDelta = new Vector2(72f, 72f);
            icon.GetComponent<Image>().color = color;

            var letterGo = MakeChild("Letter", icon.transform, typeof(TextMeshProUGUI));
            var letterRt = letterGo.GetComponent<RectTransform>();
            letterRt.anchorMin = Vector2.zero;
            letterRt.anchorMax = Vector2.one;
            letterRt.offsetMin = Vector2.zero;
            letterRt.offsetMax = Vector2.zero;
            var letterTmp = letterGo.GetComponent<TextMeshProUGUI>();
            letterTmp.text = letter;
            letterTmp.fontSize = 38f;
            letterTmp.color = Color.white;
            letterTmp.alignment = TextAlignmentOptions.Center;
            letterTmp.fontStyle = FontStyles.Bold;

            var labelGo = MakeChild("Label", icon.transform, typeof(TextMeshProUGUI));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0f);
            labelRt.pivot = new Vector2(0.5f, 1f);
            labelRt.anchoredPosition = new Vector2(0f, -2f);
            labelRt.sizeDelta = new Vector2(0f, 18f);
            var labelTmp = labelGo.GetComponent<TextMeshProUGUI>();
            labelTmp.text = labelOverlay;
            labelTmp.fontSize = 13f;
            labelTmp.color = new Color(0.9f, 0.9f, 0.92f);
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.fontStyle = FontStyles.Bold;

            var hover = icon.AddComponent<ClassIconHoverProxy>();
            hover.Init(this, tooltipText);
        }

        /// <summary>
        /// Spawn une icone ronde (rectangle pour simplicite) avec lettre + label sous, et tooltip hover.
        /// anchoredPos = position relative au bas-centre de la card (anchor 0.5, 0).
        /// </summary>
        private void SpawnInfoIcon(Transform parent, string letter, Color color, Vector2 anchoredPos, string tooltipText, string labelOverlay)
        {
            var icon = new GameObject($"Icon_{letter}", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(parent, false);
            var rt = icon.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(110f, 110f);
            icon.GetComponent<Image>().color = color;

            // Letter au centre (gros)
            var letterGo = MakeChild("Letter", icon.transform, typeof(TextMeshProUGUI));
            var letterRt = letterGo.GetComponent<RectTransform>();
            letterRt.anchorMin = Vector2.zero;
            letterRt.anchorMax = Vector2.one;
            letterRt.offsetMin = Vector2.zero;
            letterRt.offsetMax = Vector2.zero;
            var letterTmp = letterGo.GetComponent<TextMeshProUGUI>();
            letterTmp.text = letter;
            letterTmp.fontSize = 60f;
            letterTmp.color = Color.white;
            letterTmp.alignment = TextAlignmentOptions.Center;
            letterTmp.fontStyle = FontStyles.Bold;

            // Label sous l'icone
            var labelGo = MakeChild("Label", icon.transform, typeof(TextMeshProUGUI));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0f);
            labelRt.pivot = new Vector2(0.5f, 1f);
            labelRt.anchoredPosition = new Vector2(0f, -4f);
            labelRt.sizeDelta = new Vector2(0f, 24f);
            var labelTmp = labelGo.GetComponent<TextMeshProUGUI>();
            labelTmp.text = labelOverlay;
            labelTmp.fontSize = 16f;
            labelTmp.color = new Color(0.9f, 0.9f, 0.92f);
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.fontStyle = FontStyles.Bold;

            // Hover handler : show/hide tooltip
            var hover = icon.AddComponent<ClassIconHoverProxy>();
            hover.Init(this, tooltipText);
        }

        /// <summary>Affiche le tooltip avec un texte donne. Position fixe en bas-centre du carousel container.</summary>
        public void ShowTooltip(string richText)
        {
            EnsureTooltipSpawned();
            _tooltipText.text = richText;
            _tooltipPanel.SetActive(true);
            _tooltipPanel.transform.SetAsLastSibling(); // toujours au-dessus
        }

        public void HideTooltip()
        {
            if (_tooltipPanel != null) _tooltipPanel.SetActive(false);
        }

        private GameObject _tooltipPanel;
        private TextMeshProUGUI _tooltipText;

        private void EnsureTooltipSpawned()
        {
            if (_tooltipPanel != null) return;
            if (_carouselContainer == null) return;

            _tooltipPanel = new GameObject("ClassInfoTooltip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _tooltipPanel.transform.SetParent(_carouselContainer, false);
            var rt = _tooltipPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 90f);
            rt.sizeDelta = new Vector2(700f, 200f);
            var bgImg = _tooltipPanel.GetComponent<Image>();
            bgImg.color = new Color(0.05f, 0.06f, 0.08f, 0.97f);
            bgImg.raycastTarget = false; // empeche le tooltip de capter la souris (anti-flicker)

            // CanvasGroup blocksRaycasts = false (cumulative protection contre l'overlap mouse-hover)
            var cg = _tooltipPanel.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            var textGo = MakeChild("Text", _tooltipPanel.transform, typeof(TextMeshProUGUI));
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(20f, 16f);
            trt.offsetMax = new Vector2(-20f, -16f);
            _tooltipText = textGo.GetComponent<TextMeshProUGUI>();
            _tooltipText.fontSize = 18f;
            _tooltipText.color = Color.white;
            _tooltipText.alignment = TextAlignmentOptions.TopLeft;
            _tooltipText.enableWordWrapping = true;
            _tooltipText.richText = true;
            _tooltipText.raycastTarget = false;

            _tooltipPanel.SetActive(false);
        }

        private void SpawnArrow(float anchorX, string label, System.Action onClick)
        {
            var arrow = new GameObject($"Arrow_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            arrow.transform.SetParent(_carouselContainer, false);
            var rt = arrow.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(anchorX, 0.5f);
            rt.anchorMax = new Vector2(anchorX, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(70f, 100f);
            rt.anchoredPosition = Vector2.zero;
            arrow.GetComponent<Image>().color = _arrowColor;
            var btn = arrow.GetComponent<Button>();
            btn.targetGraphic = arrow.GetComponent<Image>();
            btn.onClick.AddListener(() => onClick());

            var labelGo = MakeChild("Label", arrow.transform, typeof(TextMeshProUGUI));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 48f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;

            _spawned.Add(arrow);
        }

        private void SpawnConfirmHint()
        {
            var hint = MakeChild("ConfirmHint", _carouselContainer, typeof(TextMeshProUGUI));
            var rt = hint.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 6f);
            rt.sizeDelta = new Vector2(600f, 22f);
            var tmp = hint.GetComponent<TextMeshProUGUI>();
            tmp.text = "<i>Click sur la classe centrale pour confirmer  ·  Flèches pour naviguer</i>";
            tmp.fontSize = 14f;
            tmp.color = _confirmHintColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.richText = true;
            tmp.raycastTarget = false;

            _spawned.Add(hint);
        }

        // -----------------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------------
        private static GameObject MakeChild(string name, Transform parent, params System.Type[] components)
        {
            var allComponents = new List<System.Type> { typeof(RectTransform) };
            allComponents.AddRange(components);
            var go = new GameObject(name, allComponents.ToArray());
            go.transform.SetParent(parent, false);
            return go;
        }

        private void ClearSpawned()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();
            // Tooltip est lui aussi spawne dans le container — on le clean aussi.
            if (_tooltipPanel != null) { Destroy(_tooltipPanel); _tooltipPanel = null; _tooltipText = null; }
        }
    }

    /// <summary>Hover handler proxy : delegue show/hide tooltip au panel parent.</summary>
    internal sealed class ClassIconHoverProxy : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        private HubClassSelectorPanel _panel;
        private string _tooltipText;

        public void Init(HubClassSelectorPanel panel, string text)
        {
            _panel = panel;
            _tooltipText = text;
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData _) => _panel?.ShowTooltip(_tooltipText);
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData _) => _panel?.HideTooltip();
    }
}
