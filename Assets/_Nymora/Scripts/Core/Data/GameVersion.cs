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
        // 84 (Tuto T2) : flag RuntimeConfig.TutorialPassiveBot — en mode tutoriel le bot rend son
        //                tour sans agir (mannequin passif). N'affecte aucun match IA/PvP/ranked
        //                (flag false partout ailleurs).
        // 85 (Tuto T5) : flag RuntimeConfig.TutorialFreezeTimer — en tutoriel le timer ne décrémente
        //                pas pendant le tour du joueur (il prend son temps). Idem : false ailleurs.
        // 86 (Refonte 29 mai, Brique 0) : moteur GENERIQUE limites/relances. Ajout des champs
        //                [Networked] Combatant (CastsThisTurnLog/Count/LoggedTurn + SpellCooldownLastTurn)
        //                + SpellDef.MaxUsesPerTurn/CooldownTurns + gates SpellSystem + garde IA.
        //                Purement additif : aucun sort ne renseigne encore de cap/relance
        //                (comportement inchange), les briques de classe rempliront les valeurs.
        // 87 (Fix pièges au passage, 30 mai) : les pièges se déclenchent désormais sur TOUTES les
        //                cases traversées (1) par l'IA — qui se déplace en direct hors
        //                MovementSystem.ApplyMove, ne déclenchait qu'à l'arrêt — (2) par une
        //                poussée (Bourrasque/Onde de Choc/Éboulement) sur toute la trajectoire,
        //                plus seulement la case d'arrivée, et (3) par la CATAPULTE du Piège
        //                Bondissant (éjection FogHelpers) qui survolait les autres pièges sans
        //                les déclencher. Aligne IA + push + catapulte sur le comportement joueur
        //                (MovementSystem.ApplyMove boucle de traversée).
        // 88 (Ghostra brique 1/7 Permutation deckable, 30 mai) : le slot SpellId 90 (ex-Volte-Face,
        //                SUPPRIME du pool) est REUTILISE par GhostraPermutation (1 PA, cap 2x/tour,
        //                swap Ghostra<->un de ses leurres des 1 leurre). Filtre de ciblage
        //                TileWithLure implemente (cibler SES leurres). L'ancienne Permutation
        //                gratuite Angle 3 (touche P) reste dormante / non reactivee.
        // 89 (Ghostra brique 2/7 eco leurres + caps PA, 30 mai) : Replique Fantome 3->2 PA + cap
        //                1x/tour ; caps 1x/tour sur Replique Protectrice / Marque de l'Ombre /
        //                Pas dans l'Ombre / Linceul d'Ombres (moteur generique MaxUsesPerTurn).
        //                + nerf portee Permutation (plateau -> 4 PO).
        // 90 (Ghostra brique 3/7 Éveil Spectral, 30 mai) : slot SpellId 93 (ex-Dague Lancée,
        //                supprimée) reutilise par GhostraEveilSpectral (2 PA, range 4, cap 2x/tour).
        //                Un leurre adjacent a la cible la poignarde (100) ; bonus dorsal + Plaie
        //                calcules depuis la POSITION DU LEURRE (FacingHelpers.IsDorsalFromPosition,
        //                DecoyHelpers.TryFindEveilLeurre, GhostraPassif.ApplyPlaieOuverteFromPosition).
        // 91 (Ghostra brique 4/7 Nuée Spectrale, 30 mai) : slot SpellId 95 (ex-Danse des Lames,
        //                supprimée) reutilise par GhostraNueeSpectrale. AoE Self -> CIBLE UNIQUE :
        //                4 PA, range 2, cap 1x/tour, 100 + 40/leurre actif + 20/leurre adjacent
        //                (max 280, DecoyHelpers.CountOwnDecoysAdjacent). Ne consomme pas ; skip dorsal/Plaie.
        //                + Permutation : cap 2x -> 1x/tour ET gratuite (0 PA) à l'Angle 3 (3 leurres).
        //                + fix preview Nuée (sans dorsal).
        public const int CombatRulesVersion = 91;

        /// <summary>Version de la Bible (design doc) que ce code implemente.</summary>
        public const string BibleVersion = "V7.1";
    }
}
