using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 5.3.f.bis — Bouton "Arene" dans le hub. Toggle le HubArenaPanel.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class HubArenaButton : MonoBehaviour
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
            if (HubArenaPanel.Instance == null)
            {
                Debug.LogWarning("[ArenaButton] HubArenaPanel.Instance null — lance Nymora > Setup > Patch Arena Panel.");
                return;
            }
            HubArenaPanel.Instance.Toggle();
        }
    }
}
