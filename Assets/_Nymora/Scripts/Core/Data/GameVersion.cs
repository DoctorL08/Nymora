using UnityEngine;

namespace Nymora.Core.Data
{
    /// <summary>
    /// Versioning runtime du jeu.
    /// - Current : version client semver. SOURCE = PlayerSettings.bundleVersion (Application.version),
    ///   bumpe par l'outil de publication "Nymora > Build > Publish Update" (Brique L5) sans recompilation,
    ///   et embarque dans le build. NE PAS hardcoder ici.
    /// - CombatRulesVersion : incrementee a CHAQUE modification gameplay/regles.
    ///   Utilisee par l'anti-cheat et le replay system pour rejouer un match avec les regles d'origine.
    /// - BibleVersion : version du document de design (Bible V7.1).
    /// </summary>
    public static class GameVersion
    {
        /// <summary>Version client semver, lue depuis le bundleVersion Unity (Player Settings > Version).</summary>
        public static string Current => Application.version;

        /// <summary>
        /// Version des regles de combat. Incrementer a CHAQUE modif :
        /// - Stats de classe / sort
        /// - Logique de passif
        /// - Effets de marques (saignement, venin, leurres...)
        /// - Modifications du schema Combatant DSL (impact compatibilite replay)
        /// - Modifications du schema Tile DSL (idem)
        /// </summary>
        // 82 (5.12) : délai de 1re action du bot IA (BotFirstMoveDelayTicks) — corrige le bot qui
        //             apparaissait au milieu (move exécuté avant le spawn de sa vue en IA).
        // 83 (5.12) : en mode IA, le joueur (P0) commence toujours (PvP/ranked = aléatoire Bible).
        public const int CombatRulesVersion = 83;

        /// <summary>Version de la Bible (design doc) que ce code implemente.</summary>
        public const string BibleVersion = "V7.1";
    }
}
