using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.10 — Bouton hub "Amis". Toggle HubFriendsPanel + badge rouge sur le compte
    /// de demandes pending incoming (souscrit à HubFriendsPanel.OnPendingCountChanged).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class HubFriendsButton : MonoBehaviour
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
            if (HubFriendsPanel.Instance != null)
            {
                HubFriendsPanel.Instance.OnPendingCountChanged += HandlePendingCountChanged;
                HandlePendingCountChanged(HubFriendsPanel.Instance.PendingIncomingCount);
            }
        }

        private void OnDestroy()
        {
            if (HubFriendsPanel.Instance != null)
            {
                HubFriendsPanel.Instance.OnPendingCountChanged -= HandlePendingCountChanged;
            }
        }

        private void HandlePendingCountChanged(int count)
        {
            if (_badge == null) return;
            _badge.SetActive(count > 0);
            if (_badgeText != null) _badgeText.text = count.ToString();
        }

        private static void OnClicked()
        {
            if (HubFriendsPanel.Instance == null)
            {
                Debug.LogWarning("[FriendsButton] HubFriendsPanel.Instance null — panel absent.");
                return;
            }
            HubFriendsPanel.Instance.Toggle();
        }
    }
}
