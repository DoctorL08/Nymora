namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// 2.14 — Command DEBUG : pose un Voile Nightseer 2 tours sur la case (TargetX, TargetY).
    /// Process par FogSystem. Le poseur est le joueur qui envoie la commande, valide cote sim
    /// uniquement si == joueur actif (meme regle que cast classique).
    ///
    /// Sera retire en 2.15 quand les sorts Nightseer (Pas Furtif, Voile d'Ombre, Champ de Mines)
    /// feront ca proprement via SpellSystem.
    /// </summary>
    public class DebugApplyVeilCommand : DeterministicCommand
    {
        public int TargetX;
        public int TargetY;

        public override void Serialize(BitStream stream)
        {
            stream.Serialize(ref TargetX);
            stream.Serialize(ref TargetY);
        }
    }
}
