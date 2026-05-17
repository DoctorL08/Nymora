namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// POLISH-4.5 — Command DEBUG : applique un Status arbitraire sur le combatant qui
    /// occupe (TargetX, TargetY). Sert a tester visuellement les marques/buffs/debuffs
    /// sans dependre des sorts qui les posent normalement.
    ///
    /// Magnitude / TurnsLeft : valeurs arbitraires pour test rapide. Si Magnitude=0,
    /// le system utilise une valeur "raisonnable" Bible (cf DebugSystem.HandleApplyStatus).
    ///
    /// Mapping clavier (POLISH-4.5) :
    ///   F1 = PlaieOuverte (40/tour x 2t)        — cible ennemi souris
    ///   F2 = BleedDoT     (40/tour x 2t)        — cible ennemi souris (stub Phase 2)
    ///   F8 = ShieldActive (200 HP x 2t)         — caster (self target)
    ///   F9 = Untargetable (Voile, 1 round)      — caster (self target)
    ///   F10 = MarqueDeLOmbre (mag=20, 2 rounds) — cible ennemi souris
    ///
    /// Validation cote sim : sender == ActivePlayerIndex, case dans grille, target combatant
    /// vivant (sauf self-target qui prend le caster).
    /// </summary>
    public class DebugApplyStatusCommand : DeterministicCommand
    {
        public int TargetX;
        public int TargetY;
        public byte StatusKindByte; // cast en StatusKind cote sim
        public byte SelfTarget;     // 1 = cible le caster (ignore TargetX/Y combatant lookup), 0 = ennemi sous souris

        public override void Serialize(BitStream stream)
        {
            stream.Serialize(ref TargetX);
            stream.Serialize(ref TargetY);
            stream.Serialize(ref StatusKindByte);
            stream.Serialize(ref SelfTarget);
        }
    }

    /// <summary>
    /// POLISH-4.5 — Command DEBUG : pose un TerrainKind arbitraire sur la case
    /// (TargetX, TargetY). Sert a tester visuellement les tiles (Sang Coagule, Vapeur
    /// Carmin, Brume Toxique) sans depender des sorts qui les posent normalement.
    ///
    /// Mapping clavier (POLISH-4.5) :
    ///   F3 = SangCoagule  (2 tours default)
    ///   F4 = VapeurCarmin (1 tour default)
    ///   F5 = BrumeToxique (2 tours default)
    ///
    /// Validation cote sim : sender == ActivePlayerIndex, case dans grille.
    /// </summary>
    public class DebugSpawnTerrainCommand : DeterministicCommand
    {
        public int TargetX;
        public int TargetY;
        public byte TerrainKindByte; // cast en TerrainKind cote sim

        public override void Serialize(BitStream stream)
        {
            stream.Serialize(ref TargetX);
            stream.Serialize(ref TargetY);
            stream.Serialize(ref TerrainKindByte);
        }
    }

    /// <summary>
    /// POLISH-4.5 — Command DEBUG : pose un TrapKind arbitraire sur la case
    /// (TargetX, TargetY) au nom du sender. Sert a tester visuellement les pieges
    /// (Filet de Ronces, Mine) sans depender des sorts qui les posent normalement.
    ///
    /// Mapping clavier (POLISH-4.5) :
    ///   F6 = FiletRonces
    ///   F7 = Mine
    ///
    /// Validation cote sim : sender == ActivePlayerIndex, case dans grille.
    /// </summary>
    public class DebugSpawnTrapCommand : DeterministicCommand
    {
        public int TargetX;
        public int TargetY;
        public byte TrapKindByte; // cast en TrapKind cote sim

        public override void Serialize(BitStream stream)
        {
            stream.Serialize(ref TargetX);
            stream.Serialize(ref TargetY);
            stream.Serialize(ref TrapKindByte);
        }
    }

    /// <summary>
    /// POLISH-4.5b — Command DEBUG : applique une MarkKind Nightseer (Traque ou Empreinte)
    /// sur le combatant ennemi qui occupe (TargetX, TargetY). MarkKind est distincte de
    /// StatusKind (champ Combatant.CurrentMark + MarkTurnsLeft + MarkOwnerPlayer + AppliedOnTurn),
    /// gere par MarkHelpers.ApplyMark.
    ///
    /// Mapping clavier (POLISH-4.5b) :
    ///   Shift+F2 = Traque    (sprite marque_traque_oeil_pulsant_4frame)
    ///   Shift+F3 = Empreinte (sprite marque_empreinte_4frame)
    ///
    /// Validation cote sim : sender == ActivePlayerIndex, case dans grille, combatant
    /// ennemi vivant sur la case. Default turnsLeft Bible : Traque=3, Empreinte=2.
    /// Owner = sender (pour les hooks "marque appliquee par MOI" si jamais utilises).
    /// </summary>
    public class DebugApplyMarkCommand : DeterministicCommand
    {
        public int TargetX;
        public int TargetY;
        public byte MarkKindByte; // cast en MarkKind cote sim

        public override void Serialize(BitStream stream)
        {
            stream.Serialize(ref TargetX);
            stream.Serialize(ref TargetY);
            stream.Serialize(ref MarkKindByte);
        }
    }
}
