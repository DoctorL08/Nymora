using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// PATCH 22 mai (test designer, B5) — Indicateur de tour TRANSITOIRE et anime.
    ///
    /// A chaque changement de tour, un petit bandeau apparait centre-haut (sous le timer) :
    ///   "C'EST TON TOUR" (vert, joueur local) ou "TOUR ADVERSE" (rouge).
    ///   Animation legere/rapide : fade-in + glissement gauche->centre + GROSSISSEMENT (pop),
    ///   court hold, puis glissement centre->droite + fade-out. But : que le joueur comprenne
    ///   immediatement que c'est son tour, sans etre gene (transitoire, pas permanent, sans
    ///   bloquer l'input).
    ///
    /// Drive par CombatHUDController (detecte le changement de tour) via PlayTurnBanner().
    /// Overlay auto-instancie (procedural, zero manip Unity) en scene combat (IA + Casual).
    /// Lecture seule -> zero impact sim, pas de bump CombatRulesVersion.
    /// </summary>
    public class TurnIndicatorView : MonoBehaviour
    {
        public static TurnIndicatorView Instance { get; private set; }

        [Tooltip("Optionnel : police Ari (DA hub). Câblé par 'Restyle Combat HUD' sur l'instance " +
                 "de scène. Si vide (auto-instance), police TMP par défaut.")]
        [SerializeField] private TMP_FontAsset _font;

        // --- Position / style (ajustables) ---
        private const float TopOffset = -150f;    // sous le timer (centre-haut), px @1080p
        private const float SlideDistance = 220f; // amplitude du glissement lateral
        private const float FontSize = 46f;

        // --- Scale (pop de grossissement) ---
        private const float StartScale = 0.45f;   // taille de depart (petit -> grossit)
        private const float EndScale = 1.0f;

        // --- Timings (rapide & leger) ---
        private const float SlideInDuration = 0.30f;
        private const float HoldDuration = 0.75f;
        private const float SlideOutDuration = 0.28f;

        private static readonly Color ColorYourTurn = new Color(0.37f, 0.82f, 0.42f, 1f);  // vert
        private static readonly Color ColorEnemyTurn = new Color(1.00f, 0.42f, 0.37f, 1f);  // rouge

        private CanvasGroup _group;
        private RectTransform _container;
        private TextMeshProUGUI _label;
        private Coroutine _anim;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;
            TryCreateForScene(SceneManager.GetActiveScene());
        }

        private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreateForScene(scene);
        }

        private static void TryCreateForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.name.Contains("Combat")) return;
            if (Instance != null) return;
            var go = new GameObject("TurnIndicatorView");
            go.AddComponent<TurnIndicatorView>();
        }

        private void Awake()
        {
            Instance = this;
            BuildUI();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildUI()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000; // au-dessus du HUD, SOUS l'overlay coinflip (32000)
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false; // jamais bloquer l'input : purement visuel

            // Container centre-haut (pivot centre pour que le scale grossisse a partir du centre).
            var containerGo = new GameObject("Container", typeof(RectTransform));
            containerGo.transform.SetParent(canvas.transform, false);
            _container = (RectTransform)containerGo.transform;
            _container.anchorMin = _container.anchorMax = new Vector2(0.5f, 1f);
            _container.pivot = new Vector2(0.5f, 1f);
            _container.anchoredPosition = new Vector2(0f, TopOffset);
            _container.sizeDelta = new Vector2(700f, 90f);

            // Texte centre.
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(_container, false);
            var lRt = (RectTransform)labelGo.transform;
            lRt.anchorMin = Vector2.zero;
            lRt.anchorMax = Vector2.one;
            lRt.offsetMin = Vector2.zero;
            lRt.offsetMax = Vector2.zero;
            _label = labelGo.AddComponent<TextMeshProUGUI>();
            if (_font != null) _label.font = _font;
            _label.fontSize = FontSize;
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontStyle = FontStyles.Bold;
            _label.characterSpacing = 4f;
            _label.raycastTarget = false;
            _label.text = "";
        }

        /// <summary>
        /// Joue le bandeau de tour. myTurn = true si le joueur ACTIF est le joueur LOCAL.
        /// </summary>
        public void PlayTurnBanner(bool myTurn)
        {
            if (_label == null) return;
            _label.text = myTurn ? "C'EST TON TOUR" : "TOUR ADVERSE";
            _label.color = myTurn ? ColorYourTurn : ColorEnemyTurn;

            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(AnimateBanner());
        }

        private IEnumerator AnimateBanner()
        {
            // Phase 1 : fade-in + glissement gauche -> centre + GROSSISSEMENT (overshoot back-out).
            float t = 0f;
            while (t < SlideInDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / SlideInDuration);
                float easeOut = 1f - (1f - k) * (1f - k);   // ease-out quad (alpha + position)
                _group.alpha = easeOut;
                SetX(Mathf.Lerp(-SlideDistance, 0f, easeOut));
                float s = Mathf.Lerp(StartScale, EndScale, EaseOutBack(k)); // pop overshoot
                _container.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            _group.alpha = 1f;
            SetX(0f);
            _container.localScale = Vector3.one;

            // Phase 2 : hold au centre.
            yield return new WaitForSecondsRealtime(HoldDuration);

            // Phase 3 : glissement centre -> droite + fade-out (leger shrink).
            t = 0f;
            while (t < SlideOutDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / SlideOutDuration);
                float easeIn = k * k;
                _group.alpha = 1f - easeIn;
                SetX(Mathf.Lerp(0f, SlideDistance, easeIn));
                float s = Mathf.Lerp(EndScale, 0.85f, easeIn);
                _container.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            _group.alpha = 0f;
            _anim = null;
        }

        // Ease-out-back : depasse legerement 1 puis revient -> effet "pop" de grossissement.
        private static float EaseOutBack(float k)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float km1 = k - 1f;
            return 1f + c3 * km1 * km1 * km1 + c1 * km1 * km1;
        }

        private void SetX(float x)
        {
            var pos = _container.anchoredPosition;
            pos.x = x;
            _container.anchoredPosition = pos;
        }
    }
}
