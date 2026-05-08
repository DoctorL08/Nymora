using UnityEngine;

namespace Nymora.Network
{
    /// <summary>
    /// AppIds Photon (Quantum 3 + Fusion 2) — recuperes depuis dashboard.photonengine.com.
    ///
    /// IMPORTANT — securite :
    /// L'asset .asset genere a partir de ce SO est explicitement EXCLU du repo via .gitignore.
    /// Ne jamais committer un asset rempli — meme sur repo prive, eviter le leak.
    /// Si l'AppId fuit : invalider via le dashboard et regenerer.
    ///
    /// Workflow :
    ///  1. Menu Nymora &gt; Setup &gt; Create Photon App Settings
    ///  2. Inspector → coller QuantumAppId et FusionAppId
    ///  3. L'asset reste local (gitignored) — chaque dev/machine a sa propre copie
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Photon App Settings", fileName = "PhotonAppSettings", order = 200)]
    public class PhotonAppSettings : ScriptableObject
    {
        [Header("Photon AppIds (depuis dashboard.photonengine.com)")]

        [Tooltip("AppId de l'app Photon Quantum 3 (combat ranked).")]
        [SerializeField] private string _quantumAppId;

        [Tooltip("AppId de l'app Photon Fusion 2 (hub/chat/social).")]
        [SerializeField] private string _fusionAppId;

        [Header("Region prioritaire")]
        [Tooltip("Code region Photon prioritaire (ex : eu, us, asia, jp). Vide = auto-best-region.")]
        [SerializeField] private string _preferredRegion = "eu";

        public string QuantumAppId => _quantumAppId;
        public string FusionAppId => _fusionAppId;
        public string PreferredRegion => _preferredRegion;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_quantumAppId) &&
            !string.IsNullOrWhiteSpace(_fusionAppId);
    }
}
