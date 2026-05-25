using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub.Menu
{
    /// <summary>
    /// M0 — Fabrique de widgets cohérents pour le nouveau menu hub.
    ///
    /// Toutes les vues (M1..M8) construisent leurs éléments via cette fabrique pour
    /// garantir un style identique (monochrome, font Ari, coins arrondis). uGUI only,
    /// cohérent avec les 11 panneaux existants et UiPanelAnimator.
    ///
    /// Le sprite à coins arrondis est généré par CODE (pas d'import d'asset) et mis en
    /// cache par rayon, puis teinté via Image.color.
    /// </summary>
    public sealed class HubMenuUIFactory
    {
        private readonly HubMenuTheme _t;

        public HubMenuUIFactory(HubMenuTheme theme) { _t = theme; }
        public HubMenuTheme Theme => _t;

        // ===== Primitives =====

        public RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public Image MakeImage(string name, Transform parent, Color color, bool rounded = true)
        {
            var rt = MakeRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded)
            {
                img.sprite = RoundedSprite(_t.CornerRadius);
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        public TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, Color color,
            TMP_FontAsset font = null, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var rt = MakeRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = font != null ? font : _t.Font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.richText = true;
            tmp.enableWordWrapping = true;
            return tmp;
        }

        /// <summary>Étire un RectTransform sur tout son parent avec marges optionnelles.</summary>
        public static void Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        // ===== Widgets =====

        /// <summary>
        /// Grande carte rectangulaire (menu Accueil / Arène) calquée sur la réf :
        /// illustration plein cadre + bandeau sombre en bas avec icône + TITRE (majuscules)
        /// + sous-titre, tout centré. L'illustration est un placeholder neutre remplaçable
        /// par l'art Kyami (assigne juste un sprite sur le child "Illustration").
        /// </summary>
        public Button MakeCard(Transform parent, string title, string subtitle, out TextMeshProUGUI titleLabel, Sprite illustration = null)
        {
            // Racine sombre arrondie
            var root = MakeImage("Card_" + title, parent, new Color(0.05f, 0.05f, 0.06f, 0.97f));
            root.rectTransform.sizeDelta = _t.CardSize;
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = _t.CardSize.x;
            le.preferredHeight = _t.CardSize.y;
            var btn = root.gameObject.AddComponent<Button>();

            // Illustration : sprite fourni (art) sinon placeholder neutre.
            bool hasArt = illustration != null;
            if (hasArt)
            {
                // Masque arrondi : l'illustration est clippée pour épouser le contour de la carte.
                var maskRt = MakeRect("IlloMask", root.rectTransform);
                Stretch(maskRt, 5f, 5f, 5f, 5f);
                var maskImg = maskRt.gameObject.AddComponent<Image>();
                maskImg.sprite = RoundedSprite(Mathf.Max(2f, _t.CornerRadius - 4f));
                maskImg.type = Image.Type.Sliced;
                maskImg.raycastTarget = false;
                var mask = maskRt.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;

                var illo = MakeImage("Illustration", maskRt, Color.white, rounded: false);
                Stretch(illo.rectTransform);
                illo.sprite = illustration; illo.type = Image.Type.Simple; illo.preserveAspect = false;
                illo.raycastTarget = false;
            }
            else
            {
                var illo = MakeImage("Illustration", root.rectTransform, new Color(0.20f, 0.205f, 0.23f, 1f));
                Stretch(illo.rectTransform, 5f, 5f, 5f, 5f);
                illo.raycastTarget = false;
            }

            // Bandeau sombre bas (par-dessus l'illustration)
            var footer = MakeImage("Footer", root.rectTransform, new Color(0.04f, 0.04f, 0.05f, 0.82f));
            var frt = footer.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(1f, 0f); frt.pivot = new Vector2(0.5f, 0f);
            frt.sizeDelta = new Vector2(-10f, _t.CardFooterHeight);
            frt.anchoredPosition = new Vector2(0f, 5f);
            footer.raycastTarget = false;

            // Sheen de survol (cible du Button : éclaircit doucement toute la carte)
            var hover = MakeImage("Hover", root.rectTransform, Color.white);
            Stretch(hover.rectTransform);
            hover.raycastTarget = false;
            btn.targetGraphic = hover;
            var colors = btn.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.04f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0f);
            colors.fadeDuration = 0.12f;
            btn.colors = colors;

            // Icône placeholder (haut du bandeau) — uniquement sans illustration
            if (!hasArt)
            {
                var icon = MakeImage("Icon", footer.rectTransform, new Color(1f, 1f, 1f, 0.85f));
                var irt = icon.rectTransform;
                irt.anchorMin = new Vector2(0.5f, 1f); irt.anchorMax = new Vector2(0.5f, 1f); irt.pivot = new Vector2(0.5f, 1f);
                irt.sizeDelta = new Vector2(34f, 34f);
                irt.anchoredPosition = new Vector2(0f, -16f);
                icon.raycastTarget = false;
            }

            // Titre (MAJUSCULES, centré)
            titleLabel = MakeText("Title", footer.rectTransform, title.ToUpperInvariant(), _t.FontSizeCardTitle, _t.TextPrimary, _t.FontBold, TextAlignmentOptions.Center);
            var tlrt = titleLabel.rectTransform;
            tlrt.anchorMin = new Vector2(0f, 1f); tlrt.anchorMax = new Vector2(1f, 1f); tlrt.pivot = new Vector2(0.5f, 1f);
            tlrt.sizeDelta = new Vector2(-20f, 34f);
            tlrt.anchoredPosition = new Vector2(0f, -58f);
            titleLabel.enableWordWrapping = false;
            titleLabel.characterSpacing = 2f;
            titleLabel.raycastTarget = false;

            // Sous-titre (centré, jusqu'à 2 lignes)
            var sub = MakeText("Subtitle", footer.rectTransform, subtitle, _t.FontSizeSmall, _t.TextSecondary, _t.Font, TextAlignmentOptions.Top);
            var srt = sub.rectTransform;
            srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(1f, 0f); srt.pivot = new Vector2(0.5f, 0f);
            srt.sizeDelta = new Vector2(-28f, 50f);
            srt.anchoredPosition = new Vector2(0f, 14f);
            sub.raycastTarget = false;

            return btn;
        }

        /// <summary>Onglet de la barre haute : icône (placeholder) + label empilés, comme la réf.</summary>
        public Button MakeTabButton(Transform parent, string label, out TextMeshProUGUI labelText, out Image icon)
        {
            var rt = MakeRect("Tab_" + label, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = _t.TabHeight;
            le.preferredWidth = Mathf.Max(110f, label.Length * (_t.FontSizeBody * 0.62f) + 24f);

            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f); // zone cliquable transparente
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;

            icon = MakeImage("Icon", rt, _t.TextSecondary);
            var irt = icon.rectTransform;
            irt.anchorMin = new Vector2(0.5f, 1f); irt.anchorMax = new Vector2(0.5f, 1f); irt.pivot = new Vector2(0.5f, 1f);
            irt.sizeDelta = new Vector2(_t.TabIconSize, _t.TabIconSize);
            irt.anchoredPosition = new Vector2(0f, -2f);
            icon.raycastTarget = false;

            labelText = MakeText("Label", rt, label, _t.FontSizeBody, _t.TextSecondary, _t.Font, TextAlignmentOptions.Center);
            var lrt = labelText.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f); lrt.pivot = new Vector2(0.5f, 0f);
            lrt.sizeDelta = new Vector2(0f, 24f);
            lrt.anchoredPosition = new Vector2(0f, 0f);
            labelText.enableWordWrapping = false;
            labelText.raycastTarget = false;

            return btn;
        }

        public void SetTabActive(TextMeshProUGUI labelText, Image icon, bool active)
        {
            var col = active ? _t.TextPrimary : _t.TextSecondary;
            if (labelText != null)
            {
                labelText.color = col;
                labelText.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            }
            if (icon != null) icon.color = col;
        }

        /// <summary>Trait de séparation fin (ex : sous la barre d'onglets).</summary>
        public Image MakeDivider(Transform parent)
        {
            return MakeImage("Divider", parent, _t.Divider, rounded: false);
        }

        /// <summary>Bouton primaire (pilule claire, texte sombre) ou secondaire (ghost translucide).</summary>
        public Button MakeButton(Transform parent, string label, bool primary, out TextMeshProUGUI labelText)
        {
            var img = MakeImage("Btn_" + label, parent, primary ? _t.Accent : _t.ButtonGhostBg);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var c = btn.colors;
            if (primary)
            {
                c.normalColor = _t.Accent;
                c.highlightedColor = Color.white;
                c.pressedColor = new Color(0.80f, 0.81f, 0.83f);
                c.selectedColor = _t.Accent;
            }
            else
            {
                c.normalColor = _t.ButtonGhostBg;
                c.highlightedColor = _t.ButtonGhostBgHover;
                c.pressedColor = _t.ButtonGhostBgHover;
                c.selectedColor = _t.ButtonGhostBg;
            }
            c.fadeDuration = 0.1f;
            btn.colors = c;

            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 42f;
            le.minWidth = 120f;

            labelText = MakeText("Label", img.rectTransform, label, _t.FontSizeBody, primary ? _t.TextOnLight : _t.TextPrimary, _t.FontBold, TextAlignmentOptions.Center);
            Stretch(labelText.rectTransform, 18f, 18f, 0f, 0f);
            labelText.enableWordWrapping = false;

            return btn;
        }

        public Image MakePanel(Transform parent, Color? color = null)
        {
            return MakeImage("Panel", parent, color ?? _t.PanelBg);
        }

        public TextMeshProUGUI MakeSectionHeader(Transform parent, string text)
        {
            var tmp = MakeText("Header_" + text, parent, text, _t.FontSizeHeader, _t.TextPrimary, _t.FontBold, TextAlignmentOptions.MidlineLeft);
            var le = tmp.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 34f;
            return tmp;
        }

        // ===== Sprite à coins arrondis (généré + caché par rayon) =====

        private static readonly Dictionary<int, Sprite> _roundedCache = new Dictionary<int, Sprite>();

        public static Sprite RoundedSprite(float radius)
        {
            int r = Mathf.Max(2, Mathf.RoundToInt(radius));
            if (_roundedCache.TryGetValue(r, out var cached) && cached != null) return cached;

            int size = r * 2 + 4; // 4px de zone centrale étirable (9-slice)
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte a = (byte)Mathf.RoundToInt(CornerAlpha(x, y, size, r) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();

            var border = new Vector4(r, r, r, r);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = "RoundedRect_" + r;
            _roundedCache[r] = sprite;
            return sprite;
        }

        /// <summary>Alpha d'un pixel : plein partout sauf dans les 4 coins, arrondis avec anti-aliasing 1px.</summary>
        private static float CornerAlpha(int x, int y, int size, int r)
        {
            float cx = x + 0.5f, cy = y + 0.5f;
            float cornerX = -1f, cornerY = -1f;
            if (cx < r) cornerX = r; else if (cx > size - r) cornerX = size - r;
            if (cy < r) cornerY = r; else if (cy > size - r) cornerY = size - r;
            if (cornerX < 0f || cornerY < 0f) return 1f; // bord droit ou centre -> plein

            float dx = cx - cornerX, dy = cy - cornerY;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(r - dist + 0.5f);
        }
    }
}
