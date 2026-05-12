using Quantum;
using TMPro;
using UnityEngine;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Bloc HP/PA/PM (+ ressource de classe HG/PR/FD/PT/RM) d'un combattant.
    /// Affichage texte en 2.13.a — passera en barres + chiffres en 2.13.c.
    /// </summary>
    public class ResourcePanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _statusLine;

        private static readonly Color ActiveColor   = new Color(1.00f, 0.95f, 0.55f, 1f);
        private static readonly Color InactiveColor = new Color(0.78f, 0.78f, 0.78f, 1f);

        public void Refresh(in Combatant c, bool isActive)
        {
            if (_label == null) return;

            int maxResource = CombatantStats.GetMaxResource(c.Class);
            string resourceTag = ResourceTag(c.Class);
            string resourceBlock = maxResource > 0
                ? $"  {resourceTag} {c.Resource}/{maxResource}"
                : string.Empty;

            string marker = isActive ? "> " : "  ";
            _label.color = isActive ? ActiveColor : InactiveColor;
            _label.text =
                $"{marker}P{c.PlayerIndex} {c.Class}\n" +
                $"HP {c.HP}/{c.MaxHP}\n" +
                $"PA {c.PA}/{c.MaxPA}   PM {c.PM}/{c.MaxPM}{resourceBlock}";

            if (_statusLine != null)
            {
                _statusLine.text = FormatStatuses(c);
            }
        }

        public void Clear()
        {
            if (_label != null) _label.text = string.Empty;
            if (_statusLine != null) _statusLine.text = string.Empty;
        }

        private static string ResourceTag(NymoraClass cls)
        {
            switch (cls)
            {
                case NymoraClass.Soulrender: return "HG";
                case NymoraClass.Nightseer:  return "PR";
                case NymoraClass.Colossar:   return "FD";
                case NymoraClass.Necram:     return "PT";
                case NymoraClass.Ghostra:    return "RM";
                default: return string.Empty;
            }
        }

        private static string FormatStatuses(in Combatant c)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                var s = c.Statuses[i];
                if (s.Kind == StatusKind.None || s.TurnsLeft <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(s.Kind);
                sb.Append(':');
                sb.Append(s.TurnsLeft);
                if (s.Magnitude != 0 && s.Kind != StatusKind.RageInsatiableActive)
                {
                    sb.Append('x');
                    sb.Append(s.Magnitude);
                }
            }
            return sb.ToString();
        }
    }
}
