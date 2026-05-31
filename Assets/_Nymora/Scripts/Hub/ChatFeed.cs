using System.Collections.Generic;

namespace Nymora.Hub
{
    /// <summary>
    /// Feed du chat persistant entre les scènes. Les listes statiques vivent pour toute la durée
    /// de l'application (elles ne sont pas liées à une scène), donc l'historique n'est PAS remis à
    /// zéro quand on quitte le hub pour un combat puis qu'on revient. HubChatUI lit/écrit ici et
    /// se repeuple depuis ce store au Start.
    ///
    /// NB : les messages reçus PENDANT une scène sans HubChatUI (ex : plein combat) ne sont pas
    /// captés (aucun afficheur abonné) — le feed existant est simplement préservé.
    /// </summary>
    public static class ChatFeed
    {
        public const int MaxLines = 120;

        // Journal de combat (31 mai) : les lignes poussees pendant un match sont prefixees d'un
        // espace de largeur nulle (invisible en TMP) pour pouvoir les PURGER au retour hub sans
        // toucher au chat social. Cf AppendCombat / PurgeCombat.
        public const char CombatMarker = '​';

        public static readonly List<string> Global = new List<string>();
        public static readonly List<string> Private = new List<string>();
        public static readonly List<string> Clan = new List<string>();

        public static List<string> For(HubChatUI.ChatTab tab)
        {
            switch (tab)
            {
                case HubChatUI.ChatTab.Private: return Private;
                case HubChatUI.ChatTab.Clan: return Clan;
                default: return Global;
            }
        }

        public static void Append(HubChatUI.ChatTab tab, string line)
        {
            var l = For(tab);
            l.Add(line);
            if (l.Count > MaxLines) l.RemoveRange(0, l.Count - MaxLines);
        }

        /// <summary>Ajoute une ligne de JOURNAL DE COMBAT dans l'onglet Global (taguee pour purge).</summary>
        public static void AppendCombat(string line)
        {
            Append(HubChatUI.ChatTab.Global, CombatMarker + line);
        }

        /// <summary>Retire toutes les lignes de combat de l'onglet Global (appele au retour hub).</summary>
        public static void PurgeCombat()
        {
            Global.RemoveAll(l => l.Length > 0 && l[0] == CombatMarker);
        }
    }
}
