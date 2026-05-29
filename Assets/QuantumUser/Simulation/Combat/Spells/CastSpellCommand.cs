namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// Command Quantum : demande de cast d'un sort sur (TargetX, TargetY).
    /// La validation (PA, range, filter, phase, joueur actif) est faite cote simu
    /// dans SpellSystem.
    ///
    /// HGSpend (2.10.a) : nombre de HG que le caster choisit volontairement de
    /// depenser pour ce cast. Sert pour les sorts avec cout HG OPTIONNEL :
    ///   - Ouvre-Plaie : 0 (110 dgts) ou 1 (230 dgts + anti-heal 2 tours)
    ///   - Seve Vive   : 0 (100 HP) ou 1 (160 HP) — 2.10.b
    ///   - Curee       : toujours 2 (cout obligatoire) — 2.10.c
    /// SpellSystem clamp HGSpend a SpellDef.HGCostMaxOptional et rejette si pas
    /// assez de HG. La valeur de HGCostMandatory est appliquee en plus, automatique.
    /// </summary>
    public class CastSpellCommand : DeterministicCommand
    {
        public SpellId Spell;
        public int TargetX;
        public int TargetY;
        public byte HGSpend;

        // Refonte 29 mai — sorts à PUSH DIRECTIONNEL (Bourrasque, Piège Bondissant) : 2e clic.
        //   (DirX, DirY) = case du 2e clic ; la direction = sens (TargetX,TargetY) -> (DirX,DirY),
        //   réduit à une cardinale par le sim. Si (DirX,DirY) == (TargetX,TargetY) ou non renseigné
        //   -> pas de direction (le sim retombe sur "loin du caster"). Défaut -1 = absent
        //   (initialiseurs : tout cast non-directionnel — IA, tuto, debug — reste à -1/-1).
        public int DirX = -1;
        public int DirY = -1;

        public override void Serialize(BitStream stream)
        {
            // SpellId est un enum Byte. On serialise en byte puis on cast back.
            byte spellByte = (byte)Spell;
            stream.Serialize(ref spellByte);
            Spell = (SpellId)spellByte;

            stream.Serialize(ref TargetX);
            stream.Serialize(ref TargetY);
            stream.Serialize(ref HGSpend);
            stream.Serialize(ref DirX);
            stream.Serialize(ref DirY);
        }
    }
}
