using System.Collections.Generic;
using Nymora.Core.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nymora.UI.Audio
{
    /// <summary>
    /// Brique A5 — Panneau de réglage des volumes (1 slider par bus : Master / Musique / SFX /
    /// Ambiance / Interface). Construit 100% en code (aucune manip de scène), auto-instancié
    /// dans les scènes hors combat (hub + menus). Bouton « AUDIO » en haut à droite -> toggle.
    ///
    /// Les sliders lisent GetBusVolume au montage et poussent SetBusVolume (persisté PlayerPrefs)
    /// à chaque changement. 100% View.
    /// </summary>
    public sealed class AudioSettingsPanel : MonoBehaviour
    {
        private static AudioSettingsPanel _instance;

        private static readonly (AudioBus bus, string label)[] Rows =
        {
            (AudioBus.Master, "Master"),
            (AudioBus.Music, "Musique"),
            (AudioBus.Sfx, "Effets (SFX)"),
            (AudioBus.Ambience, "Ambiance"),
            (AudioBus.Ui, "Interface"),
        };

        private GameObject _panel;
        private readonly Dictionary<AudioBus, TMP_Text> _valueLabels = new Dictionary<AudioBus, TMP_Text>(5);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreateForActiveScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreateForActiveScene();

        private static void TryCreateForActiveScene()
        {
            if (_instance != null) return;
            string scene = SceneManager.GetActiveScene().name;
            if (scene.Contains("Combat")) return; // pas de réglages volume en plein combat
            // Nettoyage menu Échap (M8) : dans le hub, l'audio est désormais géré par le menu
            // (Paramètres > Audio). On n'y crée plus le bouton « AUDIO » redondant. Conservé
            // ailleurs (ex : 00_Login) qui n'a pas le menu Échap.
            if (scene.Contains("CommunityHub")) return;
            var go = new GameObject("AudioSettingsPanel");
            _instance = go.AddComponent<AudioSettingsPanel>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000; // sous le coinflip (32000), au-dessus du HUD hub
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Bouton AUDIO (toujours visible, haut-droite).
            var openBtn = NewButton(transform, "OpenButton", "AUDIO", new Color(0.12f, 0.12f, 0.16f, 0.92f));
            var obRt = (RectTransform)openBtn.transform;
            obRt.anchorMin = obRt.anchorMax = new Vector2(1f, 1f);
            obRt.pivot = new Vector2(1f, 1f);
            obRt.anchoredPosition = new Vector2(-18f, -18f);
            obRt.sizeDelta = new Vector2(150f, 52f);
            openBtn.onClick.AddListener(Toggle);

            // Panneau (caché par défaut).
            _panel = NewUIChild("Panel", transform).gameObject;
            var pRt = (RectTransform)_panel.transform;
            pRt.anchorMin = pRt.anchorMax = pRt.pivot = new Vector2(0.5f, 0.5f);
            pRt.sizeDelta = new Vector2(680f, 540f);
            var pImg = _panel.AddComponent<Image>();
            pImg.color = new Color(0.07f, 0.07f, 0.10f, 0.97f);

            var title = NewText(pRt, "Title", "RÉGLAGES AUDIO", 40f, FontStyles.Bold, TextAlignmentOptions.Center);
            var tRt = (RectTransform)title.transform;
            tRt.anchorMin = new Vector2(0f, 1f); tRt.anchorMax = new Vector2(1f, 1f); tRt.pivot = new Vector2(0.5f, 1f);
            tRt.sizeDelta = new Vector2(0f, 70f); tRt.anchoredPosition = new Vector2(0f, -28f);

            // Croix de fermeture.
            var closeBtn = NewButton(pRt, "Close", "X", new Color(0.5f, 0.18f, 0.18f, 1f));
            var cRt = (RectTransform)closeBtn.transform;
            cRt.anchorMin = cRt.anchorMax = cRt.pivot = new Vector2(1f, 1f);
            cRt.anchoredPosition = new Vector2(-16f, -16f);
            cRt.sizeDelta = new Vector2(48f, 48f);
            closeBtn.onClick.AddListener(() => _panel.SetActive(false));

            // Lignes : label + slider + valeur.
            float y = -120f;
            const float rowH = 78f;
            var audio = NymoraAudioManager.Instance;
            foreach (var (bus, label) in Rows)
            {
                BuildRow(pRt, bus, label, y, audio);
                y -= rowH;
            }

            _panel.SetActive(false);
        }

        private void BuildRow(RectTransform parent, AudioBus bus, string label, float y, NymoraAudioManager audio)
        {
            // Label gauche
            var lbl = NewText(parent, $"Label_{bus}", label, 28f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            var lRt = (RectTransform)lbl.transform;
            lRt.anchorMin = lRt.anchorMax = new Vector2(0f, 1f); lRt.pivot = new Vector2(0f, 1f);
            lRt.sizeDelta = new Vector2(230f, 50f); lRt.anchoredPosition = new Vector2(34f, y);

            // Valeur %
            var val = NewText(parent, $"Val_{bus}", "100%", 26f, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            var vRt = (RectTransform)val.transform;
            vRt.anchorMin = vRt.anchorMax = new Vector2(1f, 1f); vRt.pivot = new Vector2(1f, 1f);
            vRt.sizeDelta = new Vector2(90f, 50f); vRt.anchoredPosition = new Vector2(-34f, y);
            _valueLabels[bus] = val;

            // Slider
            float initial = audio != null ? audio.GetBusVolume(bus) : 1f;
            var slider = CreateSlider(parent, $"Slider_{bus}", initial);
            var sRt = (RectTransform)slider.transform;
            sRt.anchorMin = new Vector2(0f, 1f); sRt.anchorMax = new Vector2(1f, 1f); sRt.pivot = new Vector2(0.5f, 1f);
            sRt.offsetMin = new Vector2(280f, 0f); sRt.offsetMax = new Vector2(-140f, 0f);
            sRt.sizeDelta = new Vector2(sRt.sizeDelta.x, 28f);
            sRt.anchoredPosition = new Vector2((280f - 140f) / 2f, y - 12f);

            val.text = Pct(initial);
            slider.onValueChanged.AddListener(v =>
            {
                NymoraAudioManager.Instance?.SetBusVolume(bus, v);
                if (_valueLabels.TryGetValue(bus, out var t) && t != null) t.text = Pct(v);
            });
        }

        private void Toggle()
        {
            if (_panel == null) return;
            bool show = !_panel.activeSelf;
            if (show)
            {
                // Resynchronise les sliders depuis l'état courant (au cas où changé ailleurs).
                var audio = NymoraAudioManager.Instance;
                if (audio != null)
                {
                    foreach (var (bus, _) in Rows)
                    {
                        var s = _panel.transform.Find($"Slider_{bus}")?.GetComponent<Slider>();
                        if (s != null) s.SetValueWithoutNotify(audio.GetBusVolume(bus));
                        if (_valueLabels.TryGetValue(bus, out var t) && t != null) t.text = Pct(audio.GetBusVolume(bus));
                    }
                }
            }
            _panel.SetActive(show);
        }

        private static string Pct(float v) => $"{Mathf.RoundToInt(v * 100f)}%";

        // ----------------------------------------------------------- Helpers UI

        private static RectTransform NewUIChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static TMP_Text NewText(Transform parent, string name, string text, float size, FontStyles style, TextAlignmentOptions align)
        {
            var rt = NewUIChild(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.fontStyle = style; t.alignment = align;
            t.color = new Color(0.95f, 0.95f, 0.92f, 1f);
            t.raycastTarget = false;
            return t;
        }

        private static Button NewButton(Transform parent, string name, string label, Color bg)
        {
            var rt = NewUIChild(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = NewText(rt, "Label", label, 26f, FontStyles.Bold, TextAlignmentOptions.Center);
            var tRt = (RectTransform)t.transform;
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one; tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
            return btn;
        }

        private static Slider CreateSlider(Transform parent, string name, float value)
        {
            var rt = NewUIChild(name, parent);
            var slider = rt.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f;
            slider.wholeNumbers = false;

            // Background
            var bg = NewUIChild("Background", rt);
            bg.anchorMin = new Vector2(0f, 0.25f); bg.anchorMax = new Vector2(1f, 0.75f);
            bg.offsetMin = Vector2.zero; bg.offsetMax = Vector2.zero;
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.20f, 0.20f, 0.26f, 1f);

            // Fill Area > Fill
            var fillArea = NewUIChild("Fill Area", rt);
            fillArea.anchorMin = new Vector2(0f, 0.25f); fillArea.anchorMax = new Vector2(1f, 0.75f);
            fillArea.offsetMin = new Vector2(0f, 0f); fillArea.offsetMax = new Vector2(0f, 0f);
            var fill = NewUIChild("Fill", fillArea);
            fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(1f, 1f);
            fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = new Color(0.45f, 0.70f, 1f, 1f);
            slider.fillRect = fill;

            // Handle Slide Area > Handle
            var handleArea = NewUIChild("Handle Slide Area", rt);
            handleArea.anchorMin = new Vector2(0f, 0f); handleArea.anchorMax = new Vector2(1f, 1f);
            handleArea.offsetMin = Vector2.zero; handleArea.offsetMax = Vector2.zero;
            var handle = NewUIChild("Handle", handleArea);
            handle.sizeDelta = new Vector2(20f, 0f);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = new Color(0.92f, 0.92f, 0.98f, 1f);
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;

            slider.SetValueWithoutNotify(value);
            return slider;
        }
    }
}
