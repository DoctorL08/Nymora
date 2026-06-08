using System.Text;
using Quantum;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Build le rich text affiche au survol d'un slot de la timeline combat (ou de ses chips de
    /// statuts, qui remontent au slot par bubbling). Patch UI combat 8 juin : tooltip MÉGA-COMPLET
    /// — toutes les infos d'état du combattant maintenant que les panneaux haut G/D sont supprimés :
    /// classe + stage, HP, PA, PM, ressource (ou leurres Ghostra), venin (xN + durée), marque
    /// Nightseer, et TOUS les statuses actifs (plus de cap à 4) avec leur magnitude.
    /// </summary>
    public static class TimelineSlotTooltipBuilder
    {
        public static string Build(Combatant c, int turnNumber)
        {
            var sb = new StringBuilder(320);

            // En-tête : classe + stage.
            sb.Append($"<size=18><color=#ffe28a><b>{c.Class}</b></color>  <color=#aaa><size=14>(Stage {ComputeStage(c)})</size></color></size>");

            // HP.
            sb.Append($"\n<size=15><color=#ffd060>HP : {c.HP} / {c.MaxHP}</color></size>");

            // PA / PM (réintroduits ici — n'étaient plus visibles depuis la suppression des panneaux).
            sb.Append($"\n<size=14><color=#e0d0a0>PA : {c.PA} / {c.MaxPA}    PM : {c.PM} / {c.MaxPM}</color></size>");

            // Ressource de classe (ou leurres actifs pour Ghostra dont la ressource = RÉMANENCE).
            if (c.Class == NymoraClass.Ghostra)
            {
                int decoys = 0;
                for (int i = 0; i < 3; i++)
                    if (c.Decoys[i].Kind != DecoyKind.None) decoys++;
                sb.Append($"\n<size=14><color=#c8b8ff>Leurres : {decoys} / 3</color></size>");
            }
            else
            {
                int maxRes = CombatantStats.GetMaxResource(c.Class);
                if (maxRes > 0)
                    sb.Append($"\n<size=14><color=#c8b8ff>{ResourceTag(c.Class)} : {c.Resource} / {maxRes}</color></size>");
            }

            // Venin Necram (champ dédié VeninStacks + minuteur caché VeninDecay). Affiche le NOMBRE.
            if (c.VeninStacks > 0)
            {
                int t = VeninTurnsLeft(c);
                string tt = t > 0 ? $" ({t}t)" : "";
                sb.Append($"\n<size=14><color=#9fd36a>Venin : x{c.VeninStacks}{tt}</color></size>");
            }

            // Marque Nightseer (champ dédié CurrentMark).
            if (c.CurrentMark != MarkKind.None && c.MarkTurnsLeft > 0)
            {
                sb.Append($"\n<size=14><color=#ff8060>{MarkLabel(c.CurrentMark)} ({c.MarkTurnsLeft} tour{(c.MarkTurnsLeft > 1 ? "s" : "")})</color></size>");
            }

            // TOUS les statuses actifs (plus de cap). Les statuts cachés (VeninDecay, réservés)
            // renvoient "" via StatusEffectLabel et sont sautés.
            string body = "";
            for (int i = 0; i < 8; i++)
            {
                var s = c.Statuses[i];
                if (s.Kind == StatusKind.None || s.TurnsLeft <= 0) continue;
                string label = StatusEffectLabel.Describe(s.Kind, s.Magnitude);
                if (string.IsNullOrEmpty(label)) continue;
                body += $"\n<size=13>• {label} ({s.TurnsLeft}t)</size>";
            }
            if (body.Length > 0)
                sb.Append("\n\n<size=13><b><color=#c8b8ff>Effets :</color></b></size>").Append(body);

            return sb.ToString();
        }

        // Durée restante du venin = minuteur caché StatusKind.VeninDecay (refresh à chaque marque).
        private static int VeninTurnsLeft(Combatant c)
        {
            for (int i = 0; i < 8; i++)
            {
                var s = c.Statuses[i];
                if (s.Kind == StatusKind.VeninDecay && s.TurnsLeft > 0) return s.TurnsLeft;
            }
            return 0;
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
                // MarkKind.Empreinte : marque legacy supprimee par la refonte 29 mai (plus jamais
                //   appliquee). Mappee sur Traqué au cas improbable d'une marque residuelle.
                case MarkKind.Empreinte: return "Traqué (Marque du Chasseur)";
                default: return k.ToString();
            }
        }
    }
}
