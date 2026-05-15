using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.3.a — Placeholder mobile clavier : se deplace via Input axes Horizontal/Vertical.
    /// AZERTY : ZQSD chez Lorenzo (Unity Input Manager mappe Horizontal sur A/D + arrows,
    /// Vertical sur W/S + arrows ; sur clavier physique AZERTY ces touches sont Q/D et Z/S visibles).
    /// Sera supprime / remplace par l'avatar joueur en 4.3.b.
    /// </summary>
    public sealed class HubPivot : MonoBehaviour
    {
        [SerializeField] private float _speedUnitsPerSec = 4f;

        private void Update()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (h == 0f && v == 0f) return;
            Vector3 delta = new Vector3(h, v, 0f).normalized * (_speedUnitsPerSec * Time.deltaTime);
            transform.position += delta;
        }
    }
}
