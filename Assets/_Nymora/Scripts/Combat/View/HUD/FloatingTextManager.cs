using System.Collections;
using TMPro;
using UnityEngine;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Spawne des FloatingText au-dessus des combatants quand HP change.
    /// Le canvas dedie (ScreenSpaceOverlay, sortingOrder &lt; HUD) parent tous les textes.
    ///
    /// Pour le moment couleurs hardcodees :
    ///   - Damage : rouge vif
    ///   - Heal   : vert pale
    /// (Bleu shield absorb : Phase 2.13.d ou plus tard, necessite un signal Quantum.)
    /// </summary>
    public class FloatingTextManager : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Camera _worldCamera;

        [Header("Style")]
        [SerializeField] private Color _damageColor = new Color(0.91f, 0.29f, 0.29f, 1f);
        [SerializeField] private Color _healColor = new Color(0.48f, 0.82f, 0.42f, 1f);
        [SerializeField] private float _fontSize = 36f;
        [SerializeField] private float _durationSeconds = 1.0f;
        [SerializeField] private float _riseDistancePx = 60f;
        [SerializeField] private float _fadeStartSeconds = 0.5f;

        private void Awake()
        {
            if (_worldCamera == null) _worldCamera = Camera.main;
        }

        /// <summary>
        /// Spawn un texte flottant a `worldPos`. Sign de la valeur :
        ///   - amount &lt; 0 -> damage rouge "-N"
        ///   - amount &gt; 0 -> heal vert "+N"
        ///   - amount == 0 : ignore
        /// </summary>
        public void Spawn(Vector3 worldPos, int amount)
        {
            if (amount == 0 || _canvas == null || _worldCamera == null) return;

            string text = amount < 0 ? amount.ToString() : "+" + amount;
            Color color = amount < 0 ? _damageColor : _healColor;

            var screenPos = _worldCamera.WorldToScreenPoint(worldPos);
            // Pour ScreenSpaceOverlay, position pixel = position de la RectTransform.
            var go = new GameObject("FloatingText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(FloatingText));
            go.transform.SetParent(_canvas.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(160f, 50f);
            rt.position = screenPos;

            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.color = color;
            label.fontSize = _fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableWordWrapping = false;

            var floater = go.GetComponent<FloatingText>();
            // FloatingText fait GetComponent<TMP_Text>() dans Configure, pas besoin de cabler manuellement.
            floater.Configure(text, color, _durationSeconds, _riseDistancePx, _fadeStartSeconds);
        }

        // 19 mai POLISH-6h — Variante EPIQUE FULL BANG pour sorts signature.
        //
        // Cocktail d'effets :
        //   1. Texte ENORME (fontSize x3.5) couleur or + outline rouge sang epais
        //   2. Pop violent (scale 0 -> 2.8 overshoot -> 1.0 en 0.35s, ease-out brutal)
        //   3. Rotation aleatoire initiale (-12 a +12 deg) pour effet "claque"
        //   4. Shake position (15px amplitude, freq haute) pendant 0.35s
        //   5. Color flash blanc -> or pulse en debut
        //   6. Onomatopee "BANG!" rouge en dessous, plus petite, sync sur le bounce
        //   7. Camera shake legere (0.2 amplitude world) pendant 0.25s
        //   8. Duration + rise plus longs (texte reste lisible une bonne 1.5s)
        [Header("Signature EPIC FULL BANG (POLISH-6h)")]
        [SerializeField] private Color _signatureColor = new Color(1f, 0.85f, 0.20f, 1f);
        [SerializeField] private Color _signatureOutlineColor = new Color(0.55f, 0.08f, 0.05f, 1f); // rouge sang
        [SerializeField] private Color _signatureFlashColor = Color.white;
        [SerializeField] private float _signatureFontMultiplier = 3.5f;
        [SerializeField] private float _signatureDurationMultiplier = 1.8f;
        [SerializeField] private float _signatureRiseMultiplier = 1.8f;
        [SerializeField] private float _signatureBounceDuration = 0.35f;
        [SerializeField] private float _signatureBouncePeak = 2.8f;
        [SerializeField] private float _signatureShakeDuration = 0.35f;
        [SerializeField] private float _signatureShakeAmplitudePx = 18f;
        [SerializeField] private float _signatureShakeFrequency = 38f;
        [SerializeField] private float _signatureRotationMaxDeg = 12f;
        [SerializeField] private float _signatureCameraShakeAmplitude = 0.18f;
        [SerializeField] private float _signatureCameraShakeDuration = 0.30f;
        [SerializeField] private float _signatureFlashDuration = 0.18f;
        [SerializeField] private string _signatureOnomatopee = "BANG !";
        [SerializeField] private float _signatureOnomatopeeFontMultiplier = 2.0f;

        public void SpawnSignatureHit(Vector3 worldPos, int amount)
        {
            if (amount == 0 || _canvas == null || _worldCamera == null) return;

            string text = amount < 0 ? amount.ToString() : "+" + amount;
            var screenPos = _worldCamera.WorldToScreenPoint(worldPos);

            // ============ MAIN BIG NUMBER ============
            var mainGo = new GameObject("FloatingTextSignature", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(FloatingText));
            mainGo.transform.SetParent(_canvas.transform, false);
            var rt = mainGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(480f, 160f);
            rt.position = screenPos;
            // Rotation random initiale pour effet "claque oblique".
            float rotZ = Random.Range(-_signatureRotationMaxDeg, _signatureRotationMaxDeg);
            rt.localRotation = Quaternion.Euler(0f, 0f, rotZ);

            var label = mainGo.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.color = _signatureFlashColor; // flash blanc au depart, transitionne vers or
            label.fontSize = _fontSize * _signatureFontMultiplier;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            // Outline rouge sang epais — instancie le material.
            label.fontMaterial = new Material(label.fontMaterial);
            label.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, _signatureOutlineColor);
            label.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.40f);
            // Faceglow leger pour booster l'effet "incandescent" (TMP underlay).
            if (label.fontMaterial.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                label.fontMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(1f, 0.55f, 0.10f, 0.7f));
                label.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.5f);
                label.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.6f);
            }

            var floater = mainGo.GetComponent<FloatingText>();
            floater.Configure(text, _signatureFlashColor,
                _durationSeconds * _signatureDurationMultiplier,
                _riseDistancePx * _signatureRiseMultiplier,
                _fadeStartSeconds * _signatureDurationMultiplier);

            StartCoroutine(BounceScale(rt));
            StartCoroutine(ShakePosition(rt));
            StartCoroutine(FlashColor(label));
            StartCoroutine(CameraShake());

            // ============ ONOMATOPEE "BANG !" ============
            if (!string.IsNullOrEmpty(_signatureOnomatopee))
            {
                var bangGo = new GameObject("FloatingTextSignatureBang", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(FloatingText));
                bangGo.transform.SetParent(_canvas.transform, false);
                var bangRt = bangGo.GetComponent<RectTransform>();
                bangRt.anchorMin = bangRt.anchorMax = new Vector2(0.5f, 0.5f);
                bangRt.pivot = new Vector2(0.5f, 0.5f);
                bangRt.sizeDelta = new Vector2(400f, 100f);
                // Position : juste en dessous du chiffre principal.
                bangRt.position = screenPos + new Vector3(0f, -70f, 0f);
                bangRt.localRotation = Quaternion.Euler(0f, 0f, -rotZ); // rotation inverse, effet symetrique

                var bangLabel = bangGo.GetComponent<TextMeshProUGUI>();
                bangLabel.text = _signatureOnomatopee;
                bangLabel.color = new Color(1f, 0.25f, 0.15f, 1f); // rouge feu
                bangLabel.fontSize = _fontSize * _signatureOnomatopeeFontMultiplier;
                bangLabel.fontStyle = FontStyles.Bold | FontStyles.Italic;
                bangLabel.alignment = TextAlignmentOptions.Center;
                bangLabel.raycastTarget = false;
                bangLabel.enableWordWrapping = false;
                bangLabel.fontMaterial = new Material(bangLabel.fontMaterial);
                bangLabel.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                bangLabel.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.30f);

                var bangFloater = bangGo.GetComponent<FloatingText>();
                bangFloater.Configure(_signatureOnomatopee, new Color(1f, 0.25f, 0.15f, 1f),
                    _durationSeconds * _signatureDurationMultiplier * 0.7f,
                    _riseDistancePx * 0.4f,
                    _fadeStartSeconds * _signatureDurationMultiplier * 0.7f);

                StartCoroutine(BounceScale(bangRt));
                StartCoroutine(ShakePosition(bangRt));
            }
        }

        private IEnumerator BounceScale(RectTransform rt)
        {
            if (rt == null) yield break;
            float duration = _signatureBounceDuration;
            float peak = _signatureBouncePeak;
            float elapsed = 0f;
            while (elapsed < duration && rt != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 0..0.4 : 0 -> peak (très rapide) ; 0.4..1 : peak -> 1.0 (settle plus lent)
                float scale = t < 0.4f
                    ? Mathf.Lerp(0f, peak, t / 0.4f)
                    : Mathf.Lerp(peak, 1f, (t - 0.4f) / 0.6f);
                rt.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        private IEnumerator ShakePosition(RectTransform rt)
        {
            if (rt == null) yield break;
            Vector3 origin = rt.position;
            float duration = _signatureShakeDuration;
            float amp = _signatureShakeAmplitudePx;
            float freq = _signatureShakeFrequency;
            float elapsed = 0f;
            while (elapsed < duration && rt != null)
            {
                elapsed += Time.deltaTime;
                // Amplitude decroissante lineairement (full at start, 0 at end).
                float decay = 1f - (elapsed / duration);
                float dx = Mathf.Sin(elapsed * freq) * amp * decay;
                float dy = Mathf.Cos(elapsed * freq * 1.3f) * amp * 0.5f * decay;
                rt.position = origin + new Vector3(dx, dy, 0f);
                yield return null;
            }
            // Pas de reset position : FloatingText.Update gere le rise, donc on laisse la
            // derniere position du shake et le rise continuera depuis la.
        }

        private IEnumerator FlashColor(TextMeshProUGUI label)
        {
            if (label == null) yield break;
            float duration = _signatureFlashDuration;
            float elapsed = 0f;
            Color flash = _signatureFlashColor;
            Color target = _signatureColor;
            while (elapsed < duration && label != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Color c = Color.Lerp(flash, target, t);
                // Preserve alpha set par FloatingText.Update (fade out).
                c.a = label.color.a;
                label.color = c;
                yield return null;
            }
            // Apres flash, on n'override plus la couleur — FloatingText.Update gere alpha,
            // la couleur reste celle qu'on vient de fixer (or).
        }

        private IEnumerator CameraShake()
        {
            var cam = _worldCamera != null ? _worldCamera : Camera.main;
            if (cam == null) yield break;
            Transform camT = cam.transform;
            Vector3 origin = camT.localPosition;
            float duration = _signatureCameraShakeDuration;
            float amp = _signatureCameraShakeAmplitude;
            float elapsed = 0f;
            while (elapsed < duration && camT != null)
            {
                elapsed += Time.deltaTime;
                float decay = 1f - (elapsed / duration);
                float dx = (Random.value - 0.5f) * 2f * amp * decay;
                float dy = (Random.value - 0.5f) * 2f * amp * decay;
                camT.localPosition = origin + new Vector3(dx, dy, 0f);
                yield return null;
            }
            if (camT != null) camT.localPosition = origin;
        }
    }
}
