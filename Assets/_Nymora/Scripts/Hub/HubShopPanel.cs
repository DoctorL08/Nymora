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
    /// Brique 5.5.c — Panneau Boutique. Grille de cosmétiques en vente (rotation hebdo + items
    /// permanents), achat en Nymos/Shards. Refetch à l'ouverture + après achat. Le solde du
    /// HubWalletWidget se met à jour tout seul via le push WS WALLET_UPDATE déclenché par l'achat.
    ///
    /// Singleton. Câblé via "Nymora > Setup > Patch Shop Panel".
    /// </summary>
    public sealed class HubShopPanel : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _headerText;
        [SerializeField] private TMP_Text _rotationText;

        [Header("Grille")]
        [SerializeField] private RectTransform _content;
        [SerializeField] private ShopItemCell _cellTemplate;

        [Header("Backend")]
        [SerializeField] private NymoraBackendSettings _backendSettings;

        public static HubShopPanel Instance { get; private set; }
        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

        private NymoraApiClient _api;
        private bool _busy;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (_backendSettings != null) _api = new NymoraApiClient(_backendSettings);
            if (_cellTemplate != null) _cellTemplate.gameObject.SetActive(false);
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
            SetHeader("Chargement de la boutique...");
            if (_rotationText != null) _rotationText.text = "";
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
            var res = await _api.GetShopAsync();
            if (!res.IsSuccess) { SetHeader($"Erreur boutique ({res.StatusCode})."); return; }
            Render(res.Data);
        }

        private void Render(ShopResponse d)
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            if (_cellTemplate != null && _content != null && d.items != null)
            {
                foreach (var item in d.items)
                {
                    var go = Instantiate(_cellTemplate.gameObject, _content);
                    go.SetActive(true);
                    _spawned.Add(go);
                    var cell = go.GetComponent<ShopItemCell>();
                    cell.Bind(item);
                    cell.OnBuy -= HandleBuy;
                    cell.OnBuy += HandleBuy;
                }
            }

            SetHeader("<b>Boutique</b>");
            if (_rotationText != null && d.rotation != null)
                _rotationText.text = $"Rotation — renouvelée dans {DaysUntil(d.rotation.endsAt)}";
        }

        private async void HandleBuy(string itemId, string currency)
        {
            if (_busy || _api == null) return;
            string token = Token();
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);
            _busy = true;
            var res = await _api.BuyItemAsync(itemId, currency);
            _busy = false;

            if (res.IsSuccess && res.Data.ok)
            {
                Nymora.Core.Audio.NymoraAudioManager.Instance?.PlaySfx(Nymora.Core.Audio.SoundId.PurchaseSuccess, Nymora.Core.Audio.NymoraAudioManager.SoftUiVolume);
                Debug.Log($"[Shop] Acheté {itemId} en {currency}. Solde : {res.Data.wallet.nymos} Nymos / {res.Data.wallet.shards} Shards");
                FetchAsync().Forget(); // refresh owned + prix
            }
            else
            {
                string err = res.IsSuccess ? res.Data.error : res.ErrorMessage;
                Debug.LogWarning($"[Shop] Achat refusé {itemId} : {err} (HTTP {res.StatusCode})");
                SetHeader(err == "INSUFFICIENT_FUNDS"
                    ? "<color=#ff8080>Solde insuffisant.</color>"
                    : "<b>Boutique</b>");
            }
        }

        private void SetHeader(string s) { if (_headerText != null) _headerText.text = s; }

        /// <summary>"Xj" ou "Yh" jusqu'à la date ISO de fin de rotation.</summary>
        private static string DaysUntil(string isoEndsAt)
        {
            if (DateTime.TryParse(isoEndsAt, null,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var ends))
            {
                var span = ends - DateTime.UtcNow;
                if (span.TotalHours <= 0) return "bientôt";
                return span.TotalDays >= 1 ? $"{(int)span.TotalDays}j" : $"{(int)span.TotalHours}h";
            }
            return "—";
        }
    }
}
