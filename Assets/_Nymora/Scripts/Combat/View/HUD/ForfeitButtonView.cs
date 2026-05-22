using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// PATCH 22 mai (test designer, B8) — Bouton "Abandonner" en combat.
    ///
    /// Bouton haut-droite ; au clic, demande confirmation (Oui / Non) pour eviter un forfait
    /// accidentel. "Oui" envoie une ForfeitCommand Quantum -> TurnSystem termine le match,
    /// le joueur local perd (l'adversaire / le bot gagne). Masque pendant l'intro pile ou
    /// face (PreMatch) et apres la fin du match (MatchEnd).
    ///
    /// Combat IA + Casual. Auto-instancie via SceneManager.sceneLoaded (zero manip Unity).
    /// La command est envoyee sur le splitscreen slot LOCAL 0 (1 joueur local par client,
    /// cf CombatInputController) — Quantum la mappe au PlayerRef global du joueur local.
    /// </summary>
    public class ForfeitButtonView : MonoBehaviour
    {
        private static ForfeitButtonView _current;

        private GameObject _mainButton;
        private GameObject _confirmPanel;
        private GameObject _confirmDim; // fond grisé plein écran derrière la confirmation
        private CanvasGroup _rootGroup;
        private bool _forfeited;

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
            if (_current != null) return;
            var go = new GameObject("ForfeitButtonView");
            _current = go.AddComponent<ForfeitButtonView>();
        }

        private void Awake()
        {
            _current = this;
            BuildUI();
        }

        private void OnDestroy()
        {
            if (_current == this) _current = null;
        }

        private void BuildUI()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000; // au-dessus du HUD, sous l'indicateur de tour (31000) et le coinflip (32000)
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            _rootGroup = gameObject.AddComponent<CanvasGroup>();

            // Bouton principal "Abandonner" (BAS-droite, sous la timeline remontee).
            _mainButton = CreateButton("AbandonButton", "Abandonner", new Color(0.62f, 0.18f, 0.18f, 0.92f),
                new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(190f, 52f), OnAbandonClicked);

            // Fond grisé plein écran (modal) derrière la confirmation, bloque les clics derriere.
            _confirmDim = new GameObject("ConfirmDim", typeof(RectTransform));
            _confirmDim.transform.SetParent(canvas.transform, false);
            var dRt = (RectTransform)_confirmDim.transform;
            dRt.anchorMin = Vector2.zero; dRt.anchorMax = Vector2.one;
            dRt.offsetMin = Vector2.zero; dRt.offsetMax = Vector2.zero;
            var dimImg = _confirmDim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            dimImg.raycastTarget = true;
            _confirmDim.SetActive(false);

            // Panneau de confirmation (cache par defaut), CENTRE de l'ecran.
            _confirmPanel = new GameObject("ConfirmPanel", typeof(RectTransform));
            _confirmPanel.transform.SetParent(canvas.transform, false);
            var cRt = (RectTransform)_confirmPanel.transform;
            cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 0.5f);
            cRt.pivot = new Vector2(0.5f, 0.5f);
            cRt.anchoredPosition = Vector2.zero; // plein centre
            cRt.sizeDelta = new Vector2(460f, 230f);
            var bg = _confirmPanel.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.10f, 0.13f, 0.98f);

            var label = CreateLabel(cRt, "Abandonner le combat ?", new Vector2(0.5f, 1f),
                new Vector2(0f, -28f), new Vector2(430f, 56f), 30f);
            label.alignment = TextAlignmentOptions.Center;

            CreateButton("ConfirmYes", "Oui, abandonner", new Color(0.62f, 0.18f, 0.18f, 0.95f),
                new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(400f, 56f), OnConfirmYes, _confirmPanel.transform);
            CreateButton("ConfirmNo", "Annuler", new Color(0.30f, 0.32f, 0.36f, 0.95f),
                new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(400f, 50f), OnConfirmNo, _confirmPanel.transform);

            _confirmPanel.SetActive(false);
        }

        private void Update()
        {
            // Masque l'UI hors phase de jeu active (intro pile ou face + fin de match).
            var game = QuantumRunner.Default?.Game;
            bool show = false;
            if (game != null && game.Frames != null && game.Frames.Verified != null)
            {
                var frame = game.Frames.Verified;
                if (frame.TryGetSingleton<CombatState>(out var state))
                {
                    show = state.CurrentPhase != CombatPhase.PreMatch
                        && state.CurrentPhase != CombatPhase.MatchEnd
                        && !_forfeited;
                }
            }
            if (_rootGroup != null)
            {
                _rootGroup.alpha = show ? 1f : 0f;
                _rootGroup.interactable = show;
                _rootGroup.blocksRaycasts = show;
            }
            // Si on quitte la phase active, referme la confirmation.
            if (!show && _confirmPanel != null && _confirmPanel.activeSelf)
            {
                SetConfirmVisible(false);
            }
        }

        // Affiche/cache la confirmation (panneau + fond grisé) ; cache le bouton principal pendant.
        private void SetConfirmVisible(bool visible)
        {
            if (_confirmDim != null) _confirmDim.SetActive(visible);
            if (_confirmPanel != null) _confirmPanel.SetActive(visible);
            if (_mainButton != null) _mainButton.SetActive(!visible);
        }

        private void OnAbandonClicked()
        {
            SetConfirmVisible(true);
        }

        private void OnConfirmNo()
        {
            SetConfirmVisible(false);
        }

        private void OnConfirmYes()
        {
            var game = QuantumRunner.Default?.Game;
            if (game != null)
            {
                // Splitscreen slot LOCAL 0 (1 joueur local par client) -> Quantum mappe au
                // PlayerRef global du joueur local. Meme convention que CombatInputController.
                game.SendCommand(0, new ForfeitCommand());
                _forfeited = true;
            }
            // Cache tout (le bouton ne revient pas : on a forfait).
            if (_confirmDim != null) _confirmDim.SetActive(false);
            if (_confirmPanel != null) _confirmPanel.SetActive(false);
            if (_mainButton != null) _mainButton.SetActive(false);
        }

        // ============================ Helpers UI procedural ============================

        private GameObject CreateButton(string name, string text, Color bgColor,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick,
            Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent != null ? parent : transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var label = CreateLabel(rt, text, new Vector2(0.5f, 0.5f), Vector2.zero, size, 24f);
            label.alignment = TextAlignmentOptions.Center;
            return go;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string text, Vector2 anchor,
            Vector2 anchoredPos, Vector2 size, float fontSize)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
