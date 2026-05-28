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
    /// Tutoriel guidé (Soulrender) — machine à états d'étapes + overlay UI (panneau d'instructions au
    /// design combat via CombatUiKit) + GATING (une étape ne se valide que quand l'action attendue est
    /// détectée dans la frame Quantum vérifiée) + COACH MARKS (cadre arrondi animé non-occultant
    /// pointant un widget HUD) + écran de fin → retour hub (T4).
    ///
    /// Pédagogie (T5) : PV, PM (déplacement), PA (action), lancer un sort, passif de classe (Appel du
    /// Sang), fin de tour + régén, Hémoglyphe (ressource de classe), charger puis lancer la signature
    /// Âme Lacérée. Le timer est gelé pendant le tour du joueur (RuntimeConfig.TutorialFreezeTimer) et
    /// le mannequin est passif (TutorialPassiveBot) → le joueur prend son temps.
    ///
    /// 100% View : lit la sim (game.Frames.Verified) comme CombatHUDController, n'écrit JAMAIS dans la
    /// sim. Instancié par CombatBootstrapIA quand TutorialContext.Active ; s'auto-détruit hors mode tuto.
    /// Le set du flag backend "tutorialCompleted" (anti-rejeu nouveau compte) = T6.
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
        // Continue = clic bouton ; Move = se déplacer ; Cast = lancer un sort ; EndTurn = finir le tour ;
        // Charge = atteindre 5 HG (Hémoglyphe) ; Signature = lancer Âme Lacérée.
        private enum Gate { Continue, Move, Cast, EndTurn, Charge, Signature }

        /// <summary>Élément du HUD que le coach mark (cadre animé) doit pointer pendant l'étape.</summary>
        private enum Coach { None, Resources, SpellBar, EndTurn, Signature }

        private sealed class Step
        {
            public string Text;
            public Gate Gate;
            public Coach Coach;
            public Step(string text, Gate gate, Coach coach) { Text = text; Gate = gate; Coach = coach; }
        }

        private const int LocalIndex = 0;                 // en IA/tuto, l'humain est slot 0
        private static int DummyIndex => AIConstants.BotPlayerIndex; // mannequin = slot 1
        private const int SignatureChargeCap = 5;          // Hémoglyphe max du Soulrender (Bible V7.1)

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

        // Coach mark (halo pulsant pointant un widget HUD).
        private Image _coachImage;
        private RectTransform _coachRt;
        private Coach _currentCoach = Coach.None;

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

            _steps.Add(new Step(
                "Bienvenue dans Nymora. Tu incarnes le SOULRENDER : un predateur en hemorragie qui " +
                "raccourcit le combat. Le but d'un duel : amener les Points de Vie (PV) de l'adversaire a 0. " +
                "On va apprendre, etape par etape, comment t'y prendre. Clique sur Continuer.",
                Gate.Continue, Coach.None));

            _steps.Add(new Step(
                "TES PV. En haut a gauche, ton panneau affiche tes 1500 PV, tes PA et tes PM. " +
                "Le mannequin en face a aussi 1500 PV. Tout part de la : protege tes PV, vide les siens.",
                Gate.Continue, Coach.Resources));

            _steps.Add(new Step(
                "LES PM (Points de Mouvement). Tu as 3 PM par tour. Chaque case parcourue coute 1 PM. " +
                "Ils servent a te placer : approcher pour frapper, ou fuir le danger. " +
                "Essaie : clique une case libre pour te rapprocher du mannequin.",
                Gate.Move, Coach.Resources));

            _steps.Add(new Step(
                "LES PA (Points d'Action). Tu as 8 PA par tour. C'est ta ressource d'ACTION : " +
                "chaque sort coute un certain nombre de PA. Plus de PA = plus de sorts par tour. " +
                "Quand tu n'as plus assez de PA, tu ne peux plus lancer le sort. Clique sur Continuer.",
                Gate.Continue, Coach.Resources));

            _steps.Add(new Step(
                "LANCER UN SORT. Dans la barre du bas, clique un sort pour l'ARMER, puis clique le " +
                "mannequin pour le frapper. Observe : tes PA baissent du cout du sort, le mannequin perd des PV. " +
                "Lance ton premier sort maintenant.",
                Gate.Cast, Coach.SpellBar));

            _steps.Add(new Step(
                "LE PASSIF DE CLASSE : l'Appel du Sang. Chaque classe a un passif unique, gratuit et permanent. " +
                "Celui du Soulrender se declenche selon les PV du mannequin : sous 70% il est MARQUE et TOUS tes " +
                "sorts coutent -1 PA ; sous 20%, le sol autour de toi devient du Sang Coagule qui le blesse. " +
                "Plus tu le blesses, plus tu deviens fort. Clique sur Continuer.",
                Gate.Continue, Coach.None));

            _steps.Add(new Step(
                "FIN DE TOUR. Tes PA et PM se rechargent a CHAQUE debut de ton tour. Quand tu as fini, " +
                "clique Fin de tour pour passer la main. Le mannequin ne riposte pas : prends ton temps. " +
                "Termine ton tour pour repartir avec 8 PA et 3 PM.",
                Gate.EndTurn, Coach.EndTurn));

            _steps.Add(new Step(
                "TA RESSOURCE DE CLASSE : l'HEMOGLYPHE (HG), de 0 a 5. Tu gagnes +1 HG chaque fois que tu " +
                "INFLIGES des degats (et +1 si tu en subis). Les HG chargent ta SIGNATURE, ton sort ultime. " +
                "Regarde ta jauge d'HG sur ton panneau. Clique sur Continuer.",
                Gate.Continue, Coach.Resources));

            _steps.Add(new Step(
                "CHARGER LA SIGNATURE. Frappe le mannequin (relance des sorts, finis et reprends tes tours) " +
                "jusqu'a atteindre 5 HG. Chaque sort qui touche = +1 HG. Continue de l'attaquer.",
                Gate.Charge, Coach.Resources));

            _steps.Add(new Step(
                "AME LACEREE (ta SIGNATURE). A 5 HG, le slot signature (a droite de la barre) s'allume. " +
                "Elle inflige 320 degats ET te soigne de la moitie des degats infliges : c'est ton execution. " +
                "Arme la signature et frappe le mannequin.",
                Gate.Signature, Coach.Signature));

            _steps.Add(new Step(
                "Parfait. Tu connais l'essentiel : PV, PM pour bouger, PA pour agir, le passif qui recompense " +
                "l'agression, et l'Hemoglyphe qui charge ta signature. Le Soulrender gagne en mettant la " +
                "pression tour apres tour. Clique sur Continuer pour terminer.",
                Gate.Continue, Coach.None));
        }

        private void ShowStep(int i)
        {
            _index = i;
            if (i >= _steps.Count) { Finish(); return; }

            var step = _steps[i];
            if (_instructionText != null) _instructionText.text = step.Text;
            if (_statusText != null) _statusText.text = $"Etape {i + 1}/{_steps.Count}";
            if (_continueGo != null) _continueGo.SetActive(step.Gate == Gate.Continue);
            _currentCoach = step.Coach;

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
                case Gate.Charge: done = local.Resource >= SignatureChargeCap; break;
                case Gate.Signature:
                    done = local.LastCastSpellId == SpellId.SoulrenderAmeLaceree && local.LastCastSequence > _baseCast;
                    break;
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
            _currentCoach = Coach.None;
            if (_coachImage != null) _coachImage.gameObject.SetActive(false);
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
            prt.sizeDelta = new Vector2(1180f, 220f);
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
            _instructionText = NewText("Instruction", panel.transform, 26, CombatUiKit.TextPrimary, TextAlignmentOptions.TopLeft);
            var irt = _instructionText.rectTransform;
            irt.anchorMin = new Vector2(0f, 0f);
            irt.anchorMax = new Vector2(1f, 1f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.offsetMin = new Vector2(28f, 82f);
            irt.offsetMax = new Vector2(-32f, -52f);

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

            // Coach mark : CADRE arrondi CREUX (centre transparent => n'occulte pas le widget),
            // repositionné chaque frame sur la cible et animé (pulsation échelle + alpha) dans Update.
            // raycastTarget = false IMPÉRATIF : il est au-dessus du HUD (sortingOrder 500) et ne doit
            // PAS intercepter les clics destinés à la barre de sorts / bouton Fin de tour.
            var coachGo = new GameObject("CoachMark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            coachGo.transform.SetParent(_canvas.transform, false);
            _coachImage = coachGo.GetComponent<Image>();
            _coachImage.sprite = OutlineSprite();
            _coachImage.type = Image.Type.Sliced;
            _coachImage.color = CombatUiKit.Accent;
            _coachImage.raycastTarget = false;
            _coachRt = _coachImage.rectTransform;
            _coachRt.anchorMin = _coachRt.anchorMax = _coachRt.pivot = new Vector2(0.5f, 0.5f);
            coachGo.transform.SetAsFirstSibling(); // sous le panneau d'instructions
            coachGo.SetActive(false);
        }

        private void Update()
        {
            if (_coachImage == null) return;
            var target = ResolveCoachTarget();
            if (target == null) { if (_coachImage.gameObject.activeSelf) _coachImage.gameObject.SetActive(false); return; }

            if (!_coachImage.gameObject.activeSelf) _coachImage.gameObject.SetActive(true);
            PlaceCoachOver(target);

            // Animation : pulsation douce de l'échelle + de l'alpha (cadre "vivant", non occultant).
            float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            var c = _coachImage.color; c.a = Mathf.Lerp(0.55f, 1f, t); _coachImage.color = c;
            _coachRt.localScale = Vector3.one * Mathf.Lerp(1.0f, 1.06f, t);
        }

        private RectTransform ResolveCoachTarget()
        {
            if (_currentCoach == Coach.None) return null;
            var hud = CombatHUDController.Instance;
            if (hud == null) return null;
            switch (_currentCoach)
            {
                case Coach.Resources: return hud.LocalResourcePanelRect;
                case Coach.SpellBar: return hud.SpellBarRect;
                case Coach.EndTurn: return hud.EndTurnButtonRect;
                case Coach.Signature: return hud.SignatureSlotRect;
                default: return null;
            }
        }

        // Place le halo (sur le canvas tuto) par-dessus la RectTransform `target` (sur le canvas HUD),
        // en passant par l'espace écran -> robuste aux différences de CanvasScaler entre les 2 canvas.
        private void PlaceCoachOver(RectTransform target)
        {
            var canvasRt = (RectTransform)_canvas.transform;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners); // overlay canvas => coords écran (pixels)

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out Vector2 local);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            const float pad = 14f;
            _coachRt.anchoredPosition = (min + max) * 0.5f;
            _coachRt.sizeDelta = (max - min) + new Vector2(pad * 2f, pad * 2f);
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

        // ===== Sprite "cadre arrondi creux" (outline 9-slice, centre transparent) =====
        private static Sprite _outlineSprite;

        private static Sprite OutlineSprite()
        {
            if (_outlineSprite != null) return _outlineSprite;
            const int r = 18;          // rayon des coins (= border 9-slice)
            const float thickness = 4f; // épaisseur du trait
            int size = r * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Anneau = (rect arrondi plein) - (même rect rétréci de `thickness`).
                    float ring = Mathf.Clamp01(RectCoverage(x, y, size, r, 0f) - RectCoverage(x, y, size, r, thickness));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(ring * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            var border = new Vector4(r, r, r, r);
            _outlineSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            _outlineSprite.name = "TutorialOutline";
            return _outlineSprite;
        }

        /// <summary>Couverture (1 dedans, 0 dehors, AA 1px) d'un rectangle arrondi rétréci de `inset` sur tous les bords.</summary>
        private static float RectCoverage(int x, int y, int size, int r, float inset)
        {
            float cx = x + 0.5f, cy = y + 0.5f;
            float min = inset, max = size - inset;
            if (cx < min || cx > max || cy < min || cy > max) return 0f;
            float rr = Mathf.Max(0f, r - inset);
            float cornerX = -1f, cornerY = -1f;
            if (cx < min + rr) cornerX = min + rr; else if (cx > max - rr) cornerX = max - rr;
            if (cy < min + rr) cornerY = min + rr; else if (cy > max - rr) cornerY = max - rr;
            if (cornerX < 0f || cornerY < 0f) return 1f; // bord droit / centre -> plein
            float dx = cx - cornerX, dy = cy - cornerY;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(rr - dist + 0.5f);
        }
    }
}
