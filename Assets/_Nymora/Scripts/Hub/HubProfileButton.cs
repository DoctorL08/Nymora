using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.12 — Bouton "Mon profil" affiche dans le hub.
    /// Toggle l'ouverture/fermeture du HubProfilePanel via l'Instance singleton.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class HubProfileButton : MonoBehaviour
    {
        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (_button != null) _button.onClick.AddListener(OnClicked);
        }

        private void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClicked);
        }

        private static void OnClicked()
        {
            if (HubProfilePanel.Instance == null)
            {
                Debug.LogWarning("[ProfileButton] HubProfilePanel.Instance null — panel absent dans la scene.");
                return;
            }
            HubProfilePanel.Instance.Toggle();
        }
    }
}
