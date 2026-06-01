using System;
using System.Collections.Generic;
using Nymora.Hub.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Tuto T8 (Brique B) — Petit tutoriel HUB joué au RETOUR du tutoriel de combat. Ouvre le menu
    /// Échap sur l'écran d'accueil et guide le nouveau joueur sur les sections clés (Arène, Personnage,
    /// Boutique) via un panneau d'instructions + un coach mark (cadre arrondi pulsant, non-occultant)
    /// pointant chaque carte d'accueil. À la fin, invoque <c>onFinished</c> (le caller marque alors
    /// l'onboarding résolu côté backend).
    ///
    /// 100% View, code-only (même esprit que CombatView.TutorialDirector mais dans l'asmdef Hub, qui
    /// ne peut pas référencer Combat). Auto-détruit en fin de séquence.
    /// </summary>
    public sealed class HubTutorialDirector : MonoBehaviour
    {
        private static HubTutorialDirector _instance;

        public static void EnsureSpawned(Action onFinished)
        {
            if (_instance != null) { return; }
            var go = new GameObject("HubTutorialDirector");
            _instance = go.AddComponent<HubTutorialDirector>();
            _instance._onFinished = onFinished;
        }

        // ---- Modèle d'étape : texte + carte d'accueil à pointer (null = pas de coach mark) ----
        private sealed class Step
        {
            public string Text;
            public string CardId; // "arena" / "character" / "shop" / null
            public Step(string text, string cardId) { Text = text; CardId = cardId; }
        }

        private readonly List<Step> _steps = new List<Step>();
        private int _index;
        private Action _onFinished;
        private bool _finished;

        // UI
        private Canvas _canvas;
        private GameObject _panelGo;
        private TextMeshProUGUI _instructionText;
        private TextMeshProUGUI _statusText;
        private Image _coachImage;
        private RectTransform _coachRt;

        // Palette (alignée HubTutorialOnboarding, indépendante du thème pour être robuste).
        private static readonly Color PanelBg = new Color(0.10f, 0.10f, 0.13f, 0.97f);
        private static readonly Color Accent = new Color(0.42f, 0.34f, 0.66f, 1f);
        private static readonly Color TextPrimary = new Color(0.96f, 0.96f, 0.98f, 1f);
        private static readonly Color TextSecondary = new Color(0.80f, 0.82f, 0.88f, 1f);

        private void Awake()
        {
            BuildSteps();
            BuildUI();
            // Ouvre le menu sur l'accueil pour que les cartes existent (cible des coach marks).
            if (HubMenuShell.Instance != null) HubMenuShell.Instance.Open();
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
                "Bienvenue dans le HUB ! C'est ton camp de base : tu t'y deplaces, tu discutes dans le " +
                "chat, et tu prepares tes combats. Ce menu s'ouvre avec Echap (ou le bouton en haut a gauche).",
                null));
            _steps.Add(new Step(
                "L'ARENE. C'est ici que tu lances tes combats : entrainement contre l'IA, ou classe contre " +
                "de vrais joueurs. Quand tu veux te battre, c'est par la.",
                "arena"));
            _steps.Add(new Step(
                "PERSONNAGE. Choisis ta classe, construis ton deck de 6 sorts + ta signature, et equipe " +
                "tes skins et familiers. C'est ton atelier.",
                "character"));
            _steps.Add(new Step(
                "BOUTIQUE. Depense tes recompenses en skins et cosmetiques. Zero pay-to-win : tout est " +
                "purement esthetique.",
                "shop"));
            _steps.Add(new Step(
                "Et voila, tu connais l'essentiel ! Ouvre l'ARENE quand tu te sens pret a combattre. " +
                "Bon jeu sur Nymora !",
                null));
        }

        private void ShowStep(int i)
        {
            _index = i;
            if (i >= _steps.Count) { Finish(); return; }
            var step = _steps[i];
            if (_instructionText != null) _instructionText.text = step.Text;
            if (_statusText != null) _statusText.text = $"Hub  {i + 1}/{_steps.Count}";
        }

        private void OnContinueClicked()
        {
            if (_finished) return;
            ShowStep(_index + 1);
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            var cb = _onFinished;
            _onFinished = null;
            cb?.Invoke();
            Destroy(gameObject);
        }

        private void Update()
        {
            if (_finished || _coachImage == null) return;

            // Garde le menu ouvert pendant la séquence (si le joueur a fermé via Échap/voile).
            if (HubMenuShell.Instance != null && !HubMenuShell.Instance.IsMenuOpen)
                HubMenuShell.Instance.Open();

            var target = ResolveCoachTarget();
            if (target == null)
            {
                if (_coachImage.gameObject.activeSelf) _coachImage.gameObject.SetActive(false);
                return;
            }
            if (!_coachImage.gameObject.activeSelf) _coachImage.gameObject.SetActive(true);
            PlaceCoachOver(target);

            float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            var c = _coachImage.color; c.a = Mathf.Lerp(0.55f, 1f, t); _coachImage.color = c;
            _coachRt.localScale = Vector3.one * Mathf.Lerp(1.0f, 1.06f, t);
        }

        private RectTransform ResolveCoachTarget()
        {
            if (_index < 0 || _index >= _steps.Count) return null;
            string id = _steps[_index].CardId;
            if (string.IsNullOrEmpty(id) || HubMenuShell.Instance == null) return null;
            return HubMenuShell.Instance.GetHomeCardRect(id);
        }

        // ============================ UI ============================

        private void BuildUI()
        {
            var canvasGo = new GameObject("HubTutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30050; // au-dessus du menu hub, sous le voile de SceneTransition (32760)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Panneau d'instructions — bas-centre (laisse les cartes d'accueil visibles au centre/haut).
            var panel = NewImage("Panel", _canvas.transform, PanelBg, 16f);
            _panelGo = panel.gameObject;
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.sizeDelta = new Vector2(1180f, 214f);
            prt.anchoredPosition = new Vector2(0f, 28f);

            _statusText = NewText("Status", panel.transform, 22, TextSecondary, TextAlignmentOptions.TopLeft);
            var srt = _statusText.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f); srt.pivot = new Vector2(0.5f, 1f);
            srt.sizeDelta = new Vector2(-48f, 28f); srt.anchoredPosition = new Vector2(0f, -10f);

            _instructionText = NewText("Instruction", panel.transform, 22, TextPrimary, TextAlignmentOptions.TopLeft);
            var irt = _instructionText.rectTransform;
            irt.anchorMin = new Vector2(0f, 0f); irt.anchorMax = new Vector2(1f, 1f); irt.pivot = new Vector2(0.5f, 0.5f);
            irt.offsetMin = new Vector2(30f, 70f);
            irt.offsetMax = new Vector2(-34f, -42f);

            // Bouton Continuer (bas-droite).
            var btnImg = NewImage("ContinueButton", panel.transform, Accent, 10f);
            var brt = btnImg.rectTransform;
            brt.anchorMin = new Vector2(1f, 0f); brt.anchorMax = new Vector2(1f, 0f); brt.pivot = new Vector2(1f, 0f);
            brt.sizeDelta = new Vector2(190f, 50f); brt.anchoredPosition = new Vector2(-20f, 12f);
            var btn = btnImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(OnContinueClicked);
            var lbl = NewText("Label", btnImg.transform, 24, TextPrimary, TextAlignmentOptions.Center);
            var lrt = lbl.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            lbl.text = "Continuer >";

            // Coach mark : cadre arrondi creux pulsant (non-occultant), repositionné chaque frame.
            var coachGo = new GameObject("CoachMark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            coachGo.transform.SetParent(_canvas.transform, false);
            _coachImage = coachGo.GetComponent<Image>();
            _coachImage.sprite = OutlineSprite();
            _coachImage.type = Image.Type.Sliced;
            _coachImage.color = Accent;
            _coachImage.raycastTarget = false;
            _coachRt = _coachImage.rectTransform;
            _coachRt.anchorMin = _coachRt.anchorMax = _coachRt.pivot = new Vector2(0.5f, 0.5f);
            coachGo.transform.SetAsFirstSibling();
            coachGo.SetActive(false);
        }

        // Place le halo (canvas tuto) par-dessus la RectTransform `target` (canvas menu), via l'espace écran.
        private void PlaceCoachOver(RectTransform target)
        {
            var canvasRt = (RectTransform)_canvas.transform;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out Vector2 local);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            const float pad = 12f;
            _coachRt.anchoredPosition = (min + max) * 0.5f;
            _coachRt.sizeDelta = (max - min) + new Vector2(pad * 2f, pad * 2f);
        }

        // ===== Helpers UI =====

        private static Image NewImage(string name, Transform parent, Color color, float radius)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            if (radius > 0f) { img.sprite = RoundedSprite(radius); img.type = Image.Type.Sliced; }
            return img;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, int size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
            t.fontSize = size; t.color = color; t.alignment = align;
            t.enableWordWrapping = true; t.raycastTarget = false;
            return t;
        }

        // ===== Sprites générés (rond plein + cadre creux), self-contained =====
        private static Sprite _roundedSprite;
        private static Sprite _outlineSprite;

        private static Sprite RoundedSprite(float radius)
        {
            if (_roundedSprite != null) return _roundedSprite;
            int r = Mathf.Max(2, Mathf.RoundToInt(radius));
            int size = r * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(RectCoverage(x, y, size, r, 0f) * 255f));
            tex.SetPixels32(px); tex.Apply();
            var border = new Vector4(r, r, r, r);
            _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            return _roundedSprite;
        }

        private static Sprite OutlineSprite()
        {
            if (_outlineSprite != null) return _outlineSprite;
            const int r = 18;
            const float thickness = 4f;
            int size = r * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float ring = Mathf.Clamp01(RectCoverage(x, y, size, r, 0f) - RectCoverage(x, y, size, r, thickness));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(ring * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            var border = new Vector4(r, r, r, r);
            _outlineSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            return _outlineSprite;
        }

        private static float RectCoverage(int x, int y, int size, int r, float inset)
        {
            float cx = x + 0.5f, cy = y + 0.5f;
            float min = inset, max = size - inset;
            if (cx < min || cx > max || cy < min || cy > max) return 0f;
            float rr = Mathf.Max(0f, r - inset);
            float cornerX = -1f, cornerY = -1f;
            if (cx < min + rr) cornerX = min + rr; else if (cx > max - rr) cornerX = max - rr;
            if (cy < min + rr) cornerY = min + rr; else if (cy > max - rr) cornerY = max - rr;
            if (cornerX < 0f || cornerY < 0f) return 1f;
            float dx = cx - cornerX, dy = cy - cornerY;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(rr - dist + 0.5f);
        }
    }
}
