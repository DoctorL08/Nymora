using Quantum;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Build le rich text affiche au survol d'un slot de la timeline combat. Resume des
    /// infos critiques d'un combatant : phase actuelle, HP, ressource, marque Nightseer
    /// active, statuses temporises actifs (top 4 max pour eviter overflow tooltip).
    /// </summary>
    public static class TimelineSlotTooltipBuilder
    {
        public static string Build(Combatant c, int turnNumber)
        {
            // Stage actuel + ressource
            string className = c.Class.ToString();
            string stageLine = $"<size=18><color=#ffe28a><b>{className}</b></color>  <color=#aaa><size=14>(Stage {ComputeStage(c)})</size></color></size>";

            // HP + Ressource (pas d'emoji ❤ ⚡ : font asset combat ne les supporte pas)
            string hpLine = $"<size=15><color=#ffd060>HP : {c.HP} / {c.MaxHP}</color></size>";
            int maxRes = CombatantStats.GetMaxResource(c.Class);
            string resLine = maxRes > 0
                ? $"<size=14><color=#c8b8ff>{ResourceTag(c.Class)} : {c.Resource} / {maxRes}</color></size>"
                : "";

            // Marque Nightseer (si presente)
            string markLine = "";
            if (c.CurrentMark != MarkKind.None && c.MarkTurnsLeft > 0)
            {
                markLine = $"\n<size=14><color=#ff8060>{MarkLabel(c.CurrentMark)} ({c.MarkTurnsLeft} tour{(c.MarkTurnsLeft > 1 ? "s" : "")})</color></size>";
            }

            // Statuses actifs (top 4 max)
            string statusBlock = BuildStatusBlock(c, turnNumber);

            return stageLine + "\n" + hpLine + (string.IsNullOrEmpty(resLine) ? "" : "\n" + resLine) + markLine + statusBlock;
        }

        private static string BuildStatusBlock(Combatant c, int turnNumber)
        {
            int count = 0;
            string body = "";
            for (int i = 0; i < 8; i++)
            {
                var s = c.Statuses[i];
                if (s.Kind == StatusKind.None) continue;
                // Patch 5 juin — affiche l'EFFET (« -1 PM »...) au lieu du nom enum. Cf StatusEffectLabel.
                //   Statuts cachés (minuteur venin, réservés) renvoient "" et sont sautés.
                string label = StatusEffectLabel.Describe(s.Kind, s.Magnitude);
                if (string.IsNullOrEmpty(label)) continue;
                if (count >= 4)
                {
                    body += "\n<size=12><i><color=#888>... et plus</color></i></size>";
                    return "\n\n<size=13><b><color=#c8b8ff>Effets :</color></b></size>" + body;
                }
                int turnsLeft = s.TurnsLeft;
                string turnsStr = turnsLeft > 0 ? $" ({turnsLeft}t)" : "";
                body += $"\n<size=13>• {label}{turnsStr}</size>";
                count++;
            }
            if (count == 0) return "";
            return "\n\n<size=13><b><color=#c8b8ff>Effets :</color></b></size>" + body;
        }

        private static int ComputeStage(Combatant c)
        {
            if (c.Class == NymoraClass.Ghostra)
            {
                int active = 0;
                for (int i = 0; i < 3; i++)
                    if (c.Decoys[i].Kind != DecoyKind.None) active++;
                if (active >= 3) return 2;
                if (active >= 1) return 1;
                return 0;
            }
            int max = CombatantStats.GetMaxResource(c.Class);
            if (max <= 0) return 0;
            if (c.Resource >= max) return 2;
            return c.Resource * 5 < max * 2 ? 0 : 1;
        }

        private static string ResourceTag(NymoraClass cls)
        {
            switch (cls)
            {
                case NymoraClass.Soulrender: return "HG";
                case NymoraClass.Nightseer:  return "PR";
                case NymoraClass.Colossar:   return "FD";
                case NymoraClass.Necram:     return "PT";
                case NymoraClass.Ghostra:    return "RM";
                default: return "";
            }
        }

        private static string MarkLabel(MarkKind k)
        {
            switch (k)
            {
                case MarkKind.Traque:    return "Traqué (Marque du Chasseur)";
                case MarkKind.Empreinte: return "Empreinté";
                default: return k.ToString();
            }
        }
    }
}
