using System.Threading;
using Cysharp.Threading.Tasks;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
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

        [Header("Photon")]
        [SerializeField] private PhotonConnectionTester _photonTester;

        [Header("Status")]
        [SerializeField] private TMP_Text _statusText;

        private NymoraApiClient _api;
        private AuthService _auth;
        private CancellationTokenSource _cts;

        private void Awake()
        {
            if (_backendSettings == null)
            {
                Debug.LogError("[Nymora.Login] NymoraBackendSettings non assigne dans l'Inspector.");
                enabled = false;
                return;
            }

            _api = new NymoraApiClient(_backendSettings);
            _auth = new AuthService(_api);
            _cts = new CancellationTokenSource();
        }

        private void OnEnable()
        {
            if (_loginButton != null) _loginButton.onClick.AddListener(OnLoginClicked);
            if (_registerButton != null) _registerButton.onClick.AddListener(OnRegisterClicked);
            if (_logoutButton != null) _logoutButton.onClick.AddListener(OnLogoutClicked);
            if (_connectPhotonButton != null) _connectPhotonButton.onClick.AddListener(OnConnectPhotonClicked);
        }

        private void OnDisable()
        {
            if (_loginButton != null) _loginButton.onClick.RemoveListener(OnLoginClicked);
            if (_registerButton != null) _registerButton.onClick.RemoveListener(OnRegisterClicked);
            if (_logoutButton != null) _logoutButton.onClick.RemoveListener(OnLogoutClicked);
            if (_connectPhotonButton != null) _connectPhotonButton.onClick.RemoveListener(OnConnectPhotonClicked);
        }

        private async void Start()
        {
            if (_auth.IsLoggedIn)
            {
                SetStatus("Token detecte, verification cote serveur...");
                var me = await _auth.GetMeAsync(_cts.Token);
                if (me.IsSuccess)
                {
                    SetStatus($"Connecte : {me.Data.displayName} ({me.Data.email})");
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
            SetStatus("Connexion...");
            var res = await _auth.LoginAsync(_emailInput.text, _passwordInput.text, _cts.Token);
            if (res.IsSuccess)
            {
                Debug.Log($"[Nymora.Login] JWT recu (longueur {res.Data.token.Length}).");
                SetStatus($"Connecte : {res.Data.user.displayName}");
            }
            else
            {
                SetStatus($"Echec login ({res.StatusCode}) : {res.ErrorMessage}");
            }
        }

        private async void OnRegisterClicked()
        {
            SetStatus("Inscription...");
            var res = await _auth.RegisterAsync(
                _emailInput.text, _passwordInput.text, _displayNameInput.text, _cts.Token);
            if (res.IsSuccess)
            {
                Debug.Log($"[Nymora.Login] JWT recu (longueur {res.Data.token.Length}).");
                SetStatus($"Inscrit + connecte : {res.Data.user.displayName}");
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
                Debug.Log($"[Nymora.Login] Photon Custom Auth validee. Region={result.Region}, UserId={result.UserId}");
            }
            else
            {
                SetStatus($"Photon refuse : {result.FailureMessage}");
                Debug.LogWarning($"[Nymora.Login] Photon connection failed: {result.FailureMessage}");
            }
        }

        private void SetStatus(string s)
        {
            if (_statusText != null) _statusText.text = s;
            Debug.Log($"[Nymora.Login] {s}");
        }
    }
}
