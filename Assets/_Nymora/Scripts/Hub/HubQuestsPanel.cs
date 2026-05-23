using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 5.8.b — Panneau Quêtes (quotidiennes / hebdo / saisonnières).
    /// Liste verticale ; chaque quête = QuestRow (progression + Réclamer). Refetch à l'ouverture
    /// + après claim. Récompenses : XP Battle Pass + Nymos (push WS met à jour les widgets).
    ///
    /// Singleton. Cable via "Nymora > Setup > Patch Quests Panel".
    /// </summary>
    public sealed class HubQuestsPanel : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _headerText;

        [Header("Liste")]
        [SerializeField] private RectTransform _content;
        [SerializeField] private QuestRow _rowTemplate;

        [Header("Backend")]
        [SerializeField] private NymoraBackendSettings _backendSettings;

        public static HubQuestsPanel Instance { get; private set; }
        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

        private NymoraApiClient _api;
        private bool _busy;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (_backendSettings != null) _api = new NymoraApiClient(_backendSettings);
            if (_rowTemplate != null) _rowTemplate.gameObject.SetActive(false);
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

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Open()
        {
            if (_panelRoot != null) _panelRoot.SetActive(true);
            SetHeader("Chargement des quêtes...");
            FetchAsync().Forget();
        }

        public void Close() { if (_panelRoot != null) _panelRoot.SetActive(false); }

        private string Token() => HubChatClient.Instance?.DevToken;

        private async UniTaskVoid FetchAsync()
        {
            if (_api == null) { SetHeader("Backend non configuré."); return; }
            string token = Token();
            if (string.IsNullOrEmpty(token)) { SetHeader("Non connecté."); return; }
            _api.SetBearerToken(token);
            var res = await _api.GetQuestsAsync();
            if (!res.IsSuccess) { SetHeader($"Erreur quêtes ({res.StatusCode})."); return; }
            Render(res.Data);
        }

        private void Render(QuestsMeResponse d)
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            int claimable = 0;
            if (_rowTemplate != null && _content != null && d.quests != null)
            {
                foreach (var q in d.quests)
                {
                    if (q.completed && !q.claimed) claimable++;
                    var go = Instantiate(_rowTemplate.gameObject, _content);
                    go.SetActive(true);
                    _spawned.Add(go);
                    var row = go.GetComponent<QuestRow>();
                    row.Bind(q);
                    row.OnClaim -= HandleClaim;
                    row.OnClaim += HandleClaim;
                }
            }
            SetHeader(claimable > 0
                ? $"<b>Quêtes</b>   <color=#7CFC7C>{claimable} à réclamer</color>"
                : "<b>Quêtes</b>");
        }

        private async void HandleClaim(string questId)
        {
            if (_busy || _api == null) return;
            string token = Token();
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);
            _busy = true;
            var res = await _api.ClaimQuestAsync(questId);
            _busy = false;
            if (res.IsSuccess && res.Data.status == "claimed")
            {
                Nymora.Core.Audio.NymoraAudioManager.Instance?.PlaySfx(Nymora.Core.Audio.SoundId.RewardClaim, Nymora.Core.Audio.NymoraAudioManager.SoftUiVolume);
                Debug.Log($"[Quests] Réclamé {questId} : +{res.Data.bpXp} XP Pass, +{res.Data.nymos} Nymos");
            }
            else
                Debug.LogWarning($"[Quests] Claim refusé {questId} : {res.Data?.error ?? res.ErrorMessage}");
            FetchAsync().Forget();
        }

        private void SetHeader(string s) { if (_headerText != null) _headerText.text = s; }
    }
}
