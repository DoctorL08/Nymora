using System;
using Nymora.Core.Data;
using Nymora.Core.SceneFlow;
using Nymora.Core.ScriptableObjects;
using Nymora.Hub.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Tuto T9 (Brique C) — Écran de CALIBRATION D'AFFICHAGE joué AVANT le tutoriel pour un nouveau
    /// joueur. Vit dans une scène clonée du hub (même décor / lights 2D / post-process) mais SANS
    /// Fusion ni backend. Le joueur règle résolution / mode écran / image (luminosité, contraste,
    /// gamma) / qualité selon son écran, avec un Soulrender STATIQUE au centre comme repère, puis
    /// clique « Entrer en jeu » → lance le tutoriel.
    ///
    /// UI montée avec le kit du hub (<see cref="HubMenuUIFactory"/> + <see cref="HubMenuTheme"/>) pour
    /// le même design que le menu Échap. Tout est piloté par <see cref="DisplaySettingsController"/>
    /// (singleton app-wide, réglages persistés en PlayerPrefs). 100% View.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DisplayCalibrationController : MonoBehaviour
    {
        [Header("Design (kit UI du hub)")]
        [Tooltip("Glisse Assets/_Nymora/ScriptableObjects/Settings/HubMenuTheme.asset (même thème que le menu Échap).")]
        [SerializeField] private HubMenuTheme _theme;

        [Header("Soulrender statique (repère visuel)")]
        [Tooltip("Glisse la NymoraClassDefinition du Soulrender (Classes/Soulrender.asset). Ses frames idle SE sont jouées en boucle.")]
        [SerializeField] private NymoraClassDefinition _soulrenderClass;
        [Tooltip("Optionnel : frames idle custom. Si vide, on prend celles de la NymoraClassDefinition.")]
        [SerializeField] private Sprite[] _soulrenderIdleFramesOverride;
        [SerializeField] private float _idleFps = 8f;
        [Tooltip("Matériau 2D Lit du sprite (même que les avatars hub). Si null, Sprites-Default.")]
        [SerializeField] private Material _spriteMaterial;
        [SerializeField] private Vector3 _soulrenderWorldPos = Vector3.zero;
        [SerializeField] private float _soulrenderScale = 1.16f;
        [SerializeField] private string _sortingLayer = "Default";
        [SerializeField] private int _sortingOrder = 100;

        [Header("Comportement")]
        [Tooltip("Désactive un HubBootstrap éventuellement resté dans la scène (sécurité anti-Fusion).")]
        [SerializeField] private bool _disableHubBootstrapIfPresent = true;
        [Tooltip("Désactive les Canvas hub résiduels de la scène clonée (chat, devise, etc.) pour ne garder que l'UI de calibration.")]
        [SerializeField] private bool _disableLeftoverHubCanvases = true;
        [Tooltip("Scène de combat tuto chargée au clic 'Entrer en jeu'.")]
        [SerializeField] private string _tutorialScene = "30_CombatIA";

        private HubMenuUIFactory _f;
        private RectTransform _rows;
        private bool _entering;

        private void Awake()
        {
            if (_disableHubBootstrapIfPresent)
            {
                var boot = FindObjectOfType<HubBootstrap>();
                if (boot != null) boot.enabled = false;
            }
        }

        private void Start()
        {
            if (_disableLeftoverHubCanvases) DisableLeftoverHubCanvases();
            BuildSoulrender();
            BuildUI();
        }

        /// <summary>Désactive tous les Canvas appartenant À CETTE scène (UI hub clonée : chat, devise,
        /// menu, bouton émote…). Ne touche pas les canvas persistants (SceneTransition, etc.) filtrés
        /// par scène. Notre propre canvas est créé APRÈS, donc épargné.</summary>
        private void DisableLeftoverHubCanvases()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c == null) continue;
                if (c.gameObject.scene != gameObject.scene) continue; // épargne le persistant (DontDestroyOnLoad)
                if (c.transform.IsChildOf(transform)) continue;       // épargne le nôtre (au cas où)
                c.gameObject.SetActive(false);
            }
        }

        // ===== Soulrender statique =====

        private void BuildSoulrender()
        {
            Sprite[] frames = (_soulrenderIdleFramesOverride != null && _soulrenderIdleFramesOverride.Length > 0)
                ? _soulrenderIdleFramesOverride
                : (_soulrenderClass != null ? _soulrenderClass.IdleFrames : null);
            float fps = (_soulrenderClass != null && _soulrenderClass.IdleFps > 0f) ? _soulrenderClass.IdleFps : _idleFps;
            if (frames == null || frames.Length == 0)
            {
                Debug.LogWarning("[Calibration] Aucune frame Soulrender (assigne sa NymoraClassDefinition sur DisplayCalibrationController).");
                return;
            }

            var go = new GameObject("CalibrationSoulrender");
            go.transform.position = _soulrenderWorldPos;
            go.transform.localScale = Vector3.one * _soulrenderScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            if (_spriteMaterial != null) sr.sharedMaterial = _spriteMaterial;
            if (!string.IsNullOrEmpty(_sortingLayer)) sr.sortingLayerName = _sortingLayer;
            sr.sortingOrder = _sortingOrder;

            var anim = go.AddComponent<SceneSpriteAnimator>();
            anim.Play(sr, frames, fps);
        }

        // ===== UI (kit hub) =====

        private void BuildUI()
        {
            if (_theme == null)
            {
                Debug.LogError("[Calibration] HubMenuTheme non assigné sur DisplayCalibrationController — UI calibration désactivée.");
                return;
            }
            _f = new HubMenuUIFactory(_theme);

            var canvasGo = new GameObject("CalibrationCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvas.transform;

            // Panneau latéral droit (même DA que le menu hub).
            var panel = _f.MakeImage("Panel", root, _theme.PanelBg);
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(1f, 0.5f); prt.anchorMax = new Vector2(1f, 0.5f); prt.pivot = new Vector2(1f, 0.5f);
            prt.sizeDelta = new Vector2(620f, 880f);
            prt.anchoredPosition = new Vector2(-40f, 0f);

            var title = _f.MakeText("Title", prt, "Réglages d'affichage", _theme.FontSizeTitle, _theme.TextPrimary, _theme.FontBold, TextAlignmentOptions.Center);
            title.raycastTarget = false;
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(-48f, 52f); trt.anchoredPosition = new Vector2(0f, -28f);

            var sub = _f.MakeText("Sub", prt, "Ajuste l'image selon ton écran. Tu retrouveras ces réglages dans les Paramètres.",
                _theme.FontSizeSmall, _theme.TextSecondary, _theme.Font, TextAlignmentOptions.Center);
            sub.raycastTarget = false;
            var subrt = sub.rectTransform;
            subrt.anchorMin = new Vector2(0f, 1f); subrt.anchorMax = new Vector2(1f, 1f); subrt.pivot = new Vector2(0.5f, 1f);
            subrt.sizeDelta = new Vector2(-56f, 52f); subrt.anchoredPosition = new Vector2(0f, -84f);

            // Conteneur de lignes (vertical layout) entre le sous-titre et le bouton.
            _rows = _f.MakeRect("Rows", prt);
            _rows.anchorMin = new Vector2(0f, 0f); _rows.anchorMax = new Vector2(1f, 1f);
            _rows.offsetMin = new Vector2(24f, 110f); _rows.offsetMax = new Vector2(-24f, -150f);
            var vlg = _rows.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f; vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var disp = DisplaySettingsController.Instance;
            if (disp == null)
            {
                SpawnInfoRow("Réglages d'affichage indisponibles.");
            }
            else
            {
                SelectorRow("Mode d'affichage",
                    DisplaySettingsController.ScreenModeLabels.Length, disp.CurrentScreenModeIndex(),
                    i => DisplaySettingsController.ScreenModeLabels[i],
                    i => disp.ScreenMode = DisplaySettingsController.ScreenModes[i]);

                SelectorRow("Résolution",
                    disp.Resolutions.Count, disp.CurrentResolutionIndex(),
                    i => { var r = disp.Resolutions[i]; return $"{r.x} × {r.y}" + (disp.IsRecommended(r) ? "  (reco)" : ""); },
                    i => disp.SetResolution(disp.Resolutions[i]));

                SliderRow("Luminosité", disp.Brightness, v => disp.Brightness = v);
                SliderRow("Contraste", disp.Contrast, v => disp.Contrast = v);
                SliderRow("Gamma", disp.Gamma, v => disp.Gamma = v);
                ButtonRow("Réinitialiser l'image", () => { disp.ResetCalibration(); RebuildRows(); });

                ToggleRow("VSync", disp.VSync, v => disp.VSync = v);

                SelectorRow("Profil graphique",
                    DisplaySettingsController.GfxProfileLabels.Length, disp.GraphicsProfileIndex,
                    i => DisplaySettingsController.GfxProfileLabels[i],
                    i => disp.GraphicsProfileIndex = i);
            }

            // Bouton « Entrer en jeu » (CTA primaire, bas du panneau).
            var enter = _f.MakeButton(prt, "Entrer en jeu", true, out _);
            var ert = (RectTransform)enter.transform;
            ert.anchorMin = new Vector2(0.5f, 0f); ert.anchorMax = new Vector2(0.5f, 0f); ert.pivot = new Vector2(0.5f, 0f);
            ert.sizeDelta = new Vector2(380f, 60f); ert.anchoredPosition = new Vector2(0f, 30f);
            enter.onClick.AddListener(OnEnterGame);
        }

        // Reconstruit les lignes (après « Réinitialiser l'image » pour rafraîchir les sliders).
        private void RebuildRows()
        {
            if (_rows == null) return;
            for (int i = _rows.childCount - 1; i >= 0; i--) Destroy(_rows.GetChild(i).gameObject);
            var disp = DisplaySettingsController.Instance;
            if (disp == null) return;
            SliderRow("Luminosité", disp.Brightness, v => disp.Brightness = v);
            SliderRow("Contraste", disp.Contrast, v => disp.Contrast = v);
            SliderRow("Gamma", disp.Gamma, v => disp.Gamma = v);
            ButtonRow("Réinitialiser l'image", () => { disp.ResetCalibration(); RebuildRows(); });
            ToggleRow("VSync", disp.VSync, v => disp.VSync = v);
            SelectorRow("Mode d'affichage",
                DisplaySettingsController.ScreenModeLabels.Length, disp.CurrentScreenModeIndex(),
                i => DisplaySettingsController.ScreenModeLabels[i],
                i => disp.ScreenMode = DisplaySettingsController.ScreenModes[i]);
            SelectorRow("Résolution",
                disp.Resolutions.Count, disp.CurrentResolutionIndex(),
                i => { var r = disp.Resolutions[i]; return $"{r.x} × {r.y}" + (disp.IsRecommended(r) ? "  (reco)" : ""); },
                i => disp.SetResolution(disp.Resolutions[i]));
            SelectorRow("Profil graphique",
                DisplaySettingsController.GfxProfileLabels.Length, disp.GraphicsProfileIndex,
                i => DisplaySettingsController.GfxProfileLabels[i],
                i => disp.GraphicsProfileIndex = i);
        }

        private void OnEnterGame()
        {
            if (_entering) return;
            _entering = true;
            TutorialContext.Active = true;
            TutorialContext.CompletedThisSession = false;
            SceneTransition.Load(_tutorialScene, null, waitForReady: true);
        }

        // ===== Lignes (mêmes patterns que HubMenuSettings) =====

        private RectTransform MakeListRow(float height)
        {
            var row = _f.MakeImage("Row", _rows, _theme.CardBg);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return row.rectTransform;
        }

        private void SpawnInfoRow(string text)
        {
            var tmp = _f.MakeText("Info", _rows, text, _theme.FontSizeSmall, _theme.TextMuted, _theme.Font, TextAlignmentOptions.MidlineLeft);
            tmp.raycastTarget = false;
            tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
        }

        private void SelectorRow(string label, int count, int current, Func<int, string> labelFor, Action<int> onChange)
        {
            var row = MakeListRow(54f);

            var lbl = _f.MakeText("Label", row, label, _theme.FontSizeBody, _theme.TextPrimary, _theme.Font, TextAlignmentOptions.MidlineLeft);
            lbl.raycastTarget = false; lbl.enableWordWrapping = false;
            var lrt = lbl.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0f, 1f); lrt.pivot = new Vector2(0f, 0.5f);
            lrt.sizeDelta = new Vector2(220f, 0f); lrt.anchoredPosition = new Vector2(16f, 0f);

            var sel = _f.MakeRect("Sel", row);
            sel.anchorMin = new Vector2(1f, 0.5f); sel.anchorMax = new Vector2(1f, 0.5f); sel.pivot = new Vector2(1f, 0.5f);
            sel.sizeDelta = new Vector2(330f, 40f); sel.anchoredPosition = new Vector2(-12f, 0f);
            var hlg = sel.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f; hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            int idx = count > 0 ? Mathf.Clamp(current, 0, count - 1) : 0;

            var prev = _f.MakeButton(sel, "‹", false, out _);
            var ple = prev.gameObject.GetComponent<LayoutElement>(); ple.minWidth = 0f; ple.preferredWidth = 38f;

            var valueTxt = _f.MakeText("Value", sel, count > 0 ? labelFor(idx) : "—", _theme.FontSizeBody, _theme.TextSecondary, _theme.FontBold, TextAlignmentOptions.Center);
            valueTxt.raycastTarget = false; valueTxt.enableWordWrapping = false;
            valueTxt.gameObject.AddComponent<LayoutElement>().preferredWidth = 210f;

            var next = _f.MakeButton(sel, "›", false, out _);
            var nle = next.gameObject.GetComponent<LayoutElement>(); nle.minWidth = 0f; nle.preferredWidth = 38f;

            void Set(int newIdx)
            {
                if (count <= 0) return;
                idx = Mathf.Clamp(newIdx, 0, count - 1);
                onChange(idx);
                valueTxt.text = labelFor(idx);
            }
            prev.onClick.AddListener(() => Set(idx - 1));
            next.onClick.AddListener(() => Set(idx + 1));
        }

        private void SliderRow(string label, float value01, Action<float> onChange)
        {
            var row = MakeListRow(54f);

            var lbl = _f.MakeText("Label", row, label, _theme.FontSizeBody, _theme.TextPrimary, _theme.Font, TextAlignmentOptions.MidlineLeft);
            lbl.raycastTarget = false; lbl.enableWordWrapping = false;
            var lrt = lbl.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0f, 1f); lrt.pivot = new Vector2(0f, 0.5f);
            lrt.sizeDelta = new Vector2(200f, 0f); lrt.anchoredPosition = new Vector2(16f, 0f);

            var slider = MakeSlider(row, Mathf.Clamp01(value01));
            var srt = (RectTransform)slider.transform;
            srt.anchorMin = new Vector2(0f, 0.5f); srt.anchorMax = new Vector2(1f, 0.5f); srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(-(230f + 30f), 20f);
            srt.anchoredPosition = new Vector2((230f - 30f) * 0.5f, 0f);
            slider.onValueChanged.AddListener(v => onChange(v));
        }

        private void ToggleRow(string label, bool value, Action<bool> onChange)
        {
            var row = MakeListRow(54f);

            var lbl = _f.MakeText("Label", row, label, _theme.FontSizeBody, _theme.TextPrimary, _theme.Font, TextAlignmentOptions.MidlineLeft);
            lbl.raycastTarget = false; lbl.enableWordWrapping = false;
            var lrt = lbl.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 1f); lrt.pivot = new Vector2(0f, 0.5f);
            lrt.offsetMin = new Vector2(16f, 0f); lrt.offsetMax = new Vector2(-70f, 0f);

            var toggle = MakeToggle(row, value);
            var trt = (RectTransform)toggle.transform;
            trt.anchorMin = new Vector2(1f, 0.5f); trt.anchorMax = new Vector2(1f, 0.5f); trt.pivot = new Vector2(1f, 0.5f);
            trt.sizeDelta = new Vector2(30f, 30f); trt.anchoredPosition = new Vector2(-16f, 0f);
            toggle.onValueChanged.AddListener(v => onChange(v));
        }

        private void ButtonRow(string label, Action onClick)
        {
            var row = MakeListRow(54f);
            var btn = _f.MakeButton(row, label, false, out _);
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = new Vector2(1f, 0.5f); brt.anchorMax = new Vector2(1f, 0.5f); brt.pivot = new Vector2(1f, 0.5f);
            brt.sizeDelta = new Vector2(260f, 40f); brt.anchoredPosition = new Vector2(-12f, 0f);
            btn.onClick.AddListener(() => onClick());
        }

        // ===== Widgets montés en code (repris de HubMenuSettings) =====

        private Slider MakeSlider(RectTransform parent, float value01)
        {
            var rootRt = _f.MakeRect("Slider", parent);
            var slider = rootRt.gameObject.AddComponent<Slider>();

            var bg = _f.MakeImage("Track", rootRt, new Color(1f, 1f, 1f, 0.12f));
            var bgrt = bg.rectTransform;
            bgrt.anchorMin = new Vector2(0f, 0.5f); bgrt.anchorMax = new Vector2(1f, 0.5f); bgrt.pivot = new Vector2(0.5f, 0.5f);
            bgrt.sizeDelta = new Vector2(0f, 8f); bgrt.anchoredPosition = Vector2.zero;

            var fillArea = _f.MakeRect("Fill Area", rootRt);
            fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f); fillArea.pivot = new Vector2(0.5f, 0.5f);
            fillArea.offsetMin = new Vector2(8f, -4f); fillArea.offsetMax = new Vector2(-8f, 4f);
            var fill = _f.MakeImage("Fill", fillArea, _theme.Accent);
            var frt = fill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(1f, 1f); frt.sizeDelta = Vector2.zero; frt.anchoredPosition = Vector2.zero;
            fill.raycastTarget = false;

            var handleArea = _f.MakeRect("Handle Slide Area", rootRt);
            handleArea.anchorMin = new Vector2(0f, 0f); handleArea.anchorMax = new Vector2(1f, 1f);
            handleArea.offsetMin = new Vector2(8f, 0f); handleArea.offsetMax = new Vector2(-8f, 0f);
            var handle = _f.MakeImage("Handle", handleArea, Color.white);
            var hrt = handle.rectTransform;
            hrt.anchorMin = new Vector2(0f, 0.5f); hrt.anchorMax = new Vector2(0f, 0.5f); hrt.pivot = new Vector2(0.5f, 0.5f);
            hrt.sizeDelta = new Vector2(18f, 18f);

            slider.fillRect = frt;
            slider.handleRect = hrt;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.wholeNumbers = false;
            slider.value = value01;
            return slider;
        }

        private Toggle MakeToggle(RectTransform parent, bool value)
        {
            var box = _f.MakeImage("Toggle", parent, _theme.ButtonGhostBg);
            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            var check = _f.MakeImage("Check", box.rectTransform, _theme.Accent);
            HubMenuUIFactory.Stretch(check.rectTransform, 6f, 6f, 6f, 6f);
            check.raycastTarget = false;
            toggle.graphic = check;
            toggle.isOn = value;
            return toggle;
        }
    }
}
