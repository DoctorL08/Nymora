using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Réglages d'affichage persistants (résolution / plein écran / luminosité / effets post-process).
    /// Auto-instancié (DontDestroyOnLoad) comme l'audio ; applique les préférences PlayerPrefs au
    /// démarrage et les ré-applique à chaque chargement de scène (les caméras changent par scène).
    ///
    /// - Résolution : liste dédupliquée des modes du moniteur ; recommandé = résolution système native.
    /// - Luminosité : overlay plein écran (canvas dédié, sortingOrder max, non bloquant). 0.5 = neutre,
    ///   en dessous assombrit (voile noir), au-dessus éclaircit (voile blanc).
    /// - Effets visuels : toggle UniversalAdditionalCameraData.renderPostProcessing sur toutes les
    ///   caméras (coupe tout le pack post-process URP).
    ///
    /// 100% View — pas de bump CombatRulesVersion.
    /// </summary>
    public sealed class DisplaySettingsController : MonoBehaviour
    {
        private const string KeyBrightness = "nymora.display.brightness"; // 0..1, defaut 1 (natif)
        private const string KeyPostFx = "nymora.display.postfx";          // 1 on / 0 off
        private const string KeyResW = "nymora.display.resw";
        private const string KeyResH = "nymora.display.resh";
        private const string KeyScreenMode = "nymora.display.screenmode";  // (int)FullScreenMode
        private const string KeyVSync = "nymora.display.vsync";            // 1 on / 0 off
        private const string KeyFps = "nymora.display.fps";                // 0 = illimité
        private const string KeyGfxProfile = "nymora.display.gfxprofile";  // index dans GfxProfiles

        // GFX-1 — Profil graphique : retravaille le post-process ET l'intensite/teinte de la lumiere
        // GLOBALE 2D (multiplicateur sur la valeur de base de la scene -> non destructif). Le dernier
        // profil (PostProcess=false) = "Sans effets". Ordre = index du selecteur UI.
        public sealed class GfxProfile
        {
            public string Name;
            public bool PostProcess = true;
            // Si false, le profil NE touche PAS le post-process : le Volume de la scène (réglé à la
            // main dans l'éditeur) fait foi. Utile quand on peaufine directement le Volume (WYSIWYG).
            public bool OverridePostProcess = true;
            // ColorAdjustments
            public float PostExposure;
            public float Contrast;
            public float Saturation;
            // WhiteBalance
            public float Temperature;
            public float Tint;
            // SplitToning
            public Color SplitShadows;
            public Color SplitHighlights;
            public float SplitBalance;
            // Vignette / Bloom
            public float Vignette = 0.15f;
            public float BloomIntensity = 14f;
            // ColorLookup : LUT chargee depuis Resources/PostProcessing/<Lut> (null = pas de LUT)
            public string Lut;
            public float LutContribution = 0.35f;
            // Lumiere globale 2D : multiplicateur sur l'intensite de base + teinte (multiplicative,
            // blanc = identite -> aucune alteration).
            public float LightIntensityMul = 1f;
            public Color LightTint = Color.white;
            // Torches : multiplicateur applique par TorchLightFlicker sur l'intensite de base de
            // CHAQUE torche (par-dessus le flicker) -> profondeur de lumiere (puits chauds) par profil.
            public float TorchIntensityMul = 1f;
        }

        // 5 ambiances + "Sans effets". Valeurs de depart ; calibrables ensuite.
        public static readonly GfxProfile[] GfxProfiles =
        {
            // Profil UNIQUE : ne touche PAS le post-process -> le Volume (Hub_PostFX / Combat_PostFX)
            // réglé à la main dans l'éditeur fait foi (WYSIWYG). N'ajuste ni la lumière globale ni les
            // torches (1× / blanc) -> le Play correspond exactement à l'éditeur.
            new GfxProfile {
                Name = "Standard", OverridePostProcess = false,
                LightIntensityMul = 1f, LightTint = Color.white, TorchIntensityMul = 1f,
            },
            new GfxProfile { Name = "Sans effets", PostProcess = false, LightIntensityMul = 1f, LightTint = Color.white },
        };

        public static string[] GfxProfileLabels
        {
            get
            {
                var a = new string[GfxProfiles.Length];
                for (int i = 0; i < a.Length; i++) a[i] = GfxProfiles[i].Name;
                return a;
            }
        }

        // Modes d'affichage exposés (ordre = index du sélecteur UI).
        public static readonly FullScreenMode[] ScreenModes =
        {
            FullScreenMode.ExclusiveFullScreen, // Plein écran
            FullScreenMode.FullScreenWindow,    // Sans bordure
            FullScreenMode.Windowed,            // Fenêtré
        };
        public static readonly string[] ScreenModeLabels = { "Plein écran", "Sans bordure", "Fenêtré" };

        // Limites FPS exposées (0 = illimité).
        public static readonly int[] FpsOptions = { 30, 60, 120, 144, 0 };
        public static readonly string[] FpsLabels = { "30", "60", "120", "144", "Illimité" };

        public static DisplaySettingsController Instance { get; private set; }

        private readonly List<Vector2Int> _resolutions = new List<Vector2Int>();
        private Image _overlay;

        // GFX-1 — lumiere globale 2D de la scene + son intensite/couleur de BASE (valeurs serialisees
        // de la scene, capturees une fois par scene). Le profil applique un multiplicateur/teinte
        // dessus -> on n'ecrase jamais les valeurs de Lorenzo, on les module.
        private Light2D _globalLight;
        private float _globalLightBaseIntensity = 1f;
        private Color _globalLightBaseColor = Color.white;
        private bool _globalLightCaptured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("DisplaySettingsController");
            DontDestroyOnLoad(go);
            go.AddComponent<DisplaySettingsController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            BuildResolutionList();
            BuildBrightnessOverlay();

            // Applique les préférences sauvegardées au démarrage.
            ApplyBrightness();
            ApplySavedScreenMode();
            ApplySavedResolution();
            ApplyFrameSettings();

            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyGraphicsProfile(); // post-process + lumiere globale de la scene courante
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Les caméras + le Volume + la lumière globale changent à chaque scène : on re-capture la
        // lumière de base et on ré-applique le profil graphique complet.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _globalLight = null;
            _globalLightCaptured = false;
            ApplyGraphicsProfile();
        }

        // ===== Résolution =====

        private void BuildResolutionList()
        {
            _resolutions.Clear();
            var seen = new HashSet<long>();
            foreach (var r in Screen.resolutions)
            {
                long key = ((long)r.width << 32) | (uint)r.height;
                if (seen.Add(key)) _resolutions.Add(new Vector2Int(r.width, r.height));
            }
            if (_resolutions.Count == 0) _resolutions.Add(new Vector2Int(Screen.width, Screen.height));
        }

        public IReadOnlyList<Vector2Int> Resolutions => _resolutions;

        /// <summary>Résolution native du moniteur (recommandée).</summary>
        public Vector2Int Recommended => new Vector2Int(Display.main.systemWidth, Display.main.systemHeight);

        public Vector2Int Current => new Vector2Int(Screen.width, Screen.height);

        public int CurrentResolutionIndex()
        {
            var cur = Current;
            for (int i = 0; i < _resolutions.Count; i++)
                if (_resolutions[i] == cur) return i;
            return Mathf.Max(0, _resolutions.Count - 1);
        }

        public bool IsRecommended(Vector2Int res) => res == Recommended;

        public void SetResolution(Vector2Int res)
        {
            Screen.SetResolution(res.x, res.y, Screen.fullScreenMode);
            PlayerPrefs.SetInt(KeyResW, res.x);
            PlayerPrefs.SetInt(KeyResH, res.y);
            PlayerPrefs.Save();
        }

        private void ApplySavedResolution()
        {
            int w = PlayerPrefs.GetInt(KeyResW, 0);
            int h = PlayerPrefs.GetInt(KeyResH, 0);
            if (w > 0 && h > 0 && (w != Screen.width || h != Screen.height))
                Screen.SetResolution(w, h, Screen.fullScreenMode);
        }

        // ===== Mode d'affichage (plein écran / sans bordure / fenêtré) =====

        public FullScreenMode ScreenMode
        {
            get => Screen.fullScreenMode;
            set
            {
                Screen.fullScreenMode = value;
                PlayerPrefs.SetInt(KeyScreenMode, (int)value);
                PlayerPrefs.Save();
            }
        }

        public int CurrentScreenModeIndex()
        {
            var cur = Screen.fullScreenMode;
            for (int i = 0; i < ScreenModes.Length; i++)
                if (ScreenModes[i] == cur) return i;
            return 0;
        }

        private void ApplySavedScreenMode()
        {
            if (!PlayerPrefs.HasKey(KeyScreenMode)) return;
            var mode = (FullScreenMode)PlayerPrefs.GetInt(KeyScreenMode);
            if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
        }

        // ===== Luminosité =====

        private void BuildBrightnessOverlay()
        {
            var go = new GameObject("BrightnessOverlay", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760; // au-dessus de tout
            _overlay = go.AddComponent<Image>();
            _overlay.raycastTarget = false; // ne bloque jamais les clics
            var rt = _overlay.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _overlay.color = new Color(0f, 0f, 0f, 0f);
        }

        /// <summary>Luminosité 0..1 : 1 = natif (aucun voile), plus bas = assombrit doucement.
        /// Assombrissement uniquement (un voile blanc qui "éclaircit" crame les yeux et lave
        /// l'image — on ne le fait pas). Plancher à 0.4 côté UI pour ne jamais tout noircir.</summary>
        public float Brightness
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(KeyBrightness, 1f));
            set
            {
                PlayerPrefs.SetFloat(KeyBrightness, Mathf.Clamp01(value));
                PlayerPrefs.Save();
                ApplyBrightness();
            }
        }

        private void ApplyBrightness()
        {
            if (_overlay == null) return;
            // v=1 -> alpha 0 (rien) ; v=0 -> alpha 0.55 (assez sombre, jamais total).
            float alpha = (1f - Brightness) * 0.55f;
            _overlay.color = new Color(0f, 0f, 0f, alpha);
        }

        // ===== VSync + limite FPS =====

        public bool VSync
        {
            get => PlayerPrefs.GetInt(KeyVSync, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(KeyVSync, value ? 1 : 0);
                PlayerPrefs.Save();
                ApplyFrameSettings();
            }
        }

        /// <summary>0 = illimité.</summary>
        public int TargetFps
        {
            get => PlayerPrefs.GetInt(KeyFps, 0);
            set
            {
                PlayerPrefs.SetInt(KeyFps, value);
                PlayerPrefs.Save();
                ApplyFrameSettings();
            }
        }

        public int CurrentFpsIndex()
        {
            int cur = TargetFps;
            for (int i = 0; i < FpsOptions.Length; i++)
                if (FpsOptions[i] == cur) return i;
            return FpsOptions.Length - 1; // illimité par défaut
        }

        private void ApplyFrameSettings()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            // targetFrameRate est ignoré quand VSync est actif (Unity cale sur le refresh écran).
            int fps = TargetFps;
            Application.targetFrameRate = fps <= 0 ? -1 : fps;
        }

        // ===== Profil graphique (shaders / colorimétrie / lumière globale) =====

        /// <summary>Index du profil choisi dans <see cref="GfxProfiles"/> (defaut 0 = Cendres).</summary>
        public int GraphicsProfileIndex
        {
            get
            {
                if (PlayerPrefs.HasKey(KeyGfxProfile))
                    return Mathf.Clamp(PlayerPrefs.GetInt(KeyGfxProfile), 0, GfxProfiles.Length - 1);
                // Migration : un ancien joueur ayant coupe les effets (KeyPostFx=0) tombe sur "Sans effets".
                if (PlayerPrefs.GetInt(KeyPostFx, 1) == 0) return GfxProfiles.Length - 1;
                return 0; // defaut = Cendres
            }
            set
            {
                PlayerPrefs.SetInt(KeyGfxProfile, Mathf.Clamp(value, 0, GfxProfiles.Length - 1));
                PlayerPrefs.Save();
                ApplyGraphicsProfile();
            }
        }

        public GfxProfile CurrentProfile => GfxProfiles[GraphicsProfileIndex];

        // Applique le profil courant : post-process (Volume de la scene) + lumiere globale 2D.
        private void ApplyGraphicsProfile()
        {
            var p = CurrentProfile;
            SetCamerasPostProcess(p.PostProcess);
            if (p.PostProcess && p.OverridePostProcess) ApplyProfileToVolume(p);
            ApplyProfileToGlobalLight(p);
        }

        private static void SetCamerasPostProcess(bool on)
        {
            foreach (var cam in Camera.allCameras)
            {
                if (cam == null) continue;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = on;
            }
        }

        // Pousse les valeurs du profil sur le Volume post-process de la scene. On mute volume.profile
        // (INSTANCE runtime, pas le sharedProfile) -> non destructif pour l'asset. Les composants
        // existent deja sur Hub_PostFX / Combat_PostFX (sinon TryGet echoue silencieusement).
        private void ApplyProfileToVolume(GfxProfile p)
        {
            var volume = FindFirstGlobalVolume();
            if (volume == null) return;
            var prof = volume.profile; // instance runtime
            if (prof == null) return;

            if (prof.TryGet<ColorAdjustments>(out var ca))
            {
                ca.postExposure.Override(p.PostExposure);
                ca.contrast.Override(p.Contrast);
                ca.saturation.Override(p.Saturation);
            }
            if (prof.TryGet<WhiteBalance>(out var wb))
            {
                wb.temperature.Override(p.Temperature);
                wb.tint.Override(p.Tint);
            }
            if (prof.TryGet<SplitToning>(out var st))
            {
                st.shadows.Override(p.SplitShadows);
                st.highlights.Override(p.SplitHighlights);
                st.balance.Override(p.SplitBalance);
            }
            if (prof.TryGet<Vignette>(out var vg))
                vg.intensity.Override(p.Vignette);
            if (prof.TryGet<Bloom>(out var bl))
                bl.intensity.Override(p.BloomIntensity);
            // LUT par profil : charge la LUT de l'ambiance depuis Resources/PostProcessing/ (copiées
            // par SetupGfxLutResourcesTool) et la pousse sur le ColorLookup -> chaque profil a sa vraie
            // colorimétrie (Braises chaud, Spectre froid…), pas juste le grading.
            if (prof.TryGet<ColorLookup>(out var cl) && !string.IsNullOrEmpty(p.Lut))
            {
                var lutTex = Resources.Load<Texture>("PostProcessing/" + p.Lut);
                if (lutTex != null)
                {
                    cl.texture.Override(lutTex);
                    cl.contribution.Override(p.LutContribution);
                }
            }
        }

        // Module l'intensite + teinte de la lumiere GLOBALE 2D de la scene a partir de sa valeur de
        // BASE (capturee une fois). Multiplicateur/teinte -> on preserve le reglage de la scene.
        private void ApplyProfileToGlobalLight(GfxProfile p)
        {
            if (!_globalLightCaptured)
            {
                _globalLight = FindGlobalLight2D();
                if (_globalLight != null)
                {
                    _globalLightBaseIntensity = _globalLight.intensity;
                    _globalLightBaseColor = _globalLight.color;
                    _globalLightCaptured = true;
                }
            }
            if (_globalLight == null) return;
            _globalLight.intensity = _globalLightBaseIntensity * Mathf.Max(0f, p.LightIntensityMul);
            _globalLight.color = _globalLightBaseColor * p.LightTint;
        }

        private static Volume FindFirstGlobalVolume()
        {
            var volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            Volume fallback = null;
            foreach (var v in volumes)
            {
                if (v == null || v.profile == null) continue;
                if (v.isGlobal) return v;
                fallback = v;
            }
            return fallback;
        }

        private static Light2D FindGlobalLight2D()
        {
            var lights = FindObjectsByType<Light2D>(FindObjectsSortMode.None);
            foreach (var l in lights)
                if (l != null && l.lightType == Light2D.LightType.Global) return l;
            return null;
        }
    }
}
