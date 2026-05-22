using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>Brique 5.5.c — Bouton hub qui ouvre le panneau Boutique.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class HubShopButton : MonoBehaviour
    {
        private void Start()
        {
            GetComponent<Button>().onClick.AddListener(() => HubShopPanel.Instance?.Open());
        }
    }
}
