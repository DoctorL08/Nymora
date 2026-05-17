using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 5.3.d — Bouton "Decks" affiche dans le hub (bas-droite, a cote de Profil/Amis/Clan).
    /// Toggle l'ouverture/fermeture du HubDeckBuilderPanel via l'Instance singleton.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class HubDeckBuilderButton : MonoBehaviour
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
            if (HubDeckBuilderPanel.Instance == null)
            {
                Debug.LogWarning("[DeckBuilderButton] HubDeckBuilderPanel.Instance null — panel absent dans la scene.");
                return;
            }
            HubDeckBuilderPanel.Instance.Toggle();
        }
    }
}
