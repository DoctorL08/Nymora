using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.11 — Bouton hub "Clan". Toggle HubClanPanel + badge rouge sur le compte
    /// d'invitations clan reçues (subscribe à HubClanPanel.OnPendingInvitesChanged).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class HubClanButton : MonoBehaviour
    {
        [SerializeField] private GameObject _badge;
        [SerializeField] private TextMeshProUGUI _badgeText;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_badge != null) _badge.SetActive(false);
        }

        private void OnEnable()
        {
            if (_button != null) _button.onClick.AddListener(OnClicked);
        }

        private void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClicked);
        }

        private void Start()
        {
            if (HubClanPanel.Instance != null)
            {
                HubClanPanel.Instance.OnPendingInvitesChanged += HandlePendingChanged;
                HandlePendingChanged(HubClanPanel.Instance.PendingInvitesCount);
            }
        }

        private void OnDestroy()
        {
            if (HubClanPanel.Instance != null)
            {
                HubClanPanel.Instance.OnPendingInvitesChanged -= HandlePendingChanged;
            }
        }

        private void HandlePendingChanged(int count)
        {
            if (_badge == null) return;
            _badge.SetActive(count > 0);
            if (_badgeText != null) _badgeText.text = count.ToString();
        }

        private static void OnClicked()
        {
            if (HubClanPanel.Instance == null)
            {
                Debug.LogWarning("[ClanButton] HubClanPanel.Instance null — panel absent.");
                return;
            }
            HubClanPanel.Instance.Toggle();
        }
    }
}
