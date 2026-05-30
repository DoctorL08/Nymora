using System.Text;
using Cysharp.Threading.Tasks;
using Nymora.Core.Data;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 6.6.b — Panneau Classement (leaderboard global par MMR).
    ///
    /// Ouvert depuis le panneau de recherche ranked (bouton "Classement"). Fetch
    /// GET /ranked/leaderboard puis rend les lignes dans un seul TMP (scrollable).
    /// La ligne du joueur local est surlignee.
    ///
    /// Singleton. Cable via "Nymora > Setup > Patch Leaderboard Panel".
    /// </summary>
    public sealed class HubLeaderboardPanel : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _closeButton;

        [Header("Contenu")]
        [SerializeField] private TMP_Text _listText;

        [Header("Backend")]
        [SerializeField] private NymoraBackendSettings _backendSettings;

        public static HubLeaderboardPanel Instance { get; private set; }

        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

        private NymoraApiClient _api;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (_backendSettings != null) _api = new NymoraApiClient(_backendSettings);
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveAllListeners();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open()
        {
            if (_panelRoot != null) _panelRoot.SetActive(true);
            SetText("Chargement du classement...");
            FetchAsync().Forget();
        }

        public void Close()
        {
            UiPanelAnimator.CloseAnimated(_panelRoot);
        }

        private async UniTaskVoid FetchAsync()
        {
            var txt = await GetLeaderboardTextAsync(100);
            SetText(txt);
        }

        /// <summary>
        /// M2 (24 mai) — Construit le texte rich-text du classement (réutilisé par HubMenuShell
        /// pour l'afficher au style du nouveau menu). Renvoie un message d'erreur lisible si échec.
        /// </summary>
        /// <summary>Fetch brut des entrées du classement. Renvoie (entries, null) si OK, sinon
        /// (null, message d'erreur lisible). Utilisé par le rendu en lignes du menu moderne.</summary>
        public async UniTask<(LeaderboardEntry[] entries, string error)> GetLeaderboardEntriesAsync(int limit)
        {
            if (_api == null) return (null, "Backend non configuré.");
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token)) return (null, "Non connecté.");
            _api.SetBearerToken(token);

            var res = await _api.GetLeaderboardAsync(limit);
            if (!res.IsSuccess) return (null, $"Erreur classement ({res.StatusCode}).");

            var entries = res.Data?.entries;
            if (entries == null || entries.Length == 0) return (null, "Aucun joueur classé pour le moment.");
            return (entries, null);
        }

        /// <summary>Identifiant utilisateur local (pour surligner sa propre ligne). Peut être null.</summary>
        public static string LocalUserId => HubChatClient.Instance?.MyUserId;

        /// <summary>MMR du joueur local via /profile/me (pour afficher son rang sur les cartes Arène).
        /// Renvoie -1 si indisponible (non connecté / erreur).</summary>
        public async UniTask<int> FetchLocalMmrAsync()
        {
            if (_api == null) return -1;
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token)) return -1;
            _api.SetBearerToken(token);
            var res = await _api.GetProfileMeAsync();
            return res.IsSuccess && res.Data != null ? res.Data.mmr : -1;
        }

        public async UniTask<string> GetLeaderboardTextAsync(int limit)
        {
            var (entries, error) = await GetLeaderboardEntriesAsync(limit);
            if (entries == null) return error;

            string myId = LocalUserId;
            // Interligne aéré ; colonnes alignées via <pos=%> (mêmes positions que l'en-tête du menu).
            var sb = new StringBuilder("<line-height=132%>");
            foreach (var e in entries)
            {
                bool isMe = !string.IsNullOrEmpty(myId) && e.userId == myId;
                string rankColor = PodiumColor(e.position);
                string pseudo = CapPseudo(e.displayName);
                if (isMe) pseudo = $"<b>{pseudo} (toi)</b>";
                string tier = RankLadder.ColoredName(e.mmr);
                string line =
                    $"<pos={ColRank}><color={rankColor}><b>#{e.position}</b></color>" +
                    $"<pos={ColName}>{pseudo}" +
                    $"<pos={ColTier}>{tier}" +
                    $"<pos={ColMmr}><color=#f5f5f5>{e.mmr}</color>" +
                    $"<pos={ColWl}><color=#9aa0ad>{e.rankedWins}V/{e.rankedLosses}D</color>";
                if (isMe) line = $"<mark=#3a3358aa>{line}</mark>";
                sb.AppendLine(line);
            }
            return sb.ToString();
        }

        // Positions de colonnes (% de la largeur du TMP) — partagées avec l'en-tête du menu
        // (HubMenuShell.BuildLeaderboard) pour que les colonnes s'alignent pile sous les titres.
        public const string ColRank = "0%";
        public const string ColName = "11%";
        public const string ColTier = "53%";
        public const string ColMmr = "77%";
        public const string ColWl = "88%";

        // Top 3 = or / argent / bronze ; le reste = mauve d'accent.
        private static string PodiumColor(int position) => position switch
        {
            1 => "#ffd24a",
            2 => "#c8d0da",
            3 => "#d99a5b",
            _ => "#cdb4ff",
        };

        // Tronque les pseudos longs pour ne pas chevaucher la colonne Rang.
        private static string CapPseudo(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            return name.Length <= 16 ? name : name.Substring(0, 15) + "…";
        }

        private void SetText(string s)
        {
            if (_listText != null) _listText.text = s;
        }
    }
}
