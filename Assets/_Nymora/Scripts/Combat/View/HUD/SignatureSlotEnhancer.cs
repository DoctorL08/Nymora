using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Enhancer du signature slot (Soulrender AmeLaceree / Nightseer Traquenard / Colossar
    /// Effondrement / Necram Virus Fatal / Ghostra Execution Spectrale).
    ///
    /// Comportement :
    ///   - Hidden par defaut (au start combat le caster a 0 ressource).
    ///   - Apparition animee (scale bounce + fade-in) quand la ressource atteint max
    ///     (= signature debloque).
    ///   - Lueur gold pulsee permanente tant que visible (Image background tinte or, alpha sin).
    ///   - Disparition immediate apres cast (ressource consommee, retour <max).
    ///
    /// Auto-attached par CombatHUDController.Awake au _signatureSlot GameObject. Aucune manip
    /// Unity requise. Cree dynamiquement son Image "Glow" en frere (siblingIndex 0) au Initialize.
    /// </summary>
    public class SignatureSlotEnhancer : MonoBehaviour
    {
        // Style constants (calibres pour le HUD combat — ajustable plus tard).
        private const float ShowAnimDuration = 0.40f;
        private const float ScaleOvershoot = 1.25f;
        private const float GlowPulsePeriod = 1.4f;
        private const float GlowAlphaMin = 0.45f;
        private const float GlowAlphaMax = 1.0f;
        private static readonly Color GlowColor = new Color(1f, 0.82f, 0.20f, 1f); // or-doré
        private const float GlowSizeMultiplier = 1.25f; // glow déborde un peu autour du slot

        private Image _glow;
        private RectTransform _glowRt;
        private RectTransform _slotRt;
        private CanvasGroup _canvasGroup;
        private bool _currentlyUnlocked;
        private bool _initialized;
        private Coroutine _showAnimCoroutine;
        private float _glowPulsePhase;

        /// <summary>
        /// Appele une fois par CombatHUDController.Awake. Cree le Glow Image en sibling
        /// derriere le slot + ajoute un CanvasGroup au slot pour piloter l'alpha+scale en
        /// animation. Idempotent.
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;
            _slotRt = transform as RectTransform;
            if (_slotRt == null)
            {
                Debug.LogWarning("[SignatureSlotEnhancer] Pas de RectTransform sur le slot — attache impossible.", this);
                return;
            }

            // CanvasGroup pour piloter alpha global du slot pendant fade-in.
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Cree Glow en frere SOUS le slot (sibling 0, donc render BEHIND).
            var glowGo = new GameObject("SignatureGlow", typeof(RectTransform), typeof(Image));
            glowGo.transform.SetParent(_slotRt.parent, false);
            // Placer le glow JUSTE AVANT le slot dans la hierarchie pour qu'il rende derriere.
            int slotSiblingIdx = _slotRt.GetSiblingIndex();
            glowGo.transform.SetSiblingIndex(slotSiblingIdx);
            _glowRt = (RectTransform)glowGo.transform;
            // Match slot anchors + offset pour suivre le slot, mais agrandi en taille.
            _glowRt.anchorMin = _slotRt.anchorMin;
            _glowRt.anchorMax = _slotRt.anchorMax;
            _glowRt.anchoredPosition = _slotRt.anchoredPosition;
            _glowRt.sizeDelta = _slotRt.sizeDelta * GlowSizeMultiplier;
            _glowRt.pivot = _slotRt.pivot;

            _glow = glowGo.GetComponent<Image>();
            _glow.sprite = CreateRadialFalloffSprite();
            _glow.color = GlowColor;
            _glow.raycastTarget = false;

            // Start hidden.
            SetVisibleImmediate(false);
            _initialized = true;
        }

        /// <summary>
        /// API publique appelee chaque tick view par CombatHUDController. Detecte les
        /// transitions hidden -> visible (anim show) et visible -> hidden (cache instant).
        /// </summary>
        public void SetUnlocked(bool unlocked)
        {
            if (!_initialized) return;
            if (unlocked == _currentlyUnlocked) return; // idempotent

            _currentlyUnlocked = unlocked;
            if (unlocked)
            {
                if (_showAnimCoroutine != null) StopCoroutine(_showAnimCoroutine);
                _showAnimCoroutine = StartCoroutine(PlayShowAnim());
            }
            else
            {
                if (_showAnimCoroutine != null) StopCoroutine(_showAnimCoroutine);
                _showAnimCoroutine = null;
                SetVisibleImmediate(false);
            }
        }

        private void Update()
        {
            // Pulse alpha du glow (sin wave) tant que visible.
            if (!_initialized || !_currentlyUnlocked || _glow == null) return;
            _glowPulsePhase += Time.deltaTime / GlowPulsePeriod;
            float t = (Mathf.Sin(_glowPulsePhase * Mathf.PI * 2f) + 1f) * 0.5f; // 0..1
            float alpha = Mathf.Lerp(GlowAlphaMin, GlowAlphaMax, t);
            var c = GlowColor; c.a = alpha;
            _glow.color = c;
            // Suit le slot si jamais il a bouge (defensif).
            if (_slotRt != null && _glowRt != null)
            {
                _glowRt.anchoredPosition = _slotRt.anchoredPosition;
            }
        }

        private void SetVisibleImmediate(bool visible)
        {
            // IMPORTANT : on NE désactive PAS le GameObject du slot signature, sinon les coroutines
            // (PlayShowAnim) ne peuvent plus etre lancees + Update ne tourne plus. On utilise le
            // CanvasGroup pour cacher visuellement + bloquer les clics. Seul le Glow (GameObject
            // separe) peut etre SetActive(false) — il est attache au parent du slot, pas au slot.
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.interactable = visible;
                _canvasGroup.blocksRaycasts = visible;
            }
            if (_glow != null) _glow.gameObject.SetActive(visible);
            if (_slotRt != null) _slotRt.localScale = Vector3.one;
        }

        private IEnumerator PlayShowAnim()
        {
            // Start at alpha 0 + scale 0 (mais le GameObject du slot reste TOUJOURS actif).
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
            if (_glow != null) _glow.gameObject.SetActive(true);
            if (_slotRt != null) _slotRt.localScale = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < ShowAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / ShowAnimDuration);
                // Alpha : ease-out cubic
                float alpha = 1f - Mathf.Pow(1f - t, 3f);
                if (_canvasGroup != null) _canvasGroup.alpha = alpha;
                // Scale : overshoot puis settle (back-out)
                //   0 -> 0.5 : 0 -> Overshoot
                //   0.5 -> 1 : Overshoot -> 1
                float scale = t < 0.5f
                    ? Mathf.Lerp(0f, ScaleOvershoot, t * 2f)
                    : Mathf.Lerp(ScaleOvershoot, 1f, (t - 0.5f) * 2f);
                if (_slotRt != null) _slotRt.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            if (_slotRt != null) _slotRt.localScale = Vector3.one;
            _showAnimCoroutine = null;
        }

        /// <summary>
        /// Sprite radial soft (centre opaque, bords transparents). Cree en code pour eviter
        /// dependance asset. Pattern : disk avec falloff lineaire en alpha.
        /// </summary>
        private static Sprite CreateRadialFalloffSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float maxDist = center;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float t = Mathf.Clamp01(1f - d / maxDist);
                    // Falloff quadratic plus doux qu'un linear.
                    float alpha = t * t;
                    px[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
