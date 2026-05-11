namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// Traite les CastSpellCommands envoyees par les joueurs.
    /// Pipeline standard :
    ///   1. Validations : phase TurnActive + joueur actif + sort existe + caster existe
    ///   2. Validation PA >= PACost
    ///   3. Validation range Manhattan caster->target dans [RangeMin..RangeMax]
    ///   4. Validation TargetingResolver.MatchesFilter (le caster a le droit de cibler ca)
    ///   5. Consommation : caster.PA -= PACost
    ///   6. Calcul effect cells via TargetingResolver.ResolveEffectCells (stackalloc, zero alloc)
    ///   7. Application des effets sur chaque cible dans la zone d'effet
    ///
    /// En 2.7 : un seul type d'effet supporte (Damage flat, depuis SpellDef.DamageAmount).
    /// Les autres effets (Heal, Mark, Push, Pull, Spawn) seront ajoutes par 2.9-2.11
    /// quand les sorts Soulrender concrets en auront besoin.
    /// </summary>
    public unsafe class SpellSystem : SystemMainThread
    {
        public override void Update(Frame f)
        {
            if (!f.TryGetSingleton<CombatState>(out var state)) return;
            if (state.CurrentPhase != CombatPhase.TurnActive) return;

            for (int playerIndex = 0; playerIndex < TurnConstants.PlayerCount; playerIndex++)
            {
                if (f.GetPlayerCommand(playerIndex) is CastSpellCommand cmd)
                {
                    TryCastSpell(f, playerIndex, cmd, state.ActivePlayerIndex);
                }
            }
        }

        private static void TryCastSpell(Frame f, int playerIndex, CastSpellCommand cmd, int activePlayerIndex)
        {
            if (playerIndex != activePlayerIndex)
            {
                Log.Warn($"[Spell] rejet : ce n'est pas le tour de P{playerIndex}");
                return;
            }

            if (!SpellRegistry.TryGet(cmd.Spell, out var spellDef))
            {
                Log.Warn($"[Spell] rejet : sort inconnu {cmd.Spell}");
                return;
            }

            // Cherche le combattant du caster (le joueur actif)
            EntityRef casterEntity = EntityRef.None;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef entity, out Combatant* c))
            {
                if (c->PlayerIndex == playerIndex)
                {
                    casterEntity = entity;
                    break;
                }
            }

            if (casterEntity == EntityRef.None)
            {
                Log.Warn($"[Spell] rejet : pas de Combatant pour P{playerIndex}");
                return;
            }

            var caster = f.Unsafe.GetPointer<Combatant>(casterEntity);

            if (caster->PA < spellDef.PACost)
            {
                Log.Warn($"[Spell] rejet : PA {caster->PA} < cost {spellDef.PACost}");
                return;
            }

            // Validation range Manhattan caster->target
            int dx = cmd.TargetX - caster->GridX;
            int dy = cmd.TargetY - caster->GridY;
            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;
            int dist = absDx + absDy;
            if (dist < spellDef.RangeMin || dist > spellDef.RangeMax)
            {
                Log.Warn($"[Spell] rejet : distance {dist} hors range [{spellDef.RangeMin},{spellDef.RangeMax}]");
                return;
            }

            // Validation filter sur la case ciblee
            if (!TargetingResolver.MatchesFilter(f, cmd.TargetX, cmd.TargetY, spellDef.Filter, casterEntity, playerIndex))
            {
                Log.Warn($"[Spell] rejet : ({cmd.TargetX},{cmd.TargetY}) ne match pas filter {spellDef.Filter}");
                return;
            }

            // Consomme PA
            caster->PA -= spellDef.PACost;

            // Calcule la zone d'effet (stackalloc cote simu = zero allocation)
            int* effectBuffer = stackalloc int[GridConstants.Count];
            TargetingResolver.ResolveEffectCells(
                f,
                caster->GridX, caster->GridY,
                cmd.TargetX, cmd.TargetY,
                spellDef.Shape,
                effectBuffer,
                out int effectCount);

            // Applique le damage a chaque cible dans la zone (uniquement les Combatants pour 2.7).
            if (spellDef.DamageAmount > 0)
            {
                for (int i = 0; i < effectCount; i++)
                {
                    int idx = effectBuffer[i];
                    int cx = idx % GridConstants.Width;
                    int cy = idx / GridConstants.Width;
                    EntityRef target = GridHelpers.GetOccupant(f, cx, cy);
                    if (target == EntityRef.None) continue;
                    if (!f.Unsafe.TryGetPointer<Combatant>(target, out Combatant* targetC)) continue;

                    int before = targetC->HP;
                    targetC->HP -= spellDef.DamageAmount;
                    if (targetC->HP < 0) targetC->HP = 0;
                    Log.Info($"[Spell] Damage {spellDef.DamageAmount} sur P{targetC->PlayerIndex} ({cx},{cy}) HP {before} -> {targetC->HP}");
                }
            }

            Log.Info($"[Spell] P{playerIndex} cast {cmd.Spell} target=({cmd.TargetX},{cmd.TargetY}) PA restant={caster->PA}");
        }
    }
}
