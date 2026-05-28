using System.Collections;
using System.Collections.Generic;
using Nymora.Combat.View.HUD;
using Nymora.Core.Data;
using Nymora.Core.SceneFlow;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace Nymora.Combat.View.Tutorial
{
    /// <summary>
    /// Brique T3 (Tutoriel) — FRAMEWORK du tutoriel guidé : machine à états d'étapes + overlay UI
    /// (panneau d'instructions au design combat via CombatUiKit) + GATING (une étape ne se valide
    /// que quand l'action attendue est détectée dans la frame Quantum vérifiée).
    ///
    /// 100% View : lit la sim (game.Frames.Verified) comme CombatHUDController, n'écrit JAMAIS dans
    /// la sim (aucun bump CombatRulesVersion). Instancié par CombatBootstrapIA quand
    /// TutorialContext.Active. S'auto-détruit hors mode tuto.
    ///
    /// ⚠️ PROVISOIRE T3 : les étapes ci-dessous démontrent les 4 types de gating (Continue / Move /
    /// Cast / EndTurn). Le CONTENU curé des étapes finales, les COACH MARKS (halo/flèche sur la barre
    /// de sorts, jauges PA/PM, bouton Fin de tour) et l'ÉCRAN DE FIN (+ retour hub + flag) = T4.
    /// La position de l'overlay est aussi provisoire (à caler en T4).
    /// </summary>
    public sealed class TutorialDirector : MonoBehaviour
    {
        private static TutorialDirector _instance;

        /// <summary>Instancie le director s'il n'existe pas déjà. Appelé par CombatBootstrapIA en mode tuto.</summary>
        public static void EnsureSpawned()
        {
            if (_instance != null) return;
            var go = new GameObject("TutorialDirector");
            _instance = go.AddComponent<TutorialDirector>();
        }

        // ---- Modèle d'étape ----
        private enum Gate { Continue, Move, Cast, EndTurn }

        private sealed class Step
        {
            public string Text;
            public Gate Gate;
            public Step(string text, Gate gate) { Text = text; Gate = gate; }
        }

        private const int LocalIndex = 0;                 // en IA/tuto, l'humain est slot 0
        private static int DummyIndex => AIConstants.BotPlayerIndex; // mannequin = slot 1

        private readonly List<Step> _steps = new List<Step>();
        private int _index;
        private bool _advancing;

        // Baselines capturées à l'entrée d'une étape, pour un gating par delta.
        private bool _needBaseline;
        private int _baseGridX, _baseGridY, _baseCast, _baseTurn;
        private bool _sawBotTurn;

        // ---- UI ----
        private Canvas _canvas;
        private GameObject _panelGo;
        private TextMeshProUGUI _instructionText;
        private TextMeshProUGUI _statusText;
        private GameObject _continueGo;
        private bool _finished;

        private void Awake()
        {
            if (!TutorialContext.Active) { Destroy(gameObject); return; }
            BuildSteps();
            BuildUI();
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
            ShowStep(0);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void BuildSteps()
        {
            _steps.Clear();
            _steps.Add(new Step("Bienvenue dans Nymora ! Tu incarnes le Soulrender, un guerrier de melee. " +
                                "On va voir les bases du combat. Clique sur Continuer.", Gate.Continue));
            _steps.Add(new Step("DEPLACEMENT : clique sur une case libre proche de toi. " +
                                "Chaque case parcourue coute 1 PM (points de mouvement).", Gate.Move));
            _steps.Add(new Step("ATTAQUE : clique sur un sort dans la barre en bas, puis sur le mannequin " +
                                "pour le frapper. Lancer un sort coute des PA (points d'action).", Gate.Cast));
            _steps.Add(new Step("FIN DE TOUR : quand tu n'as plus rien a faire, termine ton tour " +
                                "(bouton Fin de tour). Le mannequin ne riposte pas.", Gate.EndTurn));
            _steps.Add(new Step("Bravo, tu maitrises les bases du combat ! Clique sur Continuer pour terminer.", Gate.Continue));
        }

        private void ShowStep(int i)
        {
            _index = i;
            if (i >= _steps.Count) { Finish(); return; }

            var step = _steps[i];
            if (_instructionText != null) _instructionText.text = step.Text;
            if (_statusText != null) _statusText.text = $"Etape {i + 1}/{_steps.Count}";
            if (_continueGo != null) _continueGo.SetActive(step.Gate == Gate.Continue);

            _needBaseline = true; // capturée au 1er OnUpdateView de l'étape (frame dispo)
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (_advancing || _index >= _steps.Count) return;

            var frame = game.Frames.Verified;
            if (frame == null) return;
            if (!frame.TryGetSingleton<CombatState>(out var state)) return;

            Combatant local = default;
            bool hasLocal = false;
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef _, out Combatant c))
            {
                if (c.PlayerIndex == LocalIndex) { local = c; hasLocal = true; }
            }
            if (!hasLocal) return;

            if (state.ActivePlayerIndex == DummyIndex) _sawBotTurn = true;

            if (_needBaseline)
            {
                _baseGridX = local.GridX;
                _baseGridY = local.GridY;
                _baseCast = local.LastCastSequence;
                _baseTurn = state.TurnNumber;
                _sawBotTurn = false;
                _needBaseline = false;
                return; // pas d'évaluation sur la frame de capture
            }

            var step = _steps[_index];
            bool done = false;
            switch (step.Gate)
            {
                case Gate.Continue: return; // avance via le bouton Continuer
                case Gate.Move: done = local.GridX != _baseGridX || local.GridY != _baseGridY; break;
                case Gate.Cast: done = local.LastCastSequence > _baseCast; break;
                case Gate.EndTurn: done = _sawBotTurn || state.TurnNumber > _baseTurn; break;
            }

            if (done) StartCoroutine(AdvanceAfterDelay(0.7f));
        }

        private void OnContinueClicked()
        {
            if (_advancing || _index >= _steps.Count) return;
            if (_steps[_index].Gate == Gate.Continue) ShowStep(_index + 1);
        }

        private IEnumerator AdvanceAfterDelay(float delay)
        {
            _advancing = true;
            if (_statusText != null) _statusText.text = "Valide !";
            yield return new WaitForSeconds(delay);
            _advancing = false;
            ShowStep(_index + 1);
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            // T4 : masque le panneau d'instructions et affiche l'écran de fin (centre) + retour hub.
            // Le set du flag backend "tutorialCompleted" (anti-rejeu nouveau compte) arrive en T6.
            if (_panelGo != null) _panelGo.SetActive(false);
            BuildEndScreen();
        }

        private void BuildEndScreen()
        {
            if (_canvas == null) return;

            var panel = NewImage("EndPanel", _canvas.transform, CombatUiKit.PanelBg, CombatUiKit.CornerRadius);
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(720f, 320f);
            prt.anchoredPosition = Vector2.zero;

            var title = NewText("Title", panel.transform, 44, CombatUiKit.TextPrimary, TextAlignmentOptions.Center);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(-48f, 70f); trt.anchoredPosition = new Vector2(0f, -40f);
            title.text = "Tutoriel termine !";

            var body = NewText("Body", panel.transform, 26, CombatUiKit.TextSecondary, TextAlignmentOptions.Center);
            var brt = body.rectTransform;
            brt.anchorMin = new Vector2(0f, 0.5f); brt.anchorMax = new Vector2(1f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(-80f, 90f); brt.anchoredPosition = new Vector2(0f, 6f);
            body.text = "Tu connais les bases : deplacement, sorts et fin de tour.\nPret a affronter de vrais adversaires ?";

            var btnImg = NewImage("ReturnButton", panel.transform, CombatUiKit.Accent, 10f);
            var rrt = btnImg.rectTransform;
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0f);
            rrt.sizeDelta = new Vector2(320f, 64f);
            rrt.anchoredPosition = new Vector2(0f, 28f);
            var btn = btnImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(ReturnToHub);
            var lbl = NewText("Label", btnImg.transform, 28, CombatUiKit.TextOnLight, TextAlignmentOptions.Center);
            var lrt = lbl.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            lbl.text = "Retour au hub";
        }

        private void ReturnToHub()
        {
            // Même pattern que MatchEndOverlay : fondu + shutdown Quantum à mi-transition + attente
            // que le hub soit prêt. TutorialContext.Reset() est aussi fait par CombatBootstrapIA.OnDestroy.
            SceneTransition.Load("10_CommunityHub", () => QuantumRunner.ShutdownAll(), waitForReady: true);
        }

        // ============================ UI (procédural, design combat) ============================

        private void BuildUI()
        {
            var canvasGo = new GameObject("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500; // au-dessus du HUD combat
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Panneau d'instructions — haut-centre (provisoire, à caler en T5 avec les coach marks).
            var panel = NewImage("Panel", _canvas.transform, CombatUiKit.PanelBg, CombatUiKit.CornerRadius);
            _panelGo = panel.gameObject;
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 1f);
            prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.sizeDelta = new Vector2(1100f, 140f);
            prt.anchoredPosition = new Vector2(0f, -24f);

            // Compteur d'étape / feedback (haut-gauche du panneau).
            _statusText = NewText("Status", panel.transform, 24, CombatUiKit.TextSecondary, TextAlignmentOptions.TopLeft);
            Stretch(_statusText.rectTransform, 24f, 12f, 24f, 12f);
            _statusText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _statusText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _statusText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _statusText.rectTransform.sizeDelta = new Vector2(-48f, 30f);
            _statusText.rectTransform.anchoredPosition = new Vector2(0f, -10f);

            // Instruction principale (centre du panneau).
            _instructionText = NewText("Instruction", panel.transform, 30, CombatUiKit.TextPrimary, TextAlignmentOptions.Left);
            var irt = _instructionText.rectTransform;
            irt.anchorMin = new Vector2(0f, 0f);
            irt.anchorMax = new Vector2(1f, 1f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.offsetMin = new Vector2(28f, 16f);
            irt.offsetMax = new Vector2(-220f, -44f);

            // Bouton Continuer (bas-droite, visible seulement sur les étapes info).
            var btnImg = NewImage("ContinueButton", panel.transform, CombatUiKit.GhostBg, 10f);
            _continueGo = btnImg.gameObject;
            var brt = btnImg.rectTransform;
            brt.anchorMin = new Vector2(1f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(1f, 0f);
            brt.sizeDelta = new Vector2(180f, 56f);
            brt.anchoredPosition = new Vector2(-20f, 18f);
            var btn = _continueGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(OnContinueClicked);
            var lbl = NewText("Label", btnImg.transform, 26, CombatUiKit.TextPrimary, TextAlignmentOptions.Center);
            var lrt = lbl.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            lbl.text = "Continuer >";
        }

        private static Image NewImage(string name, Transform parent, Color color, float radius)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            CombatUiKit.ApplyRounded(img, radius);
            return img;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, int size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform rt, float l, float t, float r, float b)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }
    }
}
