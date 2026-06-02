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
        // 92 (Ghostra brique 5/7 Voile Spectral rework, 30 mai) : slot 97 conserve. Cleanse anti-DoT
        //                + DotImmune RETIRES -> SETUP : 2 PA, range 4 ENEMY, cap 1x/tour, TP tous les
        //                leurres actifs autour de l'ennemi (DecoyHelpers.TeleportAllDecoysAround).
        //                + gate dorsal/Marque sur IsOffensive==1 (fix : sorts non-offensifs ne font
        //                plus de dorsal accidentel sur cible ennemie, ex Marque de l'Ombre / Voile).
        // 93 (Ghostra brique 6/7 Communion Spectrale, 30 mai) : slot SpellId 100 (ex-Pas de l'Au-Delà,
        //                supprimé) reutilise par GhostraCommunionSpectrale. 2 PA self, cap 1x/tour,
        //                consomme 1 leurre -> heal 150 (HealHelper). Gate >=1 leurre. Infra
        //                PasAuDelaReady (MovementSystem/preview/renderer) laissée dormante.
        // 94 (Fix ciblage ligne droite, 1 juin) : Volée d'Épines (Nightseer, Shape Line) rejette
        //                desormais les cibles diagonales comme Choc Sismique / Charge Brutale
        //                (alignement cardinal requis). Garde sim unifie via SpellSystem.SpellIsStraightLine
        //                (+ preview View + skip IA alignes sur la meme liste). Mur de Pierre exclu (pose libre).
        // 95 (Tuto spawn rapproche, 1 juin) : en mode tuto (RuntimeConfig.TutorialPassiveBot), le
        //                mannequin (slot 1) spawn a (3,4) au lieu de (7,2) -> centre-avant, devant le joueur (tuto direct).
        //                Gate tuto only -> aucun impact ranked. Suit le pattern TutorialPassiveBot/FreezeTimer.
        // 96 (Volee d'Epines : Filet derriere la cible, 1 juin) : le Filet de Ronces se pose desormais
        //                UNE CASE plus loin dans le sens du tir (derriere la derniere cible touchee, en
        //                s'eloignant du caster) au lieu de sur la case de la cible. Fallback sur la case
        //                de la cible si la case derriere est hors grille / non walkable.
        // 97 (Fix crash recursion Piege Bondissant, 2 juin) : deux Pieges Bondissants qui s'ejectaient
        //                mutuellement la cible la renvoyaient en boucle infinie (StackOverflow -> halt sim
        //                + ecran de desync Quantum). Fix dans FogHelpers.TryTriggerTrapOnEnter : (1) le
        //                piege est consomme AVANT de rejouer la trajectoire de catapulte, (2) garde de
        //                profondeur de recursion (PiegeBondissantMaxChainDepth). Pure logique, aucun champ
        //                [Networked] touche.
        // 98 (Fix TP sur Faille/obstacle, 2 juin) : on ne peut plus teleporter ni etre deplace (MoveNonPM)
        //                sur une case occupee par un OBSTACLE (Faille d'Effondrement, Pilier, Mur). Avant,
        //                MoveNonPM ne checkait pas HasObstacleAt et le filtre EmptyTile considerait une
        //                Faille comme case vide -> on TP sur une Faille pour s'echapper de l'ulti Colossar
        //                sans la casser ni attendre son expiration. Fix : MatchesFilter(EmptyTile) exige
        //                desormais !HasObstacleAt, et MoveNonPM rejette les cases-obstacles (defense).
        //                Pure logique, aucun champ [Networked] touche.
        // 99 (Pas d'obstacle sur embuche/leurre, 2 juin) : on ne peut plus poser un Pilier / Mur /
        //                Faille (Effondrement) sur une case portant une EMBUCHE (piege Nightseer) ou
        //                un LEURRE Ghostra. Garde par-case dans ObstacleHelpers.SpawnObstacle (Mur saute
        //                le segment, Failles sautent la case) + pre-check pre-PA pour le Pilier (cast
        //                unique, evite de gaspiller le tour). Pure logique, aucun champ [Networked] touche.
        // 100 (Pas d'embuche sur case occupee, 2 juin) : symetrique du 99. Le Nightseer ne peut plus
        //                poser de piege (Filet de Ronces / Champ de Mines / Piege Bondissant) sur une
        //                case occupee par un COMBATTANT (joueur), un OBSTACLE (Pilier/Mur/Faille) ou un
        //                LEURRE Ghostra. Garde par-case dans FogHelpers.PlaceTrap (Champ de Mines saute
        //                la case, poses secondaires couvertes) + pre-check pre-PA pour Filet de Ronces
        //                et Piege Bondissant (pose unique). Pure logique, aucun champ [Networked] touche.
        // 101 (Anti-teleport respecte par les self-teleports, 2 juin) : un caster sous AnchorImmune
        //                (Ancrage Colossar / Stoicisme) ou AntiTeleport (Rugissement Soulrender) ne peut
        //                plus lancer un sort qui le teleporte (Pas Furtif, Evanescence, Traquenard, Dernier
        //                Pas, Pas dans l'Ombre, Frappe Fantome, Permutation). Avant, ces statuts ne
        //                bloquaient que les deplacements SUBIS -> un Nightseer ancre pouvait encore se TP.
        //                Helper SpellIsSelfTeleport + reject pre-PA. Pure logique, aucun champ [Networked] touche.
        // 102 (Passe d'equilibrage, 2 juin) : Colossar Mur de Pierre relance 2 tours · Ghostra Replique
        //                Protectrice 4->3 PA + duree 3->4 rounds (= leurre classique) · Ghostra Lame Vorace
        //                3->2 PA, 130->110 dmg, cap 2x/tour (outil de spam, distinct de Lame Spectrale) ·
        //                Soulrender Tranche-Ame portee 1->2 (anti-kite) · Necram Brume Toxique relance 2 tours ·
        //                Nightseer Tir Precis portee 6->4 + 200/280->150/210 dmg + 1x/tour · Nightseer Pas
        //                Furtif 2->4 PA + portee 4->3. Pure logique, aucun champ [Networked] touche.
        // 103 (Colossar tape ses propres obstacles, juin) : un sort offensif endommage desormais
        //                AUSSI les obstacles OWN du caster sur ses cases d'effet (avant : seuls les
        //                obstacles adverses). Le Colossar peut donc casser ses propres Piliers / Murs /
        //                Failles (degager une Faille, abattre un Mur, detruire un Pilier -> Densite
        //                Inerte +30 HP). Deux gardes ouvertes dans SpellSystem : (1) validation de cible
        //                (offensiveObstacleTarget) accepte desormais TOUT obstacle, plus seulement
        //                l'adverse -> un sort Filter=Enemy/AnyUnit peut viser un obstacle own ; (2) boucle
        //                damage : owner-check retire -> l'obstacle own prend les degats. Helper
        //                IsAdverseObstacleAt supprime (remplace par ObstacleHelpers.HasObstacleAt). Pure
        //                logique, aucun champ [Networked] touche.
        // 104 (Eboulement sur Mur/Faille, juin) : le pre-check d'Eboulement (ColossarSoinLourd)
        //                acceptait uniquement un PILIER own ; il accepte desormais TOUT obstacle own
        //                (Pilier/Mur/Faille). Le handler detruit l'obstacle quel que soit son Kind
        //                (AoE 150 + push identiques) ; le +30 HP Densite Inerte reste reserve au Pilier
        //                (DestroyObstacle). Description deck builder synchronisee. Pure logique, aucun
        //                champ [Networked] touche.
        // 105 (Conso ressource optionnelle en AUTO, juin) : le bind manuel "Shift+X" ayant ete retire
        //                le 17 mai, cmd.HGSpend arrivait toujours a 0 -> les bonus de consommation
        //                optionnelle ne partaient JAMAIS et la ressource n'etait pas consommee.
        //                SpellSystem auto-remplit desormais hgSpend au max finançable apres le cout
        //                obligatoire (cmd.HGSpend conserve comme plafond optionnel pour IA/futur UI).
        //                Touche 9 sorts : Ouvre-Plaie, Seve Vive, Detonation Sanglante, Bourrasque, Pas
        //                Furtif, Mur de Pierre, Regeneration Necrotique, Pas dans l'Ombre, Detonation
        //                Onirique. Descriptions deck builder synchronisees. Pure logique, aucun champ
        //                [Networked] touche.
        public const int CombatRulesVersion = 105;

        /// <summary>Version de la Bible (design doc) que ce code implemente.</summary>
        public const string BibleVersion = "V7.1";
    }
}
