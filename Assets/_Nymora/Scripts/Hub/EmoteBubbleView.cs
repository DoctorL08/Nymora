using System.Collections;
using Nymora.Hub.Menu;
using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique E1 — Bulle d'émote affichée au-dessus d'un avatar hub.
    ///
    /// 100% View : dessinée en SpriteRenderers (pas de canvas world-space fragile) → suit l'avatar
    /// (composant posé sur le root), se trie au-dessus de tout sur le layer "Personnages", et reprend
    /// la DA menu (rectangle arrondi sombre via HubMenuUIFactory.RoundedSprite + queue triangulaire).
    ///
    /// Cycle : pop-in (scale + fondu) → maintien ~2,2 s → fondu sortant. Re-déclenchable (relance le
    /// cycle). Construit ses SpriteRenderers à la 1re utilisation (lazy), aucun setup scène/prefab.
    /// </summary>
    public sealed class EmoteBubbleView : MonoBehaviour
    {
        // Réglages (const — pas de valeurs gameplay, c'est de la présentation View pure).
        private const float EmoteWorldHeight = 1.25f;   // hauteur du chibi en unités world
        private const float Padding = 0.14f;            // marge bulle autour de l'émote
        private const float HeadOffsetY = 1.30f;        // bas de la bulle au-dessus du root avatar
        private const float HoldSeconds = 2.2f;
        private const float PopInSeconds = 0.16f;
        private const float FadeInSeconds = 0.12f;
        private const float FadeOutSeconds = 0.28f;
        private const int SortingOrder = 30000;          // au-dessus des avatars/torches du layer
        private const string SortingLayer = "Personnages";

        private static readonly Color BubbleColor = new Color(0.05f, 0.05f, 0.06f, 0.97f);
        private static readonly Color BorderColor = new Color(0.32f, 0.34f, 0.42f, 1f);

        private Transform _bubbleRoot;
        private SpriteRenderer _bg;
        private SpriteRenderer _border;
        private SpriteRenderer _tail;
        private SpriteRenderer _emote;
        private Coroutine _routine;

        /// <summary>Affiche (ou re-affiche) la bulle avec le sprite d'émote donné.</summary>
        public void Show(Sprite emoteSprite)
        {
            if (emoteSprite == null) return;
            EnsureBuilt();

            // Dimensionne l'émote en gardant le ratio, puis la bulle autour.
            float ratio = emoteSprite.rect.height > 0f ? emoteSprite.rect.width / emoteSprite.rect.height : 0.73f;
            float h = EmoteWorldHeight;
            float w = h * ratio;
            _emote.sprite = emoteSprite;
            _emote.transform.localScale = SpriteScaleFor(emoteSprite, w, h);

            float bw = w + Padding * 2f;
            float bh = h + Padding * 2f;
            _bg.size = new Vector2(bw, bh);
            _border.size = new Vector2(bw + 0.06f, bh + 0.06f);
            // Bulle centrée au-dessus de la tête.
            _bubbleRoot.localPosition = new Vector3(0f, HeadOffsetY + bh * 0.5f, 0f);
            _tail.transform.localPosition = new Vector3(0f, -bh * 0.5f - 0.02f, 0f);

            _bubbleRoot.gameObject.SetActive(true);
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(PlayCycle());
        }

        private IEnumerator PlayCycle()
        {
            float t = 0f;
            // Pop-in + fondu entrant.
            while (t < PopInSeconds)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / PopInSeconds);
                float ease = 1f - (1f - p) * (1f - p) * (1f - p); // ease-out cubic
                _bubbleRoot.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, ease);
                SetAlpha(Mathf.Clamp01(t / FadeInSeconds));
                yield return null;
            }
            _bubbleRoot.localScale = Vector3.one;
            SetAlpha(1f);

            yield return WaitUnscaled(HoldSeconds);

            // Fondu sortant + légère montée.
            float baseY = _bubbleRoot.localPosition.y;
            t = 0f;
            while (t < FadeOutSeconds)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / FadeOutSeconds);
                SetAlpha(1f - p);
                _bubbleRoot.localPosition = new Vector3(0f, baseY + p * 0.18f, 0f);
                yield return null;
            }
            _bubbleRoot.gameObject.SetActive(false);
            _routine = null;
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private void SetAlpha(float a)
        {
            SetA(_bg, a, BubbleColor);
            SetA(_border, a, BorderColor);
            SetA(_tail, a, BubbleColor);
            if (_emote != null) { var c = _emote.color; c.a = a; _emote.color = c; }
        }

        private static void SetA(SpriteRenderer sr, float a, Color baseColor)
        {
            if (sr == null) return;
            var c = baseColor; c.a = baseColor.a * a; sr.color = c;
        }

        /// <summary>Scale local pour qu'un sprite (en pixels/PPU) occupe w×h unités world.</summary>
        private static Vector3 SpriteScaleFor(Sprite s, float w, float h)
        {
            // bounds.size = taille du sprite en unités world à scale 1 (rect/PPU).
            Vector3 size = s.bounds.size;
            float sx = size.x > 0f ? w / size.x : 1f;
            float sy = size.y > 0f ? h / size.y : 1f;
            return new Vector3(sx, sy, 1f);
        }

        private void EnsureBuilt()
        {
            if (_bubbleRoot != null) return;

            var rootGo = new GameObject("EmoteBubble");
            _bubbleRoot = rootGo.transform;
            _bubbleRoot.SetParent(transform, false);

            var rounded = HubMenuUIFactory.RoundedSprite(28f);

            // Liseré (légèrement plus grand, derrière le fond).
            _border = MakeSlicedSprite("Border", _bubbleRoot, rounded, BorderColor, SortingOrder);
            // Fond sombre.
            _bg = MakeSlicedSprite("Bg", _bubbleRoot, rounded, BubbleColor, SortingOrder + 1);
            // Queue de la bulle (petit losange sombre sous le fond).
            _tail = MakeSlicedSprite("Tail", _bubbleRoot, rounded, BubbleColor, SortingOrder + 1);
            _tail.size = new Vector2(0.22f, 0.22f);
            _tail.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // L'émote (par-dessus tout).
            var emGo = new GameObject("Emote");
            emGo.transform.SetParent(_bubbleRoot, false);
            _emote = emGo.AddComponent<SpriteRenderer>();
            _emote.sortingLayerName = SortingLayer;
            _emote.sortingOrder = SortingOrder + 3;

            _bubbleRoot.gameObject.SetActive(false);
        }

        private static SpriteRenderer MakeSlicedSprite(string name, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.color = color;
            sr.sortingLayerName = SortingLayer;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
