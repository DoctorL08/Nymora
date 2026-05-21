using System.Threading;
using Cysharp.Threading.Tasks;
using Nymora.Core.Data;
using Nymora.Core.Logging;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nymora.UI.Login
{
    /// <summary>
    /// Pilote la scene 00_Login : 3 champs (email, password, displayName), 3 boutons (Login, Register, Logout)
    /// et un texte de statut. Au demarrage, si un JWT est persiste, fait un /me pour valider la session.
    ///
    /// Cable via l'Editor Script "Nymora &gt; Setup &gt; Create Login Scene" qui assigne toutes les references.
    /// </summary>
    public class LoginScreenController : MonoBehaviour
    {
        [Header("Backend")]
        [SerializeField] private NymoraBackendSettings _backendSettings;

        [Header("Inputs")]
        [SerializeField] private TMP_InputField _emailInput;
        [SerializeField] private TMP_InputField _passwordInput;
        [SerializeField] private TMP_InputField _displayNameInput;

        [Header("Buttons")]
        [SerializeField] private Button _loginButton;
        [SerializeField] private Button _registerButton;
        [SerializeField] private Button _logoutButton;
        [SerializeField] private Button _connectPhotonButton;
        [SerializeField] private Button _enterHubButton;

        [Header("Hub")]
        [Tooltip("Nom de la scene hub chargee au clic sur 'Entrer dans le hub'.")]
        [SerializeField] private string _hubSceneName = "10_CommunityHub";

        [Header("Photon")]
        [SerializeField] private PhotonConnectionTester _photonTester;

        [Header("Status")]
        [SerializeField] private TMP_Text _statusText;

        [Header("Update Required panel")]
        [SerializeField] private GameObject _updateRequiredPanel;
        [SerializeField] private TMP_Text _updateRequiredText;

        private NymoraApiClient _api;
        private AuthService _auth;
        private NymoraVersionClient _versionClient;
        private CancellationTokenSource _cts;
        private bool _versionLocked;

        private void Awake()
        {
            if (_backendSettings == null)
            {
                NymoraLog.Critical("Login", "NymoraBackendSettings non assigne dans l'Inspector.");
                enabled = false;
                return;
            }

            _api = new NymoraApiClient(_backendSettings);
            _auth = new AuthService(_api);
            _versionClient = new NymoraVersionClient(_api);
            _cts = new CancellationTokenSource();

            if (_updateRequiredPanel != null) _updateRequiredPanel.SetActive(false);
            if (_enterHubButton != null) _enterHubButton.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_loginButton != null) _loginButton.onClick.AddListener(OnLoginClicked);
            if (_registerButton != null) _registerButton.onClick.AddListener(OnRegisterClicked);
            if (_logoutButton != null) _logoutButton.onClick.AddListener(OnLogoutClicked);
            if (_connectPhotonButton != null) _connectPhotonButton.onClick.AddListener(OnConnectPhotonClicked);
            if (_enterHubButton != null) _enterHubButton.onClick.AddListener(OnEnterHubClicked);
        }

        private void OnDisable()
        {
            if (_loginButton != null) _loginButton.onClick.RemoveListener(OnLoginClicked);
            if (_registerButton != null) _registerButton.onClick.RemoveListener(OnRegisterClicked);
            if (_logoutButton != null) _logoutButton.onClick.RemoveListener(OnLogoutClicked);
            if (_connectPhotonButton != null) _connectPhotonButton.onClick.RemoveListener(OnConnectPhotonClicked);
            if (_enterHubButton != null) _enterHubButton.onClick.RemoveListener(OnEnterHubClicked);
        }

        private async void Start()
        {
            // Etape 1 : check version client vs serveur AVANT toute autre requete.
            SetStatus("Verification de la version client...");
            var versionCheck = await _versionClient.CheckAsync(_cts.Token);

            if (!versionCheck.IsReachable)
            {
                SetStatus($"Backend injoignable : {versionCheck.ErrorMessage}");
                return;
            }

            if (!versionCheck.IsCompatible)
            {
                LockUiForUpdate(versionCheck.MinClientVersion, versionCheck.CurrentClientVersion);
                return;
            }

            if (versionCheck.IsUpdateAvailable)
            {
                NymoraLog.Info("Login", $"Mise a jour disponible : client={GameVersion.Current}, serveur={versionCheck.CurrentClientVersion}");
            }

            // Etape 2 : flow normal (verification de session si token persiste).
            if (_auth.IsLoggedIn)
            {
                SetStatus("Token detecte, verification cote serveur...");
                var me = await _auth.GetMeAsync(_cts.Token);
                if (me.IsSuccess)
                {
                    SetStatus($"Connecte : {me.Data.displayName} ({me.Data.email})");
                    ShowEnterHub();
                }
                else if (me.StatusCode == 426)
                {
                    LockUiForUpdate(versionCheck.MinClientVersion, versionCheck.CurrentClientVersion);
                }
                else
                {
                    SetStatus($"Token invalide ({me.StatusCode}) : {me.ErrorMessage}. Reconnecte-toi.");
                    _auth.Logout();
                }
            }
            else
            {
                SetStatus("Aucune session active. Inscris-toi ou connecte-toi.");
            }
        }

        private void OnDestroy()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }

        private async void OnLoginClicked()
        {
            if (_versionLocked) return;
            SetStatus("Connexion...");
            var res = await _auth.LoginAsync(_emailInput.text, _passwordInput.text, _cts.Token);
            if (res.IsSuccess)
            {
                NymoraLog.Info("Login", $"JWT recu (longueur {res.Data.token.Length}).");
                SetStatus($"Connecte : {res.Data.user.displayName}");
                ShowEnterHub();
            }
            else if (res.StatusCode == 426)
            {
                LockUiForUpdate(null, null);
            }
            else
            {
                SetStatus($"Echec login ({res.StatusCode}) : {res.ErrorMessage}");
            }
        }

        private async void OnRegisterClicked()
        {
            if (_versionLocked) return;
            SetStatus("Inscription...");
            var res = await _auth.RegisterAsync(
                _emailInput.text, _passwordInput.text, _displayNameInput.text, _cts.Token);
            if (res.IsSuccess)
            {
                NymoraLog.Info("Login", $"JWT recu (longueur {res.Data.token.Length}).");
                SetStatus($"Inscrit + connecte : {res.Data.user.displayName}");
                ShowEnterHub();
            }
            else if (res.StatusCode == 426)
            {
                LockUiForUpdate(null, null);
            }
            else
            {
                SetStatus($"Echec register ({res.StatusCode}) : {res.ErrorMessage}");
            }
        }

        private void OnLogoutClicked()
        {
            _auth.Logout();
            SetStatus("Deconnecte.");
            if (_enterHubButton != null) _enterHubButton.gameObject.SetActive(false);
        }

        private void OnEnterHubClicked()
        {
            if (!_auth.IsLoggedIn)
            {
                SetStatus("Connecte-toi avant d'entrer dans le hub.");
                return;
            }
            NymoraLog.Info("Login", $"Chargement de la scene hub '{_hubSceneName}'...");
            SceneManager.LoadScene(_hubSceneName);
        }

        private void ShowEnterHub()
        {
            if (_enterHubButton != null) _enterHubButton.gameObject.SetActive(true);
        }

        private async void OnConnectPhotonClicked()
        {
            if (!_auth.IsLoggedIn)
            {
                SetStatus("Connecte-toi (Login ou Register) avant de tester Photon.");
                return;
            }
            if (_photonTester == null)
            {
                SetStatus("PhotonConnectionTester non assigne dans l'Inspector.");
                return;
            }

            SetStatus("Connexion Photon (Custom Auth via webhook backend)...");
            var result = await _photonTester.TestConnectAsync(_auth.Token, _cts.Token);

            if (result.IsSuccess)
            {
                SetStatus($"Photon OK ! Region={result.Region} UserId={result.UserId}");
                NymoraLog.Info("Login", $"Photon Custom Auth validee. Region={result.Region}, UserId={result.UserId}");
            }
            else
            {
                SetStatus($"Photon refuse : {result.FailureMessage}");
                NymoraLog.Warn("Login", $"Photon connection failed: {result.FailureMessage}");
            }
        }

        private void SetStatus(string s)
        {
            if (_statusText != null) _statusText.text = s;
            NymoraLog.Info("Login", s);
        }

        /// <summary>
        /// Affiche le panel "Mise a jour requise" et desactive les boutons.
        /// Appele soit au demarrage (version client &lt; min serveur), soit si une requete
        /// HTTP retourne 426 en cours de session (pas censé arriver mais defense-in-depth).
        /// </summary>
        private void LockUiForUpdate(string minServerVersion, string currentServerVersion)
        {
            _versionLocked = true;

            if (_loginButton != null) _loginButton.interactable = false;
            if (_registerButton != null) _registerButton.interactable = false;
            if (_logoutButton != null) _logoutButton.interactable = false;
            if (_connectPhotonButton != null) _connectPhotonButton.interactable = false;

            string msg;
            if (!string.IsNullOrEmpty(minServerVersion))
            {
                msg = $"Mise a jour requise.\n\nVersion installee : {GameVersion.Current}\nVersion minimale : {minServerVersion}\nDerniere version : {currentServerVersion}";
            }
            else
            {
                msg = $"Mise a jour requise.\n\nTa version client ({GameVersion.Current}) n'est plus supportee.";
            }

            if (_updateRequiredText != null) _updateRequiredText.text = msg;
            if (_updateRequiredPanel != null) _updateRequiredPanel.SetActive(true);

            SetStatus("Client trop ancien : mise a jour requise.");
        }
    }
}
