using Nymora.Combat.View.HUD;
using Quantum;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

namespace Nymora.Combat.View.PreCombatLobby
{
    /// <summary>
    /// Lobby pré-combat (B4) — Voile de transition entre la FERMETURE du lobby et le SPAWN des
    /// combattants. Sans lui, on verrait la grille de combat vide pendant le démarrage de la
    /// session Quantum (~1-2 s entre la destruction du lobby et CallbackGameStarted).
    ///
    /// Créé par CombatBootstrapCasual juste après la résolution du lobby (avant StartAsync), il
    /// affiche un voile sombre + « Le combat commence… » et se détruit dès que la sim démarre
    /// (CallbackGameStarted) ou, par sécurité, après un délai max. 100% View.
    /// </summary>
    public sealed class PreCombatLoadingVeil : MonoBehaviour
    {
        private const float FailsafeSeconds = 20f;
        private const float SpinDegPerSec = 220f;

        private Canvas _canvas;
        private RectTransform _spinner;
        private float _spawnTime;

        private static Sprite _spinnerSprite;

        public static void Show()
        {
            var go = new GameObject("PreCombatLoadingVeil");
            go.AddComponent<PreCombatLoadingVeil>();
        }

        private void Awake()
        {
            _spawnTime = Time.unscaledTime;
            BuildUI();
            // Se détruit dès que la simulation démarre (combattants spawnés).
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => CloseSelf());
        }

        private void Update()
        {
            // Rotation fluide du spinner.
            if (_spinner != null)
                _spinner.localEulerAngles += new Vector3(0f, 0f, -SpinDegPerSec * Time.unscaledDeltaTime);

            // Filet de sécurité : si CallbackGameStarted n'arrive jamais (erreur de connexion),
            // on ne laisse pas le voile bloqué indéfiniment.
            if (Time.unscaledTime - _spawnTime > FailsafeSeconds) CloseSelf();
        }

        private void CloseSelf()
        {
            if (this != null) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("PreCombatLoadingVeilCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 4400; // sous le lobby (4500), au-dessus du HUD
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var veil = new GameObject("Veil", typeof(RectTransform));
            veil.transform.SetParent(canvasGo.transform, false);
            var vrt = (RectTransform)veil.transform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            var vimg = veil.AddComponent<Image>();
            vimg.color = new Color(0.04f, 0.045f, 0.06f, 1f);

            // Spinner fluide (anneau « comète » tournant), au centre de l'écran.
            var spinGo = new GameObject("Spinner", typeof(RectTransform));
            spinGo.transform.SetParent(canvasGo.transform, false);
            _spinner = (RectTransform)spinGo.transform;
            _spinner.anchorMin = _spinner.anchorMax = new Vector2(0.5f, 0.5f); _spinner.pivot = new Vector2(0.5f, 0.5f);
            _spinner.anchoredPosition = Vector2.zero; _spinner.sizeDelta = new Vector2(96f, 96f);
            var simg = spinGo.AddComponent<Image>();
            simg.sprite = SpinnerSprite();
            simg.color = CombatUiKit.TextPrimary;
            simg.raycastTarget = false;
        }

        // Anneau avec dégradé d'alpha angulaire (effet comète) — rotation = spinner fluide.
        private static Sprite SpinnerSprite()
        {
            if (_spinnerSprite != null) return _spinnerSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            float rOuter = size * 0.5f - 1f;
            float rInner = size * 0.30f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 0f;
                    if (dist <= rOuter && dist >= rInner)
                    {
                        float ang = Mathf.Atan2(dy, dx);            // -π..π
                        float t = (ang + Mathf.PI) / (2f * Mathf.PI); // 0..1 autour du cercle
                        float edge = Mathf.Min(dist - rInner, rOuter - dist); // adoucit les bords du tore
                        a = t * Mathf.Clamp01(edge);
                    }
                    byte ba = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, ba);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            _spinnerSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            _spinnerSprite.name = "PreCombatSpinner";
            return _spinnerSprite;
        }
    }
}
