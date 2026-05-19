using System;
using System.Collections.Generic;
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
        public enum ProfileTab { View, Stats, Classes, Achievements, Cosmetics, Wallet }

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
        [SerializeField] private Button _tabWalletButton;

        [Header("Content panels")]
        [SerializeField] private GameObject _contentView;
        [SerializeField] private GameObject _contentStats;
        [SerializeField] private GameObject _contentClasses;
        [SerializeField] private GameObject _contentAchievements;
        [SerializeField] private GameObject _contentCosmetics;
        [SerializeField] private GameObject _contentWallet;

        [Header("Vue tab fields")]
        [SerializeField] private TextMeshProUGUI _viewDisplayName;
        // Note : email volontairement NON affiche dans l'UI profil (privacy / streamer-safe).
        // Le backend continue de le retourner via /profile/me mais on ne le rend pas visible.
        [SerializeField] private TextMeshProUGUI _viewMmr;
        [SerializeField] private TextMeshProUGUI _viewCreatedAt;
        [SerializeField] private TextMeshProUGUI _viewLastLoginAt;
        [SerializeField] private TextMeshProUGUI _viewStatusLine;

        // ====== Brique 5.1 — Classes tab (progression XP) ======

        [Serializable]
        public struct ClassProgressionRow
        {
            public string classId;          // "Soulrender" | "Nightseer" | "Colossar" | "Necram" | "Ghostra"
            public TextMeshProUGUI levelLabel;
            public TextMeshProUGUI xpLabel;
            public Image xpBarFill;          // Image en mode Filled, fillAmount mis a jour
        }

        [Header("Classes tab — 5 rows (Soulrender, Nightseer, Colossar, Necram, Ghostra)")]
        [SerializeField] private ClassProgressionRow[] _classRows;

        // ====== Brique 5.2 — Achievements tab ======
        [Header("Achievements tab")]
        [SerializeField] private RectTransform _achievementsContainer;
        [SerializeField] private TextMeshProUGUI _achievementsHeader;
        [SerializeField] private float _achievementItemHeight = 48f;
        [SerializeField] private Color _achievementBgLocked = new Color(0.18f, 0.20f, 0.25f, 1f);
        [SerializeField] private Color _achievementBgUnlocked = new Color(0.20f, 0.32f, 0.22f, 1f);
        [SerializeField] private Color _achievementCategoryColor = new Color(0.85f, 0.85f, 0.9f, 1f);
        [SerializeField] private Color _achievementProgressBgColor = new Color(0.10f, 0.10f, 0.12f, 1f);
        [SerializeField] private Color _achievementProgressFillColor = new Color(0.35f, 0.65f, 0.45f, 1f);
        [SerializeField] private Color _achievementPointsColor = new Color(0.95f, 0.85f, 0.4f, 1f);

        [Header("Tab style")]
        [SerializeField] private Color _tabActiveColor = new Color(0.25f, 0.4f, 0.65f, 1f);
        [SerializeField] private Color _tabInactiveColor = new Color(0.2f, 0.2f, 0.24f, 1f);

        public static HubProfilePanel Instance { get; private set; }

        private NymoraApiClient _api;
        private ProfileTab _activeTab = ProfileTab.View;
        private bool _hasFetchedOnce;
        private bool _hasFetchedProgressionOnce;
        // 5.2 achievements
        private bool _hasFetchedAchievementsOnce;
        private AchievementDefDto[] _catalog;
        private readonly Dictionary<string, UserAchievementDto> _myAchievementsById = new Dictionary<string, UserAchievementDto>();
        private readonly Dictionary<string, GameObject> _achievementItemGOs = new Dictionary<string, GameObject>();
        private readonly List<GameObject> _spawnedAchievementGOs = new List<GameObject>();
        // Chat UI reference for unlock toast (set via 4.10 wire ; fallback null si pas wire)
        [SerializeField] private HubChatUI _chatUIForAchievementToast;

        // ====== Brique 5.4.b — Wallet tab (Portefeuille) ======
        [Header("Wallet tab")]
        [SerializeField] private TextMeshProUGUI _walletHeader;
        [SerializeField] private RectTransform _walletTransactionsContainer;
        [SerializeField] private float _walletTxItemHeight = 36f;
        [SerializeField] private Color _walletAwardColor = new Color(0.45f, 0.85f, 0.50f, 1f);
        [SerializeField] private Color _walletSpendColor = new Color(0.95f, 0.55f, 0.45f, 1f);
        [SerializeField] private Color _walletRowBg = new Color(0.15f, 0.16f, 0.20f, 1f);
        [SerializeField] private Color _walletRowBgAlt = new Color(0.18f, 0.19f, 0.23f, 1f);
        private bool _hasFetchedWalletOnce;
        private readonly List<GameObject> _spawnedWalletGOs = new List<GameObject>();

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
            if (_tabWalletButton != null) _tabWalletButton.onClick.AddListener(() => SwitchTab(ProfileTab.Wallet));
        }

        private void OnDisable()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveAllListeners();
            if (_tabViewButton != null) _tabViewButton.onClick.RemoveAllListeners();
            if (_tabStatsButton != null) _tabStatsButton.onClick.RemoveAllListeners();
            if (_tabClassesButton != null) _tabClassesButton.onClick.RemoveAllListeners();
            if (_tabAchievementsButton != null) _tabAchievementsButton.onClick.RemoveAllListeners();
            if (_tabCosmeticsButton != null) _tabCosmeticsButton.onClick.RemoveAllListeners();
            if (_tabWalletButton != null) _tabWalletButton.onClick.RemoveAllListeners();
        }

        private void Start()
        {
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnXpAwarded += HandleXpAwarded;
                HubChatClient.Instance.OnClassLevelUp += HandleClassLevelUp;
                HubChatClient.Instance.OnAchievementProgress += HandleAchievementProgress;
                HubChatClient.Instance.OnAchievementUnlocked += HandleAchievementUnlocked;
                HubChatClient.Instance.OnWalletUpdate += HandleWalletUpdate;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnXpAwarded -= HandleXpAwarded;
                HubChatClient.Instance.OnClassLevelUp -= HandleClassLevelUp;
                HubChatClient.Instance.OnAchievementProgress -= HandleAchievementProgress;
                HubChatClient.Instance.OnAchievementUnlocked -= HandleAchievementUnlocked;
                HubChatClient.Instance.OnWalletUpdate -= HandleWalletUpdate;
            }
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
            if (_contentWallet != null) _contentWallet.SetActive(tab == ProfileTab.Wallet);
            UpdateTabStyles();
            // 5.1 — Lazy fetch progression au 1er switch sur Classes
            if (tab == ProfileTab.Classes && !_hasFetchedProgressionOnce)
            {
                FetchProgressionAsync().Forget();
            }
            // 5.2 — Lazy fetch achievements au 1er switch sur Achievements
            if (tab == ProfileTab.Achievements && !_hasFetchedAchievementsOnce)
            {
                FetchAchievementsAsync().Forget();
            }
            // 5.4 — Lazy fetch wallet au 1er switch sur Wallet
            if (tab == ProfileTab.Wallet && !_hasFetchedWalletOnce)
            {
                FetchWalletAsync().Forget();
            }
        }

        private void UpdateTabStyles()
        {
            SetButtonBg(_tabViewButton, _activeTab == ProfileTab.View ? _tabActiveColor : _tabInactiveColor);
            SetButtonBg(_tabStatsButton, _activeTab == ProfileTab.Stats ? _tabActiveColor : _tabInactiveColor);
            SetButtonBg(_tabClassesButton, _activeTab == ProfileTab.Classes ? _tabActiveColor : _tabInactiveColor);
            SetButtonBg(_tabAchievementsButton, _activeTab == ProfileTab.Achievements ? _tabActiveColor : _tabInactiveColor);
            SetButtonBg(_tabCosmeticsButton, _activeTab == ProfileTab.Cosmetics ? _tabActiveColor : _tabInactiveColor);
            SetButtonBg(_tabWalletButton, _activeTab == ProfileTab.Wallet ? _tabActiveColor : _tabInactiveColor);
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

        // ====== Brique 5.1 — Classes tab logic ======

        private async UniTask FetchProgressionAsync()
        {
            string token = ResolveDevToken();
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);

            var res = await _api.GetProgressionMeAsync();
            if (!res.IsSuccess)
            {
                Debug.LogWarning($"[ProfilePanel] /progression/me failed: {res.StatusCode} {res.ErrorMessage}");
                return;
            }
            ApplyProgressionData(res.Data);
            _hasFetchedProgressionOnce = true;
        }

        private void ApplyProgressionData(ProgressionMeResponse data)
        {
            if (data?.progressions == null || _classRows == null) return;
            foreach (var prog in data.progressions)
            {
                ApplyRow(prog.classId, prog.level, prog.xp, prog.xpToNext);
            }
        }

        private void ApplyRow(string classId, int level, int xp, int xpToNext)
        {
            if (_classRows == null) return;
            for (int i = 0; i < _classRows.Length; i++)
            {
                var row = _classRows[i];
                if (row.classId != classId) continue;
                if (row.levelLabel != null) row.levelLabel.text = $"Niv. {level}";
                if (row.xpLabel != null)
                {
                    row.xpLabel.text = xpToNext > 0 ? $"{xp} / {xpToNext} XP" : "MAX";
                }
                if (row.xpBarFill != null)
                {
                    row.xpBarFill.fillAmount = xpToNext > 0 ? Mathf.Clamp01((float)xp / xpToNext) : 1f;
                }
                return;
            }
        }

        private void HandleXpAwarded(HubChatClient.XpAwardedData data)
        {
            ApplyRow(data.ClassId, data.NewLevel, data.NewXp, data.XpToNext);
        }

        private void HandleClassLevelUp(string classId, int newLevel)
        {
            // Visual feedback minimal pour MVP : juste log. Animation popup à venir en 5.9 polish.
            Debug.Log($"[ProfilePanel] LEVEL UP {classId} → niveau {newLevel}");
        }

        // ====== Brique 5.2 — Achievements tab logic ======

        private async UniTask FetchAchievementsAsync()
        {
            string token = ResolveDevToken();
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);

            var catalogTask = _api.GetAchievementsCatalogAsync();
            var meTask = _api.GetAchievementsMeAsync();
            var catalogRes = await catalogTask;
            var meRes = await meTask;

            if (!catalogRes.IsSuccess || !meRes.IsSuccess)
            {
                Debug.LogWarning($"[ProfilePanel] achievements fetch failed ({catalogRes.StatusCode}/{meRes.StatusCode})");
                return;
            }

            _catalog = catalogRes.Data.achievements ?? new AchievementDefDto[0];
            _myAchievementsById.Clear();
            if (meRes.Data.items != null)
            {
                foreach (var item in meRes.Data.items)
                {
                    _myAchievementsById[item.achievementId] = item;
                }
            }
            UpdateAchievementsHeader(meRes.Data.unlockedCount, meRes.Data.totalCount, meRes.Data.totalPoints);
            RebuildAchievementsUI();
            _hasFetchedAchievementsOnce = true;
        }

        private void UpdateAchievementsHeader(int unlocked, int total, int points)
        {
            if (_achievementsHeader != null)
            {
                _achievementsHeader.text = $"<b>{unlocked}/{total}</b> succès · <color=#f5dc66>{points} points</color>";
            }
        }

        private void RebuildAchievementsUI()
        {
            if (_achievementsContainer == null || _catalog == null) return;

            // Cleanup
            foreach (var go in _spawnedAchievementGOs) if (go != null) Destroy(go);
            _spawnedAchievementGOs.Clear();
            _achievementItemGOs.Clear();

            // Group by category dans l'ordre fixe
            string[] categoryOrder = { "first_steps", "combat", "progression" };
            string[] categoryLabels = { "Premiers pas", "Combat", "Progression" };

            for (int ci = 0; ci < categoryOrder.Length; ci++)
            {
                string cat = categoryOrder[ci];
                var inCat = new List<AchievementDefDto>();
                foreach (var d in _catalog) if (d.category == cat) inCat.Add(d);
                if (inCat.Count == 0) continue;

                SpawnCategoryHeader(_achievementsContainer, categoryLabels[ci], inCat.Count);
                foreach (var def in inCat)
                {
                    SpawnAchievementItem(_achievementsContainer, def);
                }
            }
        }

        private void SpawnCategoryHeader(RectTransform parent, string label, int count)
        {
            var go = new GameObject($"CatHeader_{label}", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = $"<b>{label}</b> ({count})";
            tmp.fontSize = 18;
            tmp.color = _achievementCategoryColor;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.richText = true;
            go.GetComponent<LayoutElement>().preferredHeight = 28f;
            _spawnedAchievementGOs.Add(go);
        }

        private void SpawnAchievementItem(RectTransform parent, AchievementDefDto def)
        {
            _myAchievementsById.TryGetValue(def.id, out var my);
            bool unlocked = my != null && my.unlocked;
            int progress = my?.progress ?? 0;

            var item = new GameObject($"Achv_{def.id}",
                typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            item.transform.SetParent(parent, false);
            item.GetComponent<Image>().color = unlocked ? _achievementBgUnlocked : _achievementBgLocked;
            var hl = item.GetComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(12, 12, 6, 6);
            hl.spacing = 10;
            hl.childForceExpandHeight = true;
            hl.childControlHeight = true;
            hl.childControlWidth = true;
            hl.childAlignment = TextAnchor.MiddleLeft;
            item.GetComponent<LayoutElement>().preferredHeight = _achievementItemHeight;

            // Icon (✓ ou 🔒)
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            iconGo.transform.SetParent(item.transform, false);
            var iconTmp = iconGo.GetComponent<TextMeshProUGUI>();
            iconTmp.text = unlocked ? "<color=#7fff7f>✓</color>" : "<color=#888888>•</color>";
            iconTmp.fontSize = 24;
            iconTmp.alignment = TextAlignmentOptions.Center;
            iconTmp.richText = true;
            iconGo.GetComponent<LayoutElement>().preferredWidth = 28f;

            // Title (flex)
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            titleGo.transform.SetParent(item.transform, false);
            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = def.title;
            titleTmp.fontSize = 16;
            titleTmp.color = unlocked ? Color.white : new Color(0.85f, 0.85f, 0.88f);
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            titleTmp.fontStyle = unlocked ? FontStyles.Bold : FontStyles.Normal;
            titleGo.GetComponent<LayoutElement>().flexibleWidth = 1f;

            // Progress bar (mini)
            var barBg = new GameObject("ProgBg", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            barBg.transform.SetParent(item.transform, false);
            barBg.GetComponent<Image>().color = _achievementProgressBgColor;
            var barBgLe = barBg.GetComponent<LayoutElement>();
            barBgLe.preferredWidth = 120f;
            barBgLe.preferredHeight = 12f;

            var barFill = new GameObject("ProgFill", typeof(RectTransform), typeof(Image));
            barFill.transform.SetParent(barBg.transform, false);
            var barFillImg = barFill.GetComponent<Image>();
            barFillImg.color = _achievementProgressFillColor;
            barFillImg.type = Image.Type.Filled;
            barFillImg.fillMethod = Image.FillMethod.Horizontal;
            barFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            float pct = def.target > 0 ? Mathf.Clamp01((float)progress / def.target) : 1f;
            barFillImg.fillAmount = pct;
            var barFillRt = barFill.GetComponent<RectTransform>();
            barFillRt.anchorMin = Vector2.zero; barFillRt.anchorMax = Vector2.one;
            barFillRt.offsetMin = Vector2.zero; barFillRt.offsetMax = Vector2.zero;

            // Progress text
            var progGo = new GameObject("ProgText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            progGo.transform.SetParent(item.transform, false);
            var progTmp = progGo.GetComponent<TextMeshProUGUI>();
            progTmp.text = $"{progress}/{def.target}";
            progTmp.fontSize = 14;
            progTmp.color = new Color(0.8f, 0.8f, 0.85f);
            progTmp.alignment = TextAlignmentOptions.MidlineRight;
            progGo.GetComponent<LayoutElement>().preferredWidth = 60f;

            // Points
            var ptsGo = new GameObject("Points", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            ptsGo.transform.SetParent(item.transform, false);
            var ptsTmp = ptsGo.GetComponent<TextMeshProUGUI>();
            ptsTmp.text = $"{def.points}<size=70%> pts</size>";
            ptsTmp.fontSize = 14;
            ptsTmp.color = _achievementPointsColor;
            ptsTmp.alignment = TextAlignmentOptions.MidlineRight;
            ptsTmp.fontStyle = FontStyles.Bold;
            ptsTmp.richText = true;
            ptsGo.GetComponent<LayoutElement>().preferredWidth = 70f;

            _spawnedAchievementGOs.Add(item);
            _achievementItemGOs[def.id] = item;
        }

        private void HandleAchievementProgress(string achievementId, int oldProgress, int newProgress, int target, bool justUnlocked)
        {
            // Update cache
            if (!_myAchievementsById.TryGetValue(achievementId, out var existing) || existing == null)
            {
                existing = new UserAchievementDto { achievementId = achievementId, target = target };
                _myAchievementsById[achievementId] = existing;
            }
            existing.progress = newProgress;
            existing.target = target;
            existing.unlocked = justUnlocked || existing.unlocked;

            // Rebuild si on a fetché une fois (sinon item pas spawné, on attend la 1ère ouverture)
            if (_hasFetchedAchievementsOnce && _activeTab == ProfileTab.Achievements)
            {
                // Refetch /me léger pour avoir le totalPoints à jour
                RefreshAchievementsHeaderAsync().Forget();
                RebuildAchievementsUI();
            }
        }

        private async UniTask RefreshAchievementsHeaderAsync()
        {
            string token = ResolveDevToken();
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);
            var res = await _api.GetAchievementsMeAsync();
            if (res.IsSuccess)
            {
                UpdateAchievementsHeader(res.Data.unlockedCount, res.Data.totalCount, res.Data.totalPoints);
            }
        }

        private void HandleAchievementUnlocked(string achievementId, string title, int points)
        {
            Debug.Log($"[ProfilePanel] ACHIEVEMENT UNLOCKED : {title} (+{points} pts)");
            // Toast dans le chat (visible même panel fermé)
            if (_chatUIForAchievementToast != null)
            {
                _chatUIForAchievementToast.AppendSystemLineExternal(
                    $"<color=#ffd700>🏆 Succès débloqué : <b>{title}</b> (+{points} pts)</color>");
            }
        }

        // ====== Brique 5.4.b — Wallet tab logic ======

        private async UniTask FetchWalletAsync()
        {
            string token = ResolveDevToken();
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);

            var res = await _api.GetWalletMeAsync();
            if (!res.IsSuccess)
            {
                Debug.LogWarning($"[ProfilePanel] /wallet/me failed: {res.StatusCode} {res.ErrorMessage}");
                return;
            }
            ApplyWalletData(res.Data);
            _hasFetchedWalletOnce = true;
        }

        private void ApplyWalletData(WalletMeResponse data)
        {
            UpdateWalletHeader(data.nymos, data.shards);
            RebuildWalletTransactions(data.transactions ?? new WalletTransactionDto[0]);
        }

        private void UpdateWalletHeader(int nymos, int shards)
        {
            if (_walletHeader == null) return;
            // ◆ / ◇ (U+25C6/U+25C7) — BMP universel, supporte par LiberationSans SDF.
            // Emojis 🪙/💎 ne rendent pas (incident 5.4.b 19 mai).
            _walletHeader.text =
                $"<color=#f5dc66>◆ <b>{nymos}</b> Nymos</color>   ·   <color=#8be0ff>◇ <b>{shards}</b> Shards</color>";
        }

        private void RebuildWalletTransactions(WalletTransactionDto[] txs)
        {
            if (_walletTransactionsContainer == null) return;

            foreach (var go in _spawnedWalletGOs) if (go != null) Destroy(go);
            _spawnedWalletGOs.Clear();

            if (txs.Length == 0)
            {
                SpawnWalletEmptyRow();
                return;
            }

            for (int i = 0; i < txs.Length; i++)
            {
                SpawnWalletTxRow(txs[i], i);
            }
        }

        private void SpawnWalletEmptyRow()
        {
            var go = new GameObject("WalletEmpty", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(_walletTransactionsContainer, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = "<i>Aucune transaction pour le moment.</i>";
            tmp.fontSize = 14;
            tmp.color = new Color(0.7f, 0.7f, 0.75f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.richText = true;
            go.GetComponent<LayoutElement>().preferredHeight = 48f;
            _spawnedWalletGOs.Add(go);
        }

        private void SpawnWalletTxRow(WalletTransactionDto tx, int index)
        {
            var row = new GameObject($"WalletTx_{index}",
                typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(_walletTransactionsContainer, false);
            row.GetComponent<Image>().color = (index % 2 == 0) ? _walletRowBg : _walletRowBgAlt;
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(10, 10, 4, 4);
            hl.spacing = 8;
            hl.childForceExpandHeight = true;
            hl.childControlHeight = true;
            hl.childControlWidth = true;
            hl.childAlignment = TextAnchor.MiddleLeft;
            row.GetComponent<LayoutElement>().preferredHeight = _walletTxItemHeight;

            // Date (compact yyyy-MM-dd HH:mm)
            var dateGo = new GameObject("Date", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            dateGo.transform.SetParent(row.transform, false);
            var dateTmp = dateGo.GetComponent<TextMeshProUGUI>();
            dateTmp.text = FormatDate(tx.createdAt) ?? "—";
            dateTmp.fontSize = 13;
            dateTmp.color = new Color(0.7f, 0.7f, 0.78f);
            dateTmp.alignment = TextAlignmentOptions.MidlineLeft;
            dateGo.GetComponent<LayoutElement>().preferredWidth = 130f;

            // Reason (flex)
            var reasonGo = new GameObject("Reason", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            reasonGo.transform.SetParent(row.transform, false);
            var reasonTmp = reasonGo.GetComponent<TextMeshProUGUI>();
            reasonTmp.text = tx.reason ?? "—";
            reasonTmp.fontSize = 14;
            reasonTmp.color = new Color(0.92f, 0.92f, 0.95f);
            reasonTmp.alignment = TextAlignmentOptions.MidlineLeft;
            reasonGo.GetComponent<LayoutElement>().flexibleWidth = 1f;

            // Amount signe + currency
            string currencyIcon = tx.currency == "Shards" ? "◇" : "◆";
            string sign = tx.amount >= 0 ? "+" : "";
            var amountGo = new GameObject("Amount", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            amountGo.transform.SetParent(row.transform, false);
            var amountTmp = amountGo.GetComponent<TextMeshProUGUI>();
            amountTmp.text = $"{sign}{tx.amount} {currencyIcon}";
            amountTmp.fontSize = 14;
            amountTmp.color = tx.amount >= 0 ? _walletAwardColor : _walletSpendColor;
            amountTmp.fontStyle = FontStyles.Bold;
            amountTmp.alignment = TextAlignmentOptions.MidlineRight;
            amountGo.GetComponent<LayoutElement>().preferredWidth = 100f;

            // BalanceAfter snapshot (en gris)
            var balGo = new GameObject("Balance", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            balGo.transform.SetParent(row.transform, false);
            var balTmp = balGo.GetComponent<TextMeshProUGUI>();
            balTmp.text = $"<size=70%>(solde {tx.balanceAfter})</size>";
            balTmp.fontSize = 13;
            balTmp.color = new Color(0.6f, 0.6f, 0.65f);
            balTmp.alignment = TextAlignmentOptions.MidlineRight;
            balTmp.richText = true;
            balGo.GetComponent<LayoutElement>().preferredWidth = 90f;

            _spawnedWalletGOs.Add(row);
        }

        private void HandleWalletUpdate(HubChatClient.WalletUpdateData data)
        {
            UpdateWalletHeader(data.Nymos, data.Shards);
            // Si on est sur le tab Wallet et qu'on a deja fetch, refetch pour avoir
            // la derniere transaction. Sinon (panel ferme ou autre tab), juste header.
            if (_hasFetchedWalletOnce && _activeTab == ProfileTab.Wallet)
            {
                FetchWalletAsync().Forget();
            }
        }
    }
}
