using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Phase 2 (View) — MORT SUBITE. Affiche, à partir de CombatState.TurnNumber (lecture seule,
    /// zéro impact sim), l'avertissement (rounds 23-24) puis le filtre rougeâtre + bandeau quand la
    /// mort subite est active (round >= 25). Seuils = source unique `Quantum.SuddenDeath`.
    ///
    /// Auto-instancié pour les scènes de combat (comme CoinFlipIntroView), codé SANS Kyami (filtre
    /// = simple Image rouge plein écran à faible alpha, pulsée légèrement).
    /// </summary>
    public sealed class SuddenDeathView : MonoBehaviour
    {
        private static SuddenDeathView _current;

        private Image _redFilter;
        private TextMeshProUGUI _banner;
        private int _lastTurn = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;
        }

        private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Toutes les scènes de combat (33_CombatCasual / 30_CombatIA / 40_CombatRanked1v1 / 41_CombatRanked2v2).
            if (!scene.IsValid() || !scene.name.Contains("Combat")) return;
            if (_current != null) return;
            var go = new GameObject("SuddenDeathView");
            _current = go.AddComponent<SuddenDeathView>();
        }

        private void Awake()
        {
            _current = this;
            BuildOverlay();
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
        }

        private void OnDestroy()
        {
            if (_current == this) _current = null;
        }

        private void BuildOverlay()
        {
            var canvasGo = new GameObject("SuddenDeathCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4500; // sous les overlays modaux (pile ou face 5000), au-dessus du HUD
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Le filtre ne doit pas bloquer les clics combat.
            canvasGo.GetComponent<GraphicRaycaster>().enabled = false;

            // Filtre rougeâtre plein écran (faible alpha, "pas trop sombre").
            var filterGo = new GameObject("RedFilter", typeof(RectTransform));
            filterGo.transform.SetParent(canvasGo.transform, false);
            var frt = (RectTransform)filterGo.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            _redFilter = filterGo.AddComponent<Image>();
            _redFilter.color = new Color(0.65f, 0.06f, 0.06f, 0f); // alpha animé quand actif
            _redFilter.raycastTarget = false;
            _redFilter.enabled = false;

            // Bandeau haut-centre.
            var bannerGo = new GameObject("Banner", typeof(RectTransform));
            bannerGo.transform.SetParent(canvasGo.transform, false);
            var brt = (RectTransform)bannerGo.transform;
            brt.anchorMin = new Vector2(0.5f, 1f); brt.anchorMax = new Vector2(0.5f, 1f); brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, -120f);
            brt.sizeDelta = new Vector2(900f, 64f);
            _banner = bannerGo.AddComponent<TextMeshProUGUI>();
            _banner.alignment = TextAlignmentOptions.Center;
            _banner.fontSize = 44f;
            _banner.fontStyle = FontStyles.Bold;
            _banner.color = new Color(1f, 0.5f, 0.45f, 1f);
            _banner.raycastTarget = false;
            _banner.enableWordWrapping = false;
            _banner.gameObject.SetActive(false);
        }

        private void OnUpdateView(QuantumGame game)
        {
            var frame = game?.Frames?.Verified;
            if (frame == null || !frame.TryGetSingleton<CombatState>(out var state)) return;
            // Pas d'overlay hors combat actif (intro pile ou face / fin de match).
            if (state.CurrentPhase == CombatPhase.MatchEnd || state.CurrentPhase == CombatPhase.PreMatch)
            {
                SetActiveOverlay(false, false, 0);
                return;
            }

            int turn = state.TurnNumber;
            if (SuddenDeath.IsActive(turn)) SetActiveOverlay(true, false, turn);
            else if (SuddenDeath.IsWarning(turn)) SetActiveOverlay(false, true, turn);
            else SetActiveOverlay(false, false, turn);
            _lastTurn = turn;
        }

        private void SetActiveOverlay(bool active, bool warning, int turn)
        {
            if (_redFilter != null)
            {
                _redFilter.enabled = active;
                if (active)
                {
                    // Pulsation douce, plus discrète (0.05 -> 0.12 alpha) — moins rougeâtre (retour Lorenzo).
                    float a = 0.085f + 0.035f * Mathf.Sin(Time.unscaledTime * 2.2f);
                    var col = _redFilter.color; col.a = a; _redFilter.color = col;
                }
            }

            if (_banner != null)
            {
                if (active)
                {
                    if (!_banner.gameObject.activeSelf) _banner.gameObject.SetActive(true);
                    _banner.text = "MORT SUBITE — POISON D'ARÈNE";
                }
                else if (warning)
                {
                    if (!_banner.gameObject.activeSelf) _banner.gameObject.SetActive(true);
                    int inN = SuddenDeath.ActivateRound - turn;
                    _banner.text = inN <= 1 ? "MORT SUBITE AU PROCHAIN TOUR" : $"MORT SUBITE DANS {inN} TOURS";
                }
                else if (_banner.gameObject.activeSelf)
                {
                    _banner.gameObject.SetActive(false);
                }
            }
        }
    }
}
