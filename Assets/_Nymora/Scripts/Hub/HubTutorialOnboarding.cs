using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using Nymora.Core.Data;
using Nymora.Core.SceneFlow;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Tutoriel T6/T7 — Aiguillage onboarding du nouveau joueur dans le hub.
    ///
    /// Au chargement de 10_CommunityHub :
    ///   - Retour du tuto (<see cref="TutorialContext.CompletedThisSession"/>) → POST
    ///     /profile/tutorial-complete (marque l'onboarding résolu), puis stop.
    ///   - Sinon GET /profile/me ; si <c>tutorialCompleted == false</c> → pop-up
    ///     « Bienvenue sur Nymora ! Veux-tu suivre le tutoriel ? [Oui] / [Plus tard] ».
    ///       • Oui      → lance 30_CombatIA en mode tuto (<see cref="LaunchTutorial"/>).
    ///       • Plus tard→ POST /profile/tutorial-complete (plus de relance au prochain login).
    ///
    /// T7 : le bouton « Rejouer le tutoriel » des Paramètres appelle <see cref="LaunchTutorial"/>.
    ///
    /// Archi : <c>TutorialDirector</c> (asmdef Combat) ne référence pas le réseau → il pose juste le
    /// flag Core en fin de tuto, c'est CE composant (asmdef Hub→Network) qui fait le POST au retour.
    /// 100% View/Network — aucun impact simulation (pas de bump CombatRulesVersion).
    ///
    /// MANIP UNITY : ajouter ce composant à un GameObject de 10_CommunityHub et glisser l'asset
    /// NymoraBackendSettings dans _backendSettings (même pattern que HubProfilePanel).
    /// </summary>
    public sealed class HubTutorialOnboarding : MonoBehaviour
    {
        [SerializeField] private NymoraBackendSettings _backendSettings;

        private const string TutorialIaScene = "30_CombatIA";

        private NymoraApiClient _api;
        private GameObject _popup;

        private void Start()
        {
            if (_backendSettings == null)
            {
                Debug.LogError("[TutorialOnboarding] _backendSettings non assigné — onboarding désactivé.");
                enabled = false;
                return;
            }
            _api = new NymoraApiClient(_backendSettings);
            RunFlowAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void OnDestroy()
        {
            if (_popup != null) Destroy(_popup);
        }

        // ===== Flux d'aiguillage =====

        private async UniTaskVoid RunFlowAsync(CancellationToken ct)
        {
            // Attend le JWT (HubChatClient connecté). Échec silencieux si jamais dispo (pas bloquant).
            string token = await WaitForTokenAsync(ct);
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);

            // Cas 1 — on revient de finir le tuto de COMBAT : on enchaîne sur le petit tuto HUB
            // (Brique B), puis on marque l'onboarding résolu côté backend à la fin de CELUI-CI.
            if (TutorialContext.CompletedThisSession)
            {
                TutorialContext.CompletedThisSession = false;
                HubTutorialDirector.EnsureSpawned(MarkCompleteFireAndForget);
                return;
            }

            // Cas 2 — nouveau joueur : propose le tuto si l'onboarding n'est pas encore résolu.
            var res = await _api.GetProfileMeAsync(ct);
            if (!res.IsSuccess || res.Data == null) return; // backend KO → on ne dérange pas le joueur
            if (res.Data.tutorialCompleted) return;         // déjà fait ou décliné

            ShowProposalPopup();
        }

        private static async UniTask<string> WaitForTokenAsync(CancellationToken ct)
        {
            const float timeoutSeconds = 8f;
            float waited = 0f;
            while (waited < timeoutSeconds)
            {
                string token = HubChatClient.Instance?.DevToken;
                if (!string.IsNullOrEmpty(token)) return token;
                waited += Time.unscaledDeltaTime;
                await UniTask.Yield(ct);
            }
            return null;
        }

        private void OnAccept()
        {
            ClosePopup();
            LaunchTutorial();
        }

        private void OnDecline()
        {
            ClosePopup();
            // Décliner = onboarding résolu → plus de pop-up au prochain login (rejouable via Paramètres).
            MarkCompleteFireAndForget();
        }

        private void MarkCompleteFireAndForget()
        {
            string token = HubChatClient.Instance?.DevToken;
            if (_api == null || string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);
            _api.MarkTutorialCompleteAsync().Forget();
        }

        /// <summary>Charge 30_CombatIA en mode tutoriel (Soulrender scripté). Statique : utilisable
        /// par la pop-up (« Oui ») ET par le bouton « Rejouer le tutoriel » des Paramètres (T7).</summary>
        public static void LaunchTutorial()
        {
            TutorialContext.Active = true;
            TutorialContext.CompletedThisSession = false;
            // Shutdown du NetworkRunner Fusion sous le voile (comme HubMatchTransition) pour éviter
            // des ticks résiduels qui tenteraient de spawner des avatars dans la scène combat.
            SceneTransition.LoadAsync(TutorialIaScene, async () =>
            {
                var runner = FindFirstObjectByType<NetworkRunner>();
                if (runner != null && runner.IsRunning)
                {
                    try { await runner.Shutdown(); }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[TutorialOnboarding] Shutdown Fusion a throw : {ex.Message} — on continue le load.");
                    }
                }
            }, waitForReady: true);
        }

        // ===== Pop-up (construite en code, Screen Space Overlay) =====

        private void ShowProposalPopup()
        {
            if (_popup != null) return;

            _popup = new GameObject("[TutorialOnboardingPopup]");
            _popup.transform.SetParent(transform, false);
            var canvas = _popup.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000; // au-dessus du hub, sous le voile de SceneTransition (32760)
            _popup.AddComponent<GraphicRaycaster>();
            var scaler = _popup.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Voile sombre plein écran (bloque les clics derrière).
            var dim = NewImage(_popup.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            // Carte centrale.
            var card = NewImage(_popup.transform, "Card", new Color(0.10f, 0.10f, 0.13f, 0.99f));
            var crt = card.rectTransform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(600f, 300f);
            crt.anchoredPosition = Vector2.zero;

            var title = MakeText(card.transform, "Title", "Bienvenue sur Nymora !", 38f,
                new Color(0.96f, 0.96f, 0.98f), TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(-48f, 60f); trt.anchoredPosition = new Vector2(0f, -30f);

            var body = MakeText(card.transform, "Body",
                "Veux-tu suivre le tutoriel pour apprendre les bases\n(déplacement, sorts, fin de tour) ?",
                24f, new Color(0.80f, 0.82f, 0.88f), TextAlignmentOptions.Center);
            var brt = body.rectTransform;
            brt.anchorMin = new Vector2(0f, 0.5f); brt.anchorMax = new Vector2(1f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(-64f, 120f); brt.anchoredPosition = new Vector2(0f, 14f);

            // Boutons : « Oui » (accent) à gauche, « Plus tard » (neutre) à droite.
            var yes = MakeButton(card.transform, "Oui", new Color(0.42f, 0.34f, 0.66f),
                new Color(0.98f, 0.98f, 1f), OnAccept);
            var yrt = yes.GetComponent<RectTransform>();
            yrt.anchorMin = yrt.anchorMax = yrt.pivot = new Vector2(0.5f, 0f);
            yrt.sizeDelta = new Vector2(240f, 58f); yrt.anchoredPosition = new Vector2(-130f, 30f);

            var later = MakeButton(card.transform, "Plus tard", new Color(0.20f, 0.20f, 0.24f),
                new Color(0.85f, 0.87f, 0.92f), OnDecline);
            var lrt = later.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0f);
            lrt.sizeDelta = new Vector2(240f, 58f); lrt.anchoredPosition = new Vector2(130f, 30f);
        }

        private void ClosePopup()
        {
            if (_popup != null) Destroy(_popup);
            _popup = null;
        }

        // ===== Helpers UI (code-only) =====

        private static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string name, string text, float size,
                                                Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button MakeButton(Transform parent, string label, Color bg, Color fg,
                                         UnityEngine.Events.UnityAction onClick)
        {
            var img = NewImage(parent, "Btn_" + label, bg);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var lbl = MakeText(img.transform, "Label", label, 26f, fg, TextAlignmentOptions.Center);
            Stretch(lbl.rectTransform);
            btn.onClick.AddListener(onClick);
            return btn;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
