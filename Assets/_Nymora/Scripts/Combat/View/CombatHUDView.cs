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

            // Resolution de la classe et ressource du joueur actif.
            string activeClassLabel = "?";
            string resourceLabel = "";
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef _, out Combatant combatant))
            {
                if (combatant.PlayerIndex == state.ActivePlayerIndex)
                {
                    activeClassLabel = combatant.Class.ToString();
                    int maxResource = Quantum.CombatantStats.GetMaxResource(combatant.Class);
                    if (maxResource > 0)
                    {
                        // Tag court de ressource selon la classe (Bible V7.1).
                        string tag = combatant.Class == NymoraClass.Soulrender ? "HG"
                                   : combatant.Class == NymoraClass.Nightseer  ? "PR"
                                   : combatant.Class == NymoraClass.Colossar   ? "FD"
                                   : combatant.Class == NymoraClass.Necram     ? "PT"
                                   : combatant.Class == NymoraClass.Ghostra    ? "RM"
                                   : "?";
                        resourceLabel = $"  [{tag} {combatant.Resource}/{maxResource}]";
                    }
                    break;
                }
            }

            _label.text = $"Phase: {state.CurrentPhase}  |  Tour {state.TurnNumber}  |  Joueur P{state.ActivePlayerIndex} {activeClassLabel}{resourceLabel}  |  Timer {secondsRemaining:0.0}s";
        }
    }
}
