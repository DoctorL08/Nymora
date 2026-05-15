using System;
using Cysharp.Threading.Tasks;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.12 — Panel profil joueur 5 onglets (Vue / Stats / Classes / Succes / Cosmetiques).
    ///
    /// Onglet Vue : alimente depuis GET /profile/me via NymoraApiClient.
    /// 4 autres onglets : placeholder "Coming soon" cable en dur dans la scene (Editor Tool 4.12.e),
    /// le panel se contente de switch leur visibilite. Pas de fake data, les systemes amont
    /// (tracking match, succes, cosmetiques) n'existent pas encore.
    ///
    /// Auth dev : reuse le JWT colle dans HubChatClient._devToken pour eviter duplication.
    /// </summary>
    public sealed class HubProfilePanel : MonoBehaviour
    {
        public enum ProfileTab { View, Stats, Classes, Achievements, Cosmetics }

        [Header("Backend")]
        [SerializeField] private NymoraBackendSettings _backendSettings;

        [Header("Root")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _closeButton;

        [Header("Tabs")]
        [SerializeField] private Button _tabViewButton;
        [SerializeField] private Button _tabStatsButton;
        [SerializeField] private Button _tabClassesButton;
        [SerializeField] private Button _tabAchievementsButton;
        [SerializeField] private Button _tabCosmeticsButton;

        [Header("Content panels")]
        [SerializeField] private GameObject _contentView;
        [SerializeField] private GameObject _contentStats;
        [SerializeField] private GameObject _contentClasses;
        [SerializeField] private GameObject _contentAchievements;
        [SerializeField] private GameObject _contentCosmetics;

        [Header("Vue tab fields")]
        [SerializeField] private TextMeshProUGUI _viewDisplayName;
        // Note : email volontairement NON affiche dans l'UI profil (privacy / streamer-safe).
        // Le backend continue de le retourner via /profile/me mais on ne le rend pas visible.
        [SerializeField] private TextMeshProUGUI _viewMmr;
        [SerializeField] private TextMeshProUGUI _viewCreatedAt;
        [SerializeField] private TextMeshProUGUI _viewLastLoginAt;
        [SerializeField] private TextMeshProUGUI _viewStatusLine;

        [Header("Tab style")]
        [SerializeField] private Color _tabActiveColor = new Color(0.25f, 0.4f, 0.65f, 1f);
        [SerializeField] private Color _tabInactiveColor = new Color(0.2f, 0.2f, 0.24f, 1f);

        public static HubProfilePanel Instance { get; private set; }

        private NymoraApiClient _api;
        private ProfileTab _activeTab = ProfileTab.View;
        private bool _hasFetchedOnce;

        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (_backendSettings == null)
            {
                Debug.LogError("[ProfilePanel] _backendSettings non assigne — panel desactive.");
                enabled = false;
                return;
            }
            _api = new NymoraApiClient(_backendSettings);

            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
            if (_tabViewButton != null) _tabViewButton.onClick.AddListener(() => SwitchTab(ProfileTab.View));
            if (_tabStatsButton != null) _tabStatsButton.onClick.AddListener(() => SwitchTab(ProfileTab.Stats));
            if (_tabClassesButton != null) _tabClassesButton.onClick.AddListener(() => SwitchTab(ProfileTab.Classes));
            if (_tabAchievementsButton != null) _tabAchievementsButton.onClick.AddListener(() => SwitchTab(ProfileTab.Achievements));
            if (_tabCosmeticsButton != null) _tabCosmeticsButton.onClick.AddListener(() => SwitchTab(ProfileTab.Cosmetics));
        }

        private void OnDisable()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveAllListeners();
            if (_tabViewButton != null) _tabViewButton.onClick.RemoveAllListeners();
            if (_tabStatsButton != null) _tabStatsButton.onClick.RemoveAllListeners();
            if (_tabClassesButton != null) _tabClassesButton.onClick.RemoveAllListeners();
            if (_tabAchievementsButton != null) _tabAchievementsButton.onClick.RemoveAllListeners();
            if (_tabCosmeticsButton != null) _tabCosmeticsButton.onClick.RemoveAllListeners();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open()
        {
            if (_panelRoot == null) return;
            _panelRoot.SetActive(true);
            SwitchTab(ProfileTab.View);
            if (!_hasFetchedOnce) FetchProfileAsync().Forget();
        }

        public void Close()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private void SwitchTab(ProfileTab tab)
        {
            _activeTab = tab;
            if (_contentView != null) _contentView.SetActive(tab == ProfileTab.View);
            if (_contentStats != null) _contentStats.SetActive(tab == ProfileTab.Stats);
            if (_contentClasses != null) _contentClasses.SetActive(tab == ProfileTab.Classes);
            if (_contentAchievements != null) _contentAchievements.SetActive(tab == ProfileTab.Achievements);
            if (_contentCosmetics != null) _contentCosmetics.SetActive(tab == ProfileTab.Cosmetics);
            UpdateTabStyles();
        }

        private void UpdateTabStyles()
        {
            SetButtonBg(_tabViewButton, _activeTab == ProfileTab.View ? _tabActiveColor : _tabInactiveColor);
            SetButtonBg(_tabStatsButton, _activeTab == ProfileTab.Stats ? _tabActiveColor : _tabInactiveColor);
            SetButtonBg(_tabClassesButton, _activeTab == ProfileTab.Classes ? _tabActiveColor : _tabInactiveColor);
            SetButtonBg(_tabAchievementsButton, _activeTab == ProfileTab.Achievements ? _tabActiveColor : _tabInactiveColor);
            SetButtonBg(_tabCosmeticsButton, _activeTab == ProfileTab.Cosmetics ? _tabActiveColor : _tabInactiveColor);
        }

        private static void SetButtonBg(Button btn, Color color)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = color;
        }

        private async UniTask FetchProfileAsync()
        {
            SetViewStatus("Chargement du profil...");
            string token = ResolveDevToken();
            if (string.IsNullOrEmpty(token))
            {
                SetViewStatus("Pas de JWT (HubChatClient._devToken vide).");
                Debug.LogWarning("[ProfilePanel] DevToken introuvable — colle un JWT dans HubChatClient._devToken.");
                return;
            }
            _api.SetBearerToken(token);

            var res = await _api.GetProfileMeAsync();
            if (!res.IsSuccess)
            {
                SetViewStatus($"Erreur {res.StatusCode} : {res.ErrorMessage}");
                Debug.LogWarning($"[ProfilePanel] /profile/me failed: {res.StatusCode} {res.ErrorMessage}");
                return;
            }
            ApplyProfileData(res.Data);
            _hasFetchedOnce = true;
        }

        private void ApplyProfileData(ProfileMeResponse data)
        {
            if (_viewDisplayName != null) _viewDisplayName.text = data.displayName ?? "(sans nom)";
            if (_viewMmr != null) _viewMmr.text = $"MMR : {data.mmr}";
            if (_viewCreatedAt != null) _viewCreatedAt.text = $"Inscrit : {FormatDate(data.createdAt) ?? "—"}";
            if (_viewLastLoginAt != null) _viewLastLoginAt.text = $"Derniere connexion : {FormatDate(data.lastLoginAt) ?? "—"}";
            if (_viewStatusLine != null) _viewStatusLine.text = "";
        }

        private void SetViewStatus(string msg)
        {
            if (_viewStatusLine != null) _viewStatusLine.text = msg;
        }

        private static string FormatDate(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return null;
            if (DateTime.TryParse(iso, out var dt))
            {
                return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            }
            return iso;
        }

        private static string ResolveDevToken()
        {
            return HubChatClient.Instance?.DevToken;
        }
    }
}
