using System.Collections;
using System.Text;
using Nymora.Hub.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique C1 — Bulle de chat (texte) affichée au-dessus d'un avatar hub quand son joueur écrit
    /// dans le canal global. Rendue dans un Canvas world-space (enfant du root avatar → suit le perso)
    /// pour bénéficier de l'auto-dimensionnement texte (ContentSize via GetPreferredValues) tout en
    /// reprenant la DA menu (rectangle arrondi sombre + liseré + queue, police Ari).
    ///
    /// Cycle : pop-in (scale + fondu) → maintien proportionnel à la longueur → fondu sortant.
    /// Re-déclenchable (un nouveau message remplace le précédent). Hub uniquement.
    /// </summary>
    public sealed class ChatBubbleView : MonoBehaviour
    {
        // Présentation (const — View pure, pas de valeurs gameplay).
        private const float HeadOffsetY = 1.35f;   // bas de la bulle au-dessus du root avatar
        private const float WorldScale = 0.0045f;   // px UI -> unités world
        private const float FontSize = 44f;
        private const float MaxTextWidth = 440f;     // px : largeur max avant retour ligne
        private const float PadX = 22f;
        private const float PadY = 14f;
        private const float MinHold = 3f;
        private const float MaxHold = 7f;
        private const float PerCharSeconds = 0.05f;
        private const float PopInSeconds = 0.16f;
        private const float FadeInSeconds = 0.12f;
        private const float FadeOutSeconds = 0.30f;
        private const int SortingOrder = 30020;       // au-dessus de la bulle d'emote (30000)
        private const string SortingLayer = "Personnages";
        private const int MaxChars = 140;

        private static readonly Color BubbleColor = new Color(0.05f, 0.05f, 0.06f, 0.97f);
        private static readonly Color BorderColor = new Color(0.32f, 0.34f, 0.42f, 1f);
        private static readonly Color TextColor = new Color(0.93f, 0.94f, 0.96f, 1f);

        private Canvas _canvas;
        private RectTransform _root;
        private CanvasGroup _group;
        private RectTransform _tail;
        private TextMeshProUGUI _text;
        private Coroutine _routine;

        public void Show(string text, TMP_FontAsset font)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            EnsureBuilt();
            _canvas.gameObject.SetActive(true); // actif AVANT de mesurer (GetPreferredValues fiable)
            if (font != null) _text.font = font;

            string clean = Sanitize(text);
            _text.text = clean;

            // Auto-dimensionne la bulle au texte (largeur bornée à MaxTextWidth, hauteur libre).
            Vector2 pref = _text.GetPreferredValues(clean, MaxTextWidth, 0f);
            float tw = Mathf.Clamp(pref.x, 30f, MaxTextWidth);
            float th = pref.y;
            _root.sizeDelta = new Vector2(tw + PadX * 2f, th + PadY * 2f);
            _root.localPosition = new Vector3(0f, HeadOffsetY, 0f);

            if (_routine != null) StopCoroutine(_routine);
            float hold = Mathf.Clamp(MinHold + clean.Length * PerCharSeconds, MinHold, MaxHold);
            _routine = StartCoroutine(PlayCycle(hold));
        }

        private IEnumerator PlayCycle(float hold)
        {
            float t = 0f;
            while (t < PopInSeconds)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / PopInSeconds);
                float ease = 1f - (1f - p) * (1f - p) * (1f - p);
                _root.localScale = Vector3.one * (WorldScale * Mathf.Lerp(0.85f, 1f, ease));
                _group.alpha = Mathf.Clamp01(t / FadeInSeconds);
                yield return null;
            }
            _root.localScale = Vector3.one * WorldScale;
            _group.alpha = 1f;

            float h = 0f;
            while (h < hold) { h += Time.unscaledDeltaTime; yield return null; }

            t = 0f;
            while (t < FadeOutSeconds)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = 1f - Mathf.Clamp01(t / FadeOutSeconds);
                yield return null;
            }
            _canvas.gameObject.SetActive(false);
            _routine = null;
        }

        private void EnsureBuilt()
        {
            if (_root != null) return;

            var go = new GameObject("ChatBubble", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            _root = go.GetComponent<RectTransform>();
            _root.SetParent(transform, false);
            _root.pivot = new Vector2(0.5f, 0f); // ancre au bas → la bulle pousse vers le haut
            _root.localScale = Vector3.one * WorldScale;

            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingLayerName = SortingLayer;
            _canvas.sortingOrder = SortingOrder;

            _group = go.GetComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            var rounded = HubMenuUIFactory.RoundedSprite(24f);

            // Liseré (derrière, légèrement débordant).
            var border = MakeImage("Border", _root, rounded, BorderColor);
            Stretch(border.rectTransform, -3f);
            // Fond.
            var bg = MakeImage("Bg", _root, rounded, BubbleColor);
            Stretch(bg.rectTransform, 0f);

            // Queue (petit losange sombre sous la bulle).
            var tail = MakeImage("Tail", _root, rounded, BubbleColor);
            _tail = tail.rectTransform;
            _tail.anchorMin = new Vector2(0.5f, 0f); _tail.anchorMax = new Vector2(0.5f, 0f);
            _tail.pivot = new Vector2(0.5f, 0.5f);
            _tail.sizeDelta = new Vector2(20f, 20f);
            _tail.anchoredPosition = new Vector2(0f, 2f);
            _tail.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // Texte.
            var txtGo = new GameObject("Text", typeof(RectTransform));
            var trt = txtGo.GetComponent<RectTransform>();
            trt.SetParent(_root, false);
            Stretch(trt, 0f);
            trt.offsetMin = new Vector2(PadX, PadY);
            trt.offsetMax = new Vector2(-PadX, -PadY);
            _text = txtGo.AddComponent<TextMeshProUGUI>();
            _text.fontSize = FontSize;
            _text.color = TextColor;
            _text.alignment = TextAlignmentOptions.Center;
            _text.enableWordWrapping = true;
            _text.raycastTarget = false;
            if (HubMenuShell.MenuFont != null) _text.font = HubMenuShell.MenuFont;

            _canvas.gameObject.SetActive(false);
        }

        private static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void Stretch(RectTransform rt, float expand)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-expand, -expand);
            rt.offsetMax = new Vector2(expand, expand);
        }

        private static string Sanitize(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            foreach (char ch in raw)
                sb.Append(ch == '\n' || ch == '\r' || ch == '\t' ? ' ' : ch);
            string s = sb.ToString().Trim();
            if (s.Length > MaxChars) s = s.Substring(0, MaxChars - 1).TrimEnd() + "…";
            return s;
        }
    }
}
