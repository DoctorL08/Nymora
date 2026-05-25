using System.Collections.Generic;
using UnityEngine;
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
            ApplyPostFx(); // caméras de la scène courante
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Les caméras changent à chaque scène : on ré-applique le toggle post-process.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyPostFx();

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

        // ===== Effets post-process (shaders) =====

        public bool PostFx
        {
            get => PlayerPrefs.GetInt(KeyPostFx, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(KeyPostFx, value ? 1 : 0);
                PlayerPrefs.Save();
                ApplyPostFx();
            }
        }

        private void ApplyPostFx()
        {
            bool on = PostFx;
            foreach (var cam in Camera.allCameras)
            {
                if (cam == null) continue;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = on;
            }
        }
    }
}
