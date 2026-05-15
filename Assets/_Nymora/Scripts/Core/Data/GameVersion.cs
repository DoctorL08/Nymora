namespace Nymora.Core.Data
{
    /// <summary>
    /// Versioning runtime du jeu.
    /// - Current : version client semver (incrementer a chaque release).
    /// - CombatRulesVersion : incrementee a CHAQUE modification gameplay/regles.
    ///   Utilisee par l'anti-cheat et le replay system pour rejouer un match avec les regles d'origine.
    /// - BibleVersion : version du document de design (Bible V7.1).
    /// </summary>
    public static class GameVersion
    {
        /// <summary>Version client semver. Bumper a chaque release.</summary>
        public const string Current = "0.1.0";

        /// <summary>
        /// Version des regles de combat. Incrementer a CHAQUE modif :
        /// - Stats de classe / sort
        /// - Logique de passif
        /// - Effets de marques (saignement, venin, leurres...)
        /// - Modifications du schema Combatant DSL (impact compatibilite replay)
        /// - Modifications du schema Tile DSL (idem)
        /// </summary>
        public const int CombatRulesVersion = 35;

        /// <summary>Version de la Bible (design doc) que ce code implemente.</summary>
        public const string BibleVersion = "V7.1";
    }
}
