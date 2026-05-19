using Quantum;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Builder static des tooltips Passif + Phases pour le HUD combat.
    /// Texte hardcode Bible V7.1 — meme contenu que HubClassSelectorPanel.BuildPassifTooltip
    /// + BuildPhasesTooltip, mais dupliqué cote Combat asmdef pour eviter une dependance
    /// croisee Hub<->Combat. Si Lorenzo modifie un de ces textes, mettre a jour les deux
    /// (faible churn vu que c'est du contenu Bible-locked).
    /// </summary>
    public static class PassiveTooltipBuilder
    {
        public static string Build(NymoraClass cls)
        {
            string passifHeader = $"<size=24><color=#ffe28a><b>Passif : {GetPassifName(cls)}</b></color></size>";
            string passifDesc = $"<size=15>{GetPassifDescription(cls)}</size>";
            string generation = $"<size=15>{GetGenerationDescription(cls)}</size>";
            string phasesHeader = "<size=20><color=#b0c8ff><b>Phases & bonus</b></color></size>";
            string phasesBody = $"<size=14>{GetPhasesDescription(cls)}</size>";

            return passifHeader + "\n\n" + passifDesc + "\n\n" +
                   generation + "\n\n" +
                   phasesHeader + "\n\n" + phasesBody;
        }

        private static string GetPassifName(NymoraClass cls)
        {
            switch (cls)
            {
                case NymoraClass.Soulrender: return "Hémoglyphe";
                case NymoraClass.Nightseer:  return "L'Œil qui n'est pas";
                case NymoraClass.Colossar:   return "Densité Inerte";
                case NymoraClass.Necram:     return "Floraison";
                case NymoraClass.Ghostra:    return "Angle Mort";
                default: return "?";
            }
        }

        private static string GetPassifDescription(NymoraClass cls)
        {
            switch (cls)
            {
                case NymoraClass.Soulrender:
                    return "Chaque sort offensif qui inflige des dégâts génère 1 HG (max 5). " +
                           "Les sorts à coût HG consomment ce pool. Au cap, la signature Âme Lacérée est débloquée.";
                case NymoraClass.Nightseer:
                    return "Récompense la furtivité totale : gagne 1 PR par round où aucun dégât n'est subi, " +
                           "perd 1 PR si touché. Cap 4. Au cap, signature Traquenard prête.";
                case NymoraClass.Colossar:
                    return "Réduit les dégâts subis de 8% par obstacle actif (-24% max à 3 obstacles). " +
                           "Bonus +20 dgts sur sorts portée 1-2 si adjacent à un obstacle propre. " +
                           "+30 HP à chaque destruction de pilier.";
                case NymoraClass.Necram:
                    return "Toutes les marques de venin/peste appliquées ticquent plus violemment au fil " +
                           "des accumulations. La signature Virus Fatal déclenche x3 tous les ticks au cap.";
                case NymoraClass.Ghostra:
                    return "Bonus dorsal qui augmente avec le nombre de leurres actifs sur le terrain. " +
                           "À Angle 2+ (1+ leurre), applique automatiquement Plaie Ouverte sur tout coup dorsal.";
                default: return "?";
            }
        }

        private static string GetGenerationDescription(NymoraClass cls)
        {
            switch (cls)
            {
                case NymoraClass.Soulrender:
                    return "<color=#ffd060><b>Génération HG :</b></color>  +1 par sort qui inflige dgts (max 1/sort). " +
                           "Bonus +1 si cible Marquée de Carnage.  Cap 5.";
                case NymoraClass.Nightseer:
                    return "<color=#c0b0d0><b>Génération PR :</b></color>  +1 par round SANS dégâts subis. " +
                           "-1 par round avec dégâts (plancher 0).  Cap 4.";
                case NymoraClass.Colossar:
                    return "<color=#a0d0ff><b>Génération FD :</b></color>  +1 par obstacle spawné (pilier ou mur).  Cap 3.";
                case NymoraClass.Necram:
                    return "<color=#b0e090><b>Génération PT :</b></color>  +1 par marque de venin/peste appliquée " +
                           "(cap +2/tour via marques).  +1 par tick global de marques sur la map.";
                case NymoraClass.Ghostra:
                    return "<color=#d0b0ff><b>Génération RM :</b></color>  Resource = nb leurres actifs (synchro auto). " +
                           "Pose / perte / expiration met à jour en temps réel.  Cap 3.";
                default: return "";
            }
        }

        private static string GetPhasesDescription(NymoraClass cls)
        {
            switch (cls)
            {
                case NymoraClass.Soulrender:
                    return "<color=#ffe2a0><b>Stage 0</b></color> — 0-1 HG : peau normale\n" +
                           "  <i>Bonus : aucun</i>\n\n" +
                           "<color=#ffb060><b>Stage 1</b></color> — 2-4 HG : aura rouge progressive\n" +
                           "  <i>Bonus : sorts à coût HG disponibles</i>\n\n" +
                           "<color=#ff6060><b>Stage 2</b></color> — 5 HG (cap) : fissures écarlates\n" +
                           "  <i><color=#ffd060>Bonus : signature Âme Lacérée prête</color></i>";
                case NymoraClass.Colossar:
                    return "<color=#a0c8ff><b>Stage 0</b></color> — 0 obstacle\n" +
                           "  <i>Bonus : aucune réduction</i>\n\n" +
                           "<color=#80b0ff><b>Stage 1</b></color> — 1-2 obstacles\n" +
                           "  <i><color=#a0d0ff>Bonus : -8% à -16% dgts subis + +20 dmg portée 1-2 si adjacent</color></i>\n\n" +
                           "<color=#6090ff><b>Stage 2</b></color> — 3 obstacles (cap)\n" +
                           "  <i><color=#a0d0ff>Bonus : -24% dgts (cap) + +30 HP/pilier détruit + signature</color></i>";
                case NymoraClass.Ghostra:
                    return "<color=#c0a0ff><b>Stage 0</b></color> — Angle 1 (0 leurre)\n" +
                           "  <i>Bonus : aucun bonus dorsal natif</i>\n\n" +
                           "<color=#a080ff><b>Stage 1</b></color> — Angle 2 (1-2 leurres)\n" +
                           "  <i><color=#d0b0ff>Bonus : +50 dgts dorsaux + Plaie Ouverte auto (40/t × 2t)</color></i>\n\n" +
                           "<color=#8060ff><b>Stage 2</b></color> — Angle 3 (3 leurres)\n" +
                           "  <i><color=#d0b0ff>Bonus : +80 dgts dorsaux + signature Exécution Spectrale</color></i>";
                case NymoraClass.Necram:
                    return "<color=#a0e090><b>Stage 0</b></color> — ressource basse\n" +
                           "  <i>Bonus : marques venin/peste appliquées par les sorts</i>\n\n" +
                           "<color=#80c070><b>Stage 1</b></color> — ressource moyenne\n" +
                           "  <i><color=#b0e090>Bonus : Floraison augmente le tick des marques</color></i>\n\n" +
                           "<color=#60a050><b>Stage 2</b></color> — ressource au cap\n" +
                           "  <i><color=#b0e090>Bonus : signature Virus Fatal prête (x3 ticks instant)</color></i>";
                case NymoraClass.Nightseer:
                    return "<color=#9090a0><b>Pas de phases visuelles</b></color>\n\n" +
                           "Le Nightseer reste discret. Sa puissance se mesure aux pièges et voiles posés, " +
                           "pas à son apparence.\n\n" +
                           "<i><color=#b0a0c0>Mindgame : l'adversaire ne sait jamais ce que cache un voile.</color></i>";
                default: return "<i>(Phases non documentées)</i>";
            }
        }
    }
}
