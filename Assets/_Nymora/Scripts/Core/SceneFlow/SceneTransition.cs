using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nymora.Core.SceneFlow
{
    /// <summary>
    /// Brique T1 — Routeur de transition de scène avec fondu doux (crossfade).
    ///
    /// Remplace les <c>SceneManager.LoadScene(...)</c> sèches (écran noir + pop-in) de tout
    /// le jeu par : fondu sortant vers un voile sombre → chargement async → fondu entrant.
    ///
    /// Singleton persistant : auto-bootstrappé AVANT la 1re scène
    /// ([RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]) + DontDestroyOnLoad → l'overlay
    /// survit aux changements de scène (le fondu entrant se joue dans la scène d'arrivée).
    /// AUCUN setup dans les scènes : le Canvas + l'Image du voile sont créés en code.
    ///
    /// 100% View/UI. Aucune dépendance simulation Quantum (rien à versionner).
    ///
    /// API :
    ///   SceneTransition.Load(scene)                       — fondu + load (fire-and-forget)
    ///   SceneTransition.Load(scene, whileCovered)         — idem + action SYNC sous le voile
    ///                                                        (ex: QuantumRunner.ShutdownAll())
    ///   await SceneTransition.LoadAsync(scene, hook)      — hook ASYNC sous le voile
    ///                                                        (ex: await runner.Shutdown() Fusion)
    ///
    /// Le hook "whileCovered" tourne APRÈS le fondu sortant (écran déjà opaque) et AVANT le
    /// LoadSceneAsync → le shutdown réseau ne se voit jamais à l'écran.
    /// </summary>
    public sealed class SceneTransition : MonoBehaviour
    {
        public static SceneTransition Instance { get; private set; }

        /// <summary>True tant qu'une transition est en cours (fondu/chargement).</summary>
        public static bool IsTransitioning => Instance != null && Instance._busy;

        // --- Tunables (aucune valeur magique : tout en const) ---
        private const float FadeOutDuration = 0.30f;
        private const float FadeInDuration = 0.30f;
        // Au sommet de tous les Canvas (Screen Space Overlay) pour masquer HUD/UI inclus.
        private const int OverlaySortingOrder = 32760;
        // Voile noir neutre (canaux égaux -> aucune teinte bleue).
        private static readonly Color VeilColor = new Color(0.03f, 0.03f, 0.03f, 1f);
        // Spinner : taille à l'écran + vitesse de rotation (degrés/seconde, sens horaire).
        private const float SpinnerSizePx = 90f;
        private const float SpinnerSpeedDegPerSec = 240f;
        // T2 — quand waitForReady : on garde le voile jusqu'au signal "combat prêt"
        // (CallbackGameStarted via CombatReadyBeacon), avec ce timeout de sécurité pour ne
        // JAMAIS rester bloqué si le signal n'arrive pas (échec de connexion p.ex.).
        private const float ReadyTimeoutSeconds = 12f;
        // Petit délai après le signal pour laisser les vues (combattants, grille) se dessiner
        // avant de révéler -> pas de pop-in d'unités au fondu entrant.
        private const float PostReadySettleSeconds = 0.20f;

        private CanvasGroup _group;
        private RectTransform _spinner;
        private bool _busy;
        private bool _awaitingReady;
        private bool _readySignaled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            if (Instance != null) return;
            var go = new GameObject("[SceneTransition]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<SceneTransition>();
            Instance.BuildOverlay();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (_group == null) BuildOverlay();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildOverlay()
        {
            // Canvas plein écran, au-dessus de tout.
            var canvasGo = new GameObject("Veil Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;
            canvasGo.AddComponent<GraphicRaycaster>(); // bloque les clics pendant le fondu

            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // Image plein écran (voile sombre uni) — base derrière le background (sert de fond si
            // l'image ne couvre pas tout l'écran et pendant le fondu).
            var imgGo = new GameObject("Veil");
            imgGo.transform.SetParent(canvasGo.transform, false);
            var img = imgGo.AddComponent<Image>();
            img.color = VeilColor;
            img.raycastTarget = true;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Background Nymora (celui de la page de login) -> l'écran de chargement fait moins vide.
            // Chargé depuis Resources (SceneTransition est 100% code/persistant). Absent -> voile uni.
            var bgSprite = Resources.Load<Sprite>("UI/Backgrounds/login_background");
            if (bgSprite != null)
            {
                var bgGo = new GameObject("Background");
                bgGo.transform.SetParent(canvasGo.transform, false);
                var bg = bgGo.AddComponent<Image>();
                bg.sprite = bgSprite;
                bg.raycastTarget = false;
                bg.preserveAspect = false; // plein cadre (fond) ; le voile sombre couvre les bords si besoin
                var brt = bg.rectTransform;
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
            }

            // Spinner centré : anneau de points avec traîne, tourné en Update tant que _busy.
            var spinGo = new GameObject("Spinner");
            spinGo.transform.SetParent(canvasGo.transform, false);
            var spinImg = spinGo.AddComponent<Image>();
            spinImg.sprite = CreateSpinnerSprite();
            spinImg.raycastTarget = false;
            _spinner = spinImg.rectTransform;
            _spinner.anchorMin = new Vector2(0.5f, 0.5f);
            _spinner.anchorMax = new Vector2(0.5f, 0.5f);
            _spinner.pivot = new Vector2(0.5f, 0.5f);
            _spinner.anchoredPosition = Vector2.zero;
            _spinner.sizeDelta = new Vector2(SpinnerSizePx, SpinnerSizePx);
        }

        private void Update()
        {
            // Rotation horaire continue tant qu'une transition est active (voile visible).
            if (_busy && _spinner != null)
            {
                _spinner.Rotate(0f, 0f, -SpinnerSpeedDegPerSec * Time.unscaledDeltaTime);
            }
        }

        /// <summary>
        /// Génère en code une roue de chargement : 12 points blancs disposés en cercle avec
        /// une traîne d'alpha croissante (effet "comète"). Tournée en Update -> spinner classique.
        /// </summary>
        private static Sprite CreateSpinnerSprite()
        {
            const int Size = 64;
            const int DotCount = 12;
            const float RingRadius = 24f;
            const float DotRadius = 5f;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var px = new Color32[Size * Size]; // initialisé transparent (0,0,0,0)
            var center = new Vector2(Size / 2f, Size / 2f);

            for (int d = 0; d < DotCount; d++)
            {
                float ang = (d / (float)DotCount) * Mathf.PI * 2f;
                var dot = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * RingRadius;
                byte alpha = (byte)Mathf.RoundToInt(255f * (d + 1) / DotCount); // traîne 0->1

                int minX = Mathf.Max(0, Mathf.FloorToInt(dot.x - DotRadius));
                int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(dot.x + DotRadius));
                int minY = Mathf.Max(0, Mathf.FloorToInt(dot.y - DotRadius));
                int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(dot.y + DotRadius));
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), dot);
                        if (dist > DotRadius) continue;
                        // Bord légèrement adouci sur le dernier pixel.
                        float edge = Mathf.Clamp01(DotRadius - dist);
                        byte a = (byte)Mathf.RoundToInt(alpha * edge);
                        int idx = y * Size + x;
                        if (a > px[idx].a) px[idx] = new Color32(255, 255, 255, a);
                    }
                }
            }

            tex.SetPixels32(px);
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        // --- API publique ---

        /// <summary>Fondu + chargement de scène (fire-and-forget).</summary>
        /// <param name="waitForReady">Si true (transition vers le combat), garde le voile + le
        /// spinner après le load jusqu'à <see cref="SignalReady"/> (combat connecté/sim prête),
        /// avec un timeout de sécurité.</param>
        public static void Load(string sceneName, Action whileCovered = null, bool waitForReady = false)
        {
            EnsureInstance();
            Instance.Begin(sceneName, null, whileCovered, null, waitForReady);
        }

        /// <summary>
        /// Variante awaitable : <paramref name="whileCovered"/> est un travail ASYNC exécuté
        /// sous le voile opaque avant le load (ex: <c>await runner.Shutdown()</c> Fusion).
        /// La Task se complète après le fondu entrant. Si une transition est déjà en cours,
        /// la Task se complète immédiatement avec <c>false</c>.
        /// </summary>
        public static Task<bool> LoadAsync(string sceneName, Func<Task> whileCovered = null,
                                           bool waitForReady = false)
        {
            EnsureInstance();
            var tcs = new TaskCompletionSource<bool>();
            Instance.Begin(sceneName, whileCovered, null, tcs, waitForReady);
            return tcs.Task;
        }

        /// <summary>
        /// Signale que la scène d'arrivée est "prête" (sim Quantum démarrée). Débloque le voile
        /// d'une transition lancée avec <c>waitForReady</c>. No-op sinon. Appelé par
        /// CombatReadyBeacon (assembly Combat) sur CallbackGameStarted.
        /// </summary>
        public static void SignalReady()
        {
            if (Instance != null && Instance._awaitingReady) Instance._readySignaled = true;
        }

        private void Begin(string sceneName, Func<Task> asyncHook, Action syncHook,
                           TaskCompletionSource<bool> tcs, bool waitForReady)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneTransition] sceneName vide — transition ignorée.");
                tcs?.TrySetResult(false);
                return;
            }
            if (_busy)
            {
                Debug.LogWarning($"[SceneTransition] Transition déjà en cours — '{sceneName}' ignorée.");
                tcs?.TrySetResult(false);
                return;
            }
            StartCoroutine(RunTransition(sceneName, asyncHook, syncHook, tcs, waitForReady));
        }

        private IEnumerator RunTransition(string sceneName, Func<Task> asyncHook, Action syncHook,
                                          TaskCompletionSource<bool> tcs, bool waitForReady)
        {
            _busy = true;
            _group.blocksRaycasts = true;

            // 1) Fondu sortant -> voile opaque.
            yield return Fade(0f, 1f, FadeOutDuration);

            // 2) Travail "sous le voile" (shutdown réseau, reset state...) avant le load.
            if (asyncHook != null)
            {
                Task hookTask = null;
                try { hookTask = asyncHook(); }
                catch (Exception ex) { Debug.LogWarning($"[SceneTransition] whileCovered (async) a throw au lancement : {ex.Message}"); }
                while (hookTask != null && !hookTask.IsCompleted) yield return null;
                if (hookTask != null && hookTask.IsFaulted)
                    Debug.LogWarning($"[SceneTransition] whileCovered (async) a échoué : {hookTask.Exception?.GetBaseException().Message} — on continue le load.");
            }
            else if (syncHook != null)
            {
                try { syncHook(); }
                catch (Exception ex) { Debug.LogWarning($"[SceneTransition] whileCovered (sync) a throw : {ex.Message} — on continue le load."); }
            }

            // Arme l'attente AVANT le load : en IA offline, la sim peut démarrer (et émettre
            // CallbackGameStarted) dès le 1er frame de la scène chargée, donc avant qu'on
            // n'atteigne le bloc d'attente. On latche ici pour ne jamais rater le signal.
            if (waitForReady)
            {
                _readySignaled = false;
                _awaitingReady = true;
            }

            // 3) Chargement async (écran masqué -> aucun pop-in visible).
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"[SceneTransition] LoadSceneAsync('{sceneName}') a renvoyé null — scène absente du Build Settings ? On dé-fade pour ne pas bloquer l'écran.");
            }
            else
            {
                while (!op.isDone) yield return null;
                // Laisse une frame aux Awake/Start de la nouvelle scène avant de révéler.
                yield return null;
            }

            // 3bis) T2 — pour le combat : garde le voile (spinner) jusqu'au signal "sim prête",
            // pour ne pas révéler une scène combat vide pendant la connexion Photon/Quantum.
            if (waitForReady)
            {
                float waited = 0f;
                while (!_readySignaled && waited < ReadyTimeoutSeconds)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                _awaitingReady = false;
                if (!_readySignaled)
                    Debug.LogWarning($"[SceneTransition] Signal 'prêt' jamais reçu pour '{sceneName}' (timeout {ReadyTimeoutSeconds}s) — révélation forcée.");
                else
                    yield return new WaitForSecondsRealtime(PostReadySettleSeconds);
            }

            // 4) Fondu entrant -> révèle la scène d'arrivée.
            yield return Fade(1f, 0f, FadeInDuration);

            _group.blocksRaycasts = false;
            _busy = false;
            tcs?.TrySetResult(true);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (duration <= 0f) { _group.alpha = to; yield break; }
            _group.alpha = from;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // indépendant d'un éventuel Time.timeScale
                _group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            _group.alpha = to;
        }
    }
}
