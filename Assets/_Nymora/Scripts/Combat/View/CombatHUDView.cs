using Quantum;
using TMPro;
using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// HUD placeholder Phase 2 : affiche "Tour N - Joueur PX Class - Timer s.ss".
    /// Lit la singleton CombatState a chaque CallbackUpdateView (frame.Verified).
    /// Sera repolish avec un vrai design en Phase 7 — pour la 2.3 c'est juste de la
    /// data lisible pour valider que la FSM tourne correctement.
    /// </summary>
    public class CombatHUDView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (_label == null) return;

            var frame = game.Frames.Verified;
            if (!frame.TryGetSingleton<CombatState>(out var state))
            {
                _label.text = "(combat non initialise)";
                return;
            }

            int updateRate = frame.UpdateRate;
            float secondsRemaining = updateRate > 0
                ? state.TurnTimerTicks / (float)updateRate
                : 0f;

            // Resolution de la classe / ressource / statuses du joueur actif ET de l'autre.
            // 2.10.a : affichage compact des statuses (utile pour valider visuellement).
            string p0Block = "";
            string p1Block = "";
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef _, out Combatant combatant))
            {
                string block = FormatCombatantInfo(combatant);
                if (combatant.PlayerIndex == 0) p0Block = block;
                else if (combatant.PlayerIndex == 1) p1Block = block;
            }

            string activeMarker0 = state.ActivePlayerIndex == 0 ? "> " : "  ";
            string activeMarker1 = state.ActivePlayerIndex == 1 ? "> " : "  ";

            _label.text =
                $"Tour {state.TurnNumber}  |  {state.CurrentPhase}  |  {secondsRemaining:0.0}s\n" +
                $"{activeMarker0}P0 {p0Block}\n" +
                $"{activeMarker1}P1 {p1Block}";
        }

        private static string FormatCombatantInfo(Combatant c)
        {
            int maxResource = Quantum.CombatantStats.GetMaxResource(c.Class);
            string resourceLabel = "";
            if (maxResource > 0)
            {
                string tag = c.Class == NymoraClass.Soulrender ? "HG"
                           : c.Class == NymoraClass.Nightseer  ? "PR"
                           : c.Class == NymoraClass.Colossar   ? "FD"
                           : c.Class == NymoraClass.Necram     ? "PT"
                           : c.Class == NymoraClass.Ghostra    ? "RM"
                           : "?";
                resourceLabel = $" [{tag} {c.Resource}/{maxResource}]";
            }

            string statuses = FormatStatuses(c);
            string statusLine = string.IsNullOrEmpty(statuses) ? "" : $"  {{{statuses}}}";

            return $"{c.Class} HP {c.HP}/{c.MaxHP} PA {c.PA}/{c.MaxPA} PM {c.PM}/{c.MaxPM}{resourceLabel}{statusLine}";
        }

        private static string FormatStatuses(Combatant c)
        {
            // Concatene les statuses actifs en format court "Kind:TurnsLeft[xMag]".
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                var s = c.Statuses[i];
                if (s.Kind == StatusKind.None || s.TurnsLeft <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(s.Kind.ToString());
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
