using System.Threading;
using Cysharp.Threading.Tasks;
using Nymora.Core.Data;
using Nymora.Core.Logging;
using Nymora.Core.SceneFlow;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nymora.UI.Login
{
    /// <summary>
    /// Pilote la scene 00_Login : launcher (check de version) + ecran de connexion epure.
    ///
    /// Demarrage -> check /version :
    ///   - MaJ dispo / version trop vieille -> panneau "Mise a jour requise pour jouer" (cf Briques L1-L4).
    ///   - A jour -> verdict vert + on revele le panneau CONNEXION (pseudo + mot de passe + bouton Connexion).
    ///
    /// Connexion = login par pseudo PUIS entree directe dans le hub.
    /// Le dernier pseudo utilise est memorise (PlayerPrefs) et pre-rempli.
    /// Le bouton "S'inscrire" ouvre un panneau INSCRIPTION separe (email + pseudo + mdp + confirmation) ;
    /// apres inscription reussie, on revient a l'ecran connexion avec le pseudo pre-rempli.
    ///
    /// Cable via "Nymora &gt; Setup &gt; Refine Login Form".
    /// </summary>
    public class LoginScreenController : MonoBehaviour
    {
        private const string LastPseudoKey = "nymora.auth.lastPseudo";
        private const string RememberKey = "nymora.auth.remember";

        [Header("Backend")]
        [SerializeField] private NymoraBackendSettings _backendSettings;

        [Header("Connexion (panneau, masque tant que pas a jour)")]
        [SerializeField] private GameObject _loginPanel;
        [SerializeField] private TMP_InputField _pseudoInput;
        [SerializeField] private TMP_InputField _passwordInput;
        [SerializeField] private Button _connexionButton;
        [SerializeField] private Button _openRegisterButton;
        [Tooltip("Case 'Memoriser mes identifiants' : si cochee, la session (token JWT) est reprise au prochain lancement.")]
        [SerializeField] private Toggle _rememberToggle;

        [Header("Inscription (panneau separe)")]
        [SerializeField] private GameObject _registerPanel;
        [SerializeField] private TMP_InputField _regEmailInput;
        [SerializeField] private TMP_InputField _regPseudoInput;
        [SerializeField] private TMP_InputField _regPasswordInput;
        [SerializeField] private TMP_InputField _regConfirmInput;
        [SerializeField] private Button _createAccountButton;
        [SerializeField] private Button _backToLoginButton;

        [Header("Hub")]
        [Tooltip("Scene hub chargee apres connexion reussie.")]
        [SerializeField] private string _hubSceneName = "10_CommunityHub";

        [Header("Status")]
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _versionVerdictText;

        [Header("Update Required panel")]
        [SerializeField] private GameObject _updateRequiredPanel;
        [SerializeField] private TMP_Text _updateRequiredText;
        [SerializeField] private Button _downloadButton;
        [SerializeField] private Image _progressBarFill;
        [SerializeField] private TMP_Text _progressText;

        private NymoraApiClient _api;
        private AuthService _auth;
        private NymoraVersionClient _versionClient;
        private LauncherUpdateService _updateService;
        private CancellationTokenSource _cts;
        private bool _versionLocked;
        private bool _downloading;
        private bool _busy;
        private bool _rememberedSession;

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
            _updateService = new LauncherUpdateService();
            _cts = new CancellationTokenSource();

            if (_updateRequiredPanel != null) _updateRequiredPanel.SetActive(false);
            if (_loginPanel != null) _loginPanel.SetActive(false);
            if (_registerPanel != null) _registerPanel.SetActive(false);
            if (_versionVerdictText != null) _versionVerdictText.text = string.Empty;
            ResetProgressBar();
        }

        private void OnEnable()
        {
            if (_connexionButton != null) _connexionButton.onClick.AddListener(OnConnexionClicked);
            if (_openRegisterButton != null) _openRegisterButton.onClick.AddListener(OnOpenRegisterClicked);
            if (_createAccountButton != null) _createAccountButton.onClick.AddListener(OnCreateAccountClicked);
            if (_backToLoginButton != null) _backToLoginButton.onClick.AddListener(OnBackToLoginClicked);
            if (_downloadButton != null) _downloadButton.onClick.AddListener(OnDownloadClicked);
        }

        private void OnDisable()
        {
            if (_connexionButton != null) _connexionButton.onClick.RemoveListener(OnConnexionClicked);
            if (_openRegisterButton != null) _openRegisterButton.onClick.RemoveListener(OnOpenRegisterClicked);
            if (_createAccountButton != null) _createAccountButton.onClick.RemoveListener(OnCreateAccountClicked);
            if (_backToLoginButton != null) _backToLoginButton.onClick.RemoveListener(OnBackToLoginClicked);
            if (_downloadButton != null) _downloadButton.onClick.RemoveListener(OnDownloadClicked);
        }

        private async void Start()
        {
            SetStatus($"Verification de la version (client {GameVersion.Current})...");
            var versionCheck = await _versionClient.CheckAsync(_cts.Token);

            if (!versionCheck.IsReachable)
            {
                SetVersionVerdict("Serveur de version injoignable (mode hors-ligne).", new Color(0.95f, 0.7f, 0.3f));
                SetStatus($"{versionCheck.ErrorMessage}\n(connexion debloquee malgre tout)");
                RevealLogin();
                return;
            }

            bool updateRequired = !versionCheck.IsCompatible || versionCheck.IsUpdateAvailable;
            if (updateRequired)
            {
                ShowUpdateRequired(versionCheck.MinClientVersion, versionCheck.CurrentClientVersion,
                    versionCheck.DownloadUrl, versionCheck.Sha256);
                return;
            }

            SetVersionVerdict("Votre version de Nymora est a jour.", new Color(0.4f, 0.85f, 0.5f));
            SetStatus(string.Empty); // efface "Verification de la version..."
            TryResumeOrReveal();
        }

        /// <summary>
        /// Si "Memoriser mes identifiants" etait coche ET qu'un token JWT est present, on prepare
        /// une reprise de session en 1 clic (pseudo pre-rempli, mdp masque "memorise") : le bouton
        /// Connexion validera le token via /me. Sinon, login classique (et on purge un token
        /// residuel d'une session non memorisee).
        /// </summary>
        private void TryResumeOrReveal()
        {
            bool remember = PlayerPrefs.GetInt(RememberKey, 0) == 1;
            if (remember && _auth.IsLoggedIn)
            {
                _rememberedSession = true;
                RevealLogin();
                SetStatus("Identifiants memorises — clique sur Connexion.");
            }
            else
            {
                if (!remember) _auth.Logout(); // une session non memorisee ne doit pas survivre au relancement
                _rememberedSession = false;
                RevealLogin();
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

        private void Update()
        {
            // Entree (clavier + pave numerique) = valide le panneau actif, comme un clic
            // sur son bouton principal. Les handlers gerent eux-memes les gardes busy/version.
            if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)) return;
            if (_versionLocked || _busy) return;

            if (_registerPanel != null && _registerPanel.activeInHierarchy)
                OnCreateAccountClicked();
            else if (_loginPanel != null && _loginPanel.activeInHierarchy)
                OnConnexionClicked();
        }

        // ---------- Connexion ----------

        private async void OnConnexionClicked()
        {
            if (_versionLocked || _busy) return;

            string pseudo = _pseudoInput != null ? _pseudoInput.text.Trim() : string.Empty;
            string password = _passwordInput != null ? _passwordInput.text : string.Empty;

            // Reprise de session memorisee : pseudo connu, mdp non saisi, token present.
            // On valide le token via /me avant d'entrer dans le hub.
            if (_rememberedSession && string.IsNullOrEmpty(password) && _auth.IsLoggedIn)
            {
                await ResumeSessionAsync();
                return;
            }

            if (string.IsNullOrEmpty(pseudo) || string.IsNullOrEmpty(password))
            {
                SetStatus("Renseigne ton pseudo et ton mot de passe.");
                return;
            }

            _busy = true;
            SetButtonsInteractable(false);
            SetStatus("Connexion...");

            var res = await _auth.LoginAsync(pseudo, password, _cts.Token);

            if (res.IsSuccess)
            {
                SaveLastPseudo(res.Data.user != null ? res.Data.user.displayName : pseudo);
                SaveRememberPreference(_rememberToggle != null && _rememberToggle.isOn);
                SetStatus($"Connecte : {res.Data.user.displayName}. Entree dans le hub...");
                NymoraLog.Info("Login", $"Connexion OK ({res.Data.user.displayName}) -> {_hubSceneName}");
                // waitForReady : garde le voile jusqu'à ce que les persos soient visibles (HubReadyBeacon).
                SceneTransition.Load(_hubSceneName, waitForReady: true);
                return;
            }

            _busy = false;
            SetButtonsInteractable(true);
            if (res.StatusCode == 426)
            {
                ShowUpdateRequired(null, null, _pendingDownloadUrl, _pendingSha256);
            }
            else if (res.StatusCode == 401)
            {
                SetStatus("Pseudo ou mot de passe incorrect.");
            }
            else
            {
                SetStatus($"Echec connexion ({res.StatusCode}) : {res.ErrorMessage}");
            }
        }

        /// <summary>Valide le token JWT memorise via /me ; succes -> hub, echec -> retour login mot de passe.</summary>
        private async UniTask ResumeSessionAsync()
        {
            _busy = true;
            SetButtonsInteractable(false);
            SetStatus("Reconnexion...");

            var me = await _auth.GetMeAsync(_cts.Token);
            if (me.IsSuccess)
            {
                string name = me.Data != null ? me.Data.displayName : string.Empty;
                SaveLastPseudo(name);
                SaveRememberPreference(_rememberToggle == null || _rememberToggle.isOn);
                SetStatus($"Reconnecte : {name}. Entree dans le hub...");
                NymoraLog.Info("Login", $"Reprise session memorisee ({name}) -> {_hubSceneName}");
                // waitForReady : garde le voile jusqu'à ce que les persos soient visibles (HubReadyBeacon).
                SceneTransition.Load(_hubSceneName, waitForReady: true);
                return;
            }

            // Token expire / invalide : on repasse en login classique.
            _auth.Logout();
            _rememberedSession = false;
            SaveRememberPreference(false);
            ResetPasswordPlaceholder();
            _busy = false;
            SetButtonsInteractable(true);
            SetStatus("Session expiree — retape ton mot de passe.");
        }

        // ---------- Inscription ----------

        private void OnOpenRegisterClicked()
        {
            if (_versionLocked || _busy) return;
            if (_loginPanel != null) _loginPanel.SetActive(false);
            if (_registerPanel != null) _registerPanel.SetActive(true);
            SetStatus("Inscription : remplis le formulaire.");
        }

        private void OnBackToLoginClicked()
        {
            if (_busy) return;
            if (_registerPanel != null) _registerPanel.SetActive(false);
            RevealLogin();
            SetStatus(string.Empty);
        }

        private async void OnCreateAccountClicked()
        {
            if (_busy) return;

            string email = _regEmailInput != null ? _regEmailInput.text.Trim() : string.Empty;
            string pseudo = _regPseudoInput != null ? _regPseudoInput.text.Trim() : string.Empty;
            string password = _regPasswordInput != null ? _regPasswordInput.text : string.Empty;
            string confirm = _regConfirmInput != null ? _regConfirmInput.text : string.Empty;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pseudo) || string.IsNullOrEmpty(password))
            {
                SetStatus("Remplis email, pseudo et mot de passe.");
                return;
            }
            if (password.Length < 8)
            {
                SetStatus("Le mot de passe doit faire au moins 8 caracteres.");
                return;
            }
            if (password != confirm)
            {
                SetStatus("Les deux mots de passe ne correspondent pas.");
                return;
            }

            _busy = true;
            SetButtonsInteractable(false);
            SetStatus("Creation du compte...");

            var res = await _auth.RegisterAsync(email, password, pseudo, _cts.Token);

            _busy = false;
            SetButtonsInteractable(true);

            if (res.IsSuccess)
            {
                // Decision : on ne file pas direct dans le hub, on revient a la connexion.
                _auth.Logout(); // on ne garde pas la session ouverte par l'inscription
                SaveLastPseudo(pseudo);
                if (_registerPanel != null) _registerPanel.SetActive(false);
                RevealLogin();
                SetStatus($"Compte '{pseudo}' cree. Connecte-toi.");
                NymoraLog.Info("Login", $"Inscription OK ({pseudo}).");
            }
            else if (res.StatusCode == 426)
            {
                ShowUpdateRequired(null, null, _pendingDownloadUrl, _pendingSha256);
            }
            else if (res.StatusCode == 409)
            {
                SetStatus($"Deja pris : {res.ErrorMessage}");
            }
            else
            {
                SetStatus($"Echec inscription ({res.StatusCode}) : {res.ErrorMessage}");
            }
        }

        // ---------- Launcher (telechargement + install) ----------

        private async void OnDownloadClicked()
        {
            if (_downloading) return;
            if (string.IsNullOrEmpty(_pendingDownloadUrl))
            {
                SetStatus("Aucun lien de telechargement disponible.");
                return;
            }

            _downloading = true;
            if (_downloadButton != null) _downloadButton.interactable = false;
            ResetProgressBar();
            SetStatus("Telechargement de la mise a jour...");

            var progress = new System.Progress<float>(OnDownloadProgress);
            var result = await _updateService.DownloadAndVerifyAsync(
                _pendingDownloadUrl, _pendingSha256, progress, _cts.Token);

            _downloading = false;

            if (result.IsSuccess)
            {
                if (_progressBarFill != null) _progressBarFill.fillAmount = 1f;
                if (_progressText != null) _progressText.text = "100 %";
                NymoraLog.Info("Login", $"MaJ telechargee : {result.FilePath}");

                var install = LauncherInstaller.StartInstall(result.FilePath);
                if (install.Started)
                {
                    SetStatus("Mise a jour en cours, le jeu va redemarrer...");
                    await UniTask.Delay(800, cancellationToken: _cts.Token);
                    Application.Quit();
                }
                else
                {
                    if (_downloadButton != null) _downloadButton.interactable = true;
                    SetStatus($"Telechargement OK ({result.FilePath}).\n{install.Message}");
                }
            }
            else
            {
                ResetProgressBar();
                if (_downloadButton != null) _downloadButton.interactable = true;
                SetStatus($"Echec du telechargement : {result.ErrorMessage}");
                NymoraLog.Warn("Login", $"Echec MaJ : {result.ErrorMessage}");
            }
        }

        private void OnDownloadProgress(float p)
        {
            if (_progressBarFill != null) _progressBarFill.fillAmount = p;
            if (_progressText != null) _progressText.text = $"{Mathf.RoundToInt(p * 100f)} %";
        }

        // ---------- Helpers UI ----------

        /// <summary>Affiche le panneau connexion (et masque l'inscription), pre-remplit le dernier pseudo.</summary>
        private void RevealLogin()
        {
            if (_registerPanel != null) _registerPanel.SetActive(false);
            if (_loginPanel != null) _loginPanel.SetActive(true);

            if (_pseudoInput != null)
            {
                string last = PlayerPrefs.GetString(LastPseudoKey, string.Empty);
                if (!string.IsNullOrEmpty(last)) _pseudoInput.text = last;
            }
            if (_rememberToggle != null) _rememberToggle.isOn = PlayerPrefs.GetInt(RememberKey, 0) == 1;
            if (_passwordInput != null) _passwordInput.text = string.Empty;
            ResetPasswordPlaceholder();
        }

        /// <summary>En session memorisee, le placeholder du mdp indique que la session sera reprise (pas de mdp a saisir).</summary>
        private void ResetPasswordPlaceholder()
        {
            if (_passwordInput == null) return;
            if (_passwordInput.placeholder is TMP_Text ph)
                ph.text = _rememberedSession ? "•••••••• (memorise)" : "Mot de passe";
        }

        private void SaveRememberPreference(bool remember)
        {
            // On stocke uniquement la PREFERENCE ; le token JWT reste en PlayerPrefs pour CETTE
            // session (le hub le relit a son demarrage). Une session non memorisee est purgee au
            // prochain lancement par TryResumeOrReveal (-> _auth.Logout()).
            PlayerPrefs.SetInt(RememberKey, remember ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ShowUpdateRequired(string minServerVersion, string currentServerVersion,
            string downloadUrl, string sha256)
        {
            _versionLocked = true;
            _pendingDownloadUrl = downloadUrl;
            _pendingSha256 = sha256;

            if (_versionVerdictText != null) _versionVerdictText.text = string.Empty;
            if (_loginPanel != null) _loginPanel.SetActive(false);
            if (_registerPanel != null) _registerPanel.SetActive(false);

            string msg;
            if (!string.IsNullOrEmpty(currentServerVersion))
            {
                msg = $"Version installee : {GameVersion.Current}\nDerniere version : {currentServerVersion}";
                if (!string.IsNullOrEmpty(minServerVersion)) msg += $"\nVersion minimale : {minServerVersion}";
            }
            else
            {
                msg = $"Ta version client ({GameVersion.Current}) doit etre mise a jour pour jouer.";
            }

            if (_updateRequiredText != null) _updateRequiredText.text = msg;
            if (_updateRequiredPanel != null) _updateRequiredPanel.SetActive(true);

            bool hasDownload = !string.IsNullOrEmpty(downloadUrl);
            if (_downloadButton != null) _downloadButton.interactable = hasDownload;
            ResetProgressBar();

            SetStatus(hasDownload
                ? "Mise a jour requise pour jouer."
                : "Mise a jour requise, mais aucun lien de telechargement publie (contacte Lorenzo).");
        }

        private void SetButtonsInteractable(bool on)
        {
            if (_connexionButton != null) _connexionButton.interactable = on;
            if (_openRegisterButton != null) _openRegisterButton.interactable = on;
            if (_createAccountButton != null) _createAccountButton.interactable = on;
            if (_backToLoginButton != null) _backToLoginButton.interactable = on;
        }

        private void SaveLastPseudo(string pseudo)
        {
            if (string.IsNullOrEmpty(pseudo)) return;
            PlayerPrefs.SetString(LastPseudoKey, pseudo);
            PlayerPrefs.Save();
        }

        private void SetStatus(string s)
        {
            if (_statusText != null) _statusText.text = s;
            if (!string.IsNullOrEmpty(s)) NymoraLog.Info("Login", s);
        }

        private void SetVersionVerdict(string s, Color color)
        {
            if (_versionVerdictText == null) return;
            _versionVerdictText.text = s;
            _versionVerdictText.color = color;
        }

        private void ResetProgressBar()
        {
            if (_progressBarFill != null) _progressBarFill.fillAmount = 0f;
            if (_progressText != null) _progressText.text = string.Empty;
        }
    }
}
