using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>Brique 5.8.b — Bouton hub qui ouvre le panneau Quêtes.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class HubQuestsButton : MonoBehaviour
    {
        private void Start()
        {
            GetComponent<Button>().onClick.AddListener(() => HubQuestsPanel.Instance?.Open());
        }
    }
}
