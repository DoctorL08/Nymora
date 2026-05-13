namespace Quantum
{
    /// <summary>
    /// Helpers d'application/lecture des marques Nightseer sur les combattants.
    /// Pattern miroir de StatusHelper (statuses) mais simplifie : Bible V7.1 dit
    /// qu'une cible ne peut avoir qu'1 marque active a la fois (pas de stack).
    /// Donc pas d'array : 4 champs scalaires sur Combatant suffisent.
    ///
    /// Decrementation centralisee dans TurnSystem.EnterTurnEnd (skip si applique ce tour).
    /// </summary>
    public static unsafe class MarkHelpers
    {
        /// <summary>
        /// Applique (ou rafraichit) une marque sur le combattant. Ecrase la marque precedente
        /// si differente (Bible : "AUCUNE marque ne s'empile").
        /// </summary>
        public static void ApplyMark(Combatant* target, MarkKind kind, int turnsLeft, int ownerPlayer, int currentTurn)
        {
            if (kind == MarkKind.None || turnsLeft <= 0)
            {
                ConsumeMark(target);
                return;
            }
            target->CurrentMark = kind;
            target->MarkTurnsLeft = turnsLeft;
            target->MarkAppliedOnTurn = currentTurn;
            target->MarkOwnerPlayer = ownerPlayer;
        }

        public static MarkKind GetCurrentMark(Combatant* c)
        {
            if (c->MarkTurnsLeft <= 0) return MarkKind.None;
            return c->CurrentMark;
        }

        public static bool HasMark(Combatant* c, MarkKind kind)
        {
            return c->MarkTurnsLeft > 0 && c->CurrentMark == kind;
        }

        /// <summary>
        /// Force l'expiration de la marque (utilise quand un sort consomme la marque,
        /// ex : Traquenard signature qui consomme Traque/Voilé/Empreinte pour bonus dgts).
        /// </summary>
        public static void ConsumeMark(Combatant* c)
        {
            c->CurrentMark = MarkKind.None;
            c->MarkTurnsLeft = 0;
            c->MarkAppliedOnTurn = 0;
            c->MarkOwnerPlayer = 0;
        }

        /// <summary>
        /// Appelee a chaque TurnEnd. Pour chaque combattant :
        ///   - skip si MarkAppliedOnTurn == currentTurn (posee ce tour)
        ///   - sinon TurnsLeft -= 1, expire si <= 0
        /// </summary>
        public static void DecrementAllMarksOnTurnEnd(Frame f, int currentTurn)
        {
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->CurrentMark == MarkKind.None) continue;
                if (c->MarkTurnsLeft <= 0) continue;
                if (c->MarkAppliedOnTurn == currentTurn) continue;
                c->MarkTurnsLeft -= 1;
                if (c->MarkTurnsLeft <= 0)
                {
                    ConsumeMark(c);
                }
            }
        }
    }
}
