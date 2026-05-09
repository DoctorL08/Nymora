using UnityEngine;

namespace Nymora.Network.Backend
{
    /// <summary>
    /// URL du backend Nymora (Express + Postgres + Redis).
    ///
    /// Dev local : http://localhost:3000 (avec backend lance via "npm run dev" depuis backend/).
    /// Staging : https://api-dev.nymora.fr (Phase 1.13 — Hetzner).
    ///
    /// L'asset est cree via le menu : Nymora &gt; Setup &gt; Create Backend Settings.
    /// Il peut etre commit sans risque (la BaseUrl n'est pas un secret).
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Backend Settings", fileName = "NymoraBackendSettings", order = 201)]
    public class NymoraBackendSettings : ScriptableObject
    {
        [Header("URL backend Nymora")]
        [Tooltip("Ex: http://localhost:3000 en dev, https://api-dev.nymora.fr en staging.")]
        [SerializeField] private string _baseUrl = "http://localhost:3000";

        [Header("Timeouts")]
        [Tooltip("Timeout secondes pour les requetes HTTP. 0 = pas de timeout.")]
        [SerializeField] private int _timeoutSeconds = 15;

        public string BaseUrl => _baseUrl;
        public int TimeoutSeconds => _timeoutSeconds;
    }
}
