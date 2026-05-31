using System.Collections.Generic;
using Nymora.Core.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub.Menu
{
    /// <summary>
    /// M6 — Écran Paramètres du nouveau menu hub (onglet "Paramètres" de la barre haute).
    ///
    /// Écran NEUF (aucun panneau existant repris). Deux sous-onglets pilule :
    ///   - Affichage : toggle Plein écran (Screen.fullScreen, persisté nativement en standalone).
    ///   - Audio     : un slider par bus (Volume général / Musique / Effets / Ambiance / Interface),
    ///                 branché direct sur NymoraAudioManager (Get/SetBusVolume, persisté PlayerPrefs).
    ///
    /// 100% View — pas de bump CombatRulesVersion. Tout est monté en code (sliders/toggles inclus).
    /// </summary>
    public sealed class HubMenuSettings
    {
        private readonly HubMenuTheme _t;
        private readonly HubMenuUIFactory _f;

        // Bus exposés (ordre d'affichage) + libellés FR.
        private static readonly AudioBus[] Buses = { AudioBus.Master, AudioBus.Music, AudioBus.Sfx, AudioBus.Ambience, AudioBus.Ui };
        private static readonly string[] BusLabels = { "Volume général", "Musique", "Effets", "Ambiance", "Interface" };

        private int _tab; // 0 = Affichage, 1 = Audio
        private readonly List<(Image bg, TextMeshProUGUI label)> _tabRefs = new List<(Image, TextMeshProUGUI)>();
        private RectTransform _content;
        private RectTransform _listRoot;

        public HubMenuSettings(HubMenuTheme t, HubMenuUIFactory f)
        {
            _t = t; _f = f;
        }

        // ===== Entrée =====

        public void Build(RectTransform parent)
        {
            var tabsRow = _f.MakeRect("SettingsTabs", parent);
            tabsRow.anchorMin = new Vector2(0.5f, 1f); tabsRow.anchorMax = new Vector2(0.5f, 1f); tabsRow.pivot = new Vector2(0.5f, 1f);
            tabsRow.sizeDelta = new Vector2(360f, 42f); tabsRow.anchoredPosition = new Vector2(0f, -18f);
            var thlg = tabsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            thlg.spacing = 10f; thlg.childAlignment = TextAnchor.MiddleCenter;
            thlg.childControlWidth = true; thlg.childControlHeight = true;
            thlg.childForceExpandWidth = false; thlg.childForceExpandHeight = false;
            SpawnTab(tabsRow, "Affichage", 0);
            SpawnTab(tabsRow, "Audio", 1);

            _content = _f.MakeRect("SettingsContent", parent);
            _content.anchorMin = new Vector2(0f, 0f); _content.anchorMax = new Vector2(1f, 1f);
            _content.offsetMin = new Vector2(28f, 18f); _content.offsetMax = new Vector2(-28f, -74f);

            UpdateTabStyles();
            ShowTab(0);
        }

        private void SpawnTab(RectTransform parent, string label, int idx)
        {
            var img = _f.MakeImage("Tab_" + label, parent, _t.ButtonGhostBg);
            var le = img.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 160f; le.preferredHeight = 40f;
            var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            var lbl = _f.MakeText("L", img.rectTransform, label, _t.FontSizeBody, _t.TextSecondary, _t.Font, TextAlignmentOptions.Center);
            HubMenuUIFactory.Stretch(lbl.rectTransform); lbl.raycastTarget = false; lbl.enableWordWrapping = false;
            btn.onClick.AddListener(() => { if (_tab != idx) ShowTab(idx); });
            _tabRefs.Add((img, lbl));
        }

        private void UpdateTabStyles()
        {
            for (int i = 0; i < _tabRefs.Count; i++)
            {
                bool active = i == _tab;
                if (_tabRefs[i].bg != null) _tabRefs[i].bg.color = active ? _t.Accent : _t.ButtonGhostBg;
                if (_tabRefs[i].label != null)
                {
                    _tabRefs[i].label.color = active ? _t.TextOnLight : _t.TextSecondary;
                    _tabRefs[i].label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }

        private void ShowTab(int idx)
        {
            _tab = idx;
            UpdateTabStyles();
            for (int i = _content.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);
            _listRoot = null;

            BuildScroll(_content);
            if (idx == 0) BuildAffichage();
            else BuildAudio();
        }

        // ===== Onglet Affichage =====

        private void BuildAffichage()
        {
            var disp = DisplaySettingsController.Instance;
            if (disp == null)
            {
                SpawnInfoRow("<i>Réglages d'affichage indisponibles.</i>");
                return;
            }

            // Mode d'affichage : Plein écran / Sans bordure / Fenêtré
            SpawnSelectorRow("Mode d'affichage",
                DisplaySettingsController.ScreenModeLabels.Length, disp.CurrentScreenModeIndex(),
                i => DisplaySettingsController.ScreenModeLabels[i],
                i => disp.ScreenMode = DisplaySettingsController.ScreenModes[i]);

            // Résolution (recommandé = native moniteur)
            SpawnSelectorRow("Résolution",
                disp.Resolutions.Count, disp.CurrentResolutionIndex(),
                i =>
                {
                    var r = disp.Resolutions[i];
                    return $"{r.x} × {r.y}" + (disp.IsRecommended(r) ? "  <size=72%><color=#8be0ff>(recommandé)</color></size>" : "");
                },
                i => disp.SetResolution(disp.Resolutions[i]));

            // Calibration image : 0.5 = neutre (affiché 0). Luminosité = post-exposure (assombrit ET
            // éclaircit) ; Contraste + Gamma passent par le post-process (sans effet en "Sans effets").
            SpawnSliderRow("Luminosité", disp.Brightness, v => disp.Brightness = v, Signed);
            SpawnSliderRow("Contraste", disp.Contrast, v => disp.Contrast = v, Signed);
            SpawnSliderRow("Gamma", disp.Gamma, v => disp.Gamma = v, Signed);
            SpawnButtonRow("Réinitialiser l'image", () => { disp.ResetCalibration(); ShowTab(_tab); });

            // VSync
            SpawnToggleRow("VSync", disp.VSync, v => disp.VSync = v);

            // Limite FPS (ignorée si VSync activé)
            SpawnSelectorRow("Limite FPS",
                DisplaySettingsController.FpsLabels.Length, disp.CurrentFpsIndex(),
                i => DisplaySettingsController.FpsLabels[i],
                i => disp.TargetFps = DisplaySettingsController.FpsOptions[i]);

            // Profil graphique (post-process + lumière globale) — inclut "Sans effets".
            var gfxLabels = DisplaySettingsController.GfxProfileLabels;
            SpawnSelectorRow("Profil graphique",
                gfxLabels.Length, disp.GraphicsProfileIndex,
                i => gfxLabels[i],
                i => disp.GraphicsProfileIndex = i);

            // T7 — relancer le tutoriel (Soulrender scripté) à la demande.
            SpawnButtonRow("Rejouer le tutoriel", HubTutorialOnboarding.LaunchTutorial);
        }

        /// <summary>Ligne avec sélecteur ‹ valeur › à droite (cycle index 0..count-1, clampé).</summary>
        private void SpawnSelectorRow(string label, int count, int current, System.Func<int, string> labelFor, System.Action<int> onChange)
        {
            var row = MakeListRow(54f);

            var lbl = _f.MakeText("Label", row, label, _t.FontSizeBody, _t.TextPrimary, _t.Font, TextAlignmentOptions.MidlineLeft);
            lbl.raycastTarget = false; lbl.enableWordWrapping = false;
            var lrt = lbl.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0f, 1f); lrt.pivot = new Vector2(0f, 0.5f);
            lrt.sizeDelta = new Vector2(220f, 0f); lrt.anchoredPosition = new Vector2(16f, 0f);

            var sel = _f.MakeRect("Sel", row);
            sel.anchorMin = new Vector2(1f, 0.5f); sel.anchorMax = new Vector2(1f, 0.5f); sel.pivot = new Vector2(1f, 0.5f);
            sel.sizeDelta = new Vector2(360f, 40f); sel.anchoredPosition = new Vector2(-12f, 0f);
            var hlg = sel.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f; hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            int idx = count > 0 ? Mathf.Clamp(current, 0, count - 1) : 0;

            var prev = _f.MakeButton(sel, "‹", false, out _);
            var ple = prev.gameObject.GetComponent<LayoutElement>(); ple.minWidth = 0f; ple.preferredWidth = 38f;

            var valueTxt = _f.MakeText("Value", sel, count > 0 ? labelFor(idx) : "—", _t.FontSizeBody, _t.TextSecondary, _t.FontBold, TextAlignmentOptions.Center);
            valueTxt.raycastTarget = false; valueTxt.enableWordWrapping = false;
            valueTxt.gameObject.AddComponent<LayoutElement>().preferredWidth = 240f;

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

        // ===== Onglet Audio =====

        private void BuildAudio()
        {
            var audio = NymoraAudioManager.Instance;
            if (audio == null)
            {
                SpawnInfoRow("<i>Gestionnaire audio indisponible.</i>");
                return;
            }
            for (int i = 0; i < Buses.Length; i++)
            {
                var bus = Buses[i];
                SpawnSliderRow(BusLabels[i], audio.GetBusVolume(bus), v => audio.SetBusVolume(bus, v));
            }
        }

        // ===== Lignes =====

        private void SpawnSliderRow(string label, float value01, System.Action<float> onChange)
            => SpawnSliderRow(label, value01, onChange, Pct);

        /// <summary>Ligne slider avec formateur de valeur personnalisé (% par défaut, signé pour la
        /// calibration image).</summary>
        private void SpawnSliderRow(string label, float value01, System.Action<float> onChange, System.Func<float, string> fmt)
        {
            var row = MakeListRow(54f);

            var lbl = _f.MakeText("Label", row, label, _t.FontSizeBody, _t.TextPrimary, _t.Font, TextAlignmentOptions.MidlineLeft);
            lbl.raycastTarget = false; lbl.enableWordWrapping = false;
            var lrt = lbl.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0f, 1f); lrt.pivot = new Vector2(0f, 0.5f);
            lrt.sizeDelta = new Vector2(220f, 0f); lrt.anchoredPosition = new Vector2(16f, 0f);

            var val = _f.MakeText("Val", row, fmt(value01), _t.FontSizeBody, _t.TextSecondary, _t.FontBold, TextAlignmentOptions.MidlineRight);
            val.raycastTarget = false; val.enableWordWrapping = false;
            var vrt = val.rectTransform;
            vrt.anchorMin = new Vector2(1f, 0.5f); vrt.anchorMax = new Vector2(1f, 0.5f); vrt.pivot = new Vector2(1f, 0.5f);
            vrt.sizeDelta = new Vector2(64f, 28f); vrt.anchoredPosition = new Vector2(-16f, 0f);

            var slider = MakeSlider(row, value01);
            var srt = (RectTransform)slider.transform;
            srt.anchorMin = new Vector2(0f, 0.5f); srt.anchorMax = new Vector2(1f, 0.5f); srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(-(250f + 100f), 20f);   // marge gauche label (250) + droite valeur (100)
            srt.anchoredPosition = new Vector2((250f - 100f) * 0.5f, 0f);

            slider.onValueChanged.AddListener(v =>
            {
                onChange(v);
                val.text = fmt(v);
            });
        }

        /// <summary>Ligne avec un seul bouton aligné à droite (ex. réinitialiser la calibration).</summary>
        private void SpawnButtonRow(string label, System.Action onClick)
        {
            var row = MakeListRow(54f);
            var btn = _f.MakeButton(row, label, false, out _);
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = new Vector2(1f, 0.5f); brt.anchorMax = new Vector2(1f, 0.5f); brt.pivot = new Vector2(1f, 0.5f);
            brt.sizeDelta = new Vector2(260f, 40f); brt.anchoredPosition = new Vector2(-12f, 0f);
            btn.onClick.AddListener(() => onClick());
        }

        private void SpawnToggleRow(string label, bool value, System.Action<bool> onChange)
        {
            var row = MakeListRow(54f);

            var lbl = _f.MakeText("Label", row, label, _t.FontSizeBody, _t.TextPrimary, _t.Font, TextAlignmentOptions.MidlineLeft);
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

        // ===== Widgets montés en code =====

        /// <summary>Slider horizontal complet (track + remplissage + poignée), monté en code.</summary>
        private Slider MakeSlider(RectTransform parent, float value01)
        {
            var root = _f.MakeRect("Slider", parent);
            var slider = root.gameObject.AddComponent<Slider>();

            var bg = _f.MakeImage("Track", root, new Color(1f, 1f, 1f, 0.12f));
            var bgrt = bg.rectTransform;
            bgrt.anchorMin = new Vector2(0f, 0.5f); bgrt.anchorMax = new Vector2(1f, 0.5f); bgrt.pivot = new Vector2(0.5f, 0.5f);
            bgrt.sizeDelta = new Vector2(0f, 8f); bgrt.anchoredPosition = Vector2.zero;

            var fillArea = _f.MakeRect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f); fillArea.pivot = new Vector2(0.5f, 0.5f);
            fillArea.offsetMin = new Vector2(8f, -4f); fillArea.offsetMax = new Vector2(-8f, 4f);
            var fill = _f.MakeImage("Fill", fillArea, _t.Accent);
            var frt = fill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(1f, 1f); frt.sizeDelta = Vector2.zero; frt.anchoredPosition = Vector2.zero;
            fill.raycastTarget = false;

            var handleArea = _f.MakeRect("Handle Slide Area", root);
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
            slider.minValue = 0f; slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = value01;
            return slider;
        }

        /// <summary>Toggle case à cocher (fond ghost + coche accent), monté en code.</summary>
        private Toggle MakeToggle(RectTransform parent, bool value)
        {
            var box = _f.MakeImage("Toggle", parent, _t.ButtonGhostBg);
            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;

            var check = _f.MakeImage("Check", box.rectTransform, _t.Accent);
            HubMenuUIFactory.Stretch(check.rectTransform, 6f, 6f, 6f, 6f);
            check.raycastTarget = false;
            toggle.graphic = check;
            toggle.isOn = value;
            return toggle;
        }

        // ===== Helpers liste (mêmes patterns que HubMenuSocial/Progression) =====

        private void BuildScroll(RectTransform parent)
        {
            var vp = _f.MakeRect("Scroll", parent);
            vp.anchorMin = new Vector2(0f, 0f); vp.anchorMax = new Vector2(1f, 1f);
            vp.offsetMin = new Vector2(0f, 8f); vp.offsetMax = new Vector2(0f, -4f);
            var vpImg = vp.gameObject.AddComponent<Image>(); vpImg.color = new Color(1f, 1f, 1f, 0.02f);
            vp.gameObject.AddComponent<RectMask2D>();
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 28f; sr.viewport = vp;

            var content = _f.MakeRect("Content", vp);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f; vlg.padding = new RectOffset(8, 8, 8, 8); vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize; fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sr.content = content;
            _listRoot = content;
        }

        private void SpawnInfoRow(string richText)
        {
            var tmp = _f.MakeText("Info", _listRoot, richText, _t.FontSizeSmall, _t.TextMuted, _t.Font, TextAlignmentOptions.MidlineLeft);
            tmp.raycastTarget = false;
            tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
        }

        private RectTransform MakeListRow(float height)
        {
            var row = _f.MakeImage("Row", _listRoot, _t.CardBg);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return row.rectTransform;
        }

        private static string Pct(float v01) => $"{Mathf.RoundToInt(v01 * 100f)}%";

        // Calibration image : 0.5 -> "0" (neutre), sinon offset signé -100..+100.
        private static string Signed(float v01)
        {
            int n = Mathf.RoundToInt((v01 - 0.5f) * 200f);
            return n > 0 ? "+" + n : n.ToString();
        }
    }
}
