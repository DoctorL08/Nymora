namespace Quantum
{
    /// <summary>
    /// Helpers d'acces aux statuses portes par un Combatant.
    ///
    /// Convention : un status avec Kind == None est un slot LIBRE. Pour appliquer,
    /// on cherche d'abord un slot existant du meme Kind (refresh), sinon un slot
    /// libre. Si plus de slot : Log.Warn (en pratique 8 slots suffit largement
    /// pour le scope Soulrender de 2.10.a).
    ///
    /// La decrementation est appelee depuis TurnSystem.EnterTurnEnd via
    /// DecrementAllOnTurnEnd(f, currentTurn).
    /// </summary>
    public static unsafe class StatusHelper
    {
        public const int SlotCount = 8;

        /// <summary>
        /// Applique (ou rafraichit) un status sur un Combatant.
        /// Si un slot existe deja avec ce Kind : il est ecrase (refresh duree/magnitude).
        /// </summary>
        public static void Apply(Combatant* target, StatusKind kind, int magnitude, int turnsLeft, int currentTurn)
        {
            if (kind == StatusKind.None || turnsLeft <= 0) return;

            int freeSlot = -1;
            for (int i = 0; i < SlotCount; i++)
            {
                if (target->Statuses[i].Kind == kind)
                {
                    target->Statuses[i].Magnitude = magnitude;
                    target->Statuses[i].TurnsLeft = turnsLeft;
                    target->Statuses[i].AppliedOnTurn = currentTurn;
                    return;
                }
                if (target->Statuses[i].Kind == StatusKind.None && freeSlot == -1)
                {
                    freeSlot = i;
                }
            }

            if (freeSlot < 0)
            {
                Log.Warn($"[Status] Apply {kind} rejete : {SlotCount} slots occupes sur P{target->PlayerIndex}");
                return;
            }

            target->Statuses[freeSlot] = new Status
            {
                Kind = kind,
                Magnitude = magnitude,
                TurnsLeft = turnsLeft,
                AppliedOnTurn = currentTurn,
            };
        }

        public static bool Has(Combatant* c, StatusKind kind)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (c->Statuses[i].Kind == kind && c->Statuses[i].TurnsLeft > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Retourne Magnitude du status si actif, sinon defaultValue.
        /// </summary>
        public static int GetMagnitude(Combatant* c, StatusKind kind, int defaultValue = 0)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (c->Statuses[i].Kind == kind && c->Statuses[i].TurnsLeft > 0) return c->Statuses[i].Magnitude;
            }
            return defaultValue;
        }

        /// <summary>
        /// Modifie Magnitude d'un status existant. No-op si absent.
        /// Utilise pour tracker "PA deja gagne ce tour" sur RageInsatiableActive.
        /// </summary>
        public static void SetMagnitude(Combatant* c, StatusKind kind, int magnitude)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (c->Statuses[i].Kind == kind && c->Statuses[i].TurnsLeft > 0)
                {
                    c->Statuses[i].Magnitude = magnitude;
                    return;
                }
            }
        }

        /// <summary>
        /// Force l'expiration d'un status (Kind = None, libere le slot).
        /// Utilise pour consommer un buff one-shot (Pacte de Sang +50%) apres usage.
        /// </summary>
        public static void Consume(Combatant* c, StatusKind kind)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (c->Statuses[i].Kind == kind)
                {
                    c->Statuses[i] = default;
                    return;
                }
            }
        }

        /// <summary>
        /// Vide tous les slots du combattant (Kind = None).
        /// Utilise au spawn pour garantir un etat propre.
        /// </summary>
        public static void ClearAll(Combatant* c)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                c->Statuses[i] = default;
            }
        }

        /// <summary>
        /// Decrementation appelee a chaque TurnEnd. Pour chaque combattant :
        ///   - skip les statuses dont AppliedOnTurn == currentTurn (poses ce tour)
        ///   - sinon TurnsLeft -= 1, expire si <= 0
        ///
        /// L'OncePerMatchUsedFlags n'est PAS reset (par definition once-per-match).
        /// </summary>
        public static void DecrementAllOnTurnEnd(Frame f, int currentTurn)
        {
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                for (int i = 0; i < SlotCount; i++)
                {
                    if (c->Statuses[i].Kind == StatusKind.None) continue;
                    if (c->Statuses[i].AppliedOnTurn == currentTurn) continue;
                    c->Statuses[i].TurnsLeft -= 1;
                    if (c->Statuses[i].TurnsLeft <= 0)
                    {
                        c->Statuses[i] = default;
                    }
                }
            }
        }
    }
}
