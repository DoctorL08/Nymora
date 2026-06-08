namespace Quantum
{
    /// <summary>
    /// Definition statique d'un sort (cost + targeting + effets).
    ///
    /// SpellDef sert de catalogue immuable lu par SpellSystem. Les effets
    /// "exotiques" (statuses appliques, conditionnels, self-damage, etc.) sont
    /// implementes par switch SpellId dans SpellSystem.ApplySpellSpecificEffects.
    /// Ce design permet de garder SpellDef simple sans tomber dans un "Effect
    /// Composition Engine" prematurement complexe.
    ///
    /// Champs :
    ///   PACost           : cout PA base. Cout EFFECTIF via EffectiveStats.GetPACost
    ///                      (peut etre modifie par RageInsatiableActive : +1 PA).
    ///   Shape/Filter     : ciblage (cf TargetingResolver).
    ///   RangeMin/Max     : portee Manhattan depuis le caster.
    ///   DamageAmount     : dgts flat applique a chaque cible dans la zone (0 si pas offensif).
    ///   HGCostMandatory  : HG toujours consomme au cast (rejet si insuffisant).
    ///   HGCostMaxOptional: HG max que le joueur peut depenser EN PLUS (via Cmd.HGSpend).
    ///   OncePerMatchBit  : bit dans Combatant.OncePerMatchUsedFlags. 255 = pas concerne.
    ///   IsOffensive      : sert au pipeline buff +50% (Pacte) et regen PA Rage Insatiable.
    ///                      Convention : sort offensif = porte une intention de damage.
    ///                      Damage > 0 sufit comme proxy, mais on stocke explicitement
    ///                      pour les futurs sorts offensifs a damage conditionnel.
    /// </summary>
    public struct SpellDef
    {
        public int PACost;
        public TargetingShape Shape;
        public TargetingFilter Filter;
        public int RangeMin;
        public int RangeMax;
        public int DamageAmount;

        // 2.10.a — extensions
        public byte HGCostMandatory;
        public byte HGCostMaxOptional;
        public byte OncePerMatchBit;
        public byte IsOffensive;     // 0/1 (Byte pour serialisation/copy clean)

        // Refonte 29 mai 2026 — limites/relances generiques (cf SpellLimitsHelper).
        //   MaxUsesPerTurn : cap "Nx/tour" (round). 0 = illimite (defaut).
        //   CooldownTurns  : relance "N tours". 0 = pas de relance (defaut).
        // Defaut 0 = comportement inchange pour tout sort qui ne les renseigne pas.
        public byte MaxUsesPerTurn;
        public byte CooldownTurns;
    }

    /// <summary>
    /// Catalogue statique des sorts par SpellId.
    /// Switch deterministe (pas de Dictionary heap-alloc).
    /// </summary>
    public static class SpellRegistry
    {
        public const byte OncePerMatchBitNone = 255;

        public const byte OncePerMatchBitPacteDeSang   = 0;
        public const byte OncePerMatchBitDernierSouffle = 1;
        public const byte OncePerMatchBitNightseerEvanescence = 2;
        public const byte OncePerMatchBitNecramCoconPutride = 3;
        public const byte OncePerMatchBitGhostraVoileSpectral = 4;
        public const byte OncePerMatchBitGhostraDernierPas    = 5;

        // Constantes Bible V7.1 partagees par plusieurs sorts / systemes.
        public const int PeauDeFerShieldHP            = 200;
        public const int PeauDeFerShieldTurns         = 2;
        public const int PeauDeFerMeleeDmgBonus       = 10;   // patch 7 juin (bis) : nerf buff 30 -> 10
        // Refonte 29 mai : Ouvre-Plaie (1 HG) reduit les soins/boucliers RECUS par la cible de 50% (÷2) 1 tour.
        public const int OuvrePlaieHealReductionPct   = 50;
        public const int MarqueDeCarnageTurns         = 3;
        public const int SeveViveHealBase             = 100;
        public const int SeveViveHealBonusHG          = 60;  // +60 si 1 HG depense -> 160
        public const int SeveViveHealBonusBleed       = 50;  // +50 si BleedDoT actif
        public const int DernierSouffleHealAmount     = 200;
        public const int DernierSouffleHGGain         = 3;
        public const int DernierSouffleHPThresholdPct = 30;  // utilisable uniquement a < 30% HP

        // Pacte de Sang (2.10.a) — patch 7 juin : nerf burst (+3 HG -> +2, +50% -> +25%).
        public const int PacteDeSangSelfDamage        = 80;  // self-damage au cast (inchange)
        public const int PacteDeSangHGGain            = 2;   // +2 HG (etait 3)
        public const int PacteDeSangDmgPercent        = 25;  // +25% prochain sort offensif (etait 50)

        // 2.10.c constants
        // Refonte 29 mai : portee Charge Brutale 5 -> 4 (design "p4 ligne").
        public const int ChargeBrutaleRange           = 4;
        public const int ChargeBrutaleDamage          = 180;
        public const int VapeurCarminTurns            = 1;
        public const int DetonationBaseDamage         = 60;
        public const int DetonationDamagePerHG        = 40;
        public const int SangCoaguleTurns             = 2;
        // Refonte 29 mai — EVENTRATION (ex-Curee) : 5 PA, p1, 220 dgts + Plaie Ouverte 50/t x 3t.
        //   (CureeBonusPANextTurn / CureeMissSelfDamage retires du gameplay, conserves morts.)
        public const int CureeDamage                  = 220;  // Eventration : dgts directs
        public const int EventrationPlaieDmgPerTurn   = 50;   // Plaie Ouverte posee : dgts/tour
        public const int EventrationPlaieTurns        = 3;    // duree (rounds)
        // Refonte 29 mai — SANG BOUILLANT (ex-Cauterisation) : 2t, subir degats -> +1 HG +
        //   prochaine frappe +30. Plus d'anti-DoT cleanse.
        public const int SangBouillantTurns           = 2;    // duree (rounds)
        public const int SangBouillantHGPerHit        = 1;    // +1 HG par fois ou le porteur subit des degats
        public const int SangBouillantNextStrikeBonus = 15;   // bonus dgts flat sur la prochaine frappe (patch 7 juin bis : 30 -> 15)
        public const int TrancheAmeKillRecul          = 2;    // 2 cases de recul si kill

        // Refonte 29 mai — EMPOIGNADE devient offensive : pull CaC + 90 dgts + -2 PM.
        public const int EmpoignadeDamage             = 90;
        public const int EmpoignadePMMalus            = 2;

        // Refonte 29 mai — FRENESIE (ex-Rage Insatiable) : 2t, chaque offensif +1 HG + +10% dgts.
        //   Reutilise StatusKind.RageInsatiableActive (renomme semantiquement). Plus de +1 PA cost.
        public const int FrenesieDmgBonusPct          = 5;    // patch 7 juin (bis) : nerf buff 10% -> 5%
        public const int FrenesieHGPerOffensive       = 1;

        // 2.11 — Signature Ame Laceree + Passif Appel du Sang.
        public const int AmeLaceeDamage               = 320;  // dgts de base
        public const int AmeLaceeHealPercentOfPassed  = 50;   // heal caster = X% des dgts qui passent (post-shield)
        public const int AmeLaceeCooldownTurns        = 4;    // 4 tours de cooldown apres usage
        public const int AppelDuSangPalierMarquage    = 70;   // cible <70% HP -> -1 PA cost
        public const int AppelDuSangPalierRageOuverte = 40;   // cible <40% HP -> +1 PM Soulrender + 50% shield bypass melee
        public const int AppelDuSangPalierLeCri       = 20;   // cible <20% HP -> Sang Coagule croix 5 autour caster
        // Refonte 29 mai : le palier <40% ne donne plus +1 PM ni bypass bouclier (retires).
        //   A la place : VOL DE VIE. Le Soulrender heal X% des dgts qui passent sur une cible <40% PV.
        public const int AppelDuSangLifestealPct      = 20;   // vol de vie 20% sur cible <40% HP

        // 2.15.a — Nightseer (constantes Bible V7.1).
        public const int TirPrecisDmg                 = 150;  // dgts base (équilibrage 2 juin : 200->150, + portée 6->4 + 1x/tour)
        public const int TirPrecisDmgIfTraque         = 210;  // dgts si target Traqué (équilibrage 2 juin : 280->210)
        public const int VoleeDEpinesDmg              = 130;  // dgts par cible touchee dans la ligne
        // Bible V7.1 (amendee) : Volee d'Epines pose le MEME Filet que le sort Filet de Ronces
        // (TrapKind.FiletRonces : 100 dgts / -2 PM / Empreinte 2 tours). Pas de constantes light dediees.
        public const int DetonationOniriqueDmg        = 170;  // dgts AoE croix de 5 (equilibrage 6 juin : etait carre 3x3)
        // (6 juin) "+80 si la zone couvre un piege" RETIRE : seul bonus = +30 par piege detone (ZoneTrapDetonationSurplusDmg).
        public const int DetonationOniriqueRangeMaxBase    = 4;   // patch 7 juin : 5 -> 4 (option 2 PR -> 10 retiree le 6 juin)
        // Surplus de degats des pieges detones par Salve Mortelle / Detonation Onirique : +30 PLAT par
        //   piege declenche (equilibrage 6 juin : remplace les degats complets du piege, trop cheat).
        //   Applique aussi TRAQUE aux ennemis de la zone. cf FogHelpers.ApplyZoneTrapDamageToEnemies.
        public const int ZoneTrapDetonationSurplusDmg = 30;
        // Patch 7 juin — Frappe de l'Ombre REFONTE EXECUTEUR : 160 base + 120 si la cible est TRAQUÉ
        //   (= 280), consomme Traqué. Ne pose PLUS Traqué, plus de bonus PM. Récompense le setup de marque.
        public const int FrappeDeLOmbreDmg            = 160;  // dgts base
        public const int FrappeDeLOmbreDmgBonusTraque = 120;  // +120 si cible TRAQUÉ (consommé)
        // Refonte 29 mai : Salve Mortelle 200/120 (était 220/130) + chaîne tes embûches (déclenche
        //   tes pièges sous la croix) ; bonus voile retiré (pièges ne voilent plus).
        public const int SalveMortelleDmgCenter       = 160;  // centre du carre 3x3 (nerf 6 juin : etait 200)
        public const int SalveMortelleDmgSide         = 90;   // 8 cases autour (nerf 6 juin : etait 120)
        // (6 juin) bonus "cible Traque" retire : Salve ne garde que le bonus de zone des pieges.
        public const int SalveMortelleHGCost          = 3;    // 3 PR mandatory (ressource Nightseer)
        public const int SalveMortelleCooldownTurns   = 2;    // equilibrage 6 juin : relance 2 tours (finisher)
        public const int SalveMortelleTrapBonusDmg    = 40;   // 6 juin : +40 dgts par piege du caster sous la zone
                                                              //   (nerf, etait 50), SANS consommer les pieges

        // 2.15.b — Nightseer Tactiques + passif L'Œil qui n'est pas.
        public const int MarqueDuChasseurTurns        = 3;    // (legacy) duree Traque — plus applique depuis le patch 7 juin (Affut)
        // Patch 7 juin — MARQUE DU CHASSEUR -> AFFUT : self-buff, ne pose plus Traqué.
        //   +AffutRangeBonus portee sur les sorts a distance + +AffutDmgBonusPct% dgts offensifs,
        //   pendant AffutTurns rounds. Relance MarqueDuChasseurCooldownTurns. StatusKind.AffutActive.
        public const int AffutRangeBonus              = 2;    // +2 portee sur les sorts a distance pendant la duree
        public const int AffutDmgBonusPct             = 10;   // +10% dgts offensifs pendant la duree
        public const int AffutTurns                   = 2;    // 2 rounds actifs
        public const int MarqueDuChasseurCooldownTurns = 3;   // relance 3 tours
        public const int FiletDeRoncesDmg             = 100;  // dgts au declenchement (ennemi entre)
        public const int FiletDeRoncesPMReduce        = 1;    // refonte 29 mai : -1 PM (était -2) après déclenchement
        public const int FiletDeRoncesEmpreinteTurns  = 2;    // duree Empreinte apres declenchement
        public const int ChampDeMinesDmg              = 70;   // dgts de la 1ere mine declenchee
        public const int ChampDeMinesEmpreinteTurns   = 2;
        // Refonte 29 mai — CHAÎNE : declencher 1 mine detonne les mines proches (cluster) du meme
        //   owner sur l'enterer : 70 (1ere) + 40 + 40 (chainees). Manhattan <= 2, cap 2 chainees.
        public const int ChampDeMinesChainDmg         = 40;
        public const int ChampDeMinesChainRadius      = 2;
        public const int ChampDeMinesChainMax         = 2;
        // Refonte 29 mai : push directionnel nerfé à 2 cases (était 3).
        // Equilibrage juin : option "1 PR -> 4 cases" retiree -> push fixe 2 (BonusBourrasquePushBonus1PR supprime).
        public const int BourrasquePushBase           = 2;    // push 2 cases (direction choisie), fixe
        // Refonte 29 mai — PIÈGE BONDISSANT (ex-Souffle Glacial, ID NightseerSouffleGlacial réutilisé) :
        //   2 PA, pose un piège-catapulte VISIBLE (invisible en phase 3) ; au déclenchement, éjecte
        //   l'ennemi de 3 cases dans la direction choisie à la pose (2e clic) + Traqué. Pas de dégâts.
        public const int PiegeBondissantPACost        = 2;
        public const int PiegeBondissantRangeMax      = 4;    // portée de pose
        public const int PiegeBondissantEjectDist     = 3;    // cases d'éjection (catapulte)
        // Garde anti-boucle (fix 2 juin) : profondeur max de chaînage de pièges déclenchés au passage
        //   d'une catapulte. Deux Pièges Bondissants qui s'éjectent l'un vers l'autre se renvoyaient la
        //   cible à l'infini -> StackOverflow + halt de la sim. Cap dur en ceinture+bretelles.
        public const int PiegeBondissantMaxChainDepth = 8;
        // Refonte 29 mai : pierce bouclier Nightseer déplacé en palier P3 (50%) -> NightseerPassif.ShieldIgnorePct.

        // 2.15.c — Nightseer Survie.
        // Patch 7 juin — REPLI ÉPINEUX (ex-Flèche Traçante, ID NightseerVoileDOmbre conservé) :
        //   2 PA self, repousse de RepliEpineuxPush cases TOUS les ennemis adjacents (Manhattan 1)
        //   + heal RepliEpineuxHeal, relance RepliEpineuxCooldownTurns. Sort de survie/désengagement.
        public const int RepliEpineuxPush             = 2;    // patch 7 juin : push 2 cases (etait 3) loin du Nightseer
        public const int RepliEpineuxHeal             = 100;  // heal au cast
        public const int RepliEpineuxCooldownTurns    = 2;    // patch 7 juin : relance 2 tours (etait 1)
        public const int PasFurtifRangeMax            = 3;    // teleport jusqu'a 3 cases (équilibrage 2 juin : 4->3, + PA 2->4)
        public const int PasFurtifVeilTurns           = 2;    // duree Voile bonus si 1 PR
        public const int CamouflageRoncesShieldHP     = 130;  // shield 130 HP
        public const int CamouflageRoncesShieldTurns  = 2;    // 2 rounds actifs
        public const int CamouflageRoncesAuraDmg      = 70;   // dgts ennemis adjacents en fin de round
        public const int CamouflageRoncesAuraTurns    = 2;    // 2 rounds actifs (sync avec shield)
        public const int CamouflageRoncesAuraEmpreinteTurns = 2; // Bible V7.1 : "+ EMPREINTE" 2 tours sur ennemis adjacents fin de round
        public const int SeveSauvageHealBase          = 130;  // heal de base
        public const int SeveSauvageHealBonusTrap     = 60;   // +60 si trap declenche dans les 2 derniers rounds
        public const int SeveSauvageHealBonusVeil     = 30;   // +30 si au moins 1 voile actif sur la map (owner caster)
        public const int EvanescenceHpThresholdPct    = 30;   // HP < 30% requis
        public const int EvanescenceHeal              = 150;  // heal au cast
        public const int EvanescenceRangeMax          = 7;    // teleport jusqu'a 7 cases
        public const int EvanescenceVeilTurns         = 2;    // duree Voile sur case quittee

        // 2.16 — Nightseer Signature TRAQUENARD (Bible V7.1).
        public const int TraquenardDmgBase            = 280;  // dgts cible
        public const int TraquenardDmgBonusIfMarked   = 80;   // +80 si target Traque/Voile/Empreinte (= 360 total)
        public const int TraquenardParalysiePMMalus   = 2;    // -2 PM au prochain tour cible (nerf 5 juin : etait 3)
        public const int TraquenardParalysieAPMalus   = 2;    // -2 PA au prochain tour cible
        public const int TraquenardParalysieTurns     = 1;    // 1 tour actif (skip-decrement convention)
        public const int TraquenardCooldownTurns      = 4;    // 4 tours apres usage (re-castable si PR remonte a 4)
        public const int TraquenardPRCost             = 5;    // refonte 29 mai : 5/5 PR (cap plein, phase 3)
        public const int TraquenardPRGainOnConsumeMark = 2;   // +2 PR au caster si bonus marque declenche
        public const int TraquenardRangeMax           = 5;    // portee Manhattan caster -> cible

        // 3.3.a.i — Colossar Offensifs (Bible V7.1).
        public const int FrappeLourdeDmgBase          = 180;  // base mêlée
        public const int FrappeLourdeDmgIfPinned      = 280;  // 180+100 si cible epinglee (case opposee au caster bloquee)
        public const int RepresaillesDmgImmediate     = 100;  // dgts directs au cast
        public const int RepresaillesReflectDmg       = 80;   // dgts reflectes sur attaque melee subie
        public const int RepresaillesReflectTurns     = 2;    // 2 tours de reflect actif
        public const int RepresaillesReflectMaxTriggers = 4;  // Bible V7.1 : cap 4 retours (vs Riposte Carmin = no cap)

        // 3.3.a.i — Passif Densite Inerte bonus adjacence (Bible V7.1).
        // Branche dans le damage compute des sorts Colossar : si caster Colossar adjacent
        // a un de ses obstacles ET sort range max <= 2 -> +20 dmg.
        public const int DensiteInerteAdjacenceBonus  = 20;   // cf ColossarPassif.AdjacentObstacleBonusDamage
        public const int DensiteInerteAdjacenceMaxRange = 2;  // sorts portee 1-2 (melee + courte)

        // 3.3.a.ii — Onde de Choc Colossar (AoE rayon 1 autour caster).
        public const int OndeDeChocDmg                = 80;   // dgts AoE base
        public const int OndeDeChocPushDistance       = 2;    // push 2 cases loin du caster
        public const int OndeDeChocBonusVsWall        = 80;   // +80 dgts si push s'arrete contre obstacle/bord
        public const int OndeDeChocTraumaPMMagnitude  = 1;    // MovementMalus 1 (1 tour)
        public const int OndeDeChocTraumaPAMagnitude  = 1;    // ActionMalus 1 (1 tour)
        public const int OndeDeChocTraumaTurns        = 1;

        // 3.3.a.ii — Marteau Punisseur Colossar (anti-caster).
        public const int MarteauPunisseurDmg          = 160;  // dgts base
        public const int MarteauPunisseurDmgIfDepleted = 240; // dgts si target.PA < 4 (a deja cast ce tour)
        public const int MarteauPunisseurDepletedPAThreshold = 4; // strict <
        public const int MarteauPunisseurTraumaPAMagnitude = 2; // ActionMalus 2 prochain tour
        public const int MarteauPunisseurTraumaTurns  = 1;

        // 3.3.a.ii — Choc Sismique Colossar (ligne 4, traverse Pilier/Mur own).
        public const int ChocSismiqueDmgBase          = 130;  // dgts toutes cibles dans ligne
        public const int ChocSismiqueBonusThroughWall = 50;   // +50 dgts a la cible suivante apres traversee d'un obstacle Colossar
        public const int ChocSismiquePMReduce         = 1;    // MovementMalus 1 sur cibles (1 tour)
        public const int ChocSismiquePMTurns          = 1;
        public const int ChocSismiqueRange            = 4;    // portee Manhattan

        // 3.3.b.i — Pilier (sort tactique Colossar) — RANGE 3 Bible V7.1.
        // Bible : "Reste jusqu'a destruction" — pas de timer auto, l'expiration vient
        // uniquement du HP qui tombe a 0 (DamageObstacle / Brisure / ChocSismique trajectoire).
        // Le spawn passe expiresOnTurn=0 (convention persistent, cf Obstacle.qtn).
        public const int PilierHP                     = 200;  // HP du Pilier pose
        public const int PilierRangeMax               = 3;    // portee Bible (case vide a moins de 3 cases)

        // 3.3.b.i — Mur de Pierre (sort tactique Colossar) — 4 PA / RANGE 4 / option 1 FD -> 5 segments.
        public const int MurDePierreSegmentHP         = 150;  // HP par segment de mur
        public const int MurDePierreTurns             = 2;    // duree avant expiration auto
        public const int MurDePierreSegmentsBase      = 3;    // segments de base (sans option ressource)
        public const int MurDePierreSegmentsBoosted   = 5;    // segments si option 1 FD depense
        public const int MurDePierreRangeMax          = 4;    // portee Bible
        public const int MurDePierreFondationGain     = 2;    // equilibrage juin : +2 FD FLAT par Mur (etait +1/segment)

        // 3.3.b.iii — Ancrage Bible-correct (Colossar TACTIQUE anti-mobilite) — refacto rétroactif.
        // Bible : 2 PA, range 4, ENEMY. Cible perd 2 PM pendant 2 tours ET ne peut pas etre deplacee
        // (push/pull/teleport) pendant 1 tour. Pas de damage. NE PAS confondre avec self-buff initial.
        public const int AncrageRangeMax              = 4;
        public const int AncrageMovementMalusMag      = 1;    // equilibrage 5 juin : -1 PM (etait -2)
        public const int AncrageMovementMalusTurns    = 2;
        public const int AncrageImmuneTurns           = 1;    // immune push/pull/teleport 1 tour

        // 3.3.b.iii — Provocation Bible-correct (Colossar) — refacto rétroactif.
        // Bible : 2 PA, range 5, 1 tour. Sorts non-ciblant le caster coutent +2 PA pour la cible.
        // -1 PM. Si la cible n'est pas adjacente au caster en fin de SON tour : 100 dgts auto.
        public const int ProvocationRangeMax          = 5;
        public const int ProvocationTurns             = 1;    // 1 tour Bible (skip-decrement convention 1 round actif)
        public const int ProvocationMovementMalusMag  = 1;    // -1 PM
        public const int ProvocationMovementMalusTurns = 1;
        // Patch 7 juin : surcout +1 PA (etait 2) applique a TOUS les sorts du provoque (plus d'exception
        //   "sort ciblant le provocateur") -> centralise dans EffectiveStats.GetPACost. + relance 2 tours.
        public const int ProvocationCostBump          = 1;    // +1 PA sur TOUS les sorts tant que Provoked
        public const int ProvocationCooldownTurns     = 2;    // relance 2 tours
        public const int ProvocationAutoDamageNotAdj  = 100;  // 100 dgts auto si pas adjacent fin tour

        // 3.3.b.iii — Brisure Bible-correct (Colossar) — anti-buff/bouclier — refacto rétroactif.
        // Bible : 3 PA range 2, ENEMY. 90 dgts. Retire 1 buff/bouclier de la cible. Si pas de buff :
        // applique TRAUMA (-2 PA prochain tour). MVP : retire en priorite ShieldActive (Peau de Fer,
        // Stoicisme), sinon RoncesAura (Camouflage Ronces), sinon BuffNextOffensiveDmgPercent (Pacte de
        // Sang), sinon RipostMelee (Riposte Carmin / Représailles), sinon RageInsatiableActive.
        public const int BrisureRangeMax              = 2;
        public const int BrisureDamage                = 90;
        public const int BrisureTraumaPAMag           = 2;    // ActionMalus -2 PA si pas de buff
        public const int BrisureTraumaTurns           = 1;

        // 3.3.c — Colossar SURVIE (Bible V7.1).

        // Stoicisme : 3 PA self, shield 200 HP / 2 tours + immune push/pull/tp 2 tours.
        // Si shield Magnitude > 0 a expiration : +80 HP heal (hook TurnSystem fin de round).
        public const int StoicismeShieldHP            = 200;
        public const int StoicismeShieldTurns         = 2;
        public const int StoicismeImmuneTurns         = 1;    // patch 7 juin : anti-deplacement 1 tour (etait 2) ; le bouclier reste 2 tours
        public const int StoicismeHealIfSurvived      = 80;   // heal si shield Magnitude > 0 a expiration

        // Garde Protectrice : 2 PA self, -15% dmg subis / 2 tours (refonte 29 mai, etait -30%).
        public const int GardeProtectricePercent      = 15;   // % reduction (etait 30)
        public const int GardeProtectriceTurns        = 2;
        public const int MaxCombinedDamageReductionPct = 45;  // refonte 29 mai : cap combine -45% (etait -50)

        // Ressac Vital : 2 PA self, heal 80 + 30/hit subi tour precedent (max +120 HP = 4 hits).
        public const int RessacVitalHealBase          = 80;
        public const int RessacVitalHealPerHit        = 30;
        public const int RessacVitalHealMaxBonus      = 120;  // cap = 4 hits * 30
        public const int RessacVitalHitsCap           = 4;    // max hits comptes (cap +120)

        // Renvoi du Bouclier : 3 PA self, RipostAll 60 dgts (melee + distance) / 1 tour / cap 4 retours.
        public const int RenvoiBouclierReflectDmg     = 60;
        public const int RenvoiBouclierTurns          = 1;
        public const int RenvoiBouclierMaxTriggers    = 4;    // cap reflects (reuse RepresaillesReflectsLeft)

        // Refonte 29 mai — EBOULEMENT (ex-Soin Lourd) : 3 PA, p3, détruis un de tes Piliers ->
        //   AoE 150 (rayon 1) + push autour. Le +30 HP vient du passif (destruction de pilier).
        public const int EboulementDamage             = 150;
        public const int EboulementRangeMax           = 3;
        public const int EboulementPushDistance       = 2;

        // 3.3.d — Effondrement (signature Colossar, Bible V7.1).
        public const int EffondrementPACost           = 4;
        public const int EffondrementFDCost           = 5;    // refonte 29 mai : consomme TOUTE la jauge FD (cap 5)
        public const int EffondrementAoeRadius        = 2;    // rayon 2 autour caster (Chebyshev/Manhattan ?)
        public const int EffondrementDamage           = 200;  // dgts ennemis dans la zone au trigger
        public const int EffondrementFailleTurns      = 2;    // duree des failles (cases impraticables)
        public const int EffondrementBuffTurns        = 2;    // duree du buff +1PM / -1PA / -30% dgts
        public const int EffondrementDmgReductionPct  = 30;   // % reduction (combine avec Densite Inerte/Garde Prot, cap 50%)
        public const int EffondrementCooldownTurns    = 4;    // 4 tours apres usage (re-castable si FD remonte a 3)
        public const int EffondrementFailleHP         = 100;   // Bible-balance : Faille destructible par AoE adverse (Lorenzo design 3.3.d)

        // 3.5.a.i — Necram Offensifs base (Bible V7.1).
        // Crachat Acide : sort de base "80% utilisation early-game". Combine dgts directs + setup marques.
        public const int CrachatAcideDmg              = 100;   // dgts directs (patch 8 juin : 90 -> 100)
        public const int CrachatAcideMarksApplied     = 2;     // applique 2 marques venin (cap 4/cible)
        public const int CrachatAcideRangeMax         = 4;     // portee Manhattan
        public const int CrachatAcidePACost           = 3;

        // Morsure Putride : finisher melee qui scale avec marques + transfere les marques au kill.
        // Bible : 110 dgts + 22/marque (max +90 = 200 total max). Si target meurt -> marques sur ennemi le plus proche.
        public const int MorsurePutrideDmgBase        = 120;   // dgts base (patch 8 juin : 110 -> 120)
        public const int MorsurePutrideDmgPerMark     = 10;    // bonus dgts par marque (patch 8 juin : 22 -> 10)
        public const int MorsurePutrideDmgBonusCap    = 40;    // cap bonus marques (= 10 * 4 = 40)
        public const int MorsurePutridePACost         = 4;

        // 3.5.a.ii — Necram Offensifs burst/AoE (Bible V7.1).
        // Detonation Virulente : 80 dgts immediats + consomme TOUTES les marques (50/marque). 4 marques = 280.
        // Refonte 29 mai : Détonation Virulente = tick venin complet instantané, SANS consommer
        //   les marques (bypass shield/réduction). Plus de base ni de "par marque consommée"
        //   (le tick = stacks * GetTickDmgPerMark + MarqueSac, calculé dans le handler).
        public const int DetonationVirulenteRangeMax  = 4;     // portee Manhattan
        public const int DetonationVirulentePACost    = 4;

        // Faux Decharnee : 130 dgts AoE Square3x3 autour caster + heal Necram 30/marque cumule (cap +120 = 4 marques).
        public const int FauxDecharneeDmg             = 130;   // dgts par cible touchee
        public const int FauxDecharneeHealPerMark     = 30;    // heal Necram par marque active sur cibles touchees
        public const int FauxDecharneeHealCap         = 120;   // cap heal total (= 4 marques * 30)
        public const int FauxDecharneePACost          = 4;

        // Refonte 29 mai — Brume Toxique SIMPLIFIÉE : zone 3x3 / 3 tours. Plus de dégâts directs
        //   (pose/entrée retirés). +1 marque par trigger (pose / entrée / fin de tour). Tick venin
        //   MAJORÉ dans la zone : +BrumeToxiqueTickBonusPerMark par marque (valeur tunable choisie).
        public const int BrumeToxiqueMarksOnHit         = 1;   // marque appliquee a chaque trigger
        public const int BrumeToxiqueRangeMax           = 4;   // portee Manhattan caster -> centre zone
        public const int BrumeToxiqueTurns              = 3;   // refonte : 3 tours (etait 2)
        public const int BrumeToxiquePACost             = 4;
        public const int BrumeToxiqueTickBonusPerMark   = 10;  // tick majoré dans la zone (tunable)

        // 3.5.b.i — Inoculation : setup, 1 PA range 5, 2 marques cap 4 (Bible V7.1).
        // Patch 8 juin : ajoute 30 dgts directs (n'est plus 100% silent).
        public const int InoculationPACost            = 1;
        public const int InoculationRangeMax          = 5;
        public const int InoculationMarksApplied      = 2;
        public const int InoculationDmg               = 30;   // patch 8 juin : dgts directs (etait 0)

        // 3.5.b.i — Marque Sacrificielle : buff DoT, 2 PA range 5, +20 dmg flat par tick venin sur la cible pendant 3 rounds (Bible V7.1).
        // Effet neutre si pas de marques actives au cast (le bonus declenchera des qu'une marque sera posee).
        public const int MarqueSacrificiellePACost    = 2;
        public const int MarqueSacrificielleRangeMax  = 5;
        public const int MarqueSacrificielleBonusDmgPerTick = 20;
        public const int MarqueSacrificielleTurns     = 3;

        // 3.5.b.ii — Symbiose Morbide : self-buff lifesteal DoT (Bible V7.1).
        // 3 PA, self, status 2 rounds. A chaque tick venin sur un ennemi, le Necram porteur
        // du status est soigne de min(stacks, 4) * 8 HP (max +32 HP/tick, +64 sur 2 rounds).
        public const int SymbioseMorbidePACost        = 3;
        // Refonte 29 mai : heal FLAT +15 HP par tick venin (n'echelonne plus par marque).
        public const int SymbioseMorbideHealPerMarkPerTick = 15;
        public const int SymbioseMorbideMaxMarksForHeal = 4; // (obsolete depuis le flat, gardé pour réf)
        public const int SymbioseMorbideTurns         = 2;

        // Refonte 29 mai — CONTAGION : 3 PA, p5, rend la cible ennemie CONTAGIOUS pendant 2 rounds.
        //   Auto-propagation : à la fin de chaque tour de la cible, elle prend +1 marque venin.
        public const int ContagionPACost              = 3;
        public const int ContagionRangeMax            = 5;
        public const int ContagionTurns               = 2;     // durée du status Contagious (rounds)

        // 3.5.b.iii — Pas Spectral : mobilite + traversee ennemis (Bible V7.1).
        // 2 PA, self. +2 PM ce tour (cap si refresh meme tour). Apply PasSpectralReady (sub-turn).
        // Tant que actif : MovementSystem passe ignoreEnemyOccupants=true a A* pour les
        // MoveCommand du Necram, et pose +1 marque venin sur chaque ennemi present sur les
        // cases intermediaires du path (destination skip car deja validee libre).
        public const int PasSpectralPACost            = 2;
        public const int PasSpectralPMBonus           = 2;
        public const int PasSpectralMarksPerCrossing  = 1;
        // Refonte 29 mai — ÉCHANGE SPECTRAL (ex-Pas Spectral) : 2 PA, p5, swap de place avec
        //   l'ennemi ciblé + 80 dgts. (Réutilise l'enum SoulId NecramPasSpectral.)
        public const int EchangeSpectralDamage        = 80;
        public const int EchangeSpectralRangeMax      = 5;

        // 3.5.c.i — Voile de Pestilence : aura defensive 2 rounds (Bible V7.1).
        // 3 PA self. Apply PestilenceAura turnsLeft=2 (refresh-only).
        // Refonte 29 mai — NUÉE DE SPORES (ex-Voile de Pestilence) : 3 PA self, buff 2 rounds.
        //   Tant qu'actif, chaque sort du Necram visant un ennemi pose +1 marque venin bonus
        //   (hook post-cast SpellSystem). Anciens hooks aura adjacence/riposte mêlée retirés.
        public const int VoilePestilencePACost            = 3;
        public const int VoilePestilenceTurns             = 2;

        // 3.5.c.ii — Carapace Visqueuse : bouclier piege 2 rounds (Bible V7.1).
        // 3 PA self. Apply ShieldActive Magnitude=110 HP + status CarapaceVisqueuse flag 2 rounds.
        // Hook (SpellSystem damage loop, apres bloc absorption shield) : si target porte
        // CarapaceVisqueuse ET shield a absorbe >=1 dmg ET sort melee (RangeMax==1)
        // -> +1 marque venin sur l'attaquant (cap 4 via ApplyMark).
        // Trigger meme si shield absorbe tout le dmg (HP_loss=0, "frappe le bouclier" Bible).
        // Pas de trigger si shield deja brise (shieldBefore=0).
        public const int CarapaceVisqueusePACost              = 3;
        public const int CarapaceVisqueuseShieldHP            = 160;  // refonte 29 mai : 110 -> 160
        public const int CarapaceVisqueuseTurns               = 2;
        public const int CarapaceVisqueuseMarksOnMeleeAttacker = 1;

        // 3.5.c.iii — Drain Vital : heal offensif a distance (Bible V7.1).
        // 3 PA range 4, 60 dgts cible. Caster Necram heal HealBase (30) ou HealBonus (60)
        // si target.VeninStacks >= MarksThreshold (3) au moment du cast (snapshot post-damage).
        // Marques cible NON consommees. Heal applique meme si target meurt sur les 60 dmg.
        // Cap MaxHP standard. Pas de status applique.
        public const int DrainVitalPACost          = 3;
        public const int DrainVitalRangeMax        = 4;
        // Refonte 29 mai : 40 dmg + heal 40 HP/marque sur la cible (cap 160 = 4 marques).
        public const int DrainVitalDamage          = 40;
        public const int DrainVitalHealPerMark     = 40;
        public const int DrainVitalHealMaxBonus    = 160;

        // 3.5.c.iv — Pulse Sanguin Vert : heal de zone (Bible V7.1 — sort canonique "REGENERATION NECROTIQUE").
        // NOTE NOM : la Bible nomme ce sort "Regeneration Necrotique" (ligne 971). On garde la constante
        // PulseSanguinVert* dans le code par soucis de non-regression (sort deja livre, scene/prefabs
        // binds, replays, STATUT historiques). Decision Lorenzo 16 mai post-audit Bible.
        // 2 PA self (Bible explicite ligne 973). Heal Necram caster : HealBase (70) + min(sumVeninStacks
        // EnemiesInRadius, capMarks) * HealPerMark. PulseSanguinVertHealCap = 90 HP de bonus (= 6 marques * 15).
        // +30 HP additionnel si hgSpend>=1 (1 PT). Itere tous ennemis vivants Manhattan <= MarksRange (4)
        // du caster. Marques NON consommees. Cap MaxHP standard. Pas de dmg.
        public const int PulseSanguinVertPACost          = 2;
        public const int PulseSanguinVertHealBase        = 70;
        public const int PulseSanguinVertHealPerMark     = 15;
        public const int PulseSanguinVertHealCap         = 90;
        public const int PulseSanguinVertMarksRange      = 4;
        public const int PulseSanguinVertOptionalPTBonus = 30;

        // 3.5.c.v — Cocon Putride : panic signature (Bible V7.1).
        // 4 PA self. Gate HP <30% requis (rejet propre avant consume PA, style Dernier Souffle).
        // 1x/match (OncePerMatchBitNecramCoconPutride). Heal Necram caster CoconPutrideHealAmount (220 HP).
        // Applique +CoconPutrideMarksPerEnemy (1) marque venin sur tous ennemis vivants Manhattan
        // <= CoconPutrideMarksRange (4) du caster. Cap 4/cible respecte par ApplyMark.
        // Cap +2 PT/tour Necram via marques appliquees respecte. Pas de status durable.
        public const int CoconPutridePACost          = 4;
        public const int CoconPutrideHpThresholdPct  = 30;
        public const int CoconPutrideHealAmount      = 220;
        public const int CoconPutrideMarksRange      = 4;
        public const int CoconPutrideMarksPerEnemy   = 1;

        // 3.5.c.vi — Virus Fatal (SIGNATURE Necram, Bible V7.1 lignes 855-866).
        // 2 PA, range 5, Filter Enemy. Cout : 6/6 PT (consomme tout = HGCostMandatory=6 sur la
        // generique Resource). Cooldown 4 tours apres usage (Bible : "Reutilisable si PT remonte
        // a 6 ET cooldown expire"). Effet : declenche un tick venin sur la cible "instantanement
        // x3" (multiplicateur Floraison applique). Calcul handler :
        //   baseDmg = stacks * GetTickDmgPerMark(densityGlobal)
        //   marqueSacBonus = StatusHelper.GetMagnitude(target, MarqueSacrificielle, 0)
        //   totalDmg = (baseDmg + marqueSacBonus) * 3 / 2  // refonte 29 mai : "tick x1.5" (etait x3)
        // Bypass shield + reduction (comme tick venin standard). Hook Symbiose Morbide :
        // chaque Necram porteur heal FLAT (Magnitude) * 3 / 2 (idem x1.5).
        // Si cible survit : VeninStacks = 0 (Bible "Les marques sont consommees").
        // Si cible meurt : marques transferees sur ennemi vivant le plus proche via
        // VeninHelpers.TryTransferVeninOnKill (Bible "marques restent disponibles pour Contagion
        // / Detonation Virulente sur d'autres cibles"). En 1v1 = perdues silencieusement.
        public const int VirusFatalPACost           = 2;
        public const int VirusFatalRangeMax         = 5;
        public const int VirusFatalPTCost           = 6;   // mandatory (consomme toute la jauge cap 6)
        // Refonte 29 mai : "tick x1.5" (60 x 4 marques x 1.5 = 360 au max) -> nerf vs le boost
        //   global du Necram. Applique en Num/Den entier (* 3 / 2).
        public const int VirusFatalMultNum          = 3;
        public const int VirusFatalMultDen          = 2;
        public const int VirusFatalCooldownTurns    = 4;   // 4 tours = 4 rounds (convention TurnNumber)

        // -------------------------------------------------------------
        // GHOSTRA — Bible V7.1 (3.7.a — 5 sorts offensifs)
        // -------------------------------------------------------------

        // 3.7.a.i.2 — Lame Spectrale (Bible V7.1) :
        //   3 PA, melee 1, 170 dgts base + bonus dorsal Angle Mort (0/+50/+80) + 60 si target
        //   a PlaieOuverte (non consommee). Cf GhostraPassif.GetDorsalBonusIfApplicable.
        public const int LameSpectralePACost        = 3;
        public const int LameSpectraleRangeMax      = 1;
        public const int LameSpectraleDmgBase       = 170;
        public const int LameSpectralePlaieBonus    = 60;

        // 3.7.a.i.2 — Lame Vorace Spectrale (Bible V7.1) :
        //   3 PA, melee 1, 130 dgts base + 60 si PlaieOuverte (NON consommee) + bonus dorsal.
        //   Coeur du combo Plaie Ouverte -> Lame Vorace x2 -> Saigne-Ame.
        public const int LameVoracePACost           = 2;    // équilibrage 2 juin : 3->2 PA (outil de spam du combo, distinct de Lame Spectrale 3 PA)
        public const int LameVoraceRangeMax         = 1;
        public const int LameVoraceDmgBase          = 110;  // équilibrage 2 juin : 130->110 (compense le passage à 2 PA + cap 2x/tour)
        public const int LameVoracePlaieBonus       = 60;

        // 3.7.b.i — Réplique Fantôme (Bible V7.1 ligne 1127) :
        //   3 PA, range 4, case vide. Pose un leurre DecoyKind.RepliqueFantome (clone visuel
        //   identique a la Ghostra cote adversaire). Dure 2 tours OU jusqu'a destruction par
        //   interaction adverse. Compte dans le cap 3 leurres (DecoyHelpers.MaxDecoys).
        //   Heal owner branche dans DecoyHelpers (cf RepliqueFantomeHealOnDestroy / OnExpire) :
        //     - Survit 2 tours      -> +80 HP (Bible)
        //     - Detruit prematurement -> +40 HP (Bible)
        public const int RepliqueFantomePACost      = 2;  // refonte 30 mai (brique 2) : 3 -> 2 PA (eco leurre)
        public const int RepliqueFantomeRangeMax    = 3;  // patch 7 juin : 4 -> 3 (nerf portee leurre)

        // 3.7.b.ii — Pas dans l'Ombre (Bible V7.1 ligne 1134) :
        //   2 PA, range 5, case vide (teleport). Toute cible ennemie adjacente Manhattan <=1 a
        //   l'arrivee PIVOTE pour faire face a la Ghostra (Facing = direction Ghostra->target).
        //   Option HGSpend >= 1 (Shift+H) : pose un DecoyKind.Standard sur la case quittee.
        //   Pas de cout PT/PR/HG reel — la "ressource" est le slot leurre lui-meme (compte dans
        //   cap 3). Refus silencieux si cap atteint (teleport reussit quand meme).
        public const int PasDansLOmbrePACost        = 2;
        public const int PasDansLOmbreRangeMax      = 4;   // equilibrage 5 juin : portee 5 -> 4

        // 3.7.b — Volte-Face SUPPRIME du pool deckable (refonte 30 mai). Le slot SpellId 90 est
        //   reutilise par PERMUTATION (constantes Permutation* declarees plus bas, bloc signature).

        // 3.7.b — Éveil Spectral (refonte 30 mai, ex-Dague Lancée slot 93) :
        //   2 PA, range 4 ENEMY, cap 2x/tour (moteur générique). Un de tes leurres ADJACENT
        //   (Manhattan 1) à la cible la poignarde pour EveilSpectraleDamage. Bonus dorsal Angle
        //   Mort + Plaie Ouverte calculés depuis la POSITION DU LEURRE (un leurre dans le dos de
        //   la cible = dorsal, même si la Ghostra est en face). Leurre NON consommé. Respecte
        //   boucliers/réductions (passe par le pipeline de dégâts standard).
        public const int EveilSpectralePACost         = 2;
        public const int EveilSpectraleRangeMax       = 3;  // patch 7 juin : 4 -> 3
        public const int EveilSpectraleDamage         = 100;
        public const int EveilSpectraleMaxUsesPerTurn = 2;

        // 3.7.b.v — Marque de l'Ombre (Bible V7.1 ligne 1155) :
        //   2 PA, range 4 ENEMY. Aucun damage direct (buff pur).
        //   Applique status MarqueDeLOmbre 2 rounds magnitude=20.
        //   Hook 1 (pipeline damage Ghostra) : si caster Ghostra ET target marquee -> dmg += 20.
        //   Hook 2 (GhostraPassif.ApplyPlaieOuverteIfAngle2Plus) : tout dorsal Ghostra sur cible
        //     marquee -> PlaieOuverte AUTO (bypass requirement Angle 2+ leurres). Bible-cohérent.
        public const int MarqueDeLOmbrePACost       = 2;
        public const int MarqueDeLOmbreRangeMax     = 4;
        public const int MarqueDeLOmbreDmgBonus     = 20; // magnitude du status (bonus dmg sur sorts Ghostra)
        public const int MarqueDeLOmbreDurationRounds = 2;

        // 3.7.a.iii — Frappe Fantome (Bible V7.1 ligne 1095) :
        //   4 PA, range 4 ENEMY. Teleport Ghostra sur case libre adjacente Manhattan=1 a la
        //   target (priorite cote caster d'origine, fallback 4 cardinaux). Si aucune case libre,
        //   rejet propre (PA non consomme). 200 dgts base + bonus dorsal Angle Mort generique.
        //   Si target.LastFacingForcedOnTurn == currentTurn -> applique PlaieOuverte (40/tour x 2t).
        //   Combo Bible "Volte-Face -> Frappe Fantome : 350+ HP shred en 1 tour".
        public const int FrappeFantomePACost        = 4;
        public const int FrappeFantomeRangeMax      = 3;  // patch 7 juin : 4 -> 3
        public const int FrappeFantomeDmgBase       = 200;

        // 3.7.a.ii — Saigne-Ame (Bible V7.1 ligne 1109) :
        //   4 PA, range 2 ENEMY. 200 dgts base + 70 si target a PlaieOuverte (consomme la plaie
        //   sur cible vivante post-damage). Bonus dorsal Angle Mort applique generiquement.
        //   Si kill : Ghostra heal +60 HP (cap MaxHP, bloque par AntiHealShield).
        //   Aboutissement combo Bible "Plaie Ouverte -> Lame Vorace xN -> Saigne-Ame".
        public const int SaigneAmePACost            = 4;
        public const int SaigneAmeRangeMax          = 2;
        public const int SaigneAmeDmgBase           = 200;
        public const int SaigneAmePlaieBonus        = 70;
        public const int SaigneAmeHealOnKill        = 60;

        // 3.7.a — Nuée Spectrale (refonte 30 mai, ex-Danse des Lames slot 95) :
        //   4 PA, range 2 ENEMY, cap 1x/tour. Burst CIBLE UNIQUE qui scale avec les leurres :
        //   base 100 + 40 par leurre ACTIF + 20 par leurre ADJACENT à la cible (max 280 = 3 leurres
        //   tous adjacents : 100 + 120 + 60). Ne consomme PAS les leurres. AUCUN bonus dorsal/Plaie
        //   en plus (le scaling EST le bonus) -> skip dorsal + Plaie dans SpellSystem. +20 Marque conservé.
        public const int NueeSpectralePACost         = 4;
        public const int NueeSpectraleBaseDamage     = 100;
        public const int NueeSpectralePerLeurre      = 40;
        public const int NueeSpectralePerAdjacent    = 20;
        public const int NueeSpectraleRangeMax       = 2;
        public const int NueeSpectraleMaxUsesPerTurn = 1;

        // 3.7.c.i — Linceul d'Ombres (Bible V7.1 ligne 1173) :
        //   3 PA self. ShieldActive 130 HP / 2 rounds + LinceulDOmbres flag (Magnitude=40
        //   dgts riposte) 2 rounds. Hook damage loop standard (apres Carapace, avant
        //   bloc HP loss) + path custom Charge Brutale : si target porte LinceulDOmbres
        //   ET attaque MELEE (Chebyshev caster-target <= 1) -> renvoie 40 dgts sur
        //   l'attaquant. PIPELINE STANDARD : reduction% (Densite Inerte + Garde
        //   Protectrice cap 50%) puis shield attaquant. Trigger meme si shield Linceul
        //   absorbe tout le dmg incoming (Bible "toute attaque melee subie").
        //   Refresh-only (recast meme tour reset shield 130 HP + duree 2 rounds).
        //   Bible "Anti-Soulrender qui charge".
        //   Pas de -1 PM sur attaquant (cf Riposte Carmin) : Bible Linceul muet sur ce
        //   point, juste "renvoie 40 dgts".
        public const int LinceulDOmbresPACost           = 3;
        public const int LinceulDOmbresShieldHP         = 130;
        public const int LinceulDOmbresTurns            = 2;
        public const int LinceulDOmbresRipostMeleeDmg   = 40;

        // 3.7.c.ii — Voile Spectral (REWORK refonte 30 mai) :
        //   2 PA, range 4 ENEMY, cap 1x/tour. SETUP : téléporte TOUS les leurres actifs de la
        //   Ghostra sur des cases libres AUTOUR de l'ennemi ciblé (cardinales en priorité — Manhattan
        //   1 pour enchaîner Éveil/dorsal — puis diagonales en secours). Gate : rejet si 0 leurre.
        //   PERD son ancien cleanse anti-DoT + DotImmune (décision Lorenzo : la Ghostra n'a PLUS
        //   d'anti-DoT, matchups durs Necram/Soulrender voulus).
        public const int VoileSpectralPACost            = 2;
        public const int VoileSpectralRangeMax          = 3;  // patch 7 juin : 4 -> 3
        public const int VoileSpectraleMaxUsesPerTurn   = 1;
        public const int VoileSpectralDmgPerAdjacent    = 60;  // #21 (5 juin) : 60 dmg par leurre TP adjacent à la cible
        public const int VoileSpectralDmgMax            = 180; // #21 : cap 180 (= 3 leurres tous adjacents)

        // 3.7.c.iii — Réplique Protectrice (Bible V7.1 ligne 1187, AMENDEMENT 16 mai 2026) :
        //   Bible orig : 3 PA, 40% redirection, +60 HP heal destroy, 2 rounds.
        //   AMENDEMENT Lorenzo (cap balance) :
        //     - PA : 3 -> 4
        //     - Redirection : 40% -> 30%
        //     - Heal destroy : +60 -> +80 HP (compense le nerf PA + %)
        //     - Lifetime : 4 rounds (amendement leurres) -> 3 rounds (override per-Kind dans
        //       DecoyHelpers.TickLifetimeAtSubTurnStart via DecoyHelpers.ProtectiveLifetimeRounds)
        //     - Stack : si plusieurs Protective vivants, UN SEUL absorbe par hit (pas de cumul
        //       30%+30% = 60%). Le code itere slot 0->1->2 et prend le PREMIER vivant trouve,
        //       comportement deja en place (cf hook redirection). Sur hits successifs, decoys
        //       meurent les uns apres les autres (multiplie la duree de protection, pas le %).
        //
        //   3 PA->4 PA, range 3 case vide. Pose un DecoyKind.Protective (HP=200 via
        //   DecoyHelpers.ProtectiveDecoyMaxHP, lifetime ProtectiveLifetimeRounds=3 override).
        //   Compte dans cap 3 leurres (DecoyHelpers.MaxDecoys). Bible "Leurre tank, sustain cache".
        //
        //   HOOK REDIRECTION 30% (Bible "redirige X% des dgts subis pendant N tours") :
        //     - Dans SpellSystem damage loop SUR la Ghostra (target=Ghostra), APRES reduction%
        //       (Densite Inerte + Garde Prot + Ancrage), AVANT shield Ghostra. Pipeline
        //       Bible-strict : "dgts subis" = incoming post-mitigation, avant shield. Le shield
        //       Ghostra absorbe ensuite ce qui passe (70% restant + overflow si decoy ne peut
        //       pas tout absorber).
        //     - Decoy absorbe min(30%, decoyHP). Si decoy meurt suite a la redirection
        //       (decoyHP -> 0) -> DecoyHelpers.DestroyByEnemyAction (heal +80 HP Ghostra,
        //       Bible amendee). La portion 30% qui depasse l'HP du decoy s'evapore (le decoy
        //       a absorbe ce qu'il pouvait, le surplus est "dans le vide"). dmgThisTarget
        //       est reduit du 30% complet (Bible-strict : on retire toujours 30% du dmg
        //       Ghostra, peu importe si le decoy avait assez d'HP).
        //     - Rebranche dans path custom Charge Brutale (avant le shield absorb CB).
        //   Fenetre de redirection : tant que un decoy Protective vivant existe (max 3 rounds
        //   amendement Protective override). Si decoy detruit -> redirection se ferme automatiquement.
        public const int RepliqueProtectricePACost            = 3;    // équilibrage 2 juin : 4->3 PA
        public const int RepliqueProtectriceRangeMax          = 3;
        public const int RepliqueProtectriceRedirectPercent   = 30;

        // 3.7.c.iv — Dernier Pas (Bible V7.1 ligne 1194) :
        //   4 PA self, range 5 case vide (teleport), 1x/match (OncePerMatchBitGhostraDernierPas=5).
        //   Gate HP < DernierPasHpThresholdPct (30%) : pre-check pre-PA (style Cocon Putride
        //   / Dernier Souffle / Evanescence) -> rejet propre si HP plein, PA NON consume.
        //   Effet en 3 temps (handler) :
        //     (1) Heal caster +200 HP (DernierPasHealAmount, cap MaxHP).
        //     (2) Teleport sur case vide range 5 (MovementHelpers.MoveNonPM).
        //     (3) Pose un DecoyKind.Standard sur la case quittee. Si cap 3 leurres atteint
        //         (DecoyHelpers.MaxDecoys), DESTROY le leurre LE PLUS ANCIEN (min SpawnedOnTurn)
        //         via DestroyAtSlot (internal, sans heal) puis TrySpawn nouveau Standard.
        //         Decision Lorenzo : le panic-button doit toujours poser son leurre, on
        //         sacrifie le plus vieux. Cohérent avec gameplay panic-button "remix les leurres".
        //   Bible "Panic button mobile : se soigne + s'echappe + laisse leurre derriere."
        public const int DernierPasPACost            = 4;
        public const int DernierPasRangeMax          = 5;
        public const int DernierPasHealAmount        = 200;
        public const int DernierPasHpThresholdPct    = 30;

        // 3.7.c.v — Pas de l'Au-Dela (Bible V7.1 ligne 1180) :
        //   2 PA self. Pas de range (Filter=Self). Pas de gate HP, pas de 1x/match.
        //   Cast pose flag NextMoveIgnoresUnits=1 + CurrentPM += PasAuDelaPMBonus (cap MaxPM
        //   ignore, addition pure au pool courant pour respecter "+2 PM ce tour" Bible).
        //   Le prochain Move ce tour traverse ennemis ET leurres (A* ignoreEnemyOccupants=true).
        //   Chaque ennemi traverse pendant le path -> PasAuDelaDorsalDamage HP loss direct
        //   (frappe gratuite "dorsale" sans pipeline shield/reflect/redirect - Bible-stricte).
        //   Decision Lorenzo : multi-targets, TOUS les ennemis traverses prennent 60 dgts.
        //   Flag consume au premier Move applique (clear dans MovementSystem.ApplyMove).
        //   Si pas utilise avant fin tour Ghostra : cleanup defensif au EnterTurnStart
        //   suivant du combattant actif (le flag ne sert qu'au combattant pendant son tour).
        // NB (refonte 30 mai) : Pas de l'Au-Dela SUPPRIME du pool (slot 100 -> Communion Spectrale).
        //   Les constantes PasAuDela* + l'infra PasAuDelaReady (MovementSystem / preview / renderer)
        //   sont laissees DORMANTES (plus aucun sort ne pose PasAuDelaReady). A nettoyer en passe ⑦.
        public const int PasAuDelaPACost          = 2;
        public const int PasAuDelaPMBonus         = 2;
        public const int PasAuDelaDorsalDamage    = 60;

        // 3.7.c — Communion Spectrale (refonte 30 mai, ex-Pas de l'Au-Dela slot 100) :
        //   2 PA self, cap 1x/tour. Consomme 1 leurre actif (premier slot) -> heal CommunionHeal
        //   (cap MaxHP, bloque par AntiHealShield via HealHelper). Gate >=1 leurre (reject pre-PA).
        public const int CommunionPACost          = 2;
        public const int CommunionHeal            = 150;
        public const int CommunionMaxUsesPerTurn  = 1;

        // 3.7.d — Execution Spectrale (Bible V7.1 ligne 1071) SIGNATURE Ghostra :
        //   3 PA, range 1 (melee dorsal requis), Filter=Enemy. Coute 3/3 LEURRES (ExecutionSpectraleRequiredDecoys).
        //   Pre-PA gate : count(decoys) == 3 ET cooldown expire (currentTurn - LastExecutionSpectraleUsedOnTurn >= ExecutionSpectraleCooldownTurns).
        //   Handler :
        //     (1) Capture positions des leurres slots 0 et 1 (re-spawn potentiel sur kill).
        //     (2) DestroyAtSlot(0/1/2) - consume 3 leurres inconditionnellement.
        //     (3) caster->LastExecutionSpectraleUsedOnTurn = currentTurn (cooldown set).
        //     (4) Check FacingHelpers.IsDorsalHit(caster, target) :
        //         - false : log RATE, break. PA + 3 leurres + cooldown deja consumes.
        //         - true  : (a) target->HP -= ExecutionSpectraleDamage (350) direct, bypass
        //                       shield/reduction (decision Lorenzo Bible-stricte "350 dmg" net signature).
        //                       target->DamageTakenThisRound += dmgApplique.
        //                   (b) StatusHelper.Apply(target, PlaieOuverte, mag=ExecutionSpectralePlaieDmgPerTurn (50),
        //                       turnsLeft=ExecutionSpectralePlaieTurns (3), currentTurn) -> ecrase plaie standard.
        //                   (c) Si target->HP <= 0 (kill) : caster->HP += ExecutionSpectraleKillHeal (100,
        //                       cap MaxHP, respect AntiHealShield) ET TrySpawn 2 leurres Standard aux positions
        //                       captees (decoy0/1) si toujours libres.
        //   Pas de OncePerMatchBit : signature standard reusable si 3 leurres reposes + cooldown expire.
        public const int ExecutionSpectralePACost              = 3;
        public const int ExecutionSpectraleRequiredDecoys      = 3;
        public const int ExecutionSpectraleCooldownTurns       = 4;
        public const int ExecutionSpectraleDamage              = 350;
        public const int ExecutionSpectralePlaieDmgPerTurn     = 50;
        public const int ExecutionSpectralePlaieTurns          = 3;
        public const int ExecutionSpectraleKillHeal            = 100;
        public const int ExecutionSpectraleKillRespawnDecoys   = 2;

        // 3.7.b — Permutation DECKABLE (refonte 30 mai) :
        //   1 PA (0 PA à l'Angle 3 = 3 leurres actifs, cf EffectiveStats.GetPACost), cap 1x/tour
        //   (moteur generique MaxUsesPerTurn), swap Ghostra<->un de ses leurres cible des 1 leurre
        //   actif. Pas de degats, pur repositionnement / mind-game.
        //   Ciblage Filter=TileWithLure (cible une case occupee par un leurre OWN). Portee 4 cases
        //   (PO, nerf 30 mai — etait plateau entier). Remplace l'ancienne Permutation gratuite Angle 3
        //   (touche P) qui reste DORMANTE (non reactivee, decision Lorenzo "vrai spell uniquement").
        public const int PermutationPACost          = 1;
        public const int PermutationRangeMax        = 3;   // patch 7 juin : 4 -> 3 ; (refonte 30 mai : etait plateau entier)
        public const int PermutationMaxUsesPerTurn  = 1;   // nerf 30 mai : 2x -> 1x/tour

        public static bool TryGet(SpellId id, out SpellDef def)
        {
            switch (id)
            {
                // -------------------------------------------------------------
                // SOULRENDER — Bible V7.1
                // -------------------------------------------------------------

                // Tranche-Ame (2.8) : 3 PA, melee 1, 220 dgts. TODO 2.10.c : recul 2 cases si kill.
                case SpellId.SoulrenderTrancheAme:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 1,                        // équilibrage 5 juin : 2->1 (retour mêlée pure, decision Lorenzo)
                        DamageAmount = 220,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Ouvre-Plaie (2.10.a) : 2 PA, range 1, 110 dgts.
                // Optionnel : 1 HG -> 230 dgts + cible AntiHealShield 2 tours.
                case SpellId.SoulrenderOuvrePlaie:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 1,
                        DamageAmount = 110,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 1,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                        MaxUsesPerTurn = 1, // patch 7 juin : cap 1x/tour (etait 2)
                    };
                    return true;

                // Pacte de Sang (2.10.a) : 1 PA, self, -80 HP self + +2 HG + BuffNextOffensiveDmgPercent.
                // 1 fois par match.
                case SpellId.SoulrenderPacteDeSang:
                    def = new SpellDef
                    {
                        PACost = 1,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitPacteDeSang,
                        IsOffensive = 0,
                    };
                    return true;

                // Rugissement (2.10.a) : 3 PA, AoE rayon 3 autour du caster. Pas de dgts.
                // Tous les ennemis dans la zone subissent MovementMalus + AntiTeleport (1 tour).
                // Si cible <50% HP : MovementMalus magnitude = 2 (au lieu de 1).
                //
                // Note ciblage : "AoE 3 autour du Soulrender" = centre = case du caster.
                // RangeMin=0/RangeMax=0 + Filter=AnyTile permet de cliquer sa propre case.
                // Shape CircleMedium = rayon 2 dans TargetingResolver actuel — on declare
                // l'intent rayon 3 ici, la resolution AoE specifique sera faite manuellement
                // par SpellSystem (radius 3 hardcode pour ce sort).
                case SpellId.SoulrenderRugissement:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.CircleLarge, // semantic intent rayon 3
                        Filter = TargetingFilter.Self,      // ciblage = case du caster
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                        CooldownTurns = 2,  // Refonte 29 mai : relance 2 tours
                    };
                    return true;

                // Rage Insatiable (2.10.a) : 3 PA, self, applique RageInsatiableActive 2 tours.
                // Effets pendant la duree (gere par helpers / SpellSystem) :
                //   - Sorts coutent +1 PA (EffectiveStats.GetPACost).
                //   - Apres chaque cast offensif : caster regen 1 PA (max 1 par tour, tracker
                //     dans Status.Magnitude = LastTurnPAGained).
                case SpellId.SoulrenderRageInsatiable:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // Riposte Carmin (2.10.a) : 2 PA, self, applique RipostMelee 1 tour.
                // Quand le Soulrender subit une attaque MELEE (range max == 1) pendant
                // ce status : l'attaquant prend 100 dgts ET gagne MovementMalus 1 (1 tour).
                case SpellId.SoulrenderRiposteCarmin:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // -------------------------------------------------------------
                // SOULRENDER 2.10.b
                // -------------------------------------------------------------

                // Marque de Carnage (2.10.b) : 2 PA, range 5, marque cible 3 tours.
                // Effet : tous les casts Soulrender sur cible marquee genere +1 HG bonus
                // (en plus du +1 normal Soulrender qui inflige). Gere dans damage loop.
                case SpellId.SoulrenderMarqueDeCarnage:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 5,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // Empoignade (2.10.b) : 3 PA, range 3, pull cible adjacent + AntiTeleport 1 tour.
                // Pas de dgts. Si pas de case adjacente libre : no-op silencieux (rare).
                // EMPOIGNADE (refonte 29 mai) : 3 PA, p3, pull CaC + 90 dgts + -2 PM. Devient
                //   OFFENSIVE (le 90 passe par le pipeline standard, puis pull + MovementMalus 2).
                case SpellId.SoulrenderEmpoignade:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 3,
                        DamageAmount = EmpoignadeDamage, // 90 (refonte)
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1, // refonte : devient offensive
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // Peau de Fer (2.10.b) : 3 PA, self, applique ShieldActive 200 HP / 2 tours.
                // Pendant la duree, sorts melee du caster gagnent +30 dgts (cf damage loop).
                case SpellId.SoulrenderPeauDeFer:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        CooldownTurns = 2, // Refonte 29 mai : relance 2 tours (bouclier 200)
                    };
                    return true;

                // Seve Vive (2.10.b) : 2 PA, self, heal 100 (+60 si 1 HG, +50 si DoT actif).
                // HGCostMaxOptional = 1 : player choisit 0 ou 1 HG depense.
                case SpellId.SoulrenderSeveVive:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 1,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // Dernier Souffle (2.10.b) : 4 PA, self, HP < 30% obligatoire.
                // Heal 200 HP + 3 HG. 1 fois par match. Check HP% dans SpellSystem amont.
                case SpellId.SoulrenderDernierSouffle:
                    def = new SpellDef
                    {
                        PACost = 4,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitDernierSouffle,
                        IsOffensive = 0,
                    };
                    return true;

                // -------------------------------------------------------------
                // SOULRENDER 2.10.c
                // -------------------------------------------------------------

                // Charge Brutale (2.10.c) : 4 PA, ligne range 5. Fonce en ligne droite jusqu'a la
                // 1ere unite ou case bloquante. Inflige 180 dgts a la cible touchee. Toutes les
                // cases foulees deviennent Vapeur Carmin 1 tour. Gestion specifique dans
                // ApplySpellSpecificEffects (mvt + dgts + terrain).
                // Shape Line existe deja (TargetingResolver) mais on gere l'effet specifique
                // ici car on a besoin du chemin precis + du mouvement caster.
                case SpellId.SoulrenderChargeBrutale:
                    def = new SpellDef
                    {
                        PACost = 3,                          // equilibrage juin : 4 -> 3 PA
                        Shape = TargetingShape.SingleTile, // on gere la ligne nous-memes
                        Filter = TargetingFilter.AnyTile,  // peut viser une case vide ou ennemi
                        RangeMin = 1,
                        RangeMax = ChargeBrutaleRange,
                        DamageAmount = 0, // applique manuellement dans le branche specifique
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                        MaxUsesPerTurn = 1, // equilibrage 5 juin : cap 1x/tour
                    };
                    return true;

                // Detonation Sanglante (2.10.c) : 4 PA, range 4, AoE croix 3.
                // Damage de base 60 + 40 par HG total consomme (mandatory 2 + optional max 3 = 5).
                // Sang Coagule pose sous la case centre 2 tours.
                // ATTENTION 2.11 : si on consomme 5 HG ici, ca interdit Ame Laceree et reset son cooldown.
                case SpellId.SoulrenderDetonationSanglante:
                    def = new SpellDef
                    {
                        PACost = 4,
                        Shape = TargetingShape.CrossSmall, // croix 3 cases (centre + 4 cardinales)
                        Filter = TargetingFilter.AnyTile,
                        RangeMin = 1,
                        RangeMax = 4,
                        DamageAmount = 0, // calcule dynamiquement (60 + 40*totalHG)
                        HGCostMandatory = 2,
                        HGCostMaxOptional = 3,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Curee (2.10.c) : 2 PA, range 2, 2 HG (mandatory), 150 dgts.
                // Kill chain : heal 50% HP manquants + 4 PA next turn.
                // Miss (target encore vivante) : caster prend 60 dgts self.
                // EVENTRATION (ex-Curee, refonte 29 mai) : 5 PA, p1 (melee), 220 dgts +
                //   Plaie Ouverte 50/tour x 3 rounds. Plus de kill-heal / miss-selfdamage / cout HG.
                //   Identifiant enum SoulrenderCuree conserve (eviter le churn de refs) ; nom
                //   affiche = "Eventration" (cf SpellBibleTexts, Passe 2b).
                case SpellId.SoulrenderCuree:
                    def = new SpellDef
                    {
                        PACost = 5,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 1,
                        DamageAmount = CureeDamage, // 220
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // Cauterisation (2.10.c, stub) : 2 PA, self, retire tous DoT + heal 60 par DoT
                // retire (min 60 toujours, max 180 cap 3 DoTs). Pour 2.10.c : aucun DoT actuel,
                // donc heal = 60 (min). Structure prete pour Phase 3 Necram.
                case SpellId.SoulrenderCauterisation:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // -------------------------------------------------------------
                // SOULRENDER 2.11 — SIGNATURE
                // -------------------------------------------------------------

                // Ame Laceree (2.11) : signature Soulrender.
                // 2 PA, range 1 (melee), 5 HG (consomme toute la jauge), 320 dgts.
                // Le Soulrender heal 50% des dgts qui passent (post-shield).
                // Si kill : Sang Coagule en croix 5 cases (caster centre + 4 cardinales).
                // Cooldown 4 tours apres usage. Re-castable si HG remonte a 5.
                // Slot separe du deck de 6 (touche dediee en View).
                case SpellId.SoulrenderAmeLaceree:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 1,
                        DamageAmount = AmeLaceeDamage,
                        HGCostMandatory = 5, // 5/5 HG obligatoire
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // -------------------------------------------------------------
                // NIGHTSEER 2.15.a — OFFENSIFS
                // -------------------------------------------------------------

                // Tir Precis (2.15.a) : 3 PA, range 6, 200 dgts.
                // Bonus Bible : si target Marque.Traque -> 280 dgts ET caster regen +1 PR.
                case SpellId.NightseerTirPrecis:
                    def = new SpellDef
                    {
                        PACost = 2,                          // patch 7 juin : 3 -> 2 PA
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 4,                        // patch 7 juin : reste 4 (PA passe a 2)
                        DamageAmount = TirPrecisDmg,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                        MaxUsesPerTurn = 1,                  // équilibrage 2 juin : cap 1x/tour
                    };
                    return true;

                // Volee d'Epines (2.15.a) : 3 PA (patch 7 juin, etait 4), ligne range 5, 130 dgts par cible.
                // Pose un Filet de Ronces (TrapKind) sur la DERNIERE case touchee.
                case SpellId.NightseerVoleeDEpines:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.Line,
                        Filter = TargetingFilter.AnyTile,
                        RangeMin = 1,
                        RangeMax = 5,
                        DamageAmount = VoleeDEpinesDmg,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // Detonation Onirique (2.15.a) : 4 PA, range 5 FIXE, AoE croix de 5, 170 dgts.
                // Declenche tes pieges sous la croix : +30 par piege detone + TRAQUE (SEUL bonus).
                // Equilibrage 6 juin : AoE carre 3x3 -> croix de 5, option "2 PR -> portee 10" retiree,
                // ancien "+80 si couvre un piege" retire.
                case SpellId.NightseerDetonationOnirique:
                    def = new SpellDef
                    {
                        PACost = 4,
                        // Equilibrage 6 juin (choix Lorenzo) : croix de 5 (centre + 4 cardinales), comme
                        //   Salve Mortelle. Avant : Square3x3 (9 cases, trop cheat). CrossSmall est géré
                        //   par TargetingResolver (sim) ET par le preview View (même resolver) -> AoE visible.
                        Shape = TargetingShape.CrossSmall,
                        Filter = TargetingFilter.AnyTile,
                        RangeMin = 1,
                        RangeMax = DetonationOniriqueRangeMaxBase, // 5 FIXE (plus d'override portee)
                        DamageAmount = DetonationOniriqueDmg,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0, // option "2 PR -> portee 10" retiree (equilibrage 6 juin)
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Frappe de l'Ombre (2.15.a) : 4 PA, range 3, 200 dgts.
                // Bonus Bible : si target.PM < (target.MaxPM/2) -> 300 dgts ET applique Empreinte 2 tours.
                case SpellId.NightseerFrappeDeLOmbre:
                    def = new SpellDef
                    {
                        PACost = 4,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 3,
                        DamageAmount = FrappeDeLOmbreDmg,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Salve Mortelle (2.15.a) : 5 PA, range 6, 3 PR mandatory. FINISHER d'execution.
                // Equilibrage 6 juin (choix Lorenzo, distinction nette vs Detonation Onirique) :
                //   AoE croix 5 -> CARRE PLEIN 3x3 (9 cases) ; 200 centre / 120 autour ; +90 sur Traque
                //   (etait +60) ; NE detone PLUS les pieges (gere dans le switch d'effets) ; relance 2 tours.
                case SpellId.NightseerSalveMortelle:
                    def = new SpellDef
                    {
                        PACost = 5,
                        Shape = TargetingShape.Square3x3, // 9 cases (carre plein)
                        Filter = TargetingFilter.AnyTile,
                        RangeMin = 1,
                        RangeMax = 6,
                        DamageAmount = 0, // calcule per-cell dans damage loop (200 centre / 120 autour)
                        HGCostMandatory = (byte)SalveMortelleHGCost,
                        HGCostMaxOptional = 0,
                        CooldownTurns = (byte)SalveMortelleCooldownTurns, // relance 2 tours (finisher)
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // -------------------------------------------------------------
                // NIGHTSEER 2.15.b — TACTIQUES
                // -------------------------------------------------------------

                // AFFUT (ex-Marque du Chasseur, patch 7 juin) : 1 PA self, self-buff +2 portee /
                //   +10% dgts pendant 2 tours, relance 3 tours. Ne pose PLUS Traqué (StatusKind.AffutActive).
                case SpellId.NightseerMarqueDuChasseur:
                    def = new SpellDef
                    {
                        PACost = 1,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1,
                        CooldownTurns = (byte)MarqueDuChasseurCooldownTurns, // relance 3 tours
                    };
                    return true;

                // Filet de Ronces (2.15.b) : 2 PA, range 4, pose Filet (Trap + Voile).
                // Quand un ennemi entre sur la case : 100 dgts + -2 PM + Empreinte 2 tours.
                case SpellId.NightseerFiletDeRonces:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.AnyTile,
                        RangeMin = 1,
                        RangeMax = 4,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 2, // Refonte 29 mai : cap 2x/tour
                    };
                    return true;

                // Champ de Mines (2.15.b) : 4 PA, range 3, AoE 3x3.
                // Pose 3 Mines voilees dans la zone (les 3 1eres cases dispo de l'AoE pour 2.15.b).
                // Chaque mine : 70 dgts + Empreinte 2 tours.
                case SpellId.NightseerChampDeMines:
                    def = new SpellDef
                    {
                        PACost = 4,
                        Shape = TargetingShape.Square3x3,
                        Filter = TargetingFilter.AnyTile,
                        RangeMin = 1,
                        RangeMax = 3,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // Bourrasque (2.15.b) : 3 PA, range 5, push cible 2 cases (direction au 2e clic).
                // Equilibrage juin : option "1 PR -> push 4 cases" RETIREE (HGCostMaxOptional 1 -> 0).
                // Push fixe BourrasquePushBase (2), aucune depense de PR.
                case SpellId.NightseerBourrasque:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 5,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,               // equilibrage juin : plus d'option PR (push fixe 2)
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 2, // Refonte 29 mai : cap 2x/tour
                    };
                    return true;

                // PIÈGE BONDISSANT (ex-Souffle Glacial, refonte 29 mai) : 2 PA, pose un piège-catapulte
                //   sur une case (p4) ; éjection directionnelle au déclenchement. DIRECTIONNEL (2 clics).
                //   Identifiant enum NightseerSouffleGlacial conservé.
                case SpellId.NightseerSouffleGlacial:
                    def = new SpellDef
                    {
                        PACost = PiegeBondissantPACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.AnyTile,    // pose sur une case
                        RangeMin = 1,
                        RangeMax = PiegeBondissantRangeMax,  // 4
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // -------------------------------------------------------------
                // NIGHTSEER 2.15.c — SURVIE
                // -------------------------------------------------------------

                // REPLI ÉPINEUX (ex-Flèche Traçante, ID NightseerVoileDOmbre conservé, patch 7 juin) :
                //   2 PA self. Repousse de 3 cases TOUS les ennemis adjacents (Manhattan 1) + heal 100.
                //   Relance 1 tour. Sort de survie / désengagement. Effet dans ApplySpellSpecificEffects.
                case SpellId.NightseerVoileDOmbre:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        CooldownTurns = (byte)RepliEpineuxCooldownTurns, // relance 1 tour
                    };
                    return true;

                // Pas Furtif : 2 PA, range 0-4, teleport sur case vide (filter EmptyTile).
                // Option : 1 PR -> case d'arrivee Voilee 2 tours (HGCostMaxOptional = 1).
                case SpellId.NightseerPasFurtif:
                    def = new SpellDef
                    {
                        PACost = 4,                          // équilibrage 2 juin : 2->4 PA (+ portée 4->3)
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.EmptyTile,
                        RangeMin = 1,
                        RangeMax = PasFurtifRangeMax,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 1,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // Camouflage Ronces : 3 PA, self, ShieldActive 130 HP / 2 rounds + RoncesAura
                // (70 dgts ennemis adjacents en fin de round, 2 rounds).
                case SpellId.NightseerCamouflageRonces:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // Seve Sauvage : 3 PA, self, heal 130 (+60 si trap declenche ces 2 rounds, +30 si voile actif).
                case SpellId.NightseerSeveSauvage:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // Evanescence : 4 PA, range 0-7, HP<30%, 1/match. Teleport sur case vide + heal 150
                // + Voile 2 tours sur case quittee. Filter EmptyTile pour valider la destination.
                case SpellId.NightseerEvanescence:
                    def = new SpellDef
                    {
                        PACost = 4,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.EmptyTile,
                        RangeMin = 1,
                        RangeMax = EvanescenceRangeMax,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNightseerEvanescence,
                        IsOffensive = 0,
                    };
                    return true;

                // -------------------------------------------------------------
                // NIGHTSEER 2.16 — SIGNATURE TRAQUENARD
                // -------------------------------------------------------------

                // Traquenard : signature Nightseer.
                // 2 PA, range 5, 4/4 PR (consomme toute la jauge), 280 dgts (+80 si marque).
                // Teleport caster a 1 case adjacente cible + Paralysie -3 PM/-2 PA prochain tour.
                // Si target Traque/Empreinte OU Voile sur case originale (caster owner) :
                //   +80 dgts, consume marque/voile, +2 PR caster apres coup.
                // Cooldown 4 tours. Re-castable si PR remonte a 4.
                case SpellId.NightseerTraquenard:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = TraquenardRangeMax,
                        DamageAmount = TraquenardDmgBase,
                        HGCostMandatory = (byte)TraquenardPRCost, // 4 PR mandatory
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // -------------------------------------------------------------
                // COLOSSAR — Bible V7.1 (3.3.a.i)
                // -------------------------------------------------------------

                // Frappe Lourde (3.3.a.i) : 3 PA, melee 1, 180 dgts.
                // Bonus "epinglee" : +100 dgts (= 280) si la case OPPOSEE au caster derriere
                // la cible contient un obstacle (Pilier/Mur Colossar) OU est hors grille (bord).
                // Bonus passif Densite Inerte adjacence : +20 dmg si caster adjacent a son obstacle
                // (range <= 2 ; FrappeLourde range 1, donc OK). Apply dans le handler.
                case SpellId.ColossarFrappeLourde:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 1,
                        DamageAmount = FrappeLourdeDmgBase,  // 180 base, modifie en handler
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                        MaxUsesPerTurn = 2, // equilibrage 5 juin : cap 2x/tour
                    };
                    return true;

                // Represailles (3.3.a.i) : 3 PA, melee 1, 100 dgts immediat + applique
                // RipostMelee 80 dmg sur le CASTER pour 2 tours (reflect sur attaque melee subie).
                // Bible cap 4 retours non implemente (edge case, TODO 3.3.a.iii ou Phase 6).
                // Bonus passif Densite Inerte adjacence : +20 dmg sur les 100 dgts immediats si caster adjacent.
                case SpellId.ColossarRepresailles:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 1,
                        DamageAmount = RepresaillesDmgImmediate, // 100, +20 adjacence en handler
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // -------------------------------------------------------------
                // COLOSSAR — Bible V7.1 (3.3.a.ii)
                // -------------------------------------------------------------

                // Onde de Choc : 3 PA, AoE rayon 1 autour caster, 80 dgts adj + push 2 cases.
                // Resolution AoE manuelle dans SpellSystem (similaire Rugissement Soulrender).
                // Si push s'arrete contre obstacle/bord : +80 dgts + TRAUMA (-1 PA -1 PM 1 tour).
                case SpellId.ColossarOndeDeChoc:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.CircleSmall, // rayon 1 (intent declare, resolution custom AoE in handler)
                        Filter = TargetingFilter.Self,      // target = case du caster
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = OndeDeChocDmg,       // 80 base ; +80 bonus + TRAUMA via handler
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                        MaxUsesPerTurn = 2, // equilibrage 5 juin : cap 2x/tour
                    };
                    return true;

                // Marteau Punisseur : 4 PA, range 1-2, 160 dgts. Si target.PA < 4 (a deja cast ce tour
                // ou debuff PA actif) : 240 dgts + TRAUMA ActionMalus 2 PA prochain tour.
                case SpellId.ColossarMarteauPunisseur:
                    def = new SpellDef
                    {
                        PACost = 4,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 2,
                        DamageAmount = MarteauPunisseurDmg, // 160 base ; modife a 240 en handler si depleted
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Choc Sismique : 4 PA, LIGNE range 4, 130 dgts a TOUTES les cibles touchees +
                // MovementMalus -1 PM (1 tour). Si un obstacle Colossar (Pilier/Mur OWN) sur trajectoire :
                // traverse + +50 dgts a la cible suivante. Custom handler complet (bypass pipeline standard).
                case SpellId.ColossarChocSismique:
                    def = new SpellDef
                    {
                        PACost = 4,
                        Shape = TargetingShape.SingleTile, // cible une case finale ; handler custom resoud la ligne
                        Filter = TargetingFilter.AnyTile,
                        RangeMin = 1,
                        RangeMax = ChocSismiqueRange,
                        DamageAmount = 0,                  // handler custom applique le dmg per-target
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,                   // 0 pour bypass damage loop standard (handler custom)
                    };
                    return true;

                // -------------------------------------------------------------
                // COLOSSAR — Bible V7.1 (3.3.b.i) — TACTIQUES
                // -------------------------------------------------------------

                // Pilier (3.3.b.iii Bible-correct) : 3 PA, range 3, case vide. Pose 1 Pilier 200 HP / 3 tours.
                // +1 FD via SpawnObstacle hook (3.2). Bloque mouvement + LoS.
                case SpellId.ColossarPilier:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.EmptyTile,
                        RangeMin = 1,
                        RangeMax = PilierRangeMax,           // 3 (Bible)
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 2, // Refonte 29 mai : cap 2x/tour
                    };
                    return true;

                // Mur de Pierre (3.3.b.iii Bible-correct) : 4 PA, range 4, ligne 3 cases (perpendiculaire
                // axe caster->cible) 150 HP / 2 tours. Optionnel : 1 FD -> 5 segments au lieu de 3.
                // Equilibrage juin : +2 FD FLAT par Mur (handler), au lieu de +1 par segment (gainFondation:false).
                case SpellId.ColossarMurDePierre:
                    def = new SpellDef
                    {
                        PACost = 4,                          // Bible 4 (vs 5 prev)
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.EmptyTile,
                        RangeMin = 1,
                        RangeMax = MurDePierreRangeMax,      // 4 (Bible)
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 1,               // option 1 FD -> 5 segments (Bible)
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                        CooldownTurns = 2,  // équilibrage 2 juin : intervalle de relance 1 tour (jouable un tour sur deux)
                    };
                    return true;

                // Ancrage (3.3.b.iii Bible-correct) : 2 PA, range 4, ENEMY. Anti-mobilite ultime.
                // Cible : -2 PM 2 tours + immune push/pull/teleport 1 tour. Pas de damage.
                case SpellId.ColossarAncrage:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,      // Bible : ENEMY (pas Self !)
                        RangeMin = 1,
                        RangeMax = AncrageRangeMax,          // 4 (Bible)
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                        CooldownTurns = 3,  // equilibrage 5 juin : relance 3 tours (Lorenzo : +1 sur la relance, etait 2)
                    };
                    return true;

                // Provocation (3.3.b.iii Bible-correct) : 2 PA, range 5, 1 tour. Apply Provoked + -1 PM.
                // Hooks : sorts non-ciblant le caster coutent +2 PA pour la cible (EffectiveStats),
                // 100 dmg auto si pas adjacent au caster en fin de SON tour (TurnSystem.EnterTurnEnd).
                case SpellId.ColossarProvocation:
                    def = new SpellDef
                    {
                        PACost = 2,                          // Bible 2 (vs 3 prev)
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = ProvocationRangeMax,      // 5 (Bible vs 4 prev)
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                        CooldownTurns = (byte)ProvocationCooldownTurns, // patch 7 juin : relance 2 tours
                    };
                    return true;

                // Brisure (3.3.b.iii Bible-correct) : 3 PA range 2, ENEMY. 90 dgts + retire 1 buff/bouclier.
                // Si pas de buff : applique TRAUMA (-2 PA prochain tour). Anti-tank/anti-tortue Bible.
                // Custom handler car logique de retrait de buff (priorite ShieldActive d'abord).
                case SpellId.ColossarBrisure:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,      // Bible : ENEMY (pas obstacle !)
                        RangeMin = 1,
                        RangeMax = BrisureRangeMax,          // 2 (Bible vs 5 prev)
                        DamageAmount = BrisureDamage,        // 90 (Bible)
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,                     // pipeline standard (90 dmg -> Densite Inerte etc.)
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // -------------------------------------------------------------
                // COLOSSAR — Bible V7.1 (3.3.c) — SURVIE
                // -------------------------------------------------------------

                // Stoicisme : 3 PA self, ShieldActive 200/2T + AnchorImmune Magnitude=0 (immune push/pull/tp)
                // 2T. Tracker StoicismeExpiresOnTurn pose pour heal 80 si shield survit. Handler dans SpellSystem.
                case SpellId.ColossarStoicisme:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        CooldownTurns = 2, // Refonte 29 mai : relance 2 tours (bouclier 200)
                    };
                    return true;

                // Garde Protectrice : 2 PA self, DamageReductionPercent 30 / 2T. Cap combine 50% avec
                // Densite Inerte (additif clamp via ColossarPassif.GetCombinedDamageReductionPercent).
                case SpellId.ColossarGardeProtectrice:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // Ressac Vital : 2 PA self, heal 80 + 30/hit subi tour precedent (max +120 = 4 hits).
                // Lit Combatant.HitsTakenLastRound (snapshot fait par TurnSystem.EnterTurnStart).
                case SpellId.ColossarRessacVital:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // Renvoi du Bouclier : 3 PA self, RipostAll 60 dgts (melee+distance) 1T, cap 4 retours
                // (reuse Combatant.RepresaillesReflectsLeft).
                case SpellId.ColossarRenvoiDuBouclier:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // EBOULEMENT (ex-Soin Lourd, refonte 29 mai) : 3 PA, p3, vise un de TES Piliers ->
                //   le détruit -> AoE 150 rayon 1 (CircleSmall, via pipeline) + push autour + 30 HP
                //   (passif destruction). Identifiant enum SoulLourd conservé (nom affiché "Éboulement").
                case SpellId.ColossarSoinLourd:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.CircleSmall, // AoE rayon 1 autour du pilier ciblé
                        Filter = TargetingFilter.AnyTile,   // on vise une case (un Pilier own)
                        RangeMin = 1,
                        RangeMax = EboulementRangeMax,       // 3
                        DamageAmount = EboulementDamage,     // 150 (pipeline standard)
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // -------------------------------------------------------------
                // COLOSSAR — Bible V7.1 (3.3.d) — SIGNATURE
                // -------------------------------------------------------------

                // Effondrement : 4 PA, self, 5 FD mandatory (consomme tout). Refonte 29 mai :
                // déclenchement IMMÉDIAT au cast (damage AoE rayon 2 + éjection + Failles + buff
                // -1 PA/-30%, plus de +1 PM) via TurnSystem.TriggerEffondrement.
                case SpellId.ColossarEffondrement:
                    def = new SpellDef
                    {
                        PACost = EffondrementPACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,            // applique par TurnSystem au trigger (pas au cast)
                        HGCostMandatory = (byte)EffondrementFDCost, // 3 FD obligatoire
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,             // pas d'effet damage au cast
                    };
                    return true;

                // -------------------------------------------------------------
                // NECRAM — Bible V7.1 (3.5.a.i Offensifs base)
                // -------------------------------------------------------------

                // Crachat Acide : 3 PA, range 4, 90 dgts + applique 2 marques venin (cap 4/cible).
                // ApplyMark fait en post-damage dans le handler SpellSystem.
                case SpellId.NecramCrachatAcide:
                    def = new SpellDef
                    {
                        PACost = CrachatAcidePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = CrachatAcideRangeMax,
                        DamageAmount = CrachatAcideDmg,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Morsure Putride : 4 PA, melee 1, 110 dgts + 22/marque (cap +90). Si kill -> transfere
                // les marques sur l'ennemi vivant le plus proche. Bonus dgts compute en handler (pre-damage).
                case SpellId.NecramMorsurePutride:
                    def = new SpellDef
                    {
                        PACost = MorsurePutridePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 1,
                        DamageAmount = MorsurePutrideDmgBase, // 110 base, +22/marque modife en handler
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Detonation Virulente : 4 PA, range 4, 80 dgts + consomme TOUTES marques (50/marque).
                // Bonus dgts compute en handler (pre-damage), reset VeninStacks=0 en post-damage.
                case SpellId.NecramDetonationVirulente:
                    def = new SpellDef
                    {
                        PACost = DetonationVirulentePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = DetonationVirulenteRangeMax,
                        DamageAmount = 0,                    // refonte : tick appliqué dans le handler (bypass)
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // Faux Decharnee : 4 PA, AoE Square3x3 autour caster, 130 dgts par cible. Heal Necram
                // selon marques sur cibles touchees (post-damage, somme cumulee). Filter=Self pour
                // que cmd.TargetX/Y soient redirigees vers caster cell par CombatInputController.
                case SpellId.NecramFauxDecharnee:
                    def = new SpellDef
                    {
                        PACost = FauxDecharneePACost,
                        Shape = TargetingShape.Square3x3, // 9 cases (centre + 8 voisines, AoE iso)
                        Filter = TargetingFilter.Self,    // target = case caster
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = FauxDecharneeDmg,  // 130 par cible touchee (autre que caster)
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Brume Toxique : 4 PA, range 4, AoE 3x3 centree sur cible souris, 2 tours.
                // DamageAmount override en 60 dans handler pour les unites DEJA dans la zone a la pose
                // (Bible distingue 60 pose / 30 entree / +1m fin de tour). Le terrain est pose
                // dans le handler post-damage en parallele.
                case SpellId.NecramBrumeToxique:
                    def = new SpellDef
                    {
                        PACost = BrumeToxiquePACost,
                        Shape = TargetingShape.Square3x3,  // 9 cases AoE
                        Filter = TargetingFilter.AnyTile,  // case quelconque (vide ou avec unite)
                        RangeMin = 1,
                        RangeMax = BrumeToxiqueRangeMax,
                        DamageAmount = 0,                    // refonte : plus de dégâts directs (zone DoT marques)
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,                     // refonte : pose de zone, pas d'attaque directe
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                        CooldownTurns = 2,  // équilibrage 2 juin : intervalle de relance 1 tour (jouable un tour sur deux)
                    };
                    return true;

                // 3.5.b.i — Inoculation : setup. 1 PA, range 5, applique 2 marques venin sur une cible
                // ennemie (cap 4 gere par VeninHelpers.ApplyMark). Patch 8 juin : IsOffensive=1 +
                // DamageAmount=30 -> 30 dgts directs via le pipeline ; le ApplyMark reste dans le handler.
                case SpellId.NecramInoculation:
                    def = new SpellDef
                    {
                        PACost = InoculationPACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = InoculationRangeMax,
                        DamageAmount = InoculationDmg, // patch 8 juin : 30 dgts (etait 0)
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1, // patch 8 juin : passe offensif pour appliquer les 30 dgts
                        MaxUsesPerTurn = 1, // equilibrage 5 juin (B4) : cap 1x/tour (anti-spam marques/PT)
                    };
                    return true;

                // 3.5.b.i — Marque Sacrificielle : buff DoT sur la cible. 2 PA, range 5,
                // applique status MarqueSacrificielle (magnitude=20, duree 3 rounds). Hook dans
                // VeninHelpers.TryTick pour bonus +20 dmg/tick. Pas de damage direct (IsOffensive=0).
                case SpellId.NecramMarqueSacrificielle:
                    def = new SpellDef
                    {
                        PACost = MarqueSacrificiellePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = MarqueSacrificielleRangeMax,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // 3.5.b.ii — Symbiose Morbide : self-buff lifesteal DoT. 3 PA, self, status 2
                // rounds. Hook dans VeninHelpers.TryTick : tout Necram porteur soigne par tick
                // venin sur ennemis. Pas de damage direct (IsOffensive=0).
                case SpellId.NecramSymbioseMorbide:
                    def = new SpellDef
                    {
                        PACost = SymbioseMorbidePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // 3.5.b.iv — Contagion : propagation AoE marques. 3 PA, range 5. Filter Enemy +
                // pre-validation target marquee dans le handler. HGCostMaxOptional=2 PT pour
                // boost cap copie (3->4). Pas de damage direct (IsOffensive=0).
                case SpellId.NecramContagion:
                    def = new SpellDef
                    {
                        PACost = ContagionPACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = ContagionRangeMax,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // 3.5.b.iii — Pas Spectral : mobilite + traversee ennemis. 2 PA self. Pas de
                // ÉCHANGE SPECTRAL (ex-Pas Spectral, refonte 29 mai) : 2 PA, p5, ENEMY. Swap de
                //   place avec la cible + 80 dgts (pipeline). Le swap est appliqué dans le handler.
                case SpellId.NecramPasSpectral:
                    def = new SpellDef
                    {
                        PACost = PasSpectralPACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = EchangeSpectralRangeMax,  // 5
                        DamageAmount = EchangeSpectralDamage, // 80 (pipeline)
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // 3.5.c.i — Voile de Pestilence : aura defensive 2 rounds, 3 PA self. Pas de
                // damage direct (IsOffensive=0). Effet pose dans le handler SpellSystem (Apply
                // PestilenceAura). Hooks dans TurnSystem.EnterTurnEnd (adjacence) + SpellSystem
                // damage loop (riposte marque).
                case SpellId.NecramVoilePestilence:
                    def = new SpellDef
                    {
                        PACost = VoilePestilencePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // 3.5.c.ii — Carapace Visqueuse : bouclier piege 2 rounds, 3 PA self. Pas de
                // damage direct (IsOffensive=0). Effet pose dans le handler SpellSystem (Apply
                // ShieldActive 110 HP + CarapaceVisqueuse flag). Hook riposte marque dans
                // SpellSystem damage loop (apres absorption shield, avant bloc HP loss).
                case SpellId.NecramCarapaceVisqueuse:
                    def = new SpellDef
                    {
                        PACost = CarapaceVisqueusePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        CooldownTurns = 2, // Refonte 29 mai : relance 2 tours (bouclier 160)
                    };
                    return true;

                // 3.5.c.iii — Drain Vital : 3 PA range 4, 60 dgts cible ennemie. Damage applique
                // via le damage pipeline standard. Heal caster Necram applique post-damage dans
                // SpellSystem (30 base, 60 si target.VeninStacks >= 3).
                case SpellId.NecramDrainVital:
                    def = new SpellDef
                    {
                        PACost = DrainVitalPACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = DrainVitalRangeMax,
                        DamageAmount = DrainVitalDamage,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour (rework scale marques en Passe 2)
                    };
                    return true;

                // 3.5.c.iv — Pulse Sanguin Vert : 3 PA self. Heal Necram caster base 70 + 15/marque
                // (cap +90) somme sur ennemis vivants Manhattan <=4. +30 HP si 1 PT optionnel.
                // Effet pose dans le handler SpellSystem (pas de damage path).
                case SpellId.NecramPulseSanguinVert:
                    def = new SpellDef
                    {
                        PACost = PulseSanguinVertPACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 1,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // Refonte 29 mai : cap 1x/tour
                    };
                    return true;

                // 3.5.c.v — Cocon Putride : 4 PA self, panic signature 1x/match. Gate HP <30%
                // verifie inline dans TryCastSpell (style Dernier Souffle / Evanescence) AVANT
                // consume PA. Effet pose dans le handler SpellSystem (heal self + AoE marques).
                case SpellId.NecramCoconPutride:
                    def = new SpellDef
                    {
                        PACost = CoconPutridePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNecramCoconPutride,
                        IsOffensive = 0,
                    };
                    return true;

                // 3.5.c.vi — Virus Fatal (SIGNATURE Necram). 2 PA, range 5, Filter Enemy,
                // 6/6 PT mandatory (consomme toute la jauge via HGCostMandatory generique).
                // Cooldown 4 tours verifie inline dans TryCastSpell AVANT consume PA (pattern
                // Ame Laceree / Traquenard / Effondrement). Damage custom dans le handler
                // (tick venin * 3, hooks Symbiose, transfert marques sur kill).
                // -------------------------------------------------------------
                // GHOSTRA — Bible V7.1 (3.7.a)
                // -------------------------------------------------------------

                // Lame Spectrale (3.7.a.i.2) : 3 PA melee 1, 170 dgts + bonus dorsal + 60 si PlaieOuverte.
                case SpellId.GhostraLameSpectrale:
                    def = new SpellDef
                    {
                        PACost = LameSpectralePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = LameSpectraleRangeMax,
                        DamageAmount = LameSpectraleDmgBase, // bonus PlaieOuverte + dorsal modifs en handler
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Lame Vorace Spectrale (3.7.a.i.2) : 3 PA melee 1, 130 dgts + 60 si PlaieOuverte
                // (NON consommee) + bonus dorsal.
                case SpellId.GhostraLameVoraceSpectrale:
                    def = new SpellDef
                    {
                        PACost = LameVoracePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = LameVoraceRangeMax,
                        DamageAmount = LameVoraceDmgBase, // bonus modifs en handler
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                        MaxUsesPerTurn = 2, // équilibrage 2 juin : outil de spam du combo, cap 2x/tour
                    };
                    return true;

                // Réplique Fantôme (3.7.b.i) : 3 PA, range 4 case vide, pose un leurre DecoyKind.RepliqueFantome
                // (clone visuel identique). Bible "Pose un Leurre sur une case vide a 4 cases".
                // Le filter EmptyTile filtre les cases occupees par combatant ; le handler ajoute des
                // checks supplementaires (pas de leurre deja la, pas d'obstacle) via DecoyHelpers.TrySpawn.
                case SpellId.GhostraRepliqueFantome:
                    def = new SpellDef
                    {
                        PACost = RepliqueFantomePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.EmptyTile,
                        RangeMin = 1,
                        RangeMax = RepliqueFantomeRangeMax,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // refonte 30 mai (brique 2) : cap 1x/tour
                    };
                    return true;

                // Pas dans l'Ombre (3.7.b.ii) : 2 PA, range 5 case vide, teleport self + pivot adj
                // enemies vers Ghostra + optionnel pose leurre Standard case quittee (HGSpend>=1).
                case SpellId.GhostraPasDansLOmbre:
                    def = new SpellDef
                    {
                        PACost = PasDansLOmbrePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.EmptyTile,
                        RangeMin = 1,
                        RangeMax = PasDansLOmbreRangeMax,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0, // fix 5 juin : leurre case quittée posé INCONDITIONNELLEMENT (la conso auto confondait l'option avec la jauge de leurres Ghostra)
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // refonte 30 mai (brique 2) : cap 1x/tour
                        CooldownTurns = 2,  // equilibrage 5 juin : relance (1 cast tous les 2 rounds)
                    };
                    return true;

                // Permutation (3.7.b refonte 30 mai, ex-Volte-Face slot 90) : 1 PA, cap 2x/tour,
                //   swap Ghostra<->un de ses leurres cible (Filter TileWithLure = case d'un leurre
                //   OWN). Des 1 leurre actif. Aucun degat, pur repositionnement.
                case SpellId.GhostraPermutation:
                    def = new SpellDef
                    {
                        PACost = PermutationPACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.TileWithLure,
                        RangeMin = 1,
                        RangeMax = PermutationRangeMax,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = PermutationMaxUsesPerTurn,
                    };
                    return true;

                // Marque de l'Ombre (3.7.b.v) : 2 PA, range 4 ENEMY. Aucun damage. Apply status 2 rounds.
                // Hook bonus +20 dmg sorts Ghostra + Hook PlaieOuverte auto dorsal (bypass Angle 2+).
                case SpellId.GhostraMarqueDeLOmbre:
                    def = new SpellDef
                    {
                        PACost = MarqueDeLOmbrePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = MarqueDeLOmbreRangeMax,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0, // pas de damage direct
                        MaxUsesPerTurn = 1, // refonte 30 mai (brique 2) : cap 1x/tour
                    };
                    return true;

                // Dague Lancee (3.7.b.iv) : 1 PA, range 5 ENEMY. 80 dmg + force target.Facing vers caster
                // + flag LastFacingForcedOnTurn (combo Dague -> Frappe Fantome = PlaieOuverte).
                // Éveil Spectral (3.7.b refonte 30 mai, ex-Dague Lancée slot 93) : un leurre adjacent
                //   à la cible la poignarde. Base 100 + dorsal/Plaie depuis le LEURRE (SpellSystem).
                case SpellId.GhostraEveilSpectral:
                    def = new SpellDef
                    {
                        PACost = EveilSpectralePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = EveilSpectraleRangeMax,
                        DamageAmount = EveilSpectraleDamage, // base ; dorsal calculé depuis le leurre (override SpellSystem)
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                        MaxUsesPerTurn = EveilSpectraleMaxUsesPerTurn,
                    };
                    return true;

                // Frappe Fantome (3.7.a.iii) : 4 PA, range 4 ENEMY. Teleport adj + 200 dmg + dorsal.
                // PlaieOuverte si target.LastFacingForcedOnTurn == currentTurn.
                case SpellId.GhostraFrappeFantome:
                    def = new SpellDef
                    {
                        PACost = FrappeFantomePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = FrappeFantomeRangeMax,
                        DamageAmount = FrappeFantomeDmgBase, // bonus dorsal en pipeline
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Saigne-Ame (3.7.a.ii) : 4 PA, range 2 ENEMY. 200 base + 70 si PlaieOuverte
                // (consume sur survivant) + bonus dorsal generique. Heal +60 si kill.
                case SpellId.GhostraSaigneAme:
                    def = new SpellDef
                    {
                        PACost = SaigneAmePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = SaigneAmeRangeMax,
                        DamageAmount = SaigneAmeDmgBase, // +70 PlaieOuverte + dorsal modifs en handler
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Nuée Spectrale (3.7.a refonte 30 mai, ex-Danse des Lames slot 95) : CIBLE UNIQUE,
                //   4 PA, range 2 ENEMY, cap 1x/tour. Dégâts scalés sur les leurres (100 + 70/leurre
                //   actif + 30/leurre adjacent), calculés dans SpellSystem (effectiveDmg). Pas de
                //   dorsal/Plaie. DamageAmount = base 100 (le scaling s'ajoute dans le pipeline).
                case SpellId.GhostraNueeSpectrale:
                    def = new SpellDef
                    {
                        PACost = NueeSpectralePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = NueeSpectraleRangeMax,
                        DamageAmount = NueeSpectraleBaseDamage,  // base ; +70/leurre +30/adjacent dans SpellSystem
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                        MaxUsesPerTurn = NueeSpectraleMaxUsesPerTurn,
                    };
                    return true;

                // 3.7.c.i — Linceul d'Ombres : bouclier epineux 2 rounds, 3 PA self.
                // Pas de damage direct (IsOffensive=0). Effet pose dans le handler SpellSystem
                // (ShieldActive 130 HP + LinceulDOmbres flag Magnitude=40). Hook riposte 40 dgts
                // direct sur attaquant melee dans damage loop standard + path custom Charge
                // Brutale (Bible-conforme pipeline reduction% + shield attaquant).
                case SpellId.GhostraLinceulDOmbres:
                    def = new SpellDef
                    {
                        PACost = LinceulDOmbresPACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // refonte 30 mai (brique 2) : 1x actif/tour
                    };
                    return true;

                // 3.7.c.ii — Voile Spectral (REWORK 30 mai) : setup, 2 PA range 4 ENEMY, cap 1x/tour.
                //   TP tous les leurres actifs autour de l'ennemi ciblé (handler SpellSystem). Pas de
                //   damage (IsOffensive=0). Plus de cleanse/DotImmune. Gate >=1 leurre dans SpellSystem.
                case SpellId.GhostraVoileSpectral:
                    def = new SpellDef
                    {
                        PACost = VoileSpectralPACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = VoileSpectralRangeMax,
                        DamageAmount = 0,                    // calculé dynamiquement : 60/leurre adjacent (effectiveDmg)
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,                     // #21 (5 juin) : offensif -> entre dans le damage loop (sinon aucun dégât)
                        MaxUsesPerTurn = VoileSpectraleMaxUsesPerTurn,
                    };
                    return true;

                // 3.7.c.iii — Réplique Protectrice : leurre tank 3 PA range 3 case vide. Pas de
                // damage direct (IsOffensive=0). Effet pose dans le handler SpellSystem
                // (DecoyHelpers.TrySpawn DecoyKind.Protective). Hook redirection 40% dans damage
                // loop standard SUR la Ghostra (apres reduction%, avant shield) + path custom
                // Charge Brutale. Heal +60 HP Ghostra a destruction du decoy via
                // DecoyHelpers.DestroyByEnemyAction.
                case SpellId.GhostraRepliqueProtectrice:
                    def = new SpellDef
                    {
                        PACost = RepliqueProtectricePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.EmptyTile,
                        RangeMin = 1,
                        RangeMax = RepliqueProtectriceRangeMax,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = 1, // refonte 30 mai (brique 2) : cap 1x/tour
                    };
                    return true;

                // 3.7.c.iv — Dernier Pas : panic-button mobile 4 PA self, gate HP<30%, 1x/match.
                // EmptyTile filter range 5 (teleport sur case vide). Pas de damage direct
                // (IsOffensive=0). Effet pose dans le handler SpellSystem (heal +200 + teleport +
                // pose leurre Standard case quittee). Cap 3 gere par destroy oldest + spawn nouveau.
                case SpellId.GhostraDernierPas:
                    def = new SpellDef
                    {
                        PACost = DernierPasPACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.EmptyTile,
                        RangeMin = 1,
                        RangeMax = DernierPasRangeMax,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitGhostraDernierPas,
                        IsOffensive = 0,
                    };
                    return true;

                // 3.7.c — Communion Spectrale (refonte 30 mai, ex-Pas de l'Au-Dela slot 100) : 2 PA
                //   Self, cap 1x/tour. Consomme 1 leurre -> heal 150 (handler SpellSystem). Pas de
                //   damage (IsOffensive=0). Gate >=1 leurre dans SpellSystem (reject pre-PA).
                case SpellId.GhostraCommunionSpectrale:
                    def = new SpellDef
                    {
                        PACost = CommunionPACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                        MaxUsesPerTurn = CommunionMaxUsesPerTurn,
                    };
                    return true;

                // 3.7.d — Execution Spectrale (Bible V7.1 ligne 1071) SIGNATURE Ghostra :
                //   3 PA, range 1 melee, Filter=Enemy. DamageAmount=0 (custom path : tout dans
                //   le handler pour gerer dorsal/non-dorsal + consume leurres + cooldown +
                //   plaie + kill bonus). IsOffensive=1 pour trigger les hooks offensifs standards
                //   (Pacte de Sang multiplier, etc. - bien que le custom path n'utilise pas le
                //   pipeline damage, on garde le flag IsOffensive pour la category Bible).
                case SpellId.GhostraExecutionSpectrale:
                    def = new SpellDef
                    {
                        PACost = ExecutionSpectralePACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 1,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                case SpellId.NecramVirusFatal:
                    def = new SpellDef
                    {
                        PACost = VirusFatalPACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = VirusFatalRangeMax,
                        DamageAmount = 0, // custom calc dans handler
                        HGCostMandatory = (byte)VirusFatalPTCost, // 6 PT (consomme toute la jauge)
                        HGCostMaxOptional = 0,
                        IsOffensive = 1,
                    };
                    return true;

                default:
                    def = default;
                    return false;
            }
        }
    }

    /// <summary>
    /// Helpers de stats EFFECTIVES, qui prennent en compte les statuses actifs et le passif.
    /// Point d'extension central pour les modificateurs.
    ///
    /// 2.10.a : Rage Insatiable Active +1 PA cost.
    /// 2.11 : Passif Appel du Sang -1 PA si cible visee <70% HP (caster Soulrender uniquement).
    /// </summary>
    public static unsafe class EffectiveStats
    {
        /// <summary>
        /// Cout PA effectif pour le caster sur ce sort.
        ///
        /// <paramref name="targetHPRatio"/> : ratio HP de la cible visee (HP * 100 / MaxHP),
        /// ou 100 si pas de cible ennemie (sorts self/AnyTile sans ennemi). Sert au passif
        /// Appel du Sang : -1 PA si caster Soulrender et target < 70% HP. Min 1 PA.
        /// </summary>
        public static int GetPACost(in SpellDef def, Combatant* caster, int targetHPRatio, int currentTurn)
        {
            int cost = def.PACost;
            // 3.7.b — Permutation (seul sort avec Filter TileWithLure) GRATUITE à l'ANGLE 3 :
            //   si la Ghostra a 3 leurres actifs, son coût tombe à 0 PA (perk passif Angle Mort).
            //   Sinon coût de base (1 PA). Court-circuite les autres modificateurs.
            if (def.Filter == TargetingFilter.TileWithLure
                && caster->Class == NymoraClass.Ghostra
                && DecoyHelpers.CountActive(caster) >= DecoyHelpers.MaxDecoys)
            {
                return 0;
            }
            // Refonte 29 mai : Frenesie (ex-Rage Insatiable) ne fait PLUS +1 PA cost (retire).
            // Passif Appel du Sang (refonte 29 mai) : -1 PA si caster Soulrender ET cible <70% HP
            //   ET c'est le 1ER SORT DU TOUR (avant ce cast, 0 sort journalise ce round). Min 1 PA.
            if (caster->Class == NymoraClass.Soulrender
                && targetHPRatio < SpellRegistry.AppelDuSangPalierMarquage
                && SpellLimitsHelper.TotalCastsThisTurn(caster, currentTurn) == 0)
            {
                cost -= 1;
                if (cost < 1) cost = 1;
            }
            // 3.3.d Effondrement : buff actif -> sorts coutent -1 PA (Bible V7.1).
            if (StatusHelper.Has(caster, StatusKind.EffondrementActive))
            {
                cost -= 1;
                if (cost < 1) cost = 1;
            }
            // Provocation (patch 7 juin) : tant que provoque, TOUS les sorts coutent +1 PA (plus
            //   d'exception "sort ciblant le provocateur"). Centralise ici -> badge + grise + sim coherents.
            if (StatusHelper.Has(caster, StatusKind.Provoked))
            {
                cost += SpellRegistry.ProvocationCostBump;
            }
            return cost;
        }

        /// <summary>
        /// Calcule le ratio HP de la cible ennemie a (targetX, targetY), ou 100 si pas d'ennemi.
        /// Utilise par GetPACost pour le passif Appel du Sang.
        /// </summary>
        public static int ResolveTargetHPRatio(Frame f, int targetX, int targetY, int casterPlayerIndex)
        {
            EntityRef occ = GridHelpers.GetOccupant(f, targetX, targetY);
            if (occ == EntityRef.None) return 100;
            if (!f.Unsafe.TryGetPointer<Combatant>(occ, out Combatant* c)) return 100;
            if (c->PlayerIndex == casterPlayerIndex) return 100; // pas un ennemi
            if (c->MaxHP <= 0) return 100;
            return c->HP * 100 / c->MaxHP;
        }
    }
}
