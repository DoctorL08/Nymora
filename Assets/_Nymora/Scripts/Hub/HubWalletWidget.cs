using Cysharp.Threading.Tasks;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 5.4.b — Widget wallet permanent en haut-droite du hub.
    /// Affiche Nymos + Shards en live. Refresh sur push WS WALLET_UPDATE.
    ///
    /// Cycle de vie :
    ///   1. Awake : resolve _api depuis _backendSettings.
    ///   2. Start : subscribe HubChatClient.OnWelcome + OnWalletUpdate. Si WELCOME deja recu, fetch direct.
    ///   3. WELCOME -> FetchAsync() -> ApplySnapshot.
    ///   4. WALLET_UPDATE WS -> ApplySnapshot live.
    ///
    /// Icones : emojis 🪙 / 💎 par defaut. Si TMP ne les rend pas (LiberationSans SDF
    /// ne couvre pas tous les emojis cf incident ⚔ Brique 4.8.b), bascule sur const
    /// _nymosIconFallback / _shardsIconFallback (◆ / ◇) via le Inspector.
    /// </summary>
    public sealed class HubWalletWidget : MonoBehaviour
    {
        [Header("Backend")]
        [SerializeField] private NymoraBackendSettings _backendSettings;

        [Header("UI labels (TextMeshProUGUI)")]
        [SerializeField] private TextMeshProUGUI _nymosLabel;
        [SerializeField] private TextMeshProUGUI _shardsLabel;

        [Header("Icons (override Inspector si besoin)")]
        // LiberationSans SDF ne porte PAS la plupart des emojis (U+1Fxxx).
        // Defaut ASCII safe : ◆ Nymos / ◇ Shards. Bascule emoji possible quand
        // le designer livrera une font TMP avec coverage emoji.
        [SerializeField] private string _nymosIcon = "◆";
        [SerializeField] private string _shardsIcon = "◇";
        [Tooltip("Couleur du chiffre + icone Nymos (doré).")]
        [SerializeField] private Color _nymosColor = new Color(0.96f, 0.86f, 0.40f, 1f);
        [Tooltip("Couleur du chiffre + icone Shards (bleu glace).")]
        [SerializeField] private Color _shardsColor = new Color(0.55f, 0.88f, 1.00f, 1f);

        [Header("Debug")]
        [SerializeField] private bool _logVerbose;

        public static HubWalletWidget Instance { get; private set; }

        private NymoraApiClient _api;
        private int _nymos;
        private int _shards;
        private bool _initialFetchDone;

        public int Nymos => _nymos;
        public int Shards => _shards;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_backendSettings == null)
            {
                Debug.LogError("[WalletWidget] _backendSettings non assigne — widget desactive.");
                enabled = false;
                return;
            }
            _api = new NymoraApiClient(_backendSettings);
            ApplySnapshot(0, 0); // affichage initial 0/0
        }

        private void Start()
        {
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnWelcome += HandleWelcome;
                HubChatClient.Instance.OnWalletUpdate += HandleWalletUpdate;

                // Si WELCOME deja recu (Start arrive apres), fetch direct.
                if (!string.IsNullOrEmpty(HubChatClient.Instance.MyUserId))
                {
                    FetchAsync().Forget();
                }
            }
            else
            {
                Debug.LogWarning("[WalletWidget] HubChatClient.Instance null au Start — pas de live refresh.");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnWelcome -= HandleWelcome;
                HubChatClient.Instance.OnWalletUpdate -= HandleWalletUpdate;
            }
        }

        private void HandleWelcome(string sub, string email, string displayName)
        {
            // POLISH-7 (20 mai) : signature OnWelcome etendue avec displayName, non utilise ici.
            if (_initialFetchDone) return;
            FetchAsync().Forget();
        }

        private void HandleWalletUpdate(HubChatClient.WalletUpdateData data)
        {
            if (_logVerbose)
            {
                Debug.Log($"[WalletWidget] WS UPDATE nymos={data.Nymos} shards={data.Shards} delta={data.DeltaCurrency}{(data.DeltaAmount >= 0 ? "+" : "")}{data.DeltaAmount} reason={data.Reason}");
            }
            ApplySnapshot(data.Nymos, data.Shards);
        }

        private async UniTaskVoid FetchAsync()
        {
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning("[WalletWidget] DevToken vide — fetch skip.");
                return;
            }
            _api.SetBearerToken(token);
            var res = await _api.GetWalletMeAsync();
            if (!res.IsSuccess)
            {
                Debug.LogWarning($"[WalletWidget] GET /wallet/me failed: {res.StatusCode} {res.ErrorMessage}");
                return;
            }
            ApplySnapshot(res.Data.nymos, res.Data.shards);
            _initialFetchDone = true;
            if (_logVerbose) Debug.Log($"[WalletWidget] Initial fetch OK : {_nymos} Nymos / {_shards} Shards");
        }

        private void ApplySnapshot(int nymos, int shards)
        {
            _nymos = nymos;
            _shards = shards;
            if (_nymosLabel != null)
            {
                _nymosLabel.color = _nymosColor;
                _nymosLabel.text = $"{_nymosIcon} {nymos}";
            }
            if (_shardsLabel != null)
            {
                _shardsLabel.color = _shardsColor;
                _shardsLabel.text = $"{_shardsIcon} {shards}";
            }
        }
    }
}
