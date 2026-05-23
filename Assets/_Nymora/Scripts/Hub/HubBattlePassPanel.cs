using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 5.7.b (refonte Brawl Stars) — Battle Pass horizontal : lecture gauche→droite,
    /// swipe (ScrollRect) + flèches, paliers en cases (gratuit en haut / premium en bas).
    ///
    /// Clone un template de colonne (BattlePassTierColumn) une fois par palier dans le contenu
    /// du scroll horizontal. Claim par case (clic). Premium VERROUILLÉ (non achetable).
    /// Singleton. Cable via "Nymora > Setup > Patch Battle Pass Panel".
    /// </summary>
    public sealed class HubBattlePassPanel : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _headerText;

        [Header("Scroll horizontal")]
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _content;
        [SerializeField] private BattlePassTierColumn _columnTemplate;
        [SerializeField] private Button _prevButton;
        [SerializeField] private Button _nextButton;

        [Header("Backend")]
        [SerializeField] private NymoraBackendSettings _backendSettings;

        public static HubBattlePassPanel Instance { get; private set; }
        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

        private NymoraApiClient _api;
        private bool _busy;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (_backendSettings != null) _api = new NymoraApiClient(_backendSettings);
            if (_columnTemplate != null) _columnTemplate.gameObject.SetActive(false);
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
            if (_prevButton != null) _prevButton.onClick.AddListener(() => Nudge(-0.12f));
            if (_nextButton != null) _nextButton.onClick.AddListener(() => Nudge(+0.12f));
        }

        private void OnDisable()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveAllListeners();
            if (_prevButton != null) _prevButton.onClick.RemoveAllListeners();
            if (_nextButton != null) _nextButton.onClick.RemoveAllListeners();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Open()
        {
            if (_panelRoot != null) _panelRoot.SetActive(true);
            SetHeader("Chargement du Battle Pass...");
            FetchAsync().Forget();
        }

        public void Close() { if (_panelRoot != null) _panelRoot.SetActive(false); }

        private void Nudge(float delta)
        {
            if (_scrollRect == null) return;
            _scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(_scrollRect.horizontalNormalizedPosition + delta);
        }

        private string Token() => HubChatClient.Instance?.DevToken;

        private async UniTaskVoid FetchAsync()
        {
            if (_api == null) { SetHeader("Backend non configuré."); return; }
            string token = Token();
            if (string.IsNullOrEmpty(token)) { SetHeader("Non connecté."); return; }
            _api.SetBearerToken(token);
            var res = await _api.GetBattlePassAsync();
            if (!res.IsSuccess) { SetHeader($"Erreur Battle Pass ({res.StatusCode})."); return; }
            Render(res.Data);
        }

        private void Render(BattlePassMeResponse d)
        {
            int xpInTier = d.xpPerTier > 0 ? d.xp % d.xpPerTier : 0;
            int toNext = d.tier >= d.maxTier ? 0 : d.xpPerTier - xpInTier;
            string premiumTag = d.hasPremium ? "" : "   <color=#ff9d2e>● Premium verrouillé</color>";
            SetHeader($"<b>Battle Pass — Saison {d.seasonNumber}</b>     Palier <b>{d.tier}</b>/{d.maxTier}     " +
                      $"{(d.tier >= d.maxTier ? "MAX" : $"{toNext} XP → palier suivant")}{premiumTag}");

            // Nettoie les colonnes précédentes.
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();
            if (_columnTemplate == null || _content == null || d.tiers == null) return;

            foreach (var t in d.tiers)
            {
                var col = Instantiate(_columnTemplate.gameObject, _content);
                col.SetActive(true);
                _spawned.Add(col);
                var comp = col.GetComponent<BattlePassTierColumn>();
                comp.Bind(
                    t.tier, t.tier == d.tier,
                    LabelOf(t.free), StateOf(t.free, t.tier, d.tier, Contains(d.claimedFree, t.tier), isPremium: false, d.hasPremium),
                    LabelOf(t.premium), StateOf(t.premium, t.tier, d.tier, Contains(d.claimedPremium, t.tier), isPremium: true, d.hasPremium));
                comp.OnClaim -= HandleClaim;
                comp.OnClaim += HandleClaim;
            }

            // Auto-scroll sur le palier courant après le layout.
            StartCoroutine(ScrollToTier(d.tier, d.maxTier));
        }

        private IEnumerator ScrollToTier(int tier, int maxTier)
        {
            yield return null; // laisse le HorizontalLayoutGroup se calculer
            if (_scrollRect == null || maxTier <= 1) yield break;
            _scrollRect.horizontalNormalizedPosition = Mathf.Clamp01((tier - 1f) / (maxTier - 1f));
        }

        private async void HandleClaim(int tier, string track)
        {
            if (_busy || _api == null) return;
            string token = Token();
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);
            _busy = true;
            var res = await _api.ClaimBattlePassTierAsync(tier, track);
            _busy = false;
            if (res.IsSuccess && res.Data.status == "claimed")
            {
                Nymora.Core.Audio.NymoraAudioManager.Instance?.PlaySfx(Nymora.Core.Audio.SoundId.RewardClaim, Nymora.Core.Audio.NymoraAudioManager.SoftUiVolume);
                Debug.Log($"[BattlePass] Réclamé palier {tier} ({track}) : {res.Data.rewardLabel}");
            }
            else
            {
                Debug.LogWarning($"[BattlePass] Claim refusé palier {tier}/{track} : {res.Data?.error ?? res.ErrorMessage}");
            }
            FetchAsync().Forget(); // refresh (WALLET_UPDATE WS met à jour le widget Nymos)
        }

        // ----- Helpers état -----

        private static bool IsReal(BpRewardDto r) => r != null && !string.IsNullOrEmpty(r.kind);
        private static string LabelOf(BpRewardDto r) => IsReal(r) ? r.label : "";

        private static BattlePassTierColumn.CellState StateOf(
            BpRewardDto r, int tier, int currentTier, bool claimed, bool isPremium, bool hasPremium)
        {
            if (!IsReal(r)) return BattlePassTierColumn.CellState.Empty;
            if (claimed) return BattlePassTierColumn.CellState.Claimed;
            if (isPremium && !hasPremium) return BattlePassTierColumn.CellState.PremiumLocked;
            if (tier <= currentTier) return BattlePassTierColumn.CellState.Claimable;
            return BattlePassTierColumn.CellState.Locked;
        }

        private static bool Contains(int[] arr, int v)
        {
            return arr != null && System.Array.IndexOf(arr, v) >= 0;
        }

        private void SetHeader(string s) { if (_headerText != null) _headerText.text = s; }
    }
}
