namespace Quantum
{
    /// <summary>
    /// 3.7.a.i.0 — Helpers facing iso pour la simulation Quantum.
    ///
    /// Le facing est trackes sur Combatant.Facing (enum IsoFacing : SE=0 / NE=1 / NW=2 / SW=3).
    /// Set au spawn (CombatantSystem) puis update au move (MovementSystem.ApplyMove).
    /// Lu par <see cref="GhostraPassif.IsDorsalHit"/> pour le bonus dorsal Bible V7.1.
    ///
    /// Mirror cote View : <c>Nymora.Combat.View.IsoFacing</c> + <c>FacingFromGridDelta</c>
    /// dans CombatantRenderer. Les valeurs sont identiques pour conversion triviale.
    /// </summary>
    public static unsafe class FacingHelpers
    {
        /// <summary>
        /// Calcule l'iso facing depuis un delta grille (dx, dy). Convertit en coordonnees
        /// world iso (dxWorld = dx-dy, dyWorld = dx+dy) puis classe en quadrant NE/SE/NW/SW.
        ///
        /// Convention identique a CombatantRenderer.FacingFromGridDelta cote View. Si
        /// (dx,dy) = (0,0) -> retourne SE par defaut (cas spawn, pas de mouvement).
        /// </summary>
        public static IsoFacing FacingFromGridDelta(int dxGrid, int dyGrid)
        {
            int dxWorld = dxGrid - dyGrid;
            int dyWorld = dxGrid + dyGrid;
            bool east = dxWorld >= 0;
            bool north = dyWorld >= 0;
            if (east && north) return IsoFacing.NE;
            if (east && !north) return IsoFacing.SE;
            if (!east && north) return IsoFacing.NW;
            return IsoFacing.SW;
        }

        /// <summary>
        /// Direction opposee : SE↔NW, NE↔SW. Utilisee pour le check dorsal :
        /// caster est dorsal si direction(target -> caster) == Opposite(target.Facing).
        /// </summary>
        public static IsoFacing Opposite(IsoFacing f)
        {
            switch (f)
            {
                case IsoFacing.SE: return IsoFacing.NW;
                case IsoFacing.NE: return IsoFacing.SW;
                case IsoFacing.NW: return IsoFacing.SE;
                case IsoFacing.SW: return IsoFacing.NE;
                default:           return IsoFacing.NW;
            }
        }

        /// <summary>
        /// 3.7.a — Bible V7.1 dorsal hit : caster est-il DERRIERE la target (target regarde
        /// dans la direction opposee a caster) ?
        ///
        /// Logique : direction(target -> caster) doit == Opposite(target.Facing).
        /// Exemple : target regarde NE, caster est au SW de target -> dorsal.
        ///
        /// Cas degenere : si caster et target sur la meme case, retourne false (pas dorsal).
        /// </summary>
        public static bool IsDorsalHit(Combatant* caster, Combatant* target)
        {
            if (caster == null || target == null) return false;
            int dx = caster->GridX - target->GridX;
            int dy = caster->GridY - target->GridY;
            if (dx == 0 && dy == 0) return false;
            IsoFacing dirFromTarget = FacingFromGridDelta(dx, dy);
            IsoFacing oppositeTargetFacing = Opposite(target->Facing);
            bool dorsal = dirFromTarget == oppositeTargetFacing;
            Log.Info($"[Dorsal check] caster P{caster->PlayerIndex}({caster->GridX},{caster->GridY}) -> target P{target->PlayerIndex}({target->GridX},{target->GridY}) | target.Facing={target->Facing}, dirFromTarget={dirFromTarget}, opposite={oppositeTargetFacing} -> dorsal={dorsal}");
            return dorsal;
        }
    }
}
