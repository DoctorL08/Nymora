namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// Command Quantum : demande de cast d'un sort sur (TargetX, TargetY).
    /// La validation (PA, range, filter, phase, joueur actif) est faite cote simu
    /// dans SpellSystem.
    /// </summary>
    public class CastSpellCommand : DeterministicCommand
    {
        public SpellId Spell;
        public int TargetX;
        public int TargetY;

        public override void Serialize(BitStream stream)
        {
            // SpellId est un enum Byte. On serialise en byte puis on cast back.
            byte spellByte = (byte)Spell;
            stream.Serialize(ref spellByte);
            Spell = (SpellId)spellByte;

            stream.Serialize(ref TargetX);
            stream.Serialize(ref TargetY);
        }
    }
}
