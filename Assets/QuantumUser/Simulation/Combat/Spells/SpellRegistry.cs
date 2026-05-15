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

        // Constantes Bible V7.1 partagees par plusieurs sorts / systemes.
        public const int PeauDeFerShieldHP            = 200;
        public const int PeauDeFerShieldTurns         = 2;
        public const int PeauDeFerMeleeDmgBonus       = 30;
        public const int MarqueDeCarnageTurns         = 3;
        public const int SeveViveHealBase             = 100;
        public const int SeveViveHealBonusHG          = 60;  // +60 si 1 HG depense -> 160
        public const int SeveViveHealBonusBleed       = 50;  // +50 si BleedDoT actif
        public const int DernierSouffleHealAmount     = 200;
        public const int DernierSouffleHGGain         = 3;
        public const int DernierSouffleHPThresholdPct = 30;  // utilisable uniquement a < 30% HP

        // 2.10.c constants
        public const int ChargeBrutaleRange           = 5;
        public const int ChargeBrutaleDamage          = 180;
        public const int VapeurCarminTurns            = 1;
        public const int DetonationBaseDamage         = 60;
        public const int DetonationDamagePerHG        = 40;
        public const int SangCoaguleTurns             = 2;
        public const int CureeDamage                  = 150;
        public const int CureeBonusPANextTurn         = 4;
        public const int CureeMissSelfDamage          = 60;
        public const int CauterisationHealMin         = 60;   // toujours applique (min)
        public const int CauterisationHealPerDoT      = 60;   // chaque DoT retire ajoute 60
        public const int CauterisationHealMax         = 180;  // cap 3 DoTs retires
        public const int TrancheAmeKillRecul          = 2;    // 2 cases de recul si kill

        // 2.11 — Signature Ame Laceree + Passif Appel du Sang.
        public const int AmeLaceeDamage               = 320;  // dgts de base
        public const int AmeLaceeHealPercentOfPassed  = 50;   // heal caster = X% des dgts qui passent (post-shield)
        public const int AmeLaceeCooldownTurns        = 4;    // 4 tours de cooldown apres usage
        public const int AppelDuSangPalierMarquage    = 70;   // cible <70% HP -> -1 PA cost
        public const int AppelDuSangPalierRageOuverte = 40;   // cible <40% HP -> +1 PM Soulrender + 50% shield bypass melee
        public const int AppelDuSangPalierLeCri       = 20;   // cible <20% HP -> Sang Coagule croix 5 autour caster
        public const int AppelDuSangShieldBypassPct   = 50;   // 50% des dgts mêlée ignorent le shield si target <40%

        // 2.15.a — Nightseer (constantes Bible V7.1).
        public const int TirPrecisDmg                 = 200;  // dgts base
        public const int TirPrecisDmgIfTraque         = 280;  // dgts si target porte MarkKind.Traque (Bible : 280)
        public const int VoleeDEpinesDmg              = 130;  // dgts par cible touchee dans la ligne
        // Bible V7.1 (amendee) : Volee d'Epines pose le MEME Filet que le sort Filet de Ronces
        // (TrapKind.FiletRonces : 100 dgts / -2 PM / Empreinte 2 tours). Pas de constantes light dediees.
        public const int DetonationOniriqueDmg        = 170;  // dgts AoE 2x2 base
        public const int DetonationOniriqueDmgVoile   = 80;   // +80 dgts dans cases voilees + dechire le voile
        public const int DetonationOniriqueRangeMaxBase    = 5;   // portee de base (Bible V7.1)
        public const int DetonationOniriqueRangeMaxBoosted = 10;  // portee si option 2 PR (Bible : "x2 -> 10")
        public const int DetonationOniriquePROptionCost    = 2;   // 2 PR (optionnel) pour bonus portee
        public const int FrappeDeLOmbreDmg            = 200;  // dgts base
        public const int FrappeDeLOmbreDmgIfMoved     = 300;  // 200 + 100 si target.PM < target.MaxPM/2 (deja deplacee)
        public const int FrappeDeLOmbreEmpreinteTurns = 2;    // duree Empreinte si bonus declenche
        public const int SalveMortelleDmgCenter       = 220;  // centre de la croix
        public const int SalveMortelleDmgSide         = 130;  // 4 cases cardinales
        public const int SalveMortelleDmgIfTraque     = 60;   // +60 sur cibles Traque
        public const int SalveMortelleDmgIfVoile      = 50;   // +50 dans cases Voilees + dechire
        public const int SalveMortelleHGCost          = 3;    // 3 PR mandatory (ressource Nightseer)

        // 2.15.b — Nightseer Tactiques + passif L'Œil qui n'est pas.
        public const int MarqueDuChasseurTurns        = 3;    // duree Traque applique
        public const int FiletDeRoncesDmg             = 100;  // dgts au declenchement (ennemi entre)
        public const int FiletDeRoncesPMReduce        = 2;    // -2 PM apres declenchement
        public const int FiletDeRoncesEmpreinteTurns  = 2;    // duree Empreinte apres declenchement
        public const int ChampDeMinesDmg              = 70;   // dgts par mine declenchee
        public const int ChampDeMinesEmpreinteTurns   = 2;
        public const int BourrasquePushBase           = 3;    // push 3 cases loin du caster
        public const int BourrasquePushBonus1PR       = 5;    // push 5 cases avec 1 PR depense
        public const int SouffleGlacialDmg            = 70;   // dgts AoE croix 3 autour caster
        public const int SouffleGlacialPushDistance   = 1;    // push 1 case loin du caster
        public const int SouffleGlacialPMReduce       = 1;    // MovementMalus -1 (1 tour)
        public const int OeilQuiNestPasShieldPiercePct = 30;  // sorts Nightseer sur Traque ignorent 30% boucliers

        // 2.15.c — Nightseer Survie.
        public const int VoileDOmbreTurns             = 1;    // Untargetable 1 round actif (skip-decrement convention)
        public const int PasFurtifRangeMax            = 4;    // teleport jusqu'a 4 cases (Manhattan)
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
        public const int TraquenardParalysiePMMalus   = 3;    // -3 PM au prochain tour cible
        public const int TraquenardParalysieAPMalus   = 2;    // -2 PA au prochain tour cible
        public const int TraquenardParalysieTurns     = 1;    // 1 tour actif (skip-decrement convention)
        public const int TraquenardCooldownTurns      = 4;    // 4 tours apres usage (re-castable si PR remonte a 4)
        public const int TraquenardPRCost             = 4;    // 4/4 PR (consomme toute la jauge)
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

        // 3.3.b.iii — Ancrage Bible-correct (Colossar TACTIQUE anti-mobilite) — refacto rétroactif.
        // Bible : 2 PA, range 4, ENEMY. Cible perd 2 PM pendant 2 tours ET ne peut pas etre deplacee
        // (push/pull/teleport) pendant 1 tour. Pas de damage. NE PAS confondre avec self-buff initial.
        public const int AncrageRangeMax              = 4;
        public const int AncrageMovementMalusMag      = 2;    // -2 PM
        public const int AncrageMovementMalusTurns    = 2;
        public const int AncrageImmuneTurns           = 1;    // immune push/pull/teleport 1 tour

        // 3.3.b.iii — Provocation Bible-correct (Colossar) — refacto rétroactif.
        // Bible : 2 PA, range 5, 1 tour. Sorts non-ciblant le caster coutent +2 PA pour la cible.
        // -1 PM. Si la cible n'est pas adjacente au caster en fin de SON tour : 100 dgts auto.
        public const int ProvocationRangeMax          = 5;
        public const int ProvocationTurns             = 1;    // 1 tour Bible (skip-decrement convention 1 round actif)
        public const int ProvocationMovementMalusMag  = 1;    // -1 PM
        public const int ProvocationMovementMalusTurns = 1;
        public const int ProvocationCostBumpNonCible  = 2;    // +2 PA pour les sorts non-ciblant le provocateur
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
        public const int StoicismeImmuneTurns         = 2;    // immune push/pull/tp pendant toute la duree
        public const int StoicismeHealIfSurvived      = 80;   // heal si shield Magnitude > 0 a expiration

        // Garde Protectrice : 2 PA self, -30% dmg subis / 2 tours. Cap combine Densite Inerte 50%.
        public const int GardeProtectricePercent      = 30;   // % reduction
        public const int GardeProtectriceTurns        = 2;
        public const int MaxCombinedDamageReductionPct = 50;  // Bible : "cap -50% total" combine Densite Inerte + Garde Prot

        // Ressac Vital : 2 PA self, heal 80 + 30/hit subi tour precedent (max +120 HP = 4 hits).
        public const int RessacVitalHealBase          = 80;
        public const int RessacVitalHealPerHit        = 30;
        public const int RessacVitalHealMaxBonus      = 120;  // cap = 4 hits * 30
        public const int RessacVitalHitsCap           = 4;    // max hits comptes (cap +120)

        // Renvoi du Bouclier : 3 PA self, RipostAll 60 dgts (melee + distance) / 1 tour / cap 4 retours.
        public const int RenvoiBouclierReflectDmg     = 60;
        public const int RenvoiBouclierTurns          = 1;
        public const int RenvoiBouclierMaxTriggers    = 4;    // cap reflects (reuse RepresaillesReflectsLeft)

        // Soin Lourd : 3 PA range 3 (self/allie). MVP 1v1 : Filter=Self range 0, heal 150 HP.
        public const int SoinLourdHeal                = 150;
        public const int SoinLourdRangeMax            = 3;    // Bible (gardee pour la transition 2v2/3v3)

        // 3.3.d — Effondrement (signature Colossar, Bible V7.1).
        public const int EffondrementPACost           = 4;
        public const int EffondrementFDCost           = 3;    // consomme TOUTE la jauge FD (cap 3)
        public const int EffondrementAoeRadius        = 2;    // rayon 2 autour caster (Chebyshev/Manhattan ?)
        public const int EffondrementDamage           = 200;  // dgts ennemis dans la zone au trigger
        public const int EffondrementFailleTurns      = 2;    // duree des failles (cases impraticables)
        public const int EffondrementBuffTurns        = 2;    // duree du buff +1PM / -1PA / -30% dgts
        public const int EffondrementDmgReductionPct  = 30;   // % reduction (combine avec Densite Inerte/Garde Prot, cap 50%)
        public const int EffondrementCooldownTurns    = 4;    // 4 tours apres usage (re-castable si FD remonte a 3)
        public const int EffondrementFailleHP         = 100;   // Bible-balance : Faille destructible par AoE adverse (Lorenzo design 3.3.d)

        // 3.5.a.i — Necram Offensifs base (Bible V7.1).
        // Crachat Acide : sort de base "80% utilisation early-game". Combine dgts directs + setup marques.
        public const int CrachatAcideDmg              = 90;    // dgts directs
        public const int CrachatAcideMarksApplied     = 2;     // applique 2 marques venin (cap 4/cible)
        public const int CrachatAcideRangeMax         = 4;     // portee Manhattan
        public const int CrachatAcidePACost           = 3;

        // Morsure Putride : finisher melee qui scale avec marques + transfere les marques au kill.
        // Bible : 110 dgts + 22/marque (max +90 = 200 total max). Si target meurt -> marques sur ennemi le plus proche.
        public const int MorsurePutrideDmgBase        = 110;   // dgts base
        public const int MorsurePutrideDmgPerMark     = 22;    // bonus dgts par marque venin sur la cible
        public const int MorsurePutrideDmgBonusCap    = 90;    // cap bonus marques (= 22 * 4 = 88, arrondi 90 Bible)
        public const int MorsurePutridePACost         = 4;

        // 3.5.a.ii — Necram Offensifs burst/AoE (Bible V7.1).
        // Detonation Virulente : 80 dgts immediats + consomme TOUTES les marques (50/marque). 4 marques = 280.
        public const int DetonationVirulenteDmgBase   = 80;    // dgts immediats
        public const int DetonationVirulenteDmgPerMark = 50;   // dgts par marque consommee
        public const int DetonationVirulenteRangeMax  = 4;     // portee Manhattan
        public const int DetonationVirulentePACost    = 4;

        // Faux Decharnee : 130 dgts AoE Square3x3 autour caster + heal Necram 30/marque cumule (cap +120 = 4 marques).
        public const int FauxDecharneeDmg             = 130;   // dgts par cible touchee
        public const int FauxDecharneeHealPerMark     = 30;    // heal Necram par marque active sur cibles touchees
        public const int FauxDecharneeHealCap         = 120;   // cap heal total (= 4 marques * 30)
        public const int FauxDecharneePACost          = 4;

        // 3.5.a.iii — Brume Toxique : zone DoT 3x3 / 2 tours (Bible V7.1).
        public const int BrumeToxiqueDmgImmediate     = 60;    // dgts pose sur unite deja dans zone
        public const int BrumeToxiqueDmgOnEnter       = 30;    // dgts unite qui entre dans zone
        public const int BrumeToxiqueMarksOnHit       = 1;     // 1 marque appliquee a chaque trigger (pose / entree / fin de tour)
        public const int BrumeToxiqueRangeMax         = 4;     // portee Manhattan caster -> centre zone
        public const int BrumeToxiqueTurns            = 2;     // duree (skip-decrement)
        public const int BrumeToxiquePACost           = 4;

        // 3.5.b.i — Inoculation : setup pur, 1 PA range 5, 2 marques cap 4 (Bible V7.1).
        public const int InoculationPACost            = 1;
        public const int InoculationRangeMax          = 5;
        public const int InoculationMarksApplied      = 2;

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
        public const int SymbioseMorbideHealPerMarkPerTick = 8;
        public const int SymbioseMorbideMaxMarksForHeal = 4;
        public const int SymbioseMorbideTurns         = 2;

        // 3.5.b.iv — Contagion : propagation AoE marques (Bible V7.1).
        // 3 PA, range 5, target ennemie marquee requise. Copie min(stacks, cap) marques sur
        // autres ennemis rayon 3 Manhattan de la cible. Cap 3 default, 4 avec 2 PT optionnel.
        // En 1v1 (pas d'autres ennemis du caster) : +1 marque sur la cible (boost de tick).
        public const int ContagionPACost              = 3;
        public const int ContagionRangeMax            = 5;
        public const int ContagionPropagationRadius   = 3;     // Manhattan rayon autour de la cible
        public const int ContagionCapDefault          = 3;     // cap marques copiees default
        public const int ContagionCapBoosted          = 4;     // cap avec 2 PT optionnel
        public const int ContagionPTCostForBoost      = 2;     // PT optionnel pour cap boost
        public const int Contagion1v1FallbackMarks    = 1;     // +1 marque sur cible en 1v1

        // 3.5.b.iii — Pas Spectral : mobilite + traversee ennemis (Bible V7.1).
        // 2 PA, self. +2 PM ce tour (cap si refresh meme tour). Apply PasSpectralReady (sub-turn).
        // Tant que actif : MovementSystem passe ignoreEnemyOccupants=true a A* pour les
        // MoveCommand du Necram, et pose +1 marque venin sur chaque ennemi present sur les
        // cases intermediaires du path (destination skip car deja validee libre).
        public const int PasSpectralPACost            = 2;
        public const int PasSpectralPMBonus           = 2;
        public const int PasSpectralMarksPerCrossing  = 1;

        // 3.5.c.i — Voile de Pestilence : aura defensive 2 rounds (Bible V7.1).
        // 3 PA self. Apply PestilenceAura turnsLeft=2 (refresh-only).
        // Hook 1 (TurnSystem.EnterTurnEnd) : fin sub-turn d'un ennemi a Manhattan <=2 d'un
        //   Necram porteur de Voile -> +1 marque venin sur l'ennemi.
        // Hook 2 (SpellSystem damage loop, apres reflect) : si target porte Voile ET le sort
        //   est melee (RangeMax == 1) -> +1 marque venin sur l'attaquant (cap 4 via ApplyMark).
        public const int VoilePestilencePACost            = 3;
        public const int VoilePestilenceTurns             = 2;
        public const int VoilePestilenceAdjacencyRange    = 2;     // Manhattan <= 2 du Necram
        public const int VoilePestilenceMarksOnAdjacency  = 1;
        public const int VoilePestilenceMarksOnMeleeAttacker = 1;

        // 3.5.c.ii — Carapace Visqueuse : bouclier piege 2 rounds (Bible V7.1).
        // 3 PA self. Apply ShieldActive Magnitude=110 HP + status CarapaceVisqueuse flag 2 rounds.
        // Hook (SpellSystem damage loop, apres bloc absorption shield) : si target porte
        // CarapaceVisqueuse ET shield a absorbe >=1 dmg ET sort melee (RangeMax==1)
        // -> +1 marque venin sur l'attaquant (cap 4 via ApplyMark).
        // Trigger meme si shield absorbe tout le dmg (HP_loss=0, "frappe le bouclier" Bible).
        // Pas de trigger si shield deja brise (shieldBefore=0).
        public const int CarapaceVisqueusePACost              = 3;
        public const int CarapaceVisqueuseShieldHP            = 110;
        public const int CarapaceVisqueuseTurns               = 2;
        public const int CarapaceVisqueuseMarksOnMeleeAttacker = 1;

        // 3.5.c.iii — Drain Vital : heal offensif a distance (Bible V7.1).
        // 3 PA range 4, 60 dgts cible. Caster Necram heal HealBase (30) ou HealBonus (60)
        // si target.VeninStacks >= MarksThreshold (3) au moment du cast (snapshot post-damage).
        // Marques cible NON consommees. Heal applique meme si target meurt sur les 60 dmg.
        // Cap MaxHP standard. Pas de status applique.
        public const int DrainVitalPACost          = 3;
        public const int DrainVitalRangeMax        = 4;
        public const int DrainVitalDamage          = 60;
        public const int DrainVitalHealBase        = 30;
        public const int DrainVitalHealBonus       = 60;
        public const int DrainVitalMarksThreshold  = 3;

        // 3.5.c.iv — Pulse Sanguin Vert : heal de zone (Bible V7.1).
        // 3 PA self. Heal Necram caster : HealBase (70) + min(sumVeninStacksEnemiesInRadius, capMarks) * HealPerMark.
        // PulseSanguinVertHealCap = 90 HP de bonus (= 6 marques * 15). +30 HP additionnel si hgSpend>=1 (1 PT).
        // Itere tous ennemis vivants Manhattan <= MarksRange (4) du caster.
        // Marques NON consommees. Cap MaxHP standard. Pas de dmg.
        public const int PulseSanguinVertPACost          = 3;
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
                        RangeMax = 1,
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
                    };
                    return true;

                // Pacte de Sang (2.10.a) : 1 PA, self, -80 HP self + +3 HG + BuffNextOffensiveDmgPercent.
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
                    };
                    return true;

                // Empoignade (2.10.b) : 3 PA, range 3, pull cible adjacent + AntiTeleport 1 tour.
                // Pas de dgts. Si pas de case adjacente libre : no-op silencieux (rare).
                case SpellId.SoulrenderEmpoignade:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 3,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
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
                        PACost = 4,
                        Shape = TargetingShape.SingleTile, // on gere la ligne nous-memes
                        Filter = TargetingFilter.AnyTile,  // peut viser une case vide ou ennemi
                        RangeMin = 1,
                        RangeMax = ChargeBrutaleRange,
                        DamageAmount = 0, // applique manuellement dans le branche specifique
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
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
                case SpellId.SoulrenderCuree:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 2,
                        DamageAmount = CureeDamage,
                        HGCostMandatory = 2,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
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
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 6,
                        DamageAmount = TirPrecisDmg,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Volee d'Epines (2.15.a) : 4 PA, ligne range 5, 130 dgts par cible.
                // Pose un Filet de Ronces (TrapKind) sur la DERNIERE case touchee.
                case SpellId.NightseerVoleeDEpines:
                    def = new SpellDef
                    {
                        PACost = 4,
                        Shape = TargetingShape.Line,
                        Filter = TargetingFilter.AnyTile,
                        RangeMin = 1,
                        RangeMax = 5,
                        DamageAmount = VoleeDEpinesDmg,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Detonation Onirique (2.15.a + bonus 2 PR Bible V7.1) : 4 PA, range 5 (10 avec 2 PR),
                // AoE 2x2, 170 dgts. Si case Voilee dans la zone : +80 dgts ET dechire le voile.
                // Option 2 PR : portee passe de 5 a 10 (cf override dans SpellSystem range check).
                case SpellId.NightseerDetonationOnirique:
                    def = new SpellDef
                    {
                        PACost = 4,
                        Shape = TargetingShape.SingleTile, // AoE 2x2 hardcoded dans SpellSystem
                        Filter = TargetingFilter.AnyTile,
                        RangeMin = 1,
                        RangeMax = DetonationOniriqueRangeMaxBase, // 5 ; override a 10 si hgSpend >= 2 (handler)
                        DamageAmount = DetonationOniriqueDmg,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = (byte)DetonationOniriquePROptionCost, // 2 PR optionnel = bonus portee
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

                // Salve Mortelle (2.15.a) : 5 PA, range 6, AoE croix 5, 3 PR mandatory.
                // 220 dgts au centre + 130 sur les 4 cardinales. +60 sur cibles Traque.
                // Cases Voilees dans la zone : +50 dgts dans la case + dechirees.
                case SpellId.NightseerSalveMortelle:
                    def = new SpellDef
                    {
                        PACost = 5,
                        Shape = TargetingShape.CrossSmall, // 5 cases (centre + 4 cardinales)
                        Filter = TargetingFilter.AnyTile,
                        RangeMin = 1,
                        RangeMax = 6,
                        DamageAmount = 0, // calcule per-cell dans damage loop (220 centre / 130 cotes)
                        HGCostMandatory = (byte)SalveMortelleHGCost,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // -------------------------------------------------------------
                // NIGHTSEER 2.15.b — TACTIQUES
                // -------------------------------------------------------------

                // Marque du Chasseur (2.15.b) : 1 PA, range 5, applique Traque 3 tours sur cible.
                // Pas de damage. Sort de setup.
                case SpellId.NightseerMarqueDuChasseur:
                    def = new SpellDef
                    {
                        PACost = 1,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 5,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
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
                    };
                    return true;

                // Bourrasque (2.15.b) : 3 PA, range 5, push cible 3 cases loin du caster.
                // Option : 1 PR depense -> push 5 cases au lieu de 3.
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
                        HGCostMaxOptional = 1,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // Souffle Glacial (2.15.b) : 3 PA, AoE croix 3 autour caster, 70 dgts + push 1 + MovementMalus -1.
                // Sort defensif anti-melee. Ciblage = case du caster (RangeMin/Max = 0, Filter = Self).
                case SpellId.NightseerSouffleGlacial:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.CrossSmall, // 5 cases mais on filtrera le centre (caster) dans damage loop
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = SouffleGlacialDmg,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1, // damage > 0 -> entre dans damage loop
                    };
                    return true;

                // -------------------------------------------------------------
                // NIGHTSEER 2.15.c — SURVIE
                // -------------------------------------------------------------

                // Voile d'Ombre : 3 PA, self, applique Untargetable 1 round (skip-decrement convention).
                case SpellId.NightseerVoileDOmbre:
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

                // Pas Furtif : 2 PA, range 0-4, teleport sur case vide (filter EmptyTile).
                // Option : 1 PR -> case d'arrivee Voilee 2 tours (HGCostMaxOptional = 1).
                case SpellId.NightseerPasFurtif:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.EmptyTile,
                        RangeMin = 1,
                        RangeMax = PasFurtifRangeMax,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 1,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
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
                    };
                    return true;

                // Mur de Pierre (3.3.b.iii Bible-correct) : 4 PA, range 4, ligne 3 cases (perpendiculaire
                // axe caster->cible) 150 HP / 2 tours. Optionnel : 1 FD -> 5 segments au lieu de 3.
                // +1 FD par segment pose via SpawnObstacle hook.
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

                // Soin Lourd : 3 PA self (MVP 1v1), heal 150 HP. Bible : range 3 self/allie en 2v2/3v3.
                // Pour MVP, Filter=Self range 0 (Bible "en 1v1 : self-only"). TODO Phase 6 : Filter=Ally.
                case SpellId.ColossarSoinLourd:
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

                // -------------------------------------------------------------
                // COLOSSAR — Bible V7.1 (3.3.d) — SIGNATURE
                // -------------------------------------------------------------

                // Effondrement : 4 PA, self, 3 FD mandatory (consomme tout). Cast pose juste l'annonce.
                // Le trigger (damage AoE rayon 2 + Failles + buff) se fait au prochain sub-turn du caster
                // via TurnSystem.EnterTurnStart en lisant EffondrementAnnouncedOnTurn.
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
                        DamageAmount = DetonationVirulenteDmgBase, // 80 base, +50/marque modife en handler
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
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
                        DamageAmount = BrumeToxiqueDmgImmediate, // 60 dgts par cible deja dans la zone
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // 3.5.b.i — Inoculation : setup pur. 1 PA, range 5, applique 2 marques venin sur
                // une cible ennemie (cap 4 gere par VeninHelpers.ApplyMark). Pas de damage. Handler
                // custom dans SpellSystem pour le ApplyMark (IsOffensive=0 -> skip damage loop).
                case SpellId.NecramInoculation:
                    def = new SpellDef
                    {
                        PACost = InoculationPACost,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = InoculationRangeMax,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
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
                        HGCostMaxOptional = ContagionPTCostForBoost,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // 3.5.b.iii — Pas Spectral : mobilite + traversee ennemis. 2 PA self. Pas de
                // damage direct (IsOffensive=0). Effet gere dans le handler SpellSystem (PM +2
                // + Apply PasSpectralReady) puis dans MovementSystem (A* traverse + marques).
                case SpellId.NecramPasSpectral:
                    def = new SpellDef
                    {
                        PACost = PasSpectralPACost,
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
        public static int GetPACost(in SpellDef def, Combatant* caster, int targetHPRatio)
        {
            int cost = def.PACost;
            if (StatusHelper.Has(caster, StatusKind.RageInsatiableActive))
            {
                cost += 1; // Rage Insatiable : sorts coutent +1 PA pendant 2 tours
            }
            // Passif Appel du Sang : caster Soulrender + cible <70% HP -> -1 PA (min 1).
            if (caster->Class == NymoraClass.Soulrender
                && targetHPRatio < SpellRegistry.AppelDuSangPalierMarquage)
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
