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
        // 106 (Passe equilibrage juin) : Nightseer Bourrasque — option "1 PR -> push 4 cases" RETIREE
        //                (HGCostMaxOptional 1 -> 0, push fixe 2 cases, const BonusBourrasquePushBonus1PR
        //                supprimee). Soulrender Charge Brutale 4 -> 3 PA. Descriptions deck builder
        //                synchronisees. Pure logique, aucun champ [Networked] touche.
        // 107 (Nerf FD du Mur, juin) : Mur de Pierre donnait +1 FD PAR SEGMENT (= +3 a +5 FD/Mur via
        //                le hook SpawnObstacle). Desormais +2 FD FLAT par Mur pose. SpawnObstacle gagne
        //                un flag gainFondation (defaut true ; false pour les segments de Mur) ; le
        //                handler accorde MurDePierreFondationGain (2). GainFondation gagne un param
        //                amount. Pilier (+1) et Failles (owner=None -> 0 FD) inchanges. Description
        //                synchronisee. Pure logique, aucun champ [Networked] touche.
        // 108 (Passe equilibrage 5 juin / brique B3) : Soulrender Tranche-Ame portee 2 -> 1 (annule
        //                l'anti-kite de v102) · Soulrender Charge Brutale cap 1x/tour · Colossar Ancrage
        //                -2 PM -> -1 PM + relance 2 -> 3 tours · Colossar
        //                Frappe Lourde cap 2x/tour · Colossar Onde de Choc cap 2x/tour · Ghostra Pas dans
        //                l'Ombre portee 5 -> 4 + relance 2 tours (cap 1x/tour deja en place). Caps/relances
        //                via le moteur generique (SpellDef.MaxUsesPerTurn/CooldownTurns, gate universel
        //                SpellSystem). Descriptions deck builder synchronisees. Pure logique, aucun champ
        //                [Networked] touche.
        // 109 (Bugs ressource 5 juin / brique B4) : (1) Soulrender Seve Vive — le bonus +50 HP "si DoT
        //                actif" testait StatusKind.BleedDoT, un statut MORT que rien n'applique -> bonus
        //                jamais declenche. Detecte desormais les vrais DoT (VeninStacks + PlaieOuverte +
        //                BleedDoT). (2) Necram Putrefaction — le gain de PT via marques mourait quand la
        //                cible etait saturee a 4 marques (applied=0). Gain base sur l'INTENTION (amount
        //                demande, cap 2/tour conserve) -> le Necram gagne ses PT meme a saturation ;
        //                DotImmune (Voile Spectral) court-circuite -> 0 PT. (3) Necram Inoculation cap
        //                1x/tour (anti-spam marques/PT, complement du fix PT). (#6b Charge Brutale +1 HG :
        //                aucun changement, decision Lorenzo "garder tel quel".) Pure logique, aucun
        //                champ [Networked] touche.
        // 110 (B5 #12, 5 juin) : INTERDICTION de poser un piège Nightseer sur une case qui porte déjà
        //                un piège (own ou adverse) — plus d'écrasement. Reject pré-PA pour Filet de Ronces
        //                / Piège Bondissant (PA non consommé) + garde par-case dans FogHelpers.PlaceTrap
        //                (le Champ de Mines saute les cases déjà piégées). Le reste de B5 (#8/#9/#11/#13)
        //                est View-only. Pure logique, aucun champ [Networked] touche.
        // 111 (B6 Ghostra, 5 juin) : (#20) Éveil Spectral REWORK auto-TP — téléporte un de tes leurres sur
        //                une case adjacente (dorsal-prioritaire) à la cible PUIS le fait poignarder (plus
        //                besoin de pré-positionner). (#21) Voile Spectral — après le TP des leurres, 60 dmg
        //                par leurre adjacent à la cible (max 180), via le pipeline standard. (#22) Poser un
        //                4e leurre (cap 3) vire le PLUS ANCIEN au lieu de partir dans le vide (Réplique
        //                Fantôme/Protectrice + Dernier Pas via DecoyHelpers.TrySpawnEvictingOldest). Les
        //                mutations de leurres sont faites APRÈS le commit du cast. + Fix Pas dans l'Ombre :
        //                le leurre sur la case quittée est posé INCONDITIONNELLEMENT (cap 3) — la conso auto
        //                du 3 juin confondait l'option avec la jauge de leurres (HGCostMaxOptional 1->0).
        //                Pure logique, aucun champ [Networked] touche.
        // 112 (#1 tooltip degats, 5 juin) : Charge Brutale infligeait ses degats via un chemin CUSTOM
        //                (ApplySpellSpecificEffects) qui bypassait le pipeline offensif generique ->
        //                Pacte de Sang +50% / Frenesie / Sang Bouillant n'etaient JAMAIS appliques (le
        //                HUD les prevoyait pourtant via SpellPreview = "l'effet ne marche pas"). CB passe
        //                desormais par ApplyOffensiveCasterBuffs + consomme les buffs one-shot localement.
        //                SpellPreview.FinalizeOffensive complete avec Frenesie + Sang Bouillant (preview
        //                == sim). Pure logique, aucun champ [Networked] touche.
        // 113 (#5 tp/charge declenchent les pieges, 5 juin) : un deplacement FORCE (teleport, Charge
        //                Brutale, recul Tranche-Ame, swap Echange Spectral) ne declenchait PAS les pieges
        //                Nightseer (Filet/Mine/Bondissant) -> l'ennemi les contournait via mobilite. Le
        //                trigger TryTriggerTrapOnEnter (deja appele par la marche + les push) est desormais
        //                appele en post-move : centralise dans MovementHelpers.MoveNonPM (Charge Brutale
        //                landing, Frappe Fantome, Dernier Pas, Pas dans l'Ombre, recul Tranche-Ame) +
        //                ajoute aux teleports a pose directe (Pas Furtif, Evanescence, Traquenard, Echange
        //                Spectral). Charge Brutale declenche en plus les cases TRAVERSEES. Owner-filtre
        //                (jamais ses propres pieges). Permutation N/A (case-leurre ne peut pas porter de piege).
        //                Pure logique, aucun champ [Networked] touche.
        // 114 (#23 couleurs miroir, 5 juin) : ajout d'un champ [Networked] FogTile.TerrainOwner
        //                (Byte, convention PlayerIndex+1) ecrit par GridHelpers.SetTerrain pour
        //                l'AFFICHAGE (outline d'equipe en match miroir). La sim ne lit jamais ce
        //                champ -> aucun impact gameplay, mais c'est un champ [Networked] => REGEN
        //                (codegen Quantum + prefabs/scenes) + rebuild standalone requis.
        // 115 (Fix gain PT Necram en miroir, 5 juin) : en match miroir Necram vs Necram, un seul
        //                Necram gagnait des PT (Putrefaction) et pas l'autre. Cause : les deux hooks
        //                de gain (GainPutrefactionFromMarkApply via marque + GainPutrefactionFromTick
        //                via tick venin) bouclaient sur Filter<Combatant> et creditaient le PREMIER
        //                Necram vivant rencontre (ordre du filter) -> toujours le meme. Fix : crediter
        //                le Necram dont l'EQUIPE (PlayerIndex) != celle de la cible affectee — le venin
        //                etant toujours pose sur un ennemi du Necram, ce Necram est forcement l'auteur
        //                (1v1 + miroir). Bonus : le cas Carapace Visqueuse (venin pose par un Ghostra
        //                sur l'attaquant) ne credite plus a tort un Necram. Pure logique, aucun champ
        //                [Networked] touche.
        // 116 (Fix anti-deplacement Stoicisme/Ancrage vs Piege Bondissant, 5 juin) : la catapulte du
        //                Piege Bondissant (Nightseer) deplacait un Colossar sous Stoicisme ou Ancrage,
        //                alors que ces statuts (AnchorImmune) disent "rien ne me deplace". Cause : le
        //                catapultage dans FogHelpers.TryTriggerTrapOnEnter ne checkait pas AnchorImmune
        //                (contrairement au push PushAndTriggerEx, au pull Empoignade et au swap Echange
        //                Spectral). Du point de vue joueur l'immunite marchait "un tour sur deux" selon
        //                le moyen de deplacement adverse. Fix : sous AnchorImmune la catapulte est annulee,
        //                mais le piege est tout de meme declenche (consomme + Traque + PR au owner).
        //                Pure logique, aucun champ [Networked] touche.
        // 117 (Nerf Traquenard PM malus, 5 juin) : la PARALYSIE de la signature Nightseer Traquenard
        //                passe de -3 PM a -2 PM (TraquenardParalysiePMMalus 3->2). Duree inchangee
        //                (1 tour, deja le cas malgre l'observation "3 tours") et -2 PA conserve.
        //                Description deck builder synchronisee. Pure logique, aucun champ [Networked] touche.
        // 118 (Necram : poisons durent 2 tours max, 5 juin) : les marques de venin n'expiraient JAMAIS
        //                par duree (consommation only). Desormais elles expirent VeninDurationTurns (2)
        //                rounds apres la DERNIERE application. Implementation via un nouveau StatusKind
        //                VeninDecay (=33) : minuteur refresh a chaque ApplyMark ; a son expiration
        //                (DecrementAllOnTurnEnd) le hook VeninHelpers.ClearExpiredVenin (fin de round)
        //                vide les VeninStacks. NB : ajout d'une valeur d'enum Byte -> codegen Quantum +
        //                rebuild standalone requis, mais PAS de regen prefab/scene (layout inchange).
        // 119 (Charge Brutale ne touche que si melee, 5 juin) : Charge Brutale infligeait ses 180 degats
        //                a la cible bloquante meme si un piege REPULSANT (Bondissant Nightseer) avait
        //                catapulte le caster hors de sa case d'arrivee pendant le deplacement. Desormais
        //                les degats ne partent que si le caster finit bien sur sa case d'arrivee prevue
        //                (finalX/finalY, adjacente a la cible) ET vivant (garde chargeConnected). Pure
        //                logique, aucun champ [Networked] touche.
        // 120 (Nightseer Detonation Onirique AoE + chaine pieges Salve/Detonation, 5 juin) :
        //                (1) Detonation Onirique passe de SingleTile (mono-case, l'AoE 2x2 annoncee
        //                n'avait jamais ete codee) a une vraie AoE Square3x3 (9 cases) — geree par
        //                TargetingResolver donc sim ET preview View d'un coup. (2) Salve Mortelle ET
        //                Detonation Onirique "declenchent tes embuches" : nouveau FogHelpers
        //                .DetonateOwnTrapsInArea -> detone TOUS les pieges du caster sous la zone
        //                (ennemi present = effet complet ; case vide = piege consomme). Avant : Salve
        //                ne declenchait que sur ennemi occupant, Detonation rien. Descriptions deck
        //                builder synchronisees. Pure logique, aucun champ [Networked] touche.
        // 121 (Leurres Ghostra avec reserve d'HP, 5 juin) : decision Lorenzo. Tous les leurres
        //                NON-protecteurs (Standard residuels + Replique Fantome) passent de 1-hit a
        //                200 HP ; la Replique Protectrice passe de 200 a 250 HP. Desormais TOUS les
        //                leurres ENCAISSENT les degats (DecoyHelpers.HitDecoyByEnemyAction generalise)
        //                et ne sont detruits qu'a 0 HP (renverse la regle Bible "un sort detruit le
        //                leurre"). Charge Brutale qui s'arrete sur un leurre lui inflige ses degats au
        //                lieu de le detruire d'office. HP de spawn centralise dans GetDecoyMaxHp.
        //                Descriptions deck builder (Replique Fantome / Protectrice) synchronisees.
        //                DecoySlot.HP existait deja -> aucun champ [Networked] ajoute, pas de regen.
        // 122 (Nightseer : clivage Detonation Onirique / Salve Mortelle + nettoyage Empreinte, 6 juin) :
        //                decision Lorenzo. Identite "Piege vs Execution" :
        //                DETONATION ONIRIQUE = setup/pression. Croix de 5, portee FIXE 5 (option "2 PR ->
        //                  portee 10" RETIREE), 170 dmg. SEUL bonus = DETONE tes pieges sous la croix :
        //                  +30 PLAT par piege (ZoneTrapDetonationSurplusDmg) + TRAQUE aux ennemis de l'AoE
        //                  (un ennemi n'est jamais pile sur une case-piege), et GENERE +1 PR par piege
        //                  detone. Cout 0 PR. (Ancien "+80 si couvre un piege" RETIRE le 6 juin.)
        //                SALVE MORTELLE = finisher d'execution. Croix de 5 -> CARRE PLEIN 3x3 (9 cases),
        //                  160 centre / 90 autour (nerf : etaient 200/120). Bonus "cible Traque" RETIRE
        //                  (choix Lorenzo) : le seul bonus est la zone des pieges -> chaque piege du caster
        //                  sous la zone ajoute +40 dmg (nerf, etait 50 ; SalveMortelleTrapBonusDmg) SANS
        //                  etre consomme (les pieges restent poses, cf ApplyZoneTrapBonusNoConsume). Ne
        //                  genere pas de PR (corrige "Salve ne consomme pas les 3 PR" : avant, ses
        //                  detonations remboursaient son cout). DEPENSE 3 PR. Relance 2 tours.
        //                + Fix PREVIEW inexact : le bonus de phase Nightseer (+30 flat P2+) etait evalue
        //                  sur la ressource AVANT la depense des 3 PR cote preview, mais la sim consomme
        //                  AVANT d'appliquer le bonus (Resource post-depense). Salve a 3 PR retombe sous
        //                  la phase 2 -> jamais de +30 ; le preview le reflete desormais (phaseResourceOverride
        //                  dans SpellPreview.ComputeOffensiveBonusGeneric/TryComputeOffensiveSimple).
        //                + Camouflage de Ronces (aura RoncesAura) applique TRAQUE au lieu d'Empreinte
        //                  (legacy pre-refonte ; la refonte 29 mai a unifie les marques Nightseer sur
        //                  TRAQUE). Tous les textes "empreinte" retires des descriptions Nightseer (spell
        //                  + passif + ressource + lore). Enum MarkKind.Empreinte conserve (inutilise).
        //                Aucun champ [Networked] ajoute, pas de regen.
        // v123-132 (7 juin 2026) — GROSSE PASSE PATCHS multi-classes (liste Lorenzo) :
        //   v123 Soulrender Pacte de Sang : +3 HG -> +2 HG, +50% -> +25% dgts (nerf burst).
        //   v124 Soulrender Ouvre-Plaie : cap 2x/tour -> 1x/tour.
        //   v125 Soulrender Empoignade : LIGNE DROITE uniquement (ajout a SpellIsStraightLine).
        //   v126 Colossar Stoicisme : anti-deplacement 2 tours -> 1 tour (bouclier reste 2 tours).
        //   v127 Colossar Provocation : surcout +2 PA (sorts non-ciblant) -> +1 PA sur TOUS les sorts,
        //        centralise dans EffectiveStats.GetPACost ; + relance 2 tours.
        //   v128 Ghostra leurres : Standard/Fantome 200 -> 100 HP ; Protectrice 250 -> 200 HP.
        //   v129 Nightseer Volee d'Epines : 4 PA -> 3 PA.
        //   v130 Nightseer Frappe de l'Ombre : refonte EXECUTEUR (160 + 120 si TRAQUE = 280, consomme
        //        Traque ; plus de +50 PM ni d'application de Traque).
        //   v131 Nightseer Marque du Chasseur -> AFFUT : self-buff +2 portee / +10% dgts 2 tours,
        //        relance 3 tours (ne pose PLUS Traque). Nouveau StatusKind.AffutActive (=34, enum Byte,
        //        codegen Quantum mis a jour, PAS de regen prefab/scene).
        //   v132 Nightseer Fleche Tracante (slot NightseerVoileDOmbre) -> REPLI EPINEUX : self,
        //        2 PA, push 3 cases tous ennemis adjacents + heal 100, relance 1 tour.
        // v133 (7 juin 2026, suite) — ajustements portee :
        //   - Affut : le +2 portee s'affiche desormais aussi dans le PREVIEW View (la sim l'appliquait
        //     deja au cast ; sans ca les cases gagnees n'etaient pas surlignees -> "Affut n'ajoute pas
        //     la portee"). Fix View-only mais regroupe sous ce bump.
        //   - Ghostra Replique Fantome : portee 4 -> 3.
        //   - Ghostra Permutation : portee 4 -> 3.
        //   - Nightseer Tir Precis : 3 PA -> 2 PA, portee 4 -> 5.
        // v134 (7 juin 2026, suite) — FIX cast == preview pour les buffs offensifs du caster :
        //   Pacte/Peau de Fer/Frenesie/AFFUT/Sang Bouillant etaient appliques en amont sur effectiveDmg
        //   et donc PERDUS par les sorts qui recalculent les degats par cible (Tir Precis Traque -> 210,
        //   Frappe +120) ou a DamageAmount=0 (Salve) -> "Affut +10% pas applique au cast". Desormais
        //   appliques PAR CIBLE via ApplyOffensiveCasterBuffs (apres bonus flat phase/Densite/Marque),
        //   meme ordre que SpellPreview.FinalizeOffensive.
        // v135 (7 juin 2026, suite) — ajustements :
        //   - Ghostra leurres HP : FIX oubli v128 (les constantes DecoyHelpers etaient restees 200/250 :
        //     seules les descriptions avaient ete changees). Standard/Fantome 200 -> 100, Protectrice 250 -> 200.
        //   - Ghostra Frappe Fantome : portee 4 -> 3.
        //   - Nightseer Repli Epineux : push 3 -> 2, relance 1 -> 2 tours.
        //   - Nightseer Tir Precis : portee 5 -> 4 (revert ; reste 2 PA).
        // v136 (7 juin 2026) — Pas dans l'Ombre (Ghostra) + Pas Furtif (Nightseer) INTERDITS au tour 1
        //   (TurnNumber <= 1) : reject pre-PA (tour non gache). Anti-kite d'ouverture.
        // v137 (7 juin 2026) — Nightseer phase 2 : le +1 PORTEE est RETIRE (decision Lorenzo). La
        //   phase 2 ne donne plus que le +30 degats flat. RangeWithPhaseBonus = pass-through ;
        //   FlatDamageRangeBonusActive renomme FlatDamageBonusActive ; RangeBonus const = 0 (legacy).
        // v138 (7 juin 2026) — portees : Ghostra Eveil Spectral 4 -> 3, Voile Spectral 4 -> 3 ;
        //   Nightseer Detonation Onirique 5 -> 4.
        // v139 (7 juin 2026) — Affut (Marque du Chasseur) ajoute aux sorts INTERDITS au tour 1
        //   (gate sim + indicateur "1t" grise dans la barre + description).
        // v140 (7 juin 2026 bis) — Nerf buffs Soulrender : Peau de Fer bonus melee 30 -> 10,
        //   Sang Bouillant prochaine frappe 30 -> 15, Frenesie +10% -> +5% dgts.
        // v141 (8 juin 2026) — FIX BUGS MIROIR Necram : densite venin PAR-NECRAM au lieu de poolee
        //   (GetDensityOnTeam / GetDensityAppliedByNecram). Tick/regen/halo/Detonation/Virus Fatal/preview
        //   lisent le pool du bon Necram ; heal Symbiose Morbide scopé au proprietaire du venin (x3 sites).
        //   En 1v1 non-miroir : comportement identique. Le tier 3 (densite 7+) n'est plus atteignable en
        //   1v1 (c'etait un artefact du pooling miroir). Virus Fatal reste gate sur 6 PT, pas la densite.
        // v142 (8 juin 2026) — Patchs Necram degats directs (patch list) : Crachat Acide 90 -> 100,
        //   Morsure Putride base 110 -> 120 + bonus/marque 22 -> 10 (cap +40, max 160), Inoculation passe
        //   offensif + 30 dgts (etait 0).
        // v143 (8 juin 2026) — Brume Toxique (patch list) : cout 4 -> 2 PA, duree 3 -> 2 tours, Brumes
        //   SUPERPOSABLES (retrait du rejet de chevauchement, sans cumul), -1 PM si un combattant DEMARRE
        //   son tour dans une Brume ADVERSE. TOUS les effets Brume (marques cast/entree/fin de tour +
        //   tick majore + kick PM) passent OWNER-BASED : immunise a SA propre Brume uniquement -> un
        //   Necram adverse est bien marque + ralenti par la Brume ennemie (fix Necram vs Necram).
        //   L'owner de terrain devient un MASQUE 2 bits (1=P0,2=P1,3=case CONTESTEE) -> 2 brumes adverses
        //   superposees affectent les DEUX (chacun par celle de l'autre) + chacun genere son PT ; aucun
        //   champ networked ajoute (reutilise le byte existant). Contour terrain : violet si contestee.
        // v144 (8 juin 2026) — Toutes classes 1500 -> 2000 HP (CombatantStats.BaseMaxHP). Degats INCHANGES
        //   (marge pour temporiser). SO de classe (BaseHP) synchronises pour l'affichage hub.
        // v145 (8 juin 2026) — Soulrender : (1) Sang Bouillant declenche desormais aussi sur les ticks de
        //   POISON (venin), pas seulement les degats de sort -> +1 HG + prochaine frappe (VeninHelpers.TryTick).
        //   (2) Vapeur Carmin (trainee de Charge Brutale) OWNER-IMMUNE : ne coute plus +1 PM au Soulrender
        //   qui l'a posee, seule la Vapeur ADVERSE ralentit (MovementSystem). (Le 'sang coagule' de la
        //   patch list visait en fait cette trainee de Vapeur.) Reliquat mineur : meme exemption cote IA.
        // v146 (8 juin 2026) — Ghostra (bloc patch, partiel) : Pas dans l'Ombre pivote les ennemis adjacents
        //   DOS a la Ghostra (au lieu de face). Lame Spectrale 170 -> 130, retire le +60 Plaie Ouverte,
        //   remplace par "retourne la cible dos au caster" (apres le dorsal). Suite du bloc Ghostra a venir.
        // v147 (8 juin 2026) — Ghostra (2/2) leurres : Éveil Spectral priorise un leurre DÉJÀ adjacent en
        //   position DORSALE (au lieu de prendre le 1er slot et finir non-dorsal). Voile Spectrale : un leurre
        //   déjà sur une cardinale ne se déplace plus inutilement (autorise sa propre case) -> le 3e leurre ne
        //   part plus en corner quand le Ghostra + 2 leurres occupent les cardinales. (#1 ciblage + #3 poison
        //   ANNULÉS : les leurres restent tués par sorts directs, le poison ne les touche pas.)
        // v148 (8 juin 2026) — Nightseer (partiel) : Filet de Ronces cap 2 -> 1x/tour ; Pas Furtif 4 -> 3 PA
        //   + relance 1 tour (CooldownTurns=1, systeme generique -> AUCUN champ networked). Suite a venir.
        // v149 (8 juin 2026) — Nightseer pièges : expirent au bout de 6 tours (FogHelpers.ClearExpiredTraps en
        //   fin de round, reutilise TrapAppliedOnTurn -> aucun champ networked) + compteur de tours restants
        //   au-dessus de chaque piège, VISIBLE CASTEUR-only (TrapView).
        // v150 (8 juin 2026) — Nightseer SIGNATURE Traquenard (refonte) : ciblage 2 clics (1er=cible, 2e=
        //   direction). 280 dgts (inchanges) + POUSSE la cible 2 cases dans la direction + le NS prend la CASE
        //   D'ORIGINE de la cible (libérée par la poussée ; fallback case adjacente libre). Paralysie + marque/
        //   voile inchanges. Si la cible meurt des degats : ni poussee ni TP. Traquenard ajoute a IsDirectionalSpell (View).
        // v151 (8 juin 2026) — Colossar (partiel) : Provocation ne pose PLUS -1 PM (garde +2 PA cost + 100 dmg
        //   si pas adjacent). Renvoi du Bouclier 3 -> 2 PA + relance 1 tour (CooldownTurns=1) + cap retours 4 -> 2.
        // v152 (8 juin 2026) — Colossar Représailles REFONTE en survie (heal d'urgence) : self, 2 PA, utilisable
        //   SOUS 50% HP (gate pre-validation), heal 200 (HealHelper) + petite riposte mêlée (RipostMelee 50,
        //   1 tour, cap 2). Plus de dégâts offensifs. Catégorie deck builder -> Survie. Comble le gros heal
        //   perdu (Soin Lourd -> Éboulement). UTILISABLE 1x/MATCH (OncePerMatchBit=6, champ Int32 existant).
        // v153 (8 juin 2026) — Colossar Piliers/Murs (#16) : cap 6 CASES d'obstacle par Colossar (Failles
        //   EXCLUES) ; au 7e, EnforceObstacleCap detruit le plus ancien (silencieux, pas de heal Densite
        //   Inerte). Mur de Pierre PERSISTANT (timer 2 tours RETIRE). ExpiresOnTurn reutilise comme TOUR DE
        //   POSE pour Piliers/Murs (l'expiration-timer ne s'applique plus qu'aux Failles) -> AUCUN champ
        //   networked ajoute. View : numero d'ordre STRICT 1->N au-dessus de chaque Pilier/Mur, CASTEUR-only
        //   (du plus ancien au plus recent ; segments d'un mur numerotes gauche->droite via gx-gy).
        // v154 (8 juin 2026) — patch list ligne 7 : les sorts panic "low-HP" 1x/match de chaque classe passent
        //   de <30% HP / 4 PA a <50% HP / 2 PA : Dernier Souffle (Soulrender), Evanescence (Nightseer), Dernier
        //   Pas (Ghostra), Cocon Putride (Necram, signature). Represailles (Colossar) etait deja a <50% / 2 PA.
        // v155 (9 juin 2026) — Phase 5 brique 5.1 (FONDATIONS 2v2/3v3) :
        //   Passe A : champ Combatant.TeamId + RuntimePlayer.TeamId + helper central TeamHelper (predicats
        //     allie/ennemi) + CombatState.WinnerTeamId. Victoire = "derniere EQUIPE debout" (EvaluateTeamMatchEnd).
        //   Passe B : conversion de TOUS les call-sites allie/ennemi de la sim vers TeamHelper + TIR ALLIE OFF
        //     (boucle de degats AoE principale, auras Ronces/Halo/Venin, pieges Nightseer, LoS, brume Toxique
        //     re-clee sur TeamId, leurres Ghostra, Effondrement, Provocation, etc.). Les checks owner/self/active
        //     player + l'AI (exclue du multi) + helpers View restent en PlayerIndex.
        //   INVARIANT 1v1 : team == slot == PlayerIndex -> comportement STRICTEMENT identique (non-regression).
        //   Le tir allie ne s'observe qu'en 2v2/3v3 (validation a la brique 5.5, scenes equipe).
        // v156 (9 juin 2026) — Phase 5 brique 5.2 (rotation N-joueurs + ordre vote capitaine) :
        //   CombatState.PlayerCount (dynamique) + TurnOrder[6] + StartingTeam + TurnOrderBuilt ;
        //   RuntimeConfig.PlayerCount (pose par bootstrap, 1v1=defaut 2) ; RuntimePlayer.TeamOrder +
        //   Combatant.TeamOrder (rang intra-equipe, vote 5.6, defaut PlayerIndex). La rotation suit
        //   TurnOrder (alternance stricte entre equipes, ordre intra-equipe par TeamOrder) au lieu d'un
        //   modulo ; longueur de round = PlayerCount. TurnConstants.MaxPlayers=6 borne les scans de
        //   commandes par slot. INVARIANT 1v1 : meme draw RNG (Next(0,2)) + TurnOrder=[start,autre] ->
        //   comportement strictement identique. La FSM de tour attend que tous les Combatants soient
        //   spawnes (TryBuildTurnOrder) avant de juger forfait/MatchEnd.
        // v157 (9 juin 2026) — Phase 5 brique 5.3 (cadavre-obstacle + forfait/déco par joueur) :
        //   - mort = le Combatant N'EST PAS détruit et garde sa case -> CADAVRE-OBSTACLE (le mouvement
        //     et le pathfinding bloquent déjà sur tout occupant). LoS : HasLineOfSight bloque désormais
        //     aussi sur un cadavre (HP<=0), obstacle NEUTRE pour tous.
        //   - forfait/déconnexion = KO du JOUEUR (HP=0, cadavre) au lieu de faire perdre toute l'équipe ;
        //     l'équipe continue en infériorité, EvaluateTeamMatchEnd décide du « dernière équipe debout ».
        //   - rotation : EnterTurnStart SAUTE le sous-tour d'un joueur KO (pas de tour pour un mort).
        //   INVARIANT 1v1 : la mort déclenche MatchEnd immédiat (1 seul joueur/équipe) -> cadavre/skip
        //   jamais atteints ; forfait/déco aboutissent au même verdict qu'avant. Comportement identique.
        // v158 (9 juin 2026) — Phase 5 brique 5.4a (grille agrandie) : GridConstants 10x10 -> MAX 15x15
        //   (225, = stride d'index + taille des fixed arrays Grid/Fog/Obstacle .qtn). Dimensions LOGIQUES
        //   par mode dans GridSingleton.Width/Height (1v1 10 / 2v2 12 / 3v3 15) posées par GridSystem.OnInit
        //   depuis RuntimeConfig.PlayerCount ; Walkable=1 seulement dans la zone logique (le reste du
        //   tableau MAX = non-walkable). TargetingResolver énumère la zone logique. View (GridRenderer) :
        //   GetTileView/TryGetWorldBounds sur dims logiques (cachées au spawn). INVARIANT 1v1 : zone 10x10
        //   aux mêmes coords + centrage sur 10x10 -> rendu et gameplay strictement identiques. Forme
        //   irrégulière (carve) + MapAsset + éditeur = sous-briques 5.4b/c/d.
        // v159 (9 juin 2026) — Phase 5 brique 5.4b (MapAsset Quantum) : AssetObject NymoraCombatMap
        //   (Width/Height + masque Walkable irrégulier + Spawns par équipe/rang) référencé par
        //   RuntimeConfig.CombatMap (AssetRef = GUID synchronisé, chargé localement -> déterministe).
        //   GridSystem.OnInit applique la forme/dims de la map si présente (sinon zone rectangulaire
        //   LogicalDims) ; CombatantSystem spawn aux points (Team,Rank) de la map (sinon hardcodé).
        //   Code DORMANT en 1v1 (aucune map -> Id invalide -> fallback) : comportement identique.
        //   Pas de .qtn modifié (AssetObject + RuntimeConfig = C# pur). L'éditeur (5.4c) crée les maps.
        // v160 (9 juin 2026) — Phase 5 brique 5.5 (scène 2v2 hot-seat) : CombatantSystem.OnPlayerAdded
        //   levait un cap 1v1 résiduel (slot>1 ignoré) -> en 2v2 les slots 2/3 (équipe 1) ne spawnaient
        //   jamais (seuls 2 combattants sur 4). Cap relevé à TurnConstants.MaxPlayers (6). INVARIANT 1v1 :
        //   seuls les slots 0/1 arrivent -> comportement strictement identique. Permet enfin le spawn des
        //   4 combattants 2v2 aux points (Team,Rank) de la CombatMap.
        // v161 (11 juin 2026) — Phase 2 MORT SUBITE (anti-antijeu). Dérivé de TurnNumber (aucun champ
        //   [Networked]) : avertissement rounds 23-24, mort subite round 25 (purge tout le terrain en
        //   gardant les positions + ressources maxxées), poison d'arène +100/round (vrais dégâts), et
        //   chaque joueur boosté à 12 PA / 4 PM + ressources max pendant la mort subite. Hook
        //   TurnSystem.EnterTurnStart + helper SuddenDeath.
        public const int CombatRulesVersion = 161;

        /// <summary>Version de la Bible (design doc) que ce code implemente.</summary>
        public const string BibleVersion = "V7.1";
    }
}
