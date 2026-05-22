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
    /// Pilote la scene 00_Login devenue LAUNCHER (Brique L2).
    ///
    /// Au demarrage, le login est MASQUE. On interroge /version puis :
    ///   - A jour (compatible ET aucune MaJ dispo) -> "Votre version de Nymora est a jour" + on revele le login.
    ///   - MaJ disponible OU version trop vieille  -> panneau "Mise a jour requise pour jouer", login reste masque.
    ///     (Le bouton de telechargement sera cable en Brique L3 ; ici il est present mais inerte.)
    ///   - Backend injoignable -> message d'erreur, login revele quand meme (mode degrade, evite de bricker
    ///     le seul testeur si le serveur a un hoquet).
    ///
    /// Cable via l'Editor Script "Nymora &gt; Setup &gt; Create Login Scene" qui assigne toutes les references.
    /// </summary>
    public class LoginScreenController : MonoBehaviour
    {
        [Header("Backend")]
        [SerializeField] private NymoraBackendSettings _backendSettings;

        [Header("Login (groupe masque tant que pas a jour)")]
        [Tooltip("Conteneur de tout l'UI de login (champs + boutons). Active uniquement quand le client est a jour.")]
        [SerializeField] private GameObject _loginPanel;
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
        [Tooltip("Verdict de version PERSISTANT, affiche en haut (vert = a jour). Distinct du statut du bas.")]
        [SerializeField] private TMP_Text _versionVerdictText;

        [Header("Update Required panel")]
        [SerializeField] private GameObject _updateRequiredPanel;
        [SerializeField] private TMP_Text _updateRequiredText;
        [Tooltip("Bouton 'Telecharger la mise a jour'. Comportement cable en Brique L3.")]
        [SerializeField] private Button _downloadButton;
        [Tooltip("Image en mode Filled (Horizontal) servant de barre de progression. Remplie en L3.")]
        [SerializeField] private Image _progressBarFill;
        [Tooltip("Texte sous la barre (ex: '42 %'). Mis a jour en L3.")]
        [SerializeField] private TMP_Text _progressText;

        private NymoraApiClient _api;
        private AuthService _auth;
        private NymoraVersionClient _versionClient;
        private CancellationTokenSource _cts;
        private bool _versionLocked;

        // Memorise pour la Brique L3 (telechargement) : URL + hash du zip de la derniere version.
        private string _pendingDownloadUrl;
        private string _pendingSha256;

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

            // Etat initial du launcher : tout masque, on attend le check de version.
            if (_updateRequiredPanel != null) _updateRequiredPanel.SetActive(false);
            if (_loginPanel != null) _loginPanel.SetActive(false);
            if (_enterHubButton != null) _enterHubButton.gameObject.SetActive(false);
            if (_versionVerdictText != null) _versionVerdictText.text = string.Empty;
            ResetProgressBar();
        }

        private void OnEnable()
        {
            if (_loginButton != null) _loginButton.onClick.AddListener(OnLoginClicked);
            if (_registerButton != null) _registerButton.onClick.AddListener(OnRegisterClicked);
            if (_logoutButton != null) _logoutButton.onClick.AddListener(OnLogoutClicked);
            if (_connectPhotonButton != null) _connectPhotonButton.onClick.AddListener(OnConnectPhotonClicked);
            if (_enterHubButton != null) _enterHubButton.onClick.AddListener(OnEnterHubClicked);
            if (_downloadButton != null) _downloadButton.onClick.AddListener(OnDownloadClicked);
        }

        private void OnDisable()
        {
            if (_loginButton != null) _loginButton.onClick.RemoveListener(OnLoginClicked);
            if (_registerButton != null) _registerButton.onClick.RemoveListener(OnRegisterClicked);
            if (_logoutButton != null) _logoutButton.onClick.RemoveListener(OnLogoutClicked);
            if (_connectPhotonButton != null) _connectPhotonButton.onClick.RemoveListener(OnConnectPhotonClicked);
            if (_enterHubButton != null) _enterHubButton.onClick.RemoveListener(OnEnterHubClicked);
            if (_downloadButton != null) _downloadButton.onClick.RemoveListener(OnDownloadClicked);
        }

        private async void Start()
        {
            // Etape 1 : check version client vs serveur AVANT de reveler quoi que ce soit.
            SetStatus($"Verification de la version (client {GameVersion.Current})...");
            var versionCheck = await _versionClient.CheckAsync(_cts.Token);

            if (!versionCheck.IsReachable)
            {
                // Mode degrade : impossible de confirmer la version. On previent mais on
                // debloque le login pour ne pas bloquer le seul testeur si le serveur hoquette.
                SetVersionVerdict("Serveur de version injoignable (mode hors-ligne).", new Color(0.95f, 0.7f, 0.3f));
                SetStatus($"{versionCheck.ErrorMessage}\n(login debloque malgre tout)");
                RevealLogin();
                await TryResumeSessionAsync();
                return;
            }

            // Toute MaJ disponible (ou version trop vieille) verrouille le login : Kyami
            // doit etre a jour avant de jouer (anti-mismatch de version en PvP).
            bool updateRequired = !versionCheck.IsCompatible || versionCheck.IsUpdateAvailable;
            if (updateRequired)
            {
                ShowUpdateRequired(versionCheck.MinClientVersion, versionCheck.CurrentClientVersion,
                    versionCheck.DownloadUrl, versionCheck.Sha256);
                return;
            }

            // Etape 2 : a jour -> verdict vert PERSISTANT, on revele le login, on reprend la session.
            SetVersionVerdict("Votre version de Nymora est a jour.", new Color(0.4f, 0.85f, 0.5f));
            RevealLogin();
            await TryResumeSessionAsync();
        }

        /// <summary>Si un JWT est persiste, valide la session cote serveur et propose d'entrer dans le hub.</summary>
        private async UniTask TryResumeSessionAsync()
        {
            if (_versionLocked) return;

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
                    ShowUpdateRequired(null, null, _pendingDownloadUrl, _pendingSha256);
                }
                else
                {
                    SetStatus($"Token invalide ({me.StatusCode}) : {me.ErrorMessage}. Reconnecte-toi.");
                    _auth.Logout();
                }
            }
            else
            {
                SetStatus("Version a jour. Inscris-toi ou connecte-toi.");
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
                ShowUpdateRequired(null, null, _pendingDownloadUrl, _pendingSha256);
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
                ShowUpdateRequired(null, null, _pendingDownloadUrl, _pendingSha256);
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

        /// <summary>
        /// Comportement cable en Brique L3 (telechargement reel du zip + barre de progression).
        /// Pour l'instant : feedback honnete que le telechargement n'est pas encore branche.
        /// </summary>
        private void OnDownloadClicked()
        {
            NymoraLog.Info("Login", $"[L2] Clic Telecharger (url='{_pendingDownloadUrl}'). Telechargement branche en Brique L3.");
            SetStatus("Telechargement disponible a la prochaine etape (Brique L3).");
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

        /// <summary>Verdict de version persistant affiche en haut (ne se fait pas ecraser par le statut du bas).</summary>
        private void SetVersionVerdict(string s, Color color)
        {
            if (_versionVerdictText == null) return;
            _versionVerdictText.text = s;
            _versionVerdictText.color = color;
        }

        /// <summary>Affiche le groupe de login (champs + boutons).</summary>
        private void RevealLogin()
        {
            if (_loginPanel != null) _loginPanel.SetActive(true);
        }

        /// <summary>
        /// Affiche le panneau "Mise a jour requise pour jouer" et garde le login masque.
        /// Appele soit au demarrage (MaJ dispo / version trop vieille), soit sur un 426 en cours de session.
        /// </summary>
        private void ShowUpdateRequired(string minServerVersion, string currentServerVersion,
            string downloadUrl, string sha256)
        {
            _versionLocked = true;
            _pendingDownloadUrl = downloadUrl;
            _pendingSha256 = sha256;

            // Le panneau orange couvre l'ecran : on vide le verdict du haut pour eviter le doublon.
            if (_versionVerdictText != null) _versionVerdictText.text = string.Empty;
            if (_loginPanel != null) _loginPanel.SetActive(false);

            string msg;
            if (!string.IsNullOrEmpty(currentServerVersion))
            {
                msg = $"Version installee : {GameVersion.Current}\nDerniere version : {currentServerVersion}";
                if (!string.IsNullOrEmpty(minServerVersion))
                {
                    msg += $"\nVersion minimale : {minServerVersion}";
                }
            }
            else
            {
                msg = $"Ta version client ({GameVersion.Current}) doit etre mise a jour pour jouer.";
            }

            if (_updateRequiredText != null) _updateRequiredText.text = msg;
            if (_updateRequiredPanel != null) _updateRequiredPanel.SetActive(true);

            // Le bouton n'est cliquable que si une URL de telechargement existe cote serveur.
            bool hasDownload = !string.IsNullOrEmpty(downloadUrl);
            if (_downloadButton != null) _downloadButton.interactable = hasDownload;
            ResetProgressBar();

            SetStatus(hasDownload
                ? "Mise a jour requise pour jouer."
                : "Mise a jour requise, mais aucun lien de telechargement publie (contacte Lorenzo).");
        }

        /// <summary>Remet la barre de progression a zero et masque son texte.</summary>
        private void ResetProgressBar()
        {
            if (_progressBarFill != null) _progressBarFill.fillAmount = 0f;
            if (_progressText != null) _progressText.text = string.Empty;
        }
    }
}
