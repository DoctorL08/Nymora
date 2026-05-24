using System.Collections;
using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Polish UI hub — anime un panneau à l'ouverture (et, sur demande, à la fermeture).
    /// Posé sur le `_panelRoot` d'un panneau hub via SetupHubPanelAnimationsTool.
    ///
    /// OUVERTURE (automatique) : dès que le root passe SetActive(true), OnEnable joue un
    /// fondu + léger zoom (pop). AUCUN changement de code dans les 11 panneaux : ils
    /// continuent d'appeler `_panelRoot.SetActive(true)`.
    ///
    /// FERMETURE (opt-in, brique suivante) : un panneau qui veut une sortie animée appelle
    /// `UiPanelAnimator.CloseAnimated(_panelRoot)` au lieu de `_panelRoot.SetActive(false)` :
    /// joue le fondu sortant PUIS désactive le root.
    ///
    /// Self-provisioning : crée son CanvasGroup si absent. 100% View, temps non-scalé
    /// (indépendant d'un éventuel Time.timeScale).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiPanelAnimator : MonoBehaviour
    {
        // Durée d'anim + échelle de départ du pop. Subtil sur l'échelle : un panneau
        // plein écran scalé trop bas laisserait voir un liseré du hub derrière pendant l'anim.
        private const float Duration = 0.18f;
        private const float StartScale = 0.96f;

        private CanvasGroup _cg;
        private RectTransform _rt;
        private Coroutine _running;

        private void Awake() => EnsureRefs();

        private void EnsureRefs()
        {
            if (_rt == null) _rt = transform as RectTransform;
            if (_cg == null) _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            EnsureRefs();
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(PlayIn());
        }

        private IEnumerator PlayIn()
        {
            float t = 0f;
            while (t < Duration)
            {
                t += Time.unscaledDeltaTime;
                float k = EaseOutCubic(Mathf.Clamp01(t / Duration));
                _cg.alpha = k;
                if (_rt != null) _rt.localScale = Vector3.one * Mathf.Lerp(StartScale, 1f, k);
                yield return null;
            }
            _cg.alpha = 1f;
            if (_rt != null) _rt.localScale = Vector3.one;
            _running = null;
        }

        /// <summary>
        /// Fermeture animée : fondu sortant puis SetActive(false). À appeler par un panneau
        /// à la place de `_panelRoot.SetActive(false)` (brique fermeture). Fallback robuste :
        /// si pas d'animator ou root déjà inactif, désactive directement.
        /// </summary>
        public static void CloseAnimated(GameObject panelRoot)
        {
            if (panelRoot == null) return;
            var anim = panelRoot.GetComponent<UiPanelAnimator>();
            if (anim == null || !panelRoot.activeInHierarchy) { panelRoot.SetActive(false); return; }
            anim.BeginClose();
        }

        private void BeginClose()
        {
            EnsureRefs();
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(PlayOut());
        }

        private IEnumerator PlayOut()
        {
            float startAlpha = _cg.alpha;
            float t = 0f;
            while (t < Duration)
            {
                t += Time.unscaledDeltaTime;
                float k = EaseOutCubic(Mathf.Clamp01(t / Duration));
                _cg.alpha = Mathf.Lerp(startAlpha, 0f, k);
                if (_rt != null) _rt.localScale = Vector3.one * Mathf.Lerp(1f, StartScale, k);
                yield return null;
            }
            _running = null;
            // Remet l'état neutre pour la prochaine ouverture, puis désactive.
            _cg.alpha = 1f;
            if (_rt != null) _rt.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
    }
}
