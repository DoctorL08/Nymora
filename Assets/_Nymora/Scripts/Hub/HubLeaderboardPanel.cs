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
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private async UniTaskVoid FetchAsync()
        {
            if (_api == null) { SetText("Backend non configuré."); return; }
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token)) { SetText("Non connecté."); return; }
            _api.SetBearerToken(token);

            var res = await _api.GetLeaderboardAsync(100);
            if (!res.IsSuccess)
            {
                SetText($"Erreur classement ({res.StatusCode}).");
                return;
            }
            var entries = res.Data?.entries;
            if (entries == null || entries.Length == 0)
            {
                SetText("Aucun joueur classé pour le moment.");
                return;
            }

            string myId = HubChatClient.Instance?.MyUserId;
            var sb = new StringBuilder();
            foreach (var e in entries)
            {
                bool isMe = !string.IsNullOrEmpty(myId) && e.userId == myId;
                string pseudo = isMe ? $"<b>{e.displayName} (toi)</b>" : e.displayName;
                string tier = RankLadder.ColoredName(e.mmr);
                string line = $"<color=#cdb4ff>#{e.position}</color>  {pseudo}  —  {tier}  " +
                              $"<color=#f5f5f5>{e.mmr}</color> <color=#999999>({e.rankedWins}V/{e.rankedLosses}D)</color>";
                if (isMe) line = $"<mark=#3a3358aa>{line}</mark>";
                sb.AppendLine(line);
            }
            SetText(sb.ToString());
        }

        private void SetText(string s)
        {
            if (_listText != null) _listText.text = s;
        }
    }
}
