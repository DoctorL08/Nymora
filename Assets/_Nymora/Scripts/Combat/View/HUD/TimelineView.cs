using TMPro;
using UnityEngine;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Timeline bas-droite : "&gt; P0 | P1" — le marqueur ">" indique le joueur actif.
    /// 2.13.a : version texte simple. Evoluera en portraits + cooldowns en Phase 6+
    /// (multi-joueur 2v2/3v3).
    /// </summary>
    public class TimelineView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;

        public void Refresh(int activePlayerIndex)
        {
            if (_label == null) return;
            string p0 = activePlayerIndex == 0 ? "> P0" : "  P0";
            string p1 = activePlayerIndex == 1 ? "> P1" : "  P1";
            _label.text = $"{p0}  |  {p1}";
        }
    }
}
