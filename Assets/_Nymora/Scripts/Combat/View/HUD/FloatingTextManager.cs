using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Spawne des FloatingText au-dessus des combatants quand HP change.
    /// Le canvas dedie (ScreenSpaceOverlay, sortingOrder &lt; HUD) parent tous les textes.
    ///
    /// Couleurs :
    ///   - Damage : rouge (vire orange sur gros coup)
    ///   - Heal   : vert pale
    ///   - Bouclier : bleu spectral (gain / absorption)
    ///   - SIGNATURE : flottant "SMASH!!" dark fantasy (eclaboussure de sang + mot + chiffre).
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

        // J3 (juice combat) — pop d'apparition + dispersion + emphase des gros coups sur les
        // chiffres NORMAUX. Tunables Inspector.
        [Header("Juice chiffres normaux (J3)")]
        [Tooltip("Duree du pop d'apparition (scale-in avec overshoot).")]
        [SerializeField] private float _popInDuration = 0.16f;
        [Tooltip("Scale max atteint pendant le pop avant de retomber a 1.")]
        [SerializeField] private float _popOvershoot = 1.18f;
        [Tooltip("Dispersion horizontale aleatoire (px) pour que les coups successifs ne se superposent pas pile.")]
        [SerializeField] private float _horizontalScatterPx = 16f;
        [Tooltip("Bonus de taille de police a emphase=1 (gros coup). 0.6 = +60%.")]
        [SerializeField] private float _emphasisFontBonus = 0.6f;
        [Tooltip("Couleur cible des gros degats (le rouge vire vers cet orange brulant selon l'emphase).")]
        [SerializeField] private Color _bigDamageColor = new Color(1f, 0.55f, 0.12f, 1f);
        [Tooltip("Couleur des chiffres de bouclier (gain +N / absorption -N). Bleu spectral, calé sur le tooltip.")]
        [SerializeField] private Color _shieldColor = new Color(0.5f, 0.78f, 1f, 1f);

        // Signature "SMASH!!" — dark fantasy / trash. Mot percutant + chiffre dessous, sur une
        // eclaboussure de sang generee par code (BloodSplatSprite). Police Anton (lourde).
        [Header("Signature \"SMASH!!\" (dark fantasy)")]
        [SerializeField] private string _signatureWord = "SMASH!!";
        [Tooltip("Couleur du mot SMASH (os/blanc casse, pour ressortir sur le sang).")]
        [SerializeField] private Color _signatureWordColor = new Color(0.94f, 0.91f, 0.85f, 1f);
        [Tooltip("Couleur du chiffre de degats (sang vif).")]
        [SerializeField] private Color _signatureNumberColor = new Color(0.86f, 0.13f, 0.12f, 1f);
        [Tooltip("Contour des textes (presque noir).")]
        [SerializeField] private Color _signatureOutlineColor = new Color(0.04f, 0.02f, 0.02f, 1f);
        [Tooltip("Ombre portee sanglante (underlay).")]
        [SerializeField] private Color _signatureShadowColor = new Color(0.45f, 0.03f, 0.04f, 0.9f);
        [SerializeField] private Color _signatureFlashColor = Color.white;
        [Tooltip("Taille du mot SMASH = taille de base x ce facteur.")]
        [SerializeField] private float _signatureWordFontMultiplier = 3.4f;
        [Tooltip("Taille du chiffre = taille de base x ce facteur.")]
        [SerializeField] private float _signatureNumberFontMultiplier = 2.3f;
        [Tooltip("Largeur de l'eclaboussure = largeur du bloc texte x ce facteur.")]
        [SerializeField] private float _signatureSplatWidthFactor = 1.5f;
        [SerializeField] private float _signatureDurationMultiplier = 1.9f;
        [SerializeField] private float _signatureRiseDistancePx = 55f;
        [Tooltip("Inclinaison aleatoire max de l'ensemble (deg).")]
        [SerializeField] private float _signatureTiltMaxDeg = 6f;
        [SerializeField] private float _signatureShakeDuration = 0.28f;
        [SerializeField] private float _signatureShakeAmplitudePx = 16f;
        [SerializeField] private float _signatureCameraShakeAmplitude = 0.20f;
        [SerializeField] private float _signatureCameraShakeDuration = 0.32f;
        [Tooltip("Échelle globale du flottant signature (0.5 = 2× plus petit).")]
        [SerializeField] private float _signatureScale = 0.5f;
        [Tooltip("Décalage vertical (px écran) pour afficher le flottant AU-DESSUS de la cible (pas dessus).")]
        [SerializeField] private float _signatureAboveTargetPx = 120f;

        private void Awake()
        {
            if (_worldCamera == null) _worldCamera = Camera.main;
            // Pre-genere les eclaboussures au chargement (derriere l'ecran de transition)
            // -> zero hitch au 1er cast de signature en plein combat.
            BloodSplatSprite.Prewarm();
        }

        // ============================================================ CHIFFRES NORMAUX (J3/J5)

        /// <summary>
        /// Spawn un texte flottant a `worldPos`. amount &lt; 0 -> damage "-N", amount &gt; 0 -> heal "+N".
        /// </summary>
        public void Spawn(Vector3 worldPos, int amount) => Spawn(worldPos, amount, 0f);

        /// <summary>
        /// J3 — Variante avec emphase. `emphasis01` (0..1) = fraction de PV perdue : plus le coup est
        /// gros, plus le chiffre est gros, vire orange et pop fort. 0 = rendu standard.
        /// </summary>
        public void Spawn(Vector3 worldPos, int amount, float emphasis01)
        {
            if (amount == 0) return;
            emphasis01 = Mathf.Clamp01(emphasis01);
            string text = amount < 0 ? amount.ToString() : "+" + amount;
            Color color = amount < 0 ? Color.Lerp(_damageColor, _bigDamageColor, emphasis01) : _healColor;
            SpawnNumber(worldPos, text, color, emphasis01);
        }

        /// <summary>
        /// J5 — Chiffre de BOUCLIER (bleu spectral). amount &gt; 0 = gain "+N", amount &lt; 0 = absorption "-N".
        /// </summary>
        public void SpawnShield(Vector3 worldPos, int amount)
        {
            if (amount == 0) return;
            string text = amount < 0 ? amount.ToString() : "+" + amount;
            SpawnNumber(worldPos, text, _shieldColor, 0f);
        }

        private void SpawnNumber(Vector3 worldPos, string text, Color color, float emphasis01)
        {
            if (_canvas == null || _worldCamera == null) return;
            emphasis01 = Mathf.Clamp01(emphasis01);

            var screenPos = _worldCamera.WorldToScreenPoint(worldPos);
            screenPos.x += Random.Range(-_horizontalScatterPx, _horizontalScatterPx);
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
            label.fontSize = _fontSize * (1f + _emphasisFontBonus * emphasis01);
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableWordWrapping = false;

            var floater = go.GetComponent<FloatingText>();
            floater.Configure(text, color, _durationSeconds, _riseDistancePx, _fadeStartSeconds);

            StartCoroutine(PopIn(rt, emphasis01));
        }

        private IEnumerator PopIn(RectTransform rt, float emphasis01)
        {
            if (rt == null) yield break;
            float overshoot = Mathf.Lerp(_popOvershoot, _popOvershoot + 0.25f, emphasis01);
            float elapsed = 0f;
            while (elapsed < _popInDuration && rt != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _popInDuration);
                float scale = t < 0.5f
                    ? Mathf.Lerp(0.4f, overshoot, t / 0.5f)
                    : Mathf.Lerp(overshoot, 1f, (t - 0.5f) / 0.5f);
                rt.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        // ============================================================ SIGNATURE "SMASH!!"

        /// <summary>
        /// Flottant de degats SIGNATURE dark fantasy : eclaboussure de sang + mot "SMASH!!" + chiffre
        /// dessous, le tout solidaire (conteneur unique qui snap, tremble, monte puis se fond).
        /// </summary>
        public void SpawnSignatureHit(Vector3 worldPos, int amount)
        {
            if (amount == 0 || _canvas == null || _worldCamera == null) return;

            string num = amount < 0 ? amount.ToString() : "+" + amount;
            var screenPos = _worldCamera.WorldToScreenPoint(worldPos);
            var font = LoadSignatureFont();

            // Conteneur commun (rise + fondu + inclinaison) -> tout reste solidaire.
            var container = new GameObject("SignatureSmash", typeof(RectTransform), typeof(CanvasGroup));
            container.transform.SetParent(_canvas.transform, false);
            var crt = container.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            // Au-dessus de la cible (pas dessus) + 2× plus petit (échelle du conteneur, tout suit).
            crt.position = screenPos + new Vector3(0f, _signatureAboveTargetPx, 0f);
            crt.localScale = Vector3.one * _signatureScale;
            crt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-_signatureTiltMaxDeg, _signatureTiltMaxDeg));
            var cg = container.GetComponent<CanvasGroup>();

            // Mot "SMASH!!" + chiffre (mesures pour empiler + dimensionner l'eclaboussure).
            var wordLabel = BuildSignatureLabel(container.transform, _signatureWord, font,
                _fontSize * _signatureWordFontMultiplier, _signatureWordColor, out var wrt);
            wordLabel.ForceMeshUpdate();
            float wordW = wordLabel.preferredWidth;
            float wordH = wordLabel.preferredHeight;

            var numLabel = BuildSignatureLabel(container.transform, num, font,
                _fontSize * _signatureNumberFontMultiplier, _signatureNumberColor, out var nrt);
            numLabel.ForceMeshUpdate();
            float numW = numLabel.preferredWidth;
            float numH = numLabel.preferredHeight;

            // Empilage vertical : SMASH au-dessus, chiffre en dessous.
            float gap = wordH * 0.05f;
            float blockH = wordH + gap + numH;
            wrt.anchoredPosition = new Vector2(0f, blockH * 0.5f - wordH * 0.5f);
            nrt.anchoredPosition = new Vector2(0f, -(blockH * 0.5f - numH * 0.5f));

            // Eclaboussure derriere, dimensionnee pour envelopper le bloc texte.
            float splatW = Mathf.Max(wordW, numW) * _signatureSplatWidthFactor;
            float splatH = blockH * 1.7f;
            var splatGo = new GameObject("Splat", typeof(RectTransform), typeof(Image));
            splatGo.transform.SetParent(container.transform, false);
            splatGo.transform.SetAsFirstSibling(); // derriere le texte
            var srt = splatGo.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = Vector2.zero;
            srt.sizeDelta = new Vector2(splatW, splatH);
            srt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-12f, 12f));
            var simg = splatGo.GetComponent<Image>();
            simg.sprite = BloodSplatSprite.GetRandom();
            simg.color = Color.white;
            simg.raycastTarget = false;

            // Coulures de sang ANIMEES qui s'ecoulent du bas de la flaque (effet "+").
            int dripCount = Random.Range(2, 4); // 2-3
            float splatBottom = -splatH * 0.5f;
            for (int i = 0; i < dripCount; i++)
            {
                var dripGo = new GameObject("BloodDrip", typeof(RectTransform), typeof(Image));
                dripGo.transform.SetParent(container.transform, false);
                dripGo.transform.SetSiblingIndex(1); // au-dessus de la flaque, sous le texte
                var dripRt = dripGo.GetComponent<RectTransform>();
                dripRt.anchorMin = dripRt.anchorMax = new Vector2(0.5f, 0.5f);
                dripRt.pivot = new Vector2(0.5f, 1f); // ancre en HAUT -> grandit vers le bas
                dripRt.anchoredPosition = new Vector2(
                    Random.Range(-splatW * 0.30f, splatW * 0.30f),
                    splatBottom * 0.55f);
                float dripW = Random.Range(splatW * 0.035f, splatW * 0.06f);
                dripRt.sizeDelta = new Vector2(dripW, 0f);
                var dripImg = dripGo.GetComponent<Image>();
                dripImg.sprite = BloodSplatSprite.GetDrip();
                dripImg.color = Color.white;
                dripImg.raycastTarget = false;
                float targetLen = Random.Range(splatH * 0.45f, splatH * 0.9f);
                StartCoroutine(DripGrow(dripRt, targetLen, Random.Range(0f, 0.12f)));
            }

            // Animations : conteneur (rise+fade), snap agressif (mot + eclaboussure), shake + flash.
            StartCoroutine(SignatureRiseFade(crt, cg));
            StartCoroutine(SignatureSnap(srt));
            StartCoroutine(SignatureSnap(wrt));
            StartCoroutine(ShakeAnchored(wrt));
            StartCoroutine(FlashColorTo(wordLabel, _signatureWordColor));
            CameraShaker.Instance?.Shake(_signatureCameraShakeAmplitude, _signatureCameraShakeDuration);
        }

        // Construit un label TMP signature (police + contour noir + ombre sang). out rt pour placement.
        private TextMeshProUGUI BuildSignatureLabel(Transform parent, string text, TMP_FontAsset font,
            float size, Color color, out RectTransform rt)
        {
            var go = new GameObject("SigLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(1400f, 400f);

            var l = go.GetComponent<TextMeshProUGUI>();
            l.text = text;
            l.color = color;
            if (font != null) l.font = font;
            l.fontSize = size;
            l.alignment = TextAlignmentOptions.Center;
            l.raycastTarget = false;
            l.enableWordWrapping = false;

            l.fontMaterial = new Material(l.fontMaterial);
            l.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, _signatureOutlineColor);
            l.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.30f);
            // Ombre portee sanglante (underlay decale bas-droite) pour le relief "trash".
            if (l.fontMaterial.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                l.fontMaterial.EnableKeyword("UNDERLAY_ON");
                l.fontMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, _signatureShadowColor);
                l.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
                l.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
                l.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.25f);
                l.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.2f);
            }
            return l;
        }

        // Rise + fondu du conteneur signature (tout solidaire). Detruit a la fin.
        private IEnumerator SignatureRiseFade(RectTransform crt, CanvasGroup cg)
        {
            if (crt == null) yield break;
            float total = _durationSeconds * _signatureDurationMultiplier;
            float rise = _signatureRiseDistancePx;
            float fadeStart = total * 0.55f;
            Vector2 start = crt.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < total && crt != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / total);
                crt.anchoredPosition = start + new Vector2(0f, rise * t);
                if (cg != null)
                {
                    cg.alpha = elapsed < fadeStart
                        ? 1f
                        : Mathf.Clamp01(1f - (elapsed - fadeStart) / Mathf.Max(0.0001f, total - fadeStart));
                }
                yield return null;
            }
            if (crt != null) Destroy(crt.gameObject);
        }

        // Snap agressif (impact) : apparait gros et se resorbe vite, sans rebond cartoon.
        private static IEnumerator SignatureSnap(RectTransform rt)
        {
            if (rt == null) yield break;
            const float dur = 0.13f;
            const float startScale = 1.4f;
            float elapsed = 0f;
            while (elapsed < dur && rt != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float ease = 1f - (1f - t) * (1f - t); // ease-out
                float s = Mathf.Lerp(startScale, 1f, ease);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        // Coulure de sang qui s'allonge vers le bas (pivot haut -> sizeDelta.y croit). Ease-out pour
        // l'effet "la goutte file puis ralentit". Le fondu/destruction sont geres par le conteneur.
        private static IEnumerator DripGrow(RectTransform rt, float targetLen, float delay)
        {
            if (rt == null) yield break;
            float waited = 0f;
            while (waited < delay && rt != null)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            const float grow = 0.5f;
            float elapsed = 0f;
            while (elapsed < grow && rt != null)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / grow);
                float ease = 1f - (1f - k) * (1f - k);
                var sd = rt.sizeDelta;
                sd.y = targetLen * ease;
                rt.sizeDelta = sd;
                yield return null;
            }
            if (rt != null)
            {
                var sd = rt.sizeDelta;
                sd.y = targetLen;
                rt.sizeDelta = sd;
            }
        }

        // Shake en LOCAL autour de la position courante (reste solidaire pendant le rise).
        private IEnumerator ShakeAnchored(RectTransform rt)
        {
            if (rt == null) yield break;
            Vector2 origin = rt.anchoredPosition;
            const float freq = 34f;
            float duration = _signatureShakeDuration;
            float amp = _signatureShakeAmplitudePx;
            float elapsed = 0f;
            while (elapsed < duration && rt != null)
            {
                elapsed += Time.deltaTime;
                float decay = 1f - (elapsed / duration);
                float dx = Mathf.Sin(elapsed * freq) * amp * decay;
                float dy = Mathf.Cos(elapsed * freq * 1.3f) * amp * 0.5f * decay;
                rt.anchoredPosition = origin + new Vector2(dx, dy);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = origin;
        }

        // Flash blanc bref -> couleur cible sur un label (impact). L'alpha reste gere par le CanvasGroup.
        private IEnumerator FlashColorTo(TextMeshProUGUI label, Color target)
        {
            if (label == null) yield break;
            const float dur = 0.14f;
            float elapsed = 0f;
            while (elapsed < dur && label != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                Color c = Color.Lerp(_signatureFlashColor, target, t);
                c.a = label.color.a;
                label.color = c;
                yield return null;
            }
        }

        // Police "Anton" (lourde/condensee, livree avec TMP dans un dossier Resources). Cache + fallback.
        private static TMP_FontAsset _signatureFont;
        private static bool _signatureFontTried;

        private static TMP_FontAsset LoadSignatureFont()
        {
            if (_signatureFontTried) return _signatureFont;
            _signatureFontTried = true;
            _signatureFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Anton SDF");
            if (_signatureFont == null)
            {
                Debug.LogWarning("[FloatingText] Police 'Anton SDF' introuvable dans Resources — " +
                                 "texte signature laisse en police par defaut.");
            }
            return _signatureFont;
        }
    }
}
