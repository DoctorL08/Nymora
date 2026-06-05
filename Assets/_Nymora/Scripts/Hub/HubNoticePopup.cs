using Nymora.Hub.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Pop-up modale d'information générique, au design du menu hub (monochrome, police Ari).
    /// Un seul bouton de fermeture + clic hors-carte pour fermer. Construite 100% en code
    /// (Screen Space Overlay) → aucune manip Unity, appelable depuis n'importe quel script Hub
    /// via les helpers statiques.
    ///
    /// Usage :
    ///   HubNoticePopup.Show("Titre", "Corps du message");
    ///   HubNoticePopup.ShowNoDeck();   // raccourci "crée un deck avant de lancer un combat"
    ///
    /// Le thème vient de <see cref="HubMenuShell.MenuTheme"/> (posé à l'init du shell hub) ; repli
    /// brut si null (ex : scène sans menu hub). 100% View → aucun impact simulation.
    /// </summary>
    public sealed class HubNoticePopup : MonoBehaviour
    {
        // Une seule pop-up affichée à la fois.
        private static HubNoticePopup _current;

        private static HubMenuTheme Theme => HubMenuShell.MenuTheme;

        /// <summary>Raccourci "lancement de combat impossible, crée un deck d'abord". Appelé par les
        /// 4 entrées de combat (Arène IA, recherche ranked, défi casual envoyé/accepté).</summary>
        public static void ShowNoDeck()
        {
            Show("Aucun deck équipé",
                 "Lancement de combat impossible.\n\nCrée un deck dans le Deck Builder avant de lancer un combat.");
        }

        /// <summary>Affiche la pop-up. Remplace toute pop-up déjà ouverte.</summary>
        public static void Show(string title, string body, string buttonLabel = "Compris")
        {
            if (_current != null) Destroy(_current.gameObject);

            var go = new GameObject("[HubNoticePopup]");
            var popup = go.AddComponent<HubNoticePopup>();
            _current = popup;
            popup.Build(title, body, buttonLabel);
        }

        private void Build(string title, string body, string buttonLabel)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Au-dessus du hub + menus contextuels, SOUS le voile de SceneTransition (32760).
            canvas.sortingOrder = 30500;
            gameObject.AddComponent<GraphicRaycaster>();
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Voile sombre plein écran : bloque les clics derrière + clic hors-carte = fermeture.
            var dim = NewImage(transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
            HubMenuUIFactory.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Dismiss);

            // Carte ajoutée APRÈS le voile → rendue au-dessus + intercepte les clics (ne ferme pas).
            if (Theme != null) BuildStyledCard(transform, title, body, buttonLabel);
            else BuildRawCard(transform, title, body, buttonLabel);
        }

        // Carte au design du menu hub (HubMenuUIFactory + thème monochrome).
        private void BuildStyledCard(Transform parent, string title, string body, string buttonLabel)
        {
            var t = Theme;
            var f = new HubMenuUIFactory(t);

            var card = f.MakeImage("Card", parent, t.PanelBg);
            card.raycastTarget = true; // bloque le clic vers le voile derrière
            var crt = card.rectTransform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(680f, 320f);
            crt.anchoredPosition = Vector2.zero;

            var titleTmp = f.MakeText("Title", crt, title, t.FontSizeTitle, t.TextPrimary, t.FontBold, TextAlignmentOptions.Center);
            titleTmp.raycastTarget = false;
            var trt = titleTmp.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(-56f, 58f); trt.anchoredPosition = new Vector2(0f, -28f);

            var bodyTmp = f.MakeText("Body", crt, body, t.FontSizeBody, t.TextSecondary, t.Font, TextAlignmentOptions.Center);
            bodyTmp.raycastTarget = false;
            var brt = bodyTmp.rectTransform;
            brt.anchorMin = new Vector2(0f, 0.5f); brt.anchorMax = new Vector2(1f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(-80f, 150f); brt.anchoredPosition = new Vector2(0f, 6f);

            var ok = f.MakeButton(crt, buttonLabel, true, out _);
            var ort = (RectTransform)ok.transform;
            ort.anchorMin = ort.anchorMax = ort.pivot = new Vector2(0.5f, 0f);
            ort.sizeDelta = new Vector2(220f, 52f); ort.anchoredPosition = new Vector2(0f, 30f);
            ok.onClick.AddListener(Dismiss);
        }

        // Repli sans thème (mêmes infos, style brut) — ex : scène sans HubMenuShell.
        private void BuildRawCard(Transform parent, string title, string body, string buttonLabel)
        {
            var card = NewImage(parent, "Card", new Color(0.10f, 0.10f, 0.13f, 0.99f));
            card.raycastTarget = true;
            var crt = card.rectTransform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(680f, 320f);
            crt.anchoredPosition = Vector2.zero;

            var titleTmp = RawText(card.transform, "Title", title, 36f, new Color(0.96f, 0.96f, 0.98f));
            titleTmp.fontStyle = FontStyles.Bold;
            var trt = titleTmp.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(-56f, 58f); trt.anchoredPosition = new Vector2(0f, -28f);

            var bodyTmp = RawText(card.transform, "Body", body, 23f, new Color(0.80f, 0.82f, 0.88f));
            var brt = bodyTmp.rectTransform;
            brt.anchorMin = new Vector2(0f, 0.5f); brt.anchorMax = new Vector2(1f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(-80f, 150f); brt.anchoredPosition = new Vector2(0f, 6f);

            var btnImg = NewImage(card.transform, "Btn_OK", new Color(0.42f, 0.34f, 0.66f));
            var ort = btnImg.rectTransform;
            ort.anchorMin = ort.anchorMax = ort.pivot = new Vector2(0.5f, 0f);
            ort.sizeDelta = new Vector2(220f, 52f); ort.anchoredPosition = new Vector2(0f, 30f);
            var btn = btnImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var lbl = RawText(btnImg.transform, "Label", buttonLabel, 24f, new Color(0.98f, 0.98f, 1f));
            HubMenuUIFactory.Stretch(lbl.rectTransform);
            btn.onClick.AddListener(Dismiss);
        }

        private void Dismiss()
        {
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_current == this) _current = null;
        }

        // ===== Helpers UI repli (sans thème) =====

        private static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static TextMeshProUGUI RawText(Transform parent, string name, string text, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
