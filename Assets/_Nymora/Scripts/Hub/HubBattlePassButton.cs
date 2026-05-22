using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>Brique 5.7.b — Bouton hub qui ouvre le panneau Battle Pass.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class HubBattlePassButton : MonoBehaviour
    {
        private void Start()
        {
            GetComponent<Button>().onClick.AddListener(() => HubBattlePassPanel.Instance?.Open());
        }
    }
}
