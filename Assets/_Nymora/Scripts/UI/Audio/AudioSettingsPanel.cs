using System.Collections.Generic;
using Nymora.Core.Audio;
using Nymora.Hub.Menu;
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
    /// DA alignée sur le menu hub « Échap » : si <see cref="_theme"/> est assigné (instance posée
    /// dans la scène par 'Restyle Login Scene'), on reprend la palette monochrome + la police Ari
    /// + les coins arrondis. Sinon (auto-instance), on retombe sur des constantes monochromes
    /// équivalentes (police TMP par défaut).
    ///
    /// Les sliders lisent GetBusVolume au montage et poussent SetBusVolume (persisté PlayerPrefs)
    /// à chaque changement. 100% View.
    /// </summary>
    public sealed class AudioSettingsPanel : MonoBehaviour
    {
        private static AudioSettingsPanel _instance;

        [Tooltip("Optionnel : thème hub pour matcher la DA (palette + police Ari). Si vide, fallback monochrome.")]
        [SerializeField] private HubMenuTheme _theme;

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

        // Palette + métriques résolues (depuis _theme ou fallback) au Build.
        private TMP_FontAsset _font, _fontBold;
        private Color _cPanel, _cChip, _cGhost, _cGhostHover, _cText, _cTextSec, _cAccent, _cTrack, _cOnLight;
        private float _radius = 14f;

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
            ResolveTheme();

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000; // sous le coinflip (32000), au-dessus du HUD hub
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Bouton AUDIO (toujours visible, haut-droite) : chip arrondi sombre, texte clair.
            var openBtn = NewButton(transform, "OpenButton", "AUDIO", _cChip, _cText, _radius);
            var obRt = (RectTransform)openBtn.transform;
            obRt.anchorMin = obRt.anchorMax = new Vector2(1f, 1f);
            obRt.pivot = new Vector2(1f, 1f);
            obRt.anchoredPosition = new Vector2(-18f, -18f);
            obRt.sizeDelta = new Vector2(140f, 48f);
            openBtn.onClick.AddListener(Toggle);

            // Panneau (caché par défaut).
            _panel = NewUIChild("Panel", transform).gameObject;
            var pRt = (RectTransform)_panel.transform;
            pRt.anchorMin = pRt.anchorMax = pRt.pivot = new Vector2(0.5f, 0.5f);
            pRt.sizeDelta = new Vector2(680f, 540f);
            RoundedImage(_panel, _cPanel, _radius);

            var title = NewText(pRt, "Title", "RÉGLAGES AUDIO", 34f, FontStyles.Bold, TextAlignmentOptions.Center, _cText, bold: true);
            title.characterSpacing = 4f;
            var tRt = (RectTransform)title.transform;
            tRt.anchorMin = new Vector2(0f, 1f); tRt.anchorMax = new Vector2(1f, 1f); tRt.pivot = new Vector2(0.5f, 1f);
            tRt.sizeDelta = new Vector2(0f, 70f); tRt.anchoredPosition = new Vector2(0f, -28f);

            // Croix de fermeture (ghost arrondi).
            var closeBtn = NewButton(pRt, "Close", "X", _cGhost, _cTextSec, 10f);
            var cRt = (RectTransform)closeBtn.transform;
            cRt.anchorMin = cRt.anchorMax = cRt.pivot = new Vector2(1f, 1f);
            cRt.anchoredPosition = new Vector2(-16f, -16f);
            cRt.sizeDelta = new Vector2(44f, 44f);
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
            var lbl = NewText(parent, $"Label_{bus}", label, 24f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, _cText);
            var lRt = (RectTransform)lbl.transform;
            lRt.anchorMin = lRt.anchorMax = new Vector2(0f, 1f); lRt.pivot = new Vector2(0f, 1f);
            lRt.sizeDelta = new Vector2(230f, 50f); lRt.anchoredPosition = new Vector2(34f, y);

            // Valeur %
            var val = NewText(parent, $"Val_{bus}", "100%", 22f, FontStyles.Bold, TextAlignmentOptions.MidlineRight, _cTextSec, bold: true);
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

        // ----------------------------------------------------------- Thème

        private void ResolveTheme()
        {
            if (_theme != null)
            {
                _font = _theme.Font; _fontBold = _theme.FontBold;
                _cPanel = _theme.PanelBg;
                _cChip = _theme.TabBarBg;
                _cGhost = _theme.ButtonGhostBg;
                _cGhostHover = _theme.ButtonGhostBgHover;
                _cText = _theme.TextPrimary;
                _cTextSec = _theme.TextSecondary;
                _cAccent = _theme.Accent;
                _cTrack = _theme.ButtonGhostBgHover;
                _cOnLight = _theme.TextOnLight;
                _radius = _theme.CornerRadius;
            }
            else
            {
                _font = null; _fontBold = null;
                _cPanel = new Color(0.10f, 0.105f, 0.12f, 0.97f);
                _cChip = new Color(0.07f, 0.072f, 0.085f, 0.98f);
                _cGhost = new Color(1f, 1f, 1f, 0.06f);
                _cGhostHover = new Color(1f, 1f, 1f, 0.12f);
                _cText = new Color(0.93f, 0.94f, 0.96f, 1f);
                _cTextSec = new Color(0.66f, 0.67f, 0.71f, 1f);
                _cAccent = new Color(0.93f, 0.94f, 0.96f, 1f);
                _cTrack = new Color(1f, 1f, 1f, 0.12f);
                _cOnLight = new Color(0.08f, 0.085f, 0.10f, 1f);
                _radius = 14f;
            }
        }

        // ----------------------------------------------------------- Helpers UI

        private static RectTransform NewUIChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image RoundedImage(GameObject go, Color color, float radius)
        {
            var img = go.AddComponent<Image>();
            img.color = color;
            img.sprite = HubMenuUIFactory.RoundedSprite(radius);
            img.type = Image.Type.Sliced;
            return img;
        }

        private TMP_Text NewText(Transform parent, string name, string text, float size, FontStyles style,
            TextAlignmentOptions align, Color color, bool bold = false)
        {
            var rt = NewUIChild(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.fontStyle = style; t.alignment = align;
            var font = bold ? (_fontBold != null ? _fontBold : _font) : _font;
            if (font != null) t.font = font;
            t.color = color;
            t.raycastTarget = false;
            return t;
        }

        private Button NewButton(Transform parent, string name, string label, Color bg, Color textColor, float radius)
        {
            var rt = NewUIChild(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            img.sprite = HubMenuUIFactory.RoundedSprite(radius);
            img.type = Image.Type.Sliced;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            // Survol : éclaircit la teinte (multiplicatif) pour un retour discret monochrome.
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1.5f, 1.5f, 1.5f, 1.5f);
            c.pressedColor = new Color(1.25f, 1.25f, 1.25f, 1.25f);
            c.selectedColor = Color.white;
            c.fadeDuration = 0.1f;
            btn.colors = c;

            var t = NewText(rt, "Label", label, 22f, FontStyles.Bold, TextAlignmentOptions.Center, textColor, bold: true);
            var tRt = (RectTransform)t.transform;
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one; tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
            return btn;
        }

        private Slider CreateSlider(Transform parent, string name, float value)
        {
            var rt = NewUIChild(name, parent);
            var slider = rt.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f;
            slider.wholeNumbers = false;

            // Background (piste arrondie subtile)
            var bg = NewUIChild("Background", rt);
            bg.anchorMin = new Vector2(0f, 0.32f); bg.anchorMax = new Vector2(1f, 0.68f);
            bg.offsetMin = Vector2.zero; bg.offsetMax = Vector2.zero;
            RoundedImage(bg.gameObject, _cTrack, 6f);

            // Fill Area > Fill (remplissage accent clair)
            var fillArea = NewUIChild("Fill Area", rt);
            fillArea.anchorMin = new Vector2(0f, 0.32f); fillArea.anchorMax = new Vector2(1f, 0.68f);
            fillArea.offsetMin = new Vector2(0f, 0f); fillArea.offsetMax = new Vector2(0f, 0f);
            var fill = NewUIChild("Fill", fillArea);
            fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(1f, 1f);
            fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            RoundedImage(fill.gameObject, _cAccent, 6f);
            slider.fillRect = fill;

            // Handle Slide Area > Handle (pastille claire ronde)
            var handleArea = NewUIChild("Handle Slide Area", rt);
            handleArea.anchorMin = new Vector2(0f, 0f); handleArea.anchorMax = new Vector2(1f, 1f);
            handleArea.offsetMin = Vector2.zero; handleArea.offsetMax = Vector2.zero;
            var handle = NewUIChild("Handle", handleArea);
            handle.sizeDelta = new Vector2(22f, 0f);
            var handleImg = RoundedImage(handle.gameObject, _cText, 11f);
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;

            slider.SetValueWithoutNotify(value);
            return slider;
        }
    }
}
