using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.UI
{
    /// <summary>
    /// Pop-up modale minimaliste, montée 100% par code (aucun prefab / manip Unity).
    /// Utilisée pour notifier une déconnexion forcée (kick / ban / maintenance) sur
    /// l'écran de login. Canvas overlay au-dessus de tout, bouton OK pour fermer.
    /// </summary>
    public static class SimpleNotice
    {
        public static void Show(string title, string message)
        {
            var go = new GameObject("[SimpleNotice]");
            Object.DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760; // au-dessus de tout
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();

            // Voile sombre plein écran (bloque les clics derrière).
            var dim = NewChild(go.transform, "Dim");
            Stretch(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.72f);

            // Boîte centrale.
            var box = NewChild(go.transform, "Box");
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(640f, 320f);
            box.anchoredPosition = Vector2.zero;
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.color = new Color(0.09f, 0.10f, 0.13f, 1f);

            // Titre (rouge).
            var t = MakeText(box, title, 30, FontStyles.Bold, new Color(1f, 0.30f, 0.30f));
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.anchorMin = new Vector2(0f, 1f);
            t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.pivot = new Vector2(0.5f, 1f);
            t.rectTransform.sizeDelta = new Vector2(-48f, 54f);
            t.rectTransform.anchoredPosition = new Vector2(0f, -26f);

            // Message.
            var m = MakeText(box, message, 22, FontStyles.Normal, Color.white);
            m.alignment = TextAlignmentOptions.Center;
            m.enableWordWrapping = true;
            m.rectTransform.anchorMin = new Vector2(0f, 0f);
            m.rectTransform.anchorMax = new Vector2(1f, 1f);
            m.rectTransform.offsetMin = new Vector2(32f, 84f);
            m.rectTransform.offsetMax = new Vector2(-32f, -92f);

            // Bouton OK.
            var btn = NewChild(box, "OK");
            btn.anchorMin = btn.anchorMax = new Vector2(0.5f, 0f);
            btn.pivot = new Vector2(0.5f, 0f);
            btn.sizeDelta = new Vector2(180f, 50f);
            btn.anchoredPosition = new Vector2(0f, 26f);
            var btnImg = btn.gameObject.AddComponent<Image>();
            btnImg.color = new Color(0.23f, 0.42f, 0.94f, 1f);
            var button = btn.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => Object.Destroy(go));
            var bt = MakeText(btn, "OK", 22, FontStyles.Bold, Color.white);
            Stretch(bt.rectTransform);
            bt.alignment = TextAlignmentOptions.Center;
        }

        private static RectTransform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string text, float size, FontStyles style, Color color)
        {
            var rt = NewChild(parent, "Text");
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            return tmp;
        }
    }
}
