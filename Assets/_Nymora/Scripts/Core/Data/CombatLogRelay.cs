using System;

namespace Nymora.Core.Data
{
    /// <summary>
    /// Relais statique View-only pour le journal de combat -> chat. Le producteur vit dans
    /// l'asmdef Nymora.Combat (CombatChatLogView) et le consommateur dans Nymora.Hub (HubChatUI) ;
    /// comme Combat ne peut pas referencer Hub (separation asmdef), on passe par ce relais Core
    /// (les deux asmdef dependent de Core). Meme pattern que MatchBridge / DeckBridge.
    ///
    /// Fire-and-forget : pas de buffer. Une ligne poussee sans abonne est perdue — sans
    /// importance car en combat il y a toujours un HubChatUI (instance du prefab ChatPanel)
    /// abonne avant le demarrage du match.
    /// </summary>
    public static class CombatLogRelay
    {
        /// <summary>Emis a chaque ligne de journal de combat (texte deja formate en rich text TMP).</summary>
        public static event Action<string> OnLine;

        public static void Push(string richLine)
        {
            if (!string.IsNullOrEmpty(richLine)) OnLine?.Invoke(richLine);
        }
    }
}
