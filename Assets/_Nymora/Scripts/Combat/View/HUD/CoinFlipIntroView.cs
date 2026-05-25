using System.Collections;
using Nymora.Core.Data;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// PATCH 22 mai (test designer) — Intro "PILE OU FACE" en combat CASUAL.
    ///
    /// Le sim tire DEJA l'initiative de facon deterministe (TurnSystem.OnInit :
    /// f.RNG->Next(0,2) -> CombatState.ActivePlayerIndex). Cette vue ne fait que REVELER
    /// ce resultat de maniere fun : une piece tourne ~2s puis s'arrete sur la face du
    /// gagnant, suivie d'un bandeau "[Pseudo] commence !".
    ///
    /// Caracteristiques :
    ///   - PvP HUMAIN uniquement (casual + ranked) : auto-instancie via [RuntimeInitializeOnLoadMethod]
    ///     seulement si la scene active contient "Casual" ou "Ranked" (donc pas en IA / hub / menu).
    ///   - Zero impact simulation (lecture seule de ActivePlayerIndex) -> pas de bump
    ///     CombatRulesVersion.
    ///   - Bloque l'input local pendant l'anim via le flag statique IsIntroActive (lu par
    ///     CombatInputController) + overlay plein ecran qui bloque les clics UI.
    ///   - 100% procedural (aucun SerializeField possible vu l'auto-instanciation). Le visuel
    ///     de la piece est un PLACEHOLDER : on pourra brancher un vrai sprite/anim designer
    ///     plus tard (cf feedback designer-fix-over-code-workaround).
    ///
    /// Pattern miroir de CombatantTooltipView (auto-create, pur code).
    /// </summary>
    public class CoinFlipIntroView : MonoBehaviour
    {
        /// <summary>True tant que l'animation d'intro joue (lu par CombatInputController
        /// pour bloquer l'input grille local). Static : un seul combat a la fois.</summary>
        public static bool IsIntroActive { get; private set; }

        [Tooltip("Optionnel : police Ari (DA hub), câblée par 'Restyle Combat HUD' sur l'instance de scène. Défaut TMP sinon.")]
        [SerializeField] private TMP_FontAsset _font;

        // --- Timings (secondes, temps non-scale). Total cale sur TurnConstants.IntroDelaySeconds
        //     (= 3s) cote sim : spin 1.5 + pop 0.15 + hold 0.9 + fade 0.35 = ~2.9s <= 3s. Ainsi
        //     l'overlay disparait au moment ou le sim demarre le tour 1 (timer 15s frais). ---
        private const float SpinDuration = 1.5f;     // duree du spin de la piece
        private const float ResultHold = 0.9f;       // duree d'affichage du bandeau resultat
        private const float FadeOutDuration = 0.35f; // fondu de sortie de l'overlay

        // --- Style ---
        // Mapping ABSOLU (identique sur les 2 clients) : PILE = Joueur 0, FACE = Joueur 1.
        // Indispensable pour que les deux joueurs voient la MEME piece tomber sur la MEME face
        // (le "toi vs adversaire" relatif faisait voir des faces opposees sur chaque ecran).
        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.62f);
        private static readonly Color CoinColorPile = new Color(0.45f, 0.78f, 1.00f, 1f);  // PILE = Joueur 0 (bleu)
        private static readonly Color CoinColorFace = new Color(1.00f, 0.55f, 0.45f, 1f);  // FACE = Joueur 1 (orange)
        private const string ColorHexPile = "#73c7ff";
        private const string ColorHexFace = "#ff8c73";
        private static readonly Color CoinRimColor = new Color(1f, 0.86f, 0.35f, 1f);       // liseré or

        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _coinRect;
        private Image _coinImage;
        private TextMeshProUGUI _coinLabel;
        private TextMeshProUGUI _resultText;

        private bool _winnerIsLocal;
        private int _starter = -1; // slot GLOBAL du joueur qui commence (deterministe)
        private string _localPseudo;
        private string _opponentPseudo;

        // Instance courante (une seule a la fois). Evite les doublons si sceneLoaded refire.
        private static CoinFlipIntroView _current;
        private bool _played;

        // IMPORTANT : [RuntimeInitializeOnLoadMethod] ne s'execute QU'UNE FOIS au lancement de
        // l'app, pas a chaque chargement de scene. Comme on entre en casual via LoadScene depuis
        // le hub, on enregistre ici un listener sceneLoaded (persistant) qui cree l'overlay a
        // CHAQUE chargement d'une scene casual. + on couvre le cas "Play direct dans la scene".
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            IsIntroActive = false;
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;
            TryCreateForScene(SceneManager.GetActiveScene());
        }

        private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreateForScene(scene);
        }

        private static void TryCreateForScene(Scene scene)
        {
            // PvP humain uniquement : scenes Casual (33_CombatCasual) et Ranked (40_CombatRanked1v1).
            // Pas en IA / hub / menu. Le belt-and-suspenders OnGameStarted verifie en plus la
            // presence de CombatBootstrapCasual.Instance (present dans les 2 scenes PvP).
            bool isPvpScene = scene.IsValid() && (scene.name.Contains("Casual") || scene.name.Contains("Ranked"));
            if (!isPvpScene) return;
            if (_current != null) return; // deja cree pour cette scene
            var go = new GameObject("CoinFlipIntroView");
            _current = go.AddComponent<CoinFlipIntroView>();
        }

        private void Awake()
        {
            _current = this;
            // Cree au chargement de la scene casual (bien avant le demarrage Quantum) -> le
            // CallbackGameStarted sera capte normalement quand la session demarre.
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
        }

        private void OnDestroy()
        {
            if (_current == this) _current = null;
            IsIntroActive = false;
        }

        private void OnGameStarted(QuantumGame game)
        {
            if (_played) return; // anti double-trigger (Awake safety + CallbackGameStarted)

            // Belt-and-suspenders : ne joue qu'en casual reel (bootstrap casual present).
            if (Nymora.Combat.Bootstrap.CombatBootstrapCasual.Instance == null) { Destroy(gameObject); return; }

            var frame = game.Frames.Verified;
            if (frame == null || !frame.TryGetSingleton<CombatState>(out var state)) { Destroy(gameObject); return; }

            _played = true;
            // ActivePlayerIndex (slot GLOBAL du starter) est deterministe et stable des l'OnInit.
            // ATTENTION : on ne resout PAS le slot LOCAL ici — en PvP, CombatBootstrapCasual
            // .LocalPlayerSlot n'est pas encore resolu au GameStarted (AddPlayer reseau async),
            // donc LocalPlayerResolver.Resolve() retomberait sur le fallback 0 -> mauvais
            // "toi/adversaire". On resout le slot local plus tard, au moment de la revelation
            // (apres le spin), quand il est garanti pret (cf PlayIntro).
            _starter = state.ActivePlayerIndex;

            _localPseudo = string.IsNullOrEmpty(PlayerProfileBridge.LocalPseudo) ? "Toi" : PlayerProfileBridge.LocalPseudo;
            _opponentPseudo = string.IsNullOrEmpty(PlayerProfileBridge.OpponentPseudo) ? "Adversaire" : PlayerProfileBridge.OpponentPseudo;

            BuildOverlay();
            StartCoroutine(PlayIntro());
        }

        private void BuildOverlay()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000; // au-dessus de tout le HUD combat
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>(); // intercepte les clics UI pendant l'intro

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 1f;

            // Fond plein ecran (raycastTarget=true -> bloque les clics sur le HUD dessous).
            var backdrop = NewUIChild("Backdrop", _canvas.transform);
            var backdropImg = backdrop.gameObject.AddComponent<Image>();
            backdropImg.color = BackdropColor;
            backdropImg.raycastTarget = true;
            Stretch(backdrop);

            // Piece (placeholder : disque procedural).
            var coin = NewUIChild("Coin", _canvas.transform);
            _coinRect = coin;
            _coinRect.anchorMin = _coinRect.anchorMax = new Vector2(0.5f, 0.5f);
            _coinRect.anchoredPosition = new Vector2(0f, 60f);
            _coinRect.sizeDelta = new Vector2(260f, 260f);
            _coinImage = coin.gameObject.AddComponent<Image>();
            _coinImage.sprite = CreateCircleSprite();
            _coinImage.color = CoinColorPile;
            _coinImage.raycastTarget = false;

            // Liseré or (image circulaire derriere, legerement plus grande).
            var rim = NewUIChild("Rim", _coinRect);
            rim.anchorMin = Vector2.zero; rim.anchorMax = Vector2.one;
            rim.offsetMin = new Vector2(-10f, -10f); rim.offsetMax = new Vector2(10f, 10f);
            var rimImg = rim.gameObject.AddComponent<Image>();
            rimImg.sprite = _coinImage.sprite;
            rimImg.color = CoinRimColor;
            rimImg.raycastTarget = false;
            rim.SetAsFirstSibling(); // derriere le disque principal

            // Label sur la piece (PILE / FACE flair pendant le spin).
            var labelGo = NewUIChild("CoinLabel", _coinRect);
            Stretch(labelGo);
            _coinLabel = labelGo.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null) _coinLabel.font = _font;
            _coinLabel.fontSize = 64f;
            _coinLabel.alignment = TextAlignmentOptions.Center;
            _coinLabel.fontStyle = FontStyles.Bold;
            _coinLabel.color = new Color(0.1f, 0.1f, 0.12f, 1f);
            _coinLabel.raycastTarget = false;
            _coinLabel.text = "PILE";

            // Bandeau resultat (cache au depart).
            var result = NewUIChild("ResultText", _canvas.transform);
            result.anchorMin = result.anchorMax = new Vector2(0.5f, 0.5f);
            result.anchoredPosition = new Vector2(0f, -150f);
            result.sizeDelta = new Vector2(1400f, 200f);
            _resultText = result.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null) _resultText.font = _font;
            _resultText.fontSize = 84f;
            _resultText.alignment = TextAlignmentOptions.Center;
            _resultText.fontStyle = FontStyles.Bold;
            _resultText.color = Color.white;
            _resultText.raycastTarget = false;
            _resultText.text = "";
        }

        private IEnumerator PlayIntro()
        {
            IsIntroActive = true;
            // A2 — SFX du lancer de pièce.
            Nymora.Core.Audio.NymoraAudioManager.Instance?.PlaySfx(Nymora.Core.Audio.SoundId.CoinFlip);

            // --- Phase 1 : spin de la piece (decelere) ---
            float t = 0f;
            float angle = 0f;
            const float initialSpeed = 22f; // rad/s
            while (t < SpinDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / SpinDuration);
                float speed = Mathf.Lerp(initialSpeed, 1.5f, k); // ralentit
                angle += speed * Time.unscaledDeltaTime;

                float c = Mathf.Cos(angle);
                // Squash vertical pour simuler la rotation de la piece sur la tranche.
                float sy = Mathf.Max(0.08f, Mathf.Abs(c));
                _coinRect.localScale = new Vector3(1f, sy, 1f);

                // Face visible selon le signe : bleu/PILE (local) ou orange/FACE (adversaire).
                SetCoinFace(c >= 0f);
                yield return null;
            }

            // --- Resolution du slot LOCAL au moment de la revelation ---
            // En PvP, CombatBootstrapCasual.LocalPlayerSlot peut n'etre resolu qu'apres le
            // GameStarted (AddPlayer reseau async). On attend ici qu'il soit pret (timeout 1s)
            // avant de decider qui est "toi". Le spin (1.5s) laisse largement le temps.
            float waited = 0f;
            var bootstrap = Nymora.Combat.Bootstrap.CombatBootstrapCasual.Instance;
            while (bootstrap != null && bootstrap.LocalPlayerSlot < 0 && waited < 1.0f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            int localSlot = LocalPlayerResolver.Resolve();
            _winnerIsLocal = _starter == localSlot;

            // --- Atterrissage sur la face ABSOLUE du starter (PILE=P0, FACE=P1) ---
            // Identique sur les 2 clients (le starter est un slot global deterministe).
            bool starterIsPile = _starter == 0;
            SetCoinFace(starterIsPile);
            _coinRect.localScale = Vector3.one;
            // Petit "pop" d'atterrissage.
            yield return StartCoroutine(CoinPop());

            // --- Phase 2 : bandeau resultat ---
            // La piece est identique partout ; seul le bandeau est "personnalise" : chaque client
            // affiche le pseudo correct du starter + un tag "(toi)" si c'est le joueur local.
            string winnerPseudo = _winnerIsLocal ? _localPseudo : _opponentPseudo;
            string colorHex = starterIsPile ? ColorHexPile : ColorHexFace;
            string youTag = _winnerIsLocal ? "  <size=60%>(toi)</size>" : "";
            _resultText.text = $"<color={colorHex}>{winnerPseudo}</color> commence !{youTag}";
            yield return new WaitForSecondsRealtime(ResultHold);

            // --- Phase 3 : fondu de sortie ---
            float f = 0f;
            while (f < FadeOutDuration)
            {
                f += Time.unscaledDeltaTime;
                _group.alpha = 1f - Mathf.Clamp01(f / FadeOutDuration);
                yield return null;
            }

            IsIntroActive = false;
            Destroy(gameObject);
        }

        private IEnumerator CoinPop()
        {
            float d = 0.18f;
            float t = 0f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                float k = t / d;
                // 1 -> 1.18 -> 1 (overshoot leger).
                float s = 1f + 0.18f * Mathf.Sin(k * Mathf.PI);
                _coinRect.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            _coinRect.localScale = Vector3.one;
        }

        // isPile == true -> face PILE (Joueur 0, bleu) ; false -> FACE (Joueur 1, orange).
        // Mapping absolu : meme rendu sur les 2 clients.
        private void SetCoinFace(bool isPile)
        {
            if (_coinImage != null)
                _coinImage.color = isPile ? CoinColorPile : CoinColorFace;
            if (_coinLabel != null)
                _coinLabel.text = isPile ? "PILE" : "FACE";
        }

        // ================================================================
        // Helpers UI procedural
        // ================================================================

        private static RectTransform NewUIChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Disque blanc plein (anti-aliasing simple) — placeholder de la piece.
        private static Sprite _circleSpriteCache;
        private static Sprite CreateCircleSprite()
        {
            if (_circleSpriteCache != null) return _circleSpriteCache;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    // Bord adouci sur ~1.5px.
                    float a = Mathf.Clamp01((r - dist) / 1.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _circleSpriteCache = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _circleSpriteCache;
        }
    }
}
