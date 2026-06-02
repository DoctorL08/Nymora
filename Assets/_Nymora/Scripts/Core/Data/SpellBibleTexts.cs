using System.Collections.Generic;
using Nymora.Core.Enums;

namespace Nymora.Core.Data
{
    /// <summary>
    /// Source-of-truth UI Bible V7.1 pour les 80 sorts (5 classes x 16 sorts).
    /// Centralise mapping (SpellIdValue Quantum int -> SpellIdTech snake_case) + classe +
    /// categorie + DisplayName Bible-friendly + Description (champ EFFET Bible) + LoreFlavor
    /// (champ PRESSION Bible).
    ///
    /// Consume par :
    ///   - Editor : <see cref="Nymora.Editor.Tools.PopulateSpellCatalog"/> pour populater
    ///     l'asset SpellCatalog.asset (Deck Builder).
    ///   - Runtime Combat HUD : <c>SpellDescriptions.Get</c> + <c>SpellDisplayInfo.GetDisplayName</c>
    ///     pour afficher tooltip + titre au survol de l'icone de sort.
    ///
    /// Avant 5.4 (18 mai 2026), descriptions+display names vivaient en double : un dict local
    /// dans PopulateSpellCatalog (80 entries) et un switch hardcode dans SpellDescriptions
    /// (16 entries Soulrender only) ce qui causait "(Description Bible non disponible)" pour
    /// les 4 autres classes en combat. Source unique factorisee ici pour eviter la divergence.
    ///
    /// Asmdef : place dans Nymora.Core (aucune dependance Quantum / Unity) -> reutilisable
    /// par Combat (qui ref Core+Quantum) et Editor (qui ref tout). Le SpellIdValue est l'int
    /// Quantum.SpellId (mappable via cast <c>(int)spellId</c>), donc on n'importe pas Quantum
    /// ici (Core est volontairement noEngineReferences-friendly).
    ///
    /// Pour modifier une description : editer l'<see cref="Entries"/> ci-dessous, puis re-run
    /// "Nymora > Setup > Populate Spell Catalog" pour propager dans SpellCatalog.asset
    /// (le combat HUD lit deja en direct depuis ce fichier, pas besoin de regen).
    /// </summary>
    public static class SpellBibleTexts
    {
        public struct Entry
        {
            public int SpellIdValue;        // = (int)Quantum.SpellId, ex 10 = SoulrenderTrancheAme
            public string SpellIdTech;      // snake_case stable key, ex "soulrender_tranche_ame"
            public NymoraClass ClassId;
            public SpellCategory Category;
            public string DisplayName;      // ex "Tranche-Ame" (Bible-friendly)
            public string Description;      // champ EFFET Bible (mecanique pure)
            public string LoreFlavor;       // champ PRESSION Bible (narratif)
        }

        // ------------------------------------------------------------------
        // 80 entries Bible V7.1 patchee 17 mai 2026 (amendements 16 mai integres :
        // Volte-Face 80 / Dague 40 / Replique Protectrice 4PA 30% 80HP 3r / Replique
        // Fantome 4 rounds). Tri par SpellIdValue numerique pour facilite lecture/audit.
        // ------------------------------------------------------------------
        public static readonly IReadOnlyList<Entry> Entries = new List<Entry>
        {
            // ===== SOULRENDER (10-25) : 5 offensifs / 5 tactiques / 5 survie / signature =====
            new Entry { SpellIdValue = 10, SpellIdTech = "soulrender_tranche_ame",          ClassId = NymoraClass.Soulrender, Category = SpellCategory.Offensive, DisplayName = "Tranche-Âme",
                Description = "Inflige 220 dégâts. Si le coup tue, le Soulrender RECULE de 2 cases gratuitement (mouvement non-PM). Effet purement de mise en scène, mais bloque les contre-attaques zone post-kill.",
                LoreFlavor  = "Le sort signature de base. Lent (3 PA), prévisible — et c'est ce qui le rend terrifiant. L'adversaire SAIT qu'il arrive. Il ne peut pas l'arrêter." },
            new Entry { SpellIdValue = 11, SpellIdTech = "soulrender_ouvre_plaie",          ClassId = NymoraClass.Soulrender, Category = SpellCategory.Offensive, DisplayName = "Ouvre-Plaie",
                Description = "Inflige 110 dégâts. Si tu as au moins 1 HG (dépensé automatiquement) : 230 dégâts ET les soins et boucliers reçus par la cible sont réduits de moitié (÷2) pendant 1 tour. Cap : 2 fois par tour.",
                LoreFlavor  = "L'anti-sustain. La simple existence de ce sort dans le deck Soulrender suffit à interdire à l'adversaire de poser un Carapace ou Soin Lourd sans préparation." },
            new Entry { SpellIdValue = 12, SpellIdTech = "soulrender_charge_brutale",       ClassId = NymoraClass.Soulrender, Category = SpellCategory.Offensive, DisplayName = "Charge Brutale",
                Description = "Le Soulrender fonce en LIGNE DROITE (portée 4, cardinale) jusqu'à la première unité ou case bloquante. Inflige 180 dégâts à la cible touchée. Toute case foulée pendant la charge devient Vapeur Carmin pendant 1 tour.",
                LoreFlavor  = "Le bélier. Charge Brutale ne fait pas seulement entrer le Soulrender — elle CRÉE un couloir de pression qui restera après son passage." },
            new Entry { SpellIdValue = 13, SpellIdTech = "soulrender_detonation_sanglante", ClassId = NymoraClass.Soulrender, Category = SpellCategory.Offensive, DisplayName = "Détonation Sanglante",
                Description = "Centre AoE croix 3. Inflige 60 dégâts de base à toutes les cibles dans la zone, +40 par HG consommé (2 HG obligatoires + jusqu'à 3 de plus dépensés automatiquement). Avec 5 HG : 260 dégâts. Sang Coagulé créé sous le centre pendant 2 tours. ATTENTION : si 5 HG sont consommés ici, Âme Lacérée est interdite et son cooldown reset.",
                LoreFlavor  = "Le payoff total. Détoner 5 HG est un acte de FOI — le Soulrender renonce à son finisher pour un coup massif." },
            new Entry { SpellIdValue = 14, SpellIdTech = "soulrender_curee",                ClassId = NymoraClass.Soulrender, Category = SpellCategory.Offensive, DisplayName = "Éventration",
                Description = "Inflige 220 dégâts et applique PLAIE OUVERTE : 50 dégâts par tour pendant 3 tours (DoT). Cap : 1 fois par tour.",
                LoreFlavor  = "Le tout ou rien. Curée est une lecture pure : si tu calcules juste, le match s'enchaîne. Si tu calcules mal, tu donnes un tempo entier à l'adversaire." },
            new Entry { SpellIdValue = 15, SpellIdTech = "soulrender_pacte_de_sang",        ClassId = NymoraClass.Soulrender, Category = SpellCategory.Tactical,  DisplayName = "Pacte de Sang",
                Description = "Le Soulrender s'inflige 80 dégâts à lui-même et gagne +3 HG immédiatement. Son prochain sort offensif ce tour gagne +50% de dégâts. UTILISABLE 1 FOIS PAR MATCH.",
                LoreFlavor  = "Le bouton clutch. Quand l'adversaire pense être safe, le Soulrender saigne lui-même pour ouvrir une fenêtre de burst. Décision à très haut risque." },
            new Entry { SpellIdValue = 16, SpellIdTech = "soulrender_marque_de_carnage",    ClassId = NymoraClass.Soulrender, Category = SpellCategory.Tactical,  DisplayName = "Marque de Carnage",
                Description = "Marque la cible 3 tours. Pendant ce temps, tous les sorts du Soulrender sur cette cible génèrent +1 HG bonus. La marque est visible sur le sprite ennemi (croix de sang). Cap : 1 fois par tour.",
                LoreFlavor  = "Le sceau. Marque de Carnage transforme une cible en machine à fabriquer de la ressource. Plus l'adversaire reçoit de coups, plus le Soulrender accélère." },
            new Entry { SpellIdValue = 17, SpellIdTech = "soulrender_empoignade",           ClassId = NymoraClass.Soulrender, Category = SpellCategory.Tactical,  DisplayName = "Empoignade",
                Description = "Inflige 90 dégâts, tire la cible jusqu'à 1 case du Soulrender (corps à corps) et lui retire 2 PM à son prochain tour. Cap : 1 fois par tour.",
                LoreFlavor  = "L'arrachement. Empoignade défait la map des classes-kite. Une Nightseer qui pensait son setup safe se retrouve au corps à corps, son Évanescence verrouillée." },
            new Entry { SpellIdValue = 18, SpellIdTech = "soulrender_rugissement",          ClassId = NymoraClass.Soulrender, Category = SpellCategory.Tactical,  DisplayName = "Rugissement",
                Description = "AoE rayon 3 autour du Soulrender. Toutes les cibles ennemies subissent -1 PM ET ne peuvent pas téléporter au tour suivant. Si une cible est sous 50% HP : -2 PM au lieu de -1. Pas de dégâts. Cap : 1×/tour, relance 2 tours.",
                LoreFlavor  = "Le cri primal. Rugissement ne tue pas — il fige. Combiné à Charge Brutale derrière, c'est un piège géométrique. Anti-Ghostra par excellence." },
            new Entry { SpellIdValue = 19, SpellIdTech = "soulrender_rage_insatiable",      ClassId = NymoraClass.Soulrender, Category = SpellCategory.Tactical,  DisplayName = "Frénésie",
                Description = "Pendant 2 tours, chaque sort OFFENSIF lancé par le Soulrender gagne +10% de dégâts ET génère +1 HG. (Recast = rafraîchit la durée, pas de cumul.)",
                LoreFlavor  = "L'emballement. Frénésie transforme chaque coup en carburant : plus le Soulrender frappe, plus sa jauge monte, plus ça fait mal." },
            new Entry { SpellIdValue = 20, SpellIdTech = "soulrender_riposte_carmin",       ClassId = NymoraClass.Soulrender, Category = SpellCategory.Survival,  DisplayName = "Riposte Carmin",
                Description = "Pendant 1 tour, toute attaque MÊLÉE subie par le Soulrender renvoie 100 dégâts à l'attaquant ET lui coûte 1 PM additionnel pour son prochain mouvement. Le Soulrender prend les dégâts normalement.",
                LoreFlavor  = "Le piège du chasseur. Riposte Carmin n'est pas une défense — c'est une invitation. Elle dit à l'adversaire : 'Viens me frapper.'" },
            new Entry { SpellIdValue = 21, SpellIdTech = "soulrender_cauterisation",        ClassId = NymoraClass.Soulrender, Category = SpellCategory.Survival,  DisplayName = "Sang Bouillant",
                Description = "Pendant 2 tours, chaque fois que le Soulrender SUBIT des dégâts : il gagne +1 HG et sa prochaine frappe inflige +30 dégâts. (Recast = rafraîchit, pas de cumul.)",
                LoreFlavor  = "Le sang qui bout. Plus on le frappe, plus il accumule de rage et de hémoglyphe — chaque coup reçu nourrit le prochain coup rendu." },
            new Entry { SpellIdValue = 22, SpellIdTech = "soulrender_peau_de_fer",          ClassId = NymoraClass.Soulrender, Category = SpellCategory.Survival,  DisplayName = "Peau de Fer",
                Description = "Le Soulrender gagne un BOUCLIER de 200 HP pendant 2 tours. Pendant la durée du bouclier, ses sorts à portée 1 (mêlée) gagnent +30 dégâts. Le bouclier se vide normalement aux dégâts subis. Relance : 2 tours.",
                LoreFlavor  = "Le mur viandard. Peau de Fer ne fait pas que protéger — elle ENCOURAGE l'engagement. Anti-Colossar/Nightseer qui zone à distance." },
            new Entry { SpellIdValue = 23, SpellIdTech = "soulrender_seve_vive",            ClassId = NymoraClass.Soulrender, Category = SpellCategory.Survival,  DisplayName = "Sève Vive",
                Description = "Le Soulrender se soigne de 100 HP, ou 160 si tu as au moins 1 HG (dépensé automatiquement). Si le Soulrender saigne actuellement (DoT actif sur lui) : +50 HP additionnels. Cap : 1 fois par tour.",
                LoreFlavor  = "Le rapide. Sève Vive est le micro-heal qui maintient le Soulrender en vie sans qu'il quitte le combat." },
            new Entry { SpellIdValue = 24, SpellIdTech = "soulrender_dernier_souffle",      ClassId = NymoraClass.Soulrender, Category = SpellCategory.Survival,  DisplayName = "Dernier Souffle",
                Description = "Utilisable uniquement à <30% HP. Le Soulrender se soigne de 200 HP ET gagne 3 HG. UTILISABLE 1 FOIS PAR MATCH.",
                LoreFlavor  = "L'ultime. Dernier Souffle n'est pas un heal — c'est une renaissance. Le Soulrender qui aurait dû mourir au tour 5 revient à 50% HP avec 3 HG en main, prêt pour un Âme Lacérée." },
            new Entry { SpellIdValue = 25, SpellIdTech = "soulrender_ame_laceree",          ClassId = NymoraClass.Soulrender, Category = SpellCategory.Signature, DisplayName = "Âme Lacérée",
                Description = "Inflige 320 dégâts. Le Soulrender se soigne de 50% des dégâts qui ont passé (après bouclier). Si la cible meurt sur ce sort : le combat est marqué d'une explosion de sang qui crée du Sang Coagulé en croix 5 cases.",
                LoreFlavor  = "L'exécution rituelle. Âme Lacérée n'est pas un simple finisher — c'est l'aboutissement d'un cycle. Le Soulrender a saigné, fait saigner, accumulé. Maintenant il récolte." },

            // ===== NIGHTSEER (30-45) =====
            new Entry { SpellIdValue = 30, SpellIdTech = "nightseer_tir_precis",            ClassId = NymoraClass.Nightseer, Category = SpellCategory.Offensive, DisplayName = "Tir Précis",
                Description = "Inflige 150 dégâts. Si la cible est Traqué : 210 dégâts. Cap : 1 fois par tour.",
                LoreFlavor  = "Le sniper. Tir Précis n'a pas besoin de surprendre — sa simple existence à 4 cases force l'adversaire à toujours regarder en l'air." },
            new Entry { SpellIdValue = 31, SpellIdTech = "nightseer_volee_epines",          ClassId = NymoraClass.Nightseer, Category = SpellCategory.Offensive, DisplayName = "Volée d'Épines",
                Description = "Tir en ligne droite. Inflige 130 dégâts à toutes les cibles touchées. Pose un Filet de Ronces (100 dégâts, -1 PM, applique TRAQUÉ au déclenchement) une case DERRIÈRE la dernière cible touchée, dans le sens du tir. Cap : 1 fois par tour.",
                LoreFlavor  = "Le double effet. Volée d'Épines fait des dégâts ET pose un piège. L'adversaire qui survit doit décider : foncer dans le filet ou contourner et perdre du tempo." },
            new Entry { SpellIdValue = 32, SpellIdTech = "nightseer_detonation_onirique",   ClassId = NymoraClass.Nightseer, Category = SpellCategory.Offensive, DisplayName = "Détonation Onirique",
                Description = "AoE 2x2 cases, 170 dégâts. Si la zone couvre un de tes pièges : +80 dégâts. Si tu as au moins 2 PR (dépensés automatiquement) : portée passe de 5 à 10.",
                LoreFlavor  = "L'œil qui frappe à travers le brouillard. Détonation Onirique punit la lecture. Si l'adversaire pensait être hors de portée, il ne l'était pas — le Nightseer voyait à travers." },
            new Entry { SpellIdValue = 33, SpellIdTech = "nightseer_frappe_ombre",          ClassId = NymoraClass.Nightseer, Category = SpellCategory.Offensive, DisplayName = "Frappe de l'Ombre",
                Description = "Inflige 160 dégâts et applique TRAQUÉ. Si TU as dépensé 3 PM au dernier tour : +50 dégâts.",
                LoreFlavor  = "L'archer mobile. Frappe de l'Ombre récompense le repositionnement : bouge à fond, puis frappe plus fort." },
            new Entry { SpellIdValue = 34, SpellIdTech = "nightseer_salve_mortelle",        ClassId = NymoraClass.Nightseer, Category = SpellCategory.Offensive, DisplayName = "Salve Mortelle",
                Description = "Croix de 5 cases : 200 dégâts au centre, 120 sur les côtés. Toute cible Traqué dans la zone : +60 dégâts. Déclenche aussi tes embûches situées sous la croix. Coûte 3 PR. Cap : 1 fois par tour.",
                LoreFlavor  = "Le moment où la map révèle sa vérité. Salve Mortelle déchire toutes les illusions du Nightseer en même temps." },
            new Entry { SpellIdValue = 35, SpellIdTech = "nightseer_marque_chasseur",       ClassId = NymoraClass.Nightseer, Category = SpellCategory.Tactical,  DisplayName = "Marque du Chasseur",
                Description = "Applique TRAQUÉ à la cible pendant 3 tours (+1 PR). Sort très peu cher : le payoff est dans les autres sorts (bonus sur Traqué). Cap : 1 fois par tour.",
                LoreFlavor  = "L'oeil. Quand l'adversaire prend une Marque du Chasseur, il sait que les 3 prochains tours vont être violents." },
            new Entry { SpellIdValue = 36, SpellIdTech = "nightseer_filet_ronces",          ClassId = NymoraClass.Nightseer, Category = SpellCategory.Tactical,  DisplayName = "Filet de Ronces",
                Description = "Pose une embûche VISIBLE sur une case (invisible seulement si le Nightseer est en phase 3). Toute unité ennemie qui entre : 100 dégâts, -1 PM, et applique TRAQUÉ.",
                LoreFlavor  = "Le piège classique, mais ré-ingéniéré. Le Filet est lisible pour le Nightseer — il SAIT où il l'a posé. Il l'utilise pour pousser l'adversaire ailleurs." },
            new Entry { SpellIdValue = 37, SpellIdTech = "nightseer_champ_mines",           ClassId = NymoraClass.Nightseer, Category = SpellCategory.Tactical,  DisplayName = "Champ de Mines",
                Description = "Pose 3 embûches VISIBLES dans une zone 3x3 (invisibles seulement en phase 3). Chaque embûche : 70 dégâts + applique TRAQUÉ. Cap : 1 fois par tour.",
                LoreFlavor  = "Le terrain miné. Champ de Mines transforme une zone en no-go. L'adversaire doit faire un détour OU absorber 3 mines pour passer." },
            new Entry { SpellIdValue = 38, SpellIdTech = "nightseer_bourrasque",            ClassId = NymoraClass.Nightseer, Category = SpellCategory.Tactical,  DisplayName = "Bourrasque",
                Description = "Pousse la cible 2 cases dans la direction choisie (2e clic). Si la cible finit sa course sur un Filet, une mine, ou du Sang Coagulé : effets déclenchés. Cap : 2 fois par tour.",
                LoreFlavor  = "L'arme du conducteur. Bourrasque n'est pas une frappe — c'est un volant. Le Nightseer décide où l'adversaire VA, pas où il EST." },
            new Entry { SpellIdValue = 39, SpellIdTech = "nightseer_souffle_glacial",       ClassId = NymoraClass.Nightseer, Category = SpellCategory.Tactical,  DisplayName = "Piège Bondissant",
                Description = "Pose un piège-catapulte sur une case (portée 4). Au déclenchement, l'ennemi est ÉJECTÉ de 3 cases dans la direction choisie à la pose (2e clic) et devient Traqué. Pas de dégâts directs. Cap : 1 fois par tour.",
                LoreFlavor  = "Le tremplin piégé. Le Nightseer ne repousse pas l'ennemi — il le PROJETTE là où il l'a décidé : dans un autre piège, un coin, ou hors de portée." },
            new Entry { SpellIdValue = 40, SpellIdTech = "nightseer_voile_ombre",           ClassId = NymoraClass.Nightseer, Category = SpellCategory.Offensive,  DisplayName = "Flèche Traçante",
                Description = "Si la cible est TRAQUÉ : inflige 60 dégâts par PM que tu as dépensé au tour précédent (max 180 à 3 PM). Sinon : aucun dégât. Cap : 1 fois par tour.",
                LoreFlavor  = "La flèche qui suit la piste. Plus le Nightseer a couru pour prendre l'angle, plus le tir traqueur frappe fort." },
            new Entry { SpellIdValue = 41, SpellIdTech = "nightseer_pas_furtif",            ClassId = NymoraClass.Nightseer, Category = SpellCategory.Survival,  DisplayName = "Pas Furtif",
                Description = "Téléporte le Nightseer jusqu'à 3 cases (coûte 4 PA). Si tu as au moins 1 PR (dépensée automatiquement) : pose un Filet de Ronces sur la case quittée. Cap : 1 fois par tour.",
                LoreFlavor  = "Le coup le plus frustrant pour l'adversaire. Le Nightseer disparaît littéralement. L'adversaire doit deviner où il est." },
            new Entry { SpellIdValue = 42, SpellIdTech = "nightseer_camouflage_ronces",     ClassId = NymoraClass.Nightseer, Category = SpellCategory.Survival,  DisplayName = "Camouflage de Ronces",
                Description = "Le Nightseer gagne un BOUCLIER de 130 HP pendant 2 tours. Pendant la durée, sa case est entourée d'un Filet de Ronces invisible : tout ennemi adjacent fin de tour subit 70 dégâts + EMPREINTÉ.",
                LoreFlavor  = "L'épine défensive. Camouflage Ronces dit à l'adversaire : 'Approche-toi, vois ce qui se passe.' Anti-engage parfait contre Soulrender et Ghostra mêlée." },
            new Entry { SpellIdValue = 43, SpellIdTech = "nightseer_seve_sauvage",          ClassId = NymoraClass.Nightseer, Category = SpellCategory.Survival,  DisplayName = "Sève Sauvage",
                Description = "Le Nightseer se soigne de 130 HP. Si une de ses embûches a été déclenchée ce tour ou le tour précédent : +60 HP additionnels.",
                LoreFlavor  = "Le heal de récolte. Sève Sauvage récompense le Nightseer qui a déjà fait son setup. Plus la map est piégée, plus il survit." },
            new Entry { SpellIdValue = 44, SpellIdTech = "nightseer_evanescence",           ClassId = NymoraClass.Nightseer, Category = SpellCategory.Survival,  DisplayName = "Évanescence",
                Description = "Utilisable uniquement à <30% HP. Le Nightseer se téléporte jusqu'à 7 cases, se soigne de 150 HP ET pose un Filet de Ronces sur la case quittée. UTILISABLE 1 FOIS PAR MATCH.",
                LoreFlavor  = "L'évasion totale. Évanescence permet au Nightseer de quitter complètement le combat le temps d'un tour." },
            new Entry { SpellIdValue = 45, SpellIdTech = "nightseer_traquenard",            ClassId = NymoraClass.Nightseer, Category = SpellCategory.Signature, DisplayName = "Traquenard",
                Description = "Coûte 5 PR (jauge pleine, phase 3). Le Nightseer se téléporte à 1 case de la cible. Inflige 280 dégâts. Applique PARALYSIE (-3 PM, -2 PA) au prochain tour de la cible. Si la cible est TRAQUÉ : +80 dégâts.",
                LoreFlavor  = "L'embuscade pure. Traquenard n'est pas un finisher de DPS — c'est l'aboutissement d'un piège mental. La paralysie verrouille le tour adverse, le Nightseer peut décrocher ou enchaîner." },

            // ===== COLOSSAR (50-65) =====
            new Entry { SpellIdValue = 50, SpellIdTech = "colossar_frappe_lourde",          ClassId = NymoraClass.Colossar, Category = SpellCategory.Offensive, DisplayName = "Frappe Lourde",
                Description = "Inflige 180 dégâts. Si la cible est ÉPINGLÉE (adjacente à un Pilier, mur, ou bord de map du côté opposé au Colossar) : 280 dégâts.",
                LoreFlavor  = "Le coup signature. La cible doit littéralement éviter d'avoir un mur derrière elle pour exister. Le Colossar transforme les bords de map en pièges." },
            new Entry { SpellIdValue = 51, SpellIdTech = "colossar_onde_de_choc",           ClassId = NymoraClass.Colossar, Category = SpellCategory.Offensive, DisplayName = "Onde de Choc",
                Description = "AoE autour du Colossar. Inflige 80 dégâts à toutes les unités adjacentes ET les pousse de 2 cases. Si une unité est poussée contre un mur, Pilier, ou bord de map : 80 dégâts supplémentaires + APPLIQUE TRAUMA (-1 PM, -1 PA pendant 1 tour).",
                LoreFlavor  = "Le sort qui transforme un Pilier en arme. Sans Onde, un Pilier est juste décoratif. Avec, c'est un mur sur lequel l'adversaire va s'écraser." },
            new Entry { SpellIdValue = 52, SpellIdTech = "colossar_marteau_punisseur",      ClassId = NymoraClass.Colossar, Category = SpellCategory.Offensive, DisplayName = "Marteau Punisseur",
                Description = "Inflige 160 dégâts. Si la cible a moins de 4 PA actuels (donc a déjà cast ce tour) : 240 dégâts ET applique TRAUMA (-2 PA prochain tour).",
                LoreFlavor  = "L'anti-tempo. Marteau Punisseur punit les classes qui spam — Soulrender, Necram. Le Colossar dit : 'Tu as fini ton tour ? Tant mieux. Maintenant tu prends.'" },
            new Entry { SpellIdValue = 53, SpellIdTech = "colossar_choc_sismique",          ClassId = NymoraClass.Colossar, Category = SpellCategory.Offensive, DisplayName = "Choc Sismique",
                Description = "Frappe en ligne droite 4 cases. Inflige 130 dégâts à toutes les cibles touchées. Toutes les cibles touchées : -1 PM au prochain tour. Si une case Pilier ou Mur du Colossar se trouve sur la trajectoire : la frappe traverse, +50 dégâts à la cible suivante.",
                LoreFlavor  = "L'onde tellurique. Choc Sismique passe à travers ses propres murs comme un piston. Le Colossar tire à travers ses fortifications — il est le seul." },
            new Entry { SpellIdValue = 54, SpellIdTech = "colossar_represailles",           ClassId = NymoraClass.Colossar, Category = SpellCategory.Offensive, DisplayName = "Représailles",
                Description = "Inflige 100 dégâts immédiatement. Pendant 2 tours après le cast, chaque attaque mêlée subie par le Colossar renvoie 80 dégâts à l'attaquant. Cap à 4 retours.",
                LoreFlavor  = "Le contre-engage. Représailles est posé AVANT le combat rapproché — c'est un engagement délibéré du Colossar pour dire à un Soulrender ou une Ghostra : 'Vas-y, viens.'" },
            new Entry { SpellIdValue = 55, SpellIdTech = "colossar_pilier",                 ClassId = NymoraClass.Colossar, Category = SpellCategory.Tactical,  DisplayName = "Pilier",
                Description = "Pose un Pilier (200 HP, infranchissable, occupe 1 case) sur une case vide. Reste jusqu'à destruction. Le Colossar gagne +1 FD à la pose. Le Pilier bloque les lignes de vue et de tir des sorts directs.",
                LoreFlavor  = "L'outil. À lui seul, Pilier ne menace personne. En combinaison avec push/pull, il devient un instrument de meurtre." },
            new Entry { SpellIdValue = 56, SpellIdTech = "colossar_mur_de_pierre",          ClassId = NymoraClass.Colossar, Category = SpellCategory.Tactical,  DisplayName = "Mur de Pierre",
                Description = "Crée un mur infranchissable de 3 cases (en ligne) pendant 2 tours — 5 cases si tu as au moins 1 FD (dépensé automatiquement). Le Colossar gagne +2 FD à la pose. Le Mur bloque tout : déplacements, ciblages directs, lignes de tir. Cap : 1×/tour, relance 2 tours.",
                LoreFlavor  = "Le grand séparateur. Un Mur bien posé peut couper la map en deux et forcer l'adversaire à choisir : il fait demi-tour ou il détruit le mur." },
            new Entry { SpellIdValue = 57, SpellIdTech = "colossar_ancrage",                ClassId = NymoraClass.Colossar, Category = SpellCategory.Tactical,  DisplayName = "Ancrage",
                Description = "La cible perd 2 PM pendant 2 tours ET ne peut pas être déplacée par effets externes (push/pull/téléport) au prochain tour. Pas de dégâts.",
                LoreFlavor  = "Le gel. Ancrage est l'anti-mobilité ultime. Une Ghostra ancrée ne peut plus se téléporter. C'est un sort qui DÉSACTIVE des kits entiers." },
            new Entry { SpellIdValue = 58, SpellIdTech = "colossar_provocation",            ClassId = NymoraClass.Colossar, Category = SpellCategory.Tactical,  DisplayName = "Provocation",
                Description = "Force la cible à tenter d'attaquer le Colossar pendant 1 tour (ses sorts non-ciblant le Colossar coûtent +2 PA). La cible perd aussi 1 PM. Si la cible n'est pas adjacente au Colossar à la fin de son tour : 100 dégâts auto.",
                LoreFlavor  = "L'humiliation. Provocation force l'adversaire à venir au Colossar — qui l'attend avec Représailles posé. Le Colossar dicte les engagements directement par sort." },
            new Entry { SpellIdValue = 59, SpellIdTech = "colossar_brisure",                ClassId = NymoraClass.Colossar, Category = SpellCategory.Tactical,  DisplayName = "Brisure",
                Description = "Inflige 90 dégâts. Retire un buff/bouclier de la cible (au choix du joueur). Si la cible n'a pas de buff/bouclier : applique TRAUMA (-2 PA prochain tour). Si la cible avait Camouflage Ronces, Linceul d'Ombres, Carapace Visqueuse, Stoïcisme, Peau de Fer : le bouclier est entièrement retiré.",
                LoreFlavor  = "Le briseur de mur. Brisure est l'anti-tank, l'anti-tortue. Aucune classe ne peut se reposer derrière un bouclier face au Colossar — il les casse explicitement." },
            new Entry { SpellIdValue = 60, SpellIdTech = "colossar_stoicisme",              ClassId = NymoraClass.Colossar, Category = SpellCategory.Survival,  DisplayName = "Stoïcisme",
                Description = "Le Colossar gagne un BOUCLIER de 200 HP pour 2 tours. Pendant ces 2 tours, il ne peut PAS être déplacé (push/pull/téléport ennemi sans effet). Si le bouclier survit aux 2 tours sans être brisé, le Colossar récupère 80 HP.",
                LoreFlavor  = "Le rocher. Stoïcisme est le contraire d'un panic-button — c'est une déclaration. Le Colossar plante les pieds." },
            new Entry { SpellIdValue = 61, SpellIdTech = "colossar_garde_protectrice",      ClassId = NymoraClass.Colossar, Category = SpellCategory.Survival,  DisplayName = "Garde Protectrice",
                Description = "Pendant 2 tours, le Colossar subit -15% de dégâts de TOUTES les sources (sauf DoT venin Necram qui ignore les réductions). Ne se cumule pas avec le passif Densité Inerte au-delà du cap -45% total.",
                LoreFlavor  = "L'armure mobile. Garde Protectrice est le bouclier qui ne casse pas. Il n'a pas de HP — il a un timer. Le Colossar peut traverser une zone hostile sans se faire démolir." },
            new Entry { SpellIdValue = 62, SpellIdTech = "colossar_ressac_vital",           ClassId = NymoraClass.Colossar, Category = SpellCategory.Survival,  DisplayName = "Ressac Vital",
                Description = "Le Colossar se soigne de 80 HP + 30 HP par attaque qu'il a subie au tour précédent (max +120 HP, donc cap 200 HP).",
                LoreFlavor  = "Le contre-tank. Ressac Vital récompense le Colossar qui s'est fait taper. Plus l'adversaire l'agresse, plus il se soigne. Anti-burst implacable." },
            new Entry { SpellIdValue = 63, SpellIdTech = "colossar_renvoi_bouclier",        ClassId = NymoraClass.Colossar, Category = SpellCategory.Survival,  DisplayName = "Renvoi du Bouclier",
                Description = "Pendant 1 tour, toute attaque (mêlée OU à distance) subie par le Colossar renvoie 60 dégâts à l'attaquant. Cap à 4 retours.",
                LoreFlavor  = "Le miroir. Renvoi du Bouclier est l'anti-Nightseer — un sort à distance qui frappe le Colossar lui revient direct." },
            new Entry { SpellIdValue = 64, SpellIdTech = "colossar_soin_lourd",             ClassId = NymoraClass.Colossar, Category = SpellCategory.Offensive, DisplayName = "Éboulement",
                Description = "Vise un de TES obstacles — Pilier, Mur ou Faille (portée 3) — et le fait s'effondrer : 150 dégâts en zone (rayon 1) sur les ennemis autour + ils sont poussés. Si c'est un Pilier, tu récupères 30 HP. Cap : 1 fois par tour.",
                LoreFlavor  = "Le sacrifice de pierre. Le Colossar transforme sa propre fortification en bombe : la défense devient l'attaque." },
            new Entry { SpellIdValue = 65, SpellIdTech = "colossar_effondrement",           ClassId = NymoraClass.Colossar, Category = SpellCategory.Signature, DisplayName = "Effondrement",
                Description = "IMMÉDIAT : toutes les cases autour du Colossar (rayon 2) deviennent IMPRATICABLES pendant 2 tours. Les ennemis dessus prennent 200 dégâts et sont éjectés vers la case libre la plus proche. Pendant 2 tours, les sorts du Colossar coûtent -1 PA et toute attaque qu'il subit est réduite de 30%. Coûte 5 FD (jauge pleine). Relance 4 tours.",
                LoreFlavor  = "L'arme tellurique. Plus d'annonce : le sol cède SOUS l'ennemi à l'instant où le Colossar le décide. Pas de fenêtre pour fuir." },

            // ===== NECRAM (70-85) =====
            new Entry { SpellIdValue = 70, SpellIdTech = "necram_crachat_acide",            ClassId = NymoraClass.Necram, Category = SpellCategory.Offensive, DisplayName = "Crachat Acide",
                Description = "Inflige 90 dégâts ET applique 2 marques de venin (au lieu de 1). Cap à 4 marques par cible.",
                LoreFlavor  = "Le sort de base, mais redoutable. Crachat Acide combine dégâts directs et setup en 1 PA-efficace. C'est l'arme à 80% de l'utilisation Necram en early." },
            new Entry { SpellIdValue = 71, SpellIdTech = "necram_morsure_putride",          ClassId = NymoraClass.Necram, Category = SpellCategory.Offensive, DisplayName = "Morsure Putride",
                Description = "Inflige 110 dégâts + 22 par marque sur la cible (max +90, donc 200 dégâts max). Si la cible meurt : toutes ses marques sont transférées sur l'unité ennemie la plus proche.",
                LoreFlavor  = "L'embrasement. Morsure Putride est le finisher qui propage. Tuer une cible avec elle ne stoppe pas le DoT — elle migre. Anti-team, mais aussi outil pour cycler en 1v1." },
            new Entry { SpellIdValue = 72, SpellIdTech = "necram_detonation_virulente",     ClassId = NymoraClass.Necram, Category = SpellCategory.Offensive, DisplayName = "Détonation Virulente",
                Description = "Inflige instantanément tous les dégâts de venin de la cible (marques × dégâts de Floraison + bonus Marque Sacrificielle), en ignorant boucliers ET réductions. Les marques NE sont PAS consommées — rejouable chaque tour. Cap : 1 fois par tour.",
                LoreFlavor  = "Le détonateur. Plus besoin de tout dépenser d'un coup : le Necram fait pulser son poison à la demande, tour après tour." },
            new Entry { SpellIdValue = 73, SpellIdTech = "necram_faux_decharnee",           ClassId = NymoraClass.Necram, Category = SpellCategory.Offensive, DisplayName = "Faux Décharnée",
                Description = "AoE 1 case (le Necram et ses 8 voisines). Inflige 130 dégâts. Le Necram se SOIGNE de 30 HP par marque active sur toutes les cibles touchées (cap +120 HP).",
                LoreFlavor  = "Le moment où le mage devient bête. La Faux est anti-Soulrender, anti-Ghostra : si tu te rapproches du Necram, il en profite pour se soigner sur ton dos." },
            new Entry { SpellIdValue = 74, SpellIdTech = "necram_brume_toxique",            ClassId = NymoraClass.Necram, Category = SpellCategory.Offensive, DisplayName = "Brume Toxique",
                Description = "Pose une zone toxique 3x3 pendant 3 tours. Toute unité ennemie qui se trouve dans la zone (à la pose, en entrant, ou en finissant son tour) prend +1 marque de venin. Tant qu'une unité tient dans la Brume, ses dégâts de venin par tour sont MAJORÉS (+10 par marque). Pas de dégâts directs. Cap : 1×/tour, relance 2 tours.",
                LoreFlavor  = "L'air vicié. Brume Toxique ne tue pas — elle CONDAMNE : rester dedans, c'est accélérer sa propre pourriture." },
            new Entry { SpellIdValue = 75, SpellIdTech = "necram_inoculation",              ClassId = NymoraClass.Necram, Category = SpellCategory.Tactical,  DisplayName = "Inoculation",
                Description = "Applique 2 marques de venin sur la cible (sans dégâts directs). Cap à 4 marques par cible.",
                LoreFlavor  = "Le baiser de la mort. Inoculation ne fait rien d'immédiat. L'adversaire qui prend 2 marques sait que les 3 prochains tours vont être un compte à rebours. La pression vient du SILENCE." },
            new Entry { SpellIdValue = 76, SpellIdTech = "necram_marque_sacrificielle",     ClassId = NymoraClass.Necram, Category = SpellCategory.Tactical,  DisplayName = "Marque Sacrificielle",
                Description = "Pendant 3 tours, les marques de venin sur la cible infligent +20 dégâts par tour. La cible peut recevoir Marque Sacrificielle même si elle n'a pas encore de marques (mais sans marques actives, l'effet est neutre).",
                LoreFlavor  = "L'engrais. Marque Sacrificielle force l'adversaire à se soigner CONSTAMMENT. 70 dégâts de venin par tour ne pardonnent aucun délai." },
            new Entry { SpellIdValue = 77, SpellIdTech = "necram_symbiose_morbide",         ClassId = NymoraClass.Necram, Category = SpellCategory.Tactical,  DisplayName = "Symbiose Morbide",
                Description = "Pendant 2 tours, chaque fois que le venin inflige ses dégâts à un ennemi (chaque tour), le Necram est soigné de 15 HP (montant fixe, peu importe le nombre de marques). Recast = rafraîchit.",
                LoreFlavor  = "Le parasite. Symbiose transforme le Necram en machine à régen : chaque pulsation de poison le nourrit." },
            new Entry { SpellIdValue = 78, SpellIdTech = "necram_contagion",                ClassId = NymoraClass.Necram, Category = SpellCategory.Tactical,  DisplayName = "Contagion",
                Description = "Rend une unité ennemie CONTAGIEUSE pendant 2 tours : à la fin de chacun de ses tours, elle prend +1 marque de venin automatiquement (auto-propagation). Cap : 1 fois par tour.",
                LoreFlavor  = "L'épidémie. La cible devient son propre foyer d'infection : chaque tour qui passe la gangrène un peu plus, sans que le Necram lève le petit doigt." },
            new Entry { SpellIdValue = 79, SpellIdTech = "necram_pas_spectral",             ClassId = NymoraClass.Necram, Category = SpellCategory.Offensive, DisplayName = "Échange Spectral",
                Description = "Le Necram échange instantanément sa place avec une unité ennemie (portée 5) et lui inflige 80 dégâts. Cap : 1 fois par tour.",
                LoreFlavor  = "Le pacte d'os. Le Necram et sa proie permutent dans un éclair spectral : il quitte le danger, elle le subit." },
            new Entry { SpellIdValue = 80, SpellIdTech = "necram_voile_pestilence",         ClassId = NymoraClass.Necram, Category = SpellCategory.Tactical,  DisplayName = "Nuée de Spores",
                Description = "Pendant 2 tours, chacun de tes sorts visant un ennemi pose +1 marque de venin BONUS sur la cible. (Recast = rafraîchit, pas de cumul.)",
                LoreFlavor  = "L'essaim. Le Necram s'enveloppe de spores : chaque sort qu'il lance ensemence un peu plus la chair de sa proie." },
            new Entry { SpellIdValue = 81, SpellIdTech = "necram_carapace_visqueuse",       ClassId = NymoraClass.Necram, Category = SpellCategory.Survival,  DisplayName = "Carapace Visqueuse",
                Description = "Le Necram gagne un BOUCLIER de 160 HP pour 2 tours. Tout attaquant mêlée qui frappe le bouclier reçoit 1 marque automatiquement. Relance : 2 tours.",
                LoreFlavor  = "L'épine pourrie. Carapace Visqueuse n'est pas un mur — c'est un piège défensif. Frapper le Necram en mêlée = signer son arrêt de mort." },
            new Entry { SpellIdValue = 82, SpellIdTech = "necram_drain_vital",              ClassId = NymoraClass.Necram, Category = SpellCategory.Survival,  DisplayName = "Drain Vital",
                Description = "Inflige 40 dégâts à la cible et soigne le Necram de 40 HP par marque de venin sur elle (max 160 HP à 4 marques). Les marques ne sont pas consommées. Cap : 1 fois par tour.",
                LoreFlavor  = "Le siphon. Plus la cible est gangrenée, plus Drain Vital régénère : le poison devient une fontaine de vie." },
            new Entry { SpellIdValue = 83, SpellIdTech = "necram_regeneration_necrotique",  ClassId = NymoraClass.Necram, Category = SpellCategory.Survival,  DisplayName = "Régénération Nécrotique",
                Description = "Le Necram se soigne de 70 HP + 15 HP par marque ennemie dans rayon 4 (max +90 HP). Si tu as au moins 1 PT (dépensé automatiquement) : +30 HP additionnels.",
                LoreFlavor  = "La récolte. Régénération Nécrotique scale avec le travail accompli. Plus de marques = plus de heal. C'est le heal qui dit : 'J'ai bien semé.'" },
            new Entry { SpellIdValue = 84, SpellIdTech = "necram_cocon_putride",            ClassId = NymoraClass.Necram, Category = SpellCategory.Survival,  DisplayName = "Cocon Putride",
                Description = "Utilisable uniquement à <30% HP. Le Necram se soigne de 220 HP ET applique 1 marque à toutes les unités ennemies dans rayon 4. UTILISABLE 1 FOIS PAR MATCH.",
                LoreFlavor  = "L'explosion fongique. Cocon Putride n'est pas qu'un panic-heal — c'est une aspersion. Le Necram à l'agonie devient soudain le Necram avec 6+ marques sur la map." },
            new Entry { SpellIdValue = 85, SpellIdTech = "necram_virus_fatal",              ClassId = NymoraClass.Necram, Category = SpellCategory.Signature, DisplayName = "Virus Fatal",
                Description = "Cible une unité ennemie. TOUTES ses marques de venin infligent leurs dégâts instantanément ×1,5. Une cible à 4 marques en Floraison max (60 par marque × 4 × 1,5) prend ~360 dégâts d'un coup, qui ignorent boucliers ET réductions. Marques consommées si la cible survit. Coûte 6 PT (jauge pleine), relance 4 tours.",
                LoreFlavor  = "L'apoptose. Virus Fatal est l'aboutissement absolu de la stratégie Necram : 4-5 tours de setup transformés en 1 tour de mort lente accélérée." },

            // ===== GHOSTRA (86-101). Volte-Face (90) reste class Tactique malgre amendement offensif 16 mai. =====
            new Entry { SpellIdValue = 86, SpellIdTech = "ghostra_lame_spectrale",          ClassId = NymoraClass.Ghostra, Category = SpellCategory.Offensive, DisplayName = "Lame Spectrale",
                Description = "Inflige 170 dégâts. Si dorsal : +50 dégâts (Angle 2) ou +80 (Angle 3) du passif. Si la cible a PLAIE OUVERTE : +60 dégâts.",
                LoreFlavor  = "La frappe la plus banale du jeu — sauf que personne ne sait d'où elle vient. La banalité du sort est sa force : il sort de partout, depuis n'importe quel leurre." },
            new Entry { SpellIdValue = 87, SpellIdTech = "ghostra_lame_vorace_spectrale",   ClassId = NymoraClass.Ghostra, Category = SpellCategory.Offensive, DisplayName = "Lame Vorace Spectrale",
                Description = "Frappe bon marché et répétable. Inflige 110 dégâts + 60 si la cible a PLAIE OUVERTE. Si dorsal : +bonus passif. La Plaie Ouverte n'est PAS consommée. Cap : 2 fois par tour.",
                LoreFlavor  = "Le coup qui ronge. Lame Vorace empile sur une plaie ouverte sans la fermer. Légère et rapide, elle se relance dans le tour là où la Lame Spectrale frappe lourd une seule fois." },
            new Entry { SpellIdValue = 88, SpellIdTech = "ghostra_replique_fantome",        ClassId = NymoraClass.Ghostra, Category = SpellCategory.Tactical,  DisplayName = "Réplique Fantôme",
                Description = "Pose un Leurre sur une case vide à 4 cases. Le Leurre est visuellement identique à la Ghostra. Dure 4 rounds ou jusqu'à interaction. Si le Leurre survit la durée complète, la Ghostra regagne 80 HP. Si le Leurre est détruit par un sort adverse, la Ghostra regagne 40 HP. UTILISABLE 1 FOIS PAR TOUR.",
                LoreFlavor  = "Le clone qui paye les frais. Réplique Fantôme FORCE l'adversaire à choisir : 'je frappe ce qui ressemble à la Ghostra ?' Toute lecture coûte. La Ghostra gagne quoi qu'il arrive." },
            new Entry { SpellIdValue = 89, SpellIdTech = "ghostra_pas_dans_ombre",          ClassId = NymoraClass.Ghostra, Category = SpellCategory.Tactical,  DisplayName = "Pas dans l'Ombre",
                Description = "Téléporte la Ghostra jusqu'à 5 cases. Si une case adjacente à l'arrivée contient une cible ennemie : la cible PIVOTE pour faire face à la Ghostra. Laisse aussi automatiquement un leurre sur la case quittée si ta jauge le permet (compte dans le cap 3 leurres). UTILISABLE 1 FOIS PAR TOUR.",
                LoreFlavor  = "Le saut de l'absent. Pas dans l'Ombre n'est pas seulement une mobilité — c'est un GÉNÉRATEUR de leurre." },
            new Entry { SpellIdValue = 90, SpellIdTech = "ghostra_permutation",            ClassId = NymoraClass.Ghostra, Category = SpellCategory.Tactical,  DisplayName = "Permutation",
                Description = "Échange instantanément la position de la Ghostra avec un de ses leurres ciblé (jusqu'à 4 cases), dès 1 leurre actif. Aucun dégât. Échange invisible côté adversaire (silhouettes identiques). À 3 leurres actifs (Angle 3), son coût tombe à 0 PA. UTILISABLE 1 FOIS PAR TOUR.",
                LoreFlavor  = "Le pas de côté du fantôme. La Ghostra n'est jamais où on la frappe — elle était déjà l'autre silhouette." },
            new Entry { SpellIdValue = 91, SpellIdTech = "ghostra_saigne_ame",              ClassId = NymoraClass.Ghostra, Category = SpellCategory.Offensive, DisplayName = "Saigne-Âme",
                Description = "Inflige 200 dégâts + 70 si la cible a PLAIE OUVERTE (consomme la plaie). Si la cible meurt : la Ghostra regagne 60 HP.",
                LoreFlavor  = "L'aboutissement. Saigne-Âme consomme la plaie pour un payoff massif. Le sort de fin du combo Plaie Ouverte → Lame Vorace → Saigne-Âme." },
            new Entry { SpellIdValue = 92, SpellIdTech = "ghostra_frappe_fantome",          ClassId = NymoraClass.Ghostra, Category = SpellCategory.Offensive, DisplayName = "Frappe Fantôme",
                Description = "La Ghostra se téléporte à 1 case de la cible (côté libre). Inflige 200 dégâts. Si dorsal : +bonus passif. Si la direction de la cible a été modifiée ce tour : applique PLAIE OUVERTE (40 dégâts/tour pendant 2 tours).",
                LoreFlavor  = "Le finisseur. Frappe Fantôme arrive de nulle part. Combinée à un retournement de la cible, c'est un combo qui peut shred 350+ HP en un tour." },
            new Entry { SpellIdValue = 93, SpellIdTech = "ghostra_eveil_spectral",          ClassId = NymoraClass.Ghostra, Category = SpellCategory.Tactical,  DisplayName = "Éveil Spectral",
                Description = "Un de tes leurres adjacent à la cible (1 case) la poignarde pour 100 dégâts. Le bonus dorsal et la PLAIE OUVERTE sont calculés depuis la position du leurre : un leurre dans le dos de la cible frappe en dorsal, même si la Ghostra est en face. Le leurre n'est pas consommé. UTILISABLE 2 FOIS PAR TOUR.",
                LoreFlavor  = "L'illusion qui mord. Un leurre n'est plus seulement un decoy : il se réveille et plante sa lame. La cible ne sait jamais quelle silhouette va frapper." },
            new Entry { SpellIdValue = 94, SpellIdTech = "ghostra_marque_ombre",            ClassId = NymoraClass.Ghostra, Category = SpellCategory.Tactical,  DisplayName = "Marque de l'Ombre",
                Description = "Pendant 2 tours, tous les sorts de la Ghostra sur la cible gagnent +20 dégâts. Si la cible est touchée en dorsal pendant ces 2 tours : applique automatiquement PLAIE OUVERTE. UTILISABLE 1 FOIS PAR TOUR.",
                LoreFlavor  = "Le sceau. Marque de l'Ombre pré-charge une cible. La Ghostra peut ensuite alterner Réplique → permutation → Lame Spectrale dorsal et l'effet plaie est garanti. Anti-tank par contournement." },
            new Entry { SpellIdValue = 95, SpellIdTech = "ghostra_nuee_spectrale",          ClassId = NymoraClass.Ghostra, Category = SpellCategory.Offensive, DisplayName = "Nuée Spectrale",
                Description = "Un déluge de lames spectrales s'abat sur une cible unique : 100 dégâts + 40 par leurre actif + 20 par leurre adjacent à la cible (jusqu'à 280 au plein setup). Ne consomme aucun leurre. UTILISABLE 1 FOIS PAR TOUR.",
                LoreFlavor  = "L'apocalypse en miniature. Plus la Ghostra a semé de silhouettes, plus la nuée déchire. Toutes les lames frappent au même instant." },
            new Entry { SpellIdValue = 96, SpellIdTech = "ghostra_linceul_ombres",          ClassId = NymoraClass.Ghostra, Category = SpellCategory.Survival,  DisplayName = "Linceul d'Ombres",
                Description = "La Ghostra gagne un BOUCLIER de 130 HP pendant 2 tours. Toute attaque mêlée subie pendant la durée renvoie 40 dégâts à l'attaquant. UTILISABLE 1 FOIS PAR TOUR.",
                LoreFlavor  = "Le suaire. Linceul d'Ombres est le bouclier qui mord. Anti-Soulrender qui charge." },
            new Entry { SpellIdValue = 97, SpellIdTech = "ghostra_voile_spectral",          ClassId = NymoraClass.Ghostra, Category = SpellCategory.Survival,  DisplayName = "Voile Spectral",
                Description = "Téléporte tous tes leurres actifs sur des cases libres autour de l'ennemi ciblé (collés à lui en priorité) pour préparer tes frappes dorsales (Éveil Spectral, Nuée). Requiert au moins 1 leurre actif. UTILISABLE 1 FOIS PAR TOUR.",
                LoreFlavor  = "Le voile se resserre. D'un geste, la Ghostra rappelle toutes ses silhouettes autour de sa proie — le piège se referme avant la première lame." },
            new Entry { SpellIdValue = 98, SpellIdTech = "ghostra_replique_protectrice",    ClassId = NymoraClass.Ghostra, Category = SpellCategory.Survival,  DisplayName = "Réplique Protectrice",
                Description = "Pose un Leurre PROTECTEUR (200 HP, redirige 30% des dégâts subis par la Ghostra pendant 4 rounds). Si le Leurre est détruit, la Ghostra regagne 80 HP. Pas de stack si plusieurs Protective vivants (un seul absorbe par hit). Compte dans le cap 3 leurres. UTILISABLE 1 FOIS PAR TOUR.",
                LoreFlavor  = "Le clone-bouclier. Réplique Protectrice n'est pas un leurre offensif — c'est un sustain caché. Elle prolonge la vie de la Ghostra de 1-2 tours." },
            new Entry { SpellIdValue = 99, SpellIdTech = "ghostra_dernier_pas",             ClassId = NymoraClass.Ghostra, Category = SpellCategory.Survival,  DisplayName = "Dernier Pas",
                Description = "Utilisable uniquement à <30% HP. La Ghostra se soigne de 200 HP, se téléporte jusqu'à 5 cases, ET pose un leurre sur la case quittée. UTILISABLE 1 FOIS PAR MATCH.",
                LoreFlavor  = "L'évasion finale. Dernier Pas n'est pas qu'un heal — c'est un tour offert. La Ghostra à 200 HP se retrouve à 50% HP, à 5 cases de l'engagement, avec un leurre fraîchement posé." },
            new Entry { SpellIdValue = 100, SpellIdTech = "ghostra_communion_spectrale",    ClassId = NymoraClass.Ghostra, Category = SpellCategory.Survival,  DisplayName = "Communion Spectrale",
                Description = "La Ghostra absorbe un de ses leurres actifs et se soigne de 150 HP. Requiert au moins 1 leurre actif. UTILISABLE 1 FOIS PAR TOUR.",
                LoreFlavor  = "Le retour à soi. La Ghostra rappelle l'une de ses silhouettes et se nourrit de sa propre illusion pour recoller ses plaies." },
            new Entry { SpellIdValue = 101, SpellIdTech = "ghostra_execution_spectrale",    ClassId = NymoraClass.Ghostra, Category = SpellCategory.Signature, DisplayName = "Exécution Spectrale",
                Description = "Inflige 350 dégâts SI la cible est dorsale (regarde ailleurs). Applique PLAIE OUVERTE (50 dégâts/tour × 3 tours). Si la cible meurt sur ce sort, la Ghostra regagne 100 HP ET 2 leurres réapparaissent immédiatement (2 prêts pour le cycle suivant). Si la cible n'est PAS dorsale au moment du cast, le sort RATE et les 3 leurres sont quand même consommés.",
                LoreFlavor  = "Le coup le plus risqué du jeu. Exécution Spectrale demande une LECTURE PARFAITE — la cible doit être dorsale. Ratée, la Ghostra perd tout son setup. Réussie, elle finit le match en 1 tour." },
        };

        // ------------------------------------------------------------------
        // Lookup indexes : initialises au premier acces (statique cold) puis
        // re-utilises. Cle = SpellIdValue (int Quantum) pour l'acces par sort.
        // ------------------------------------------------------------------
        private static Dictionary<int, Entry> _byQuantumId;
        private static Dictionary<string, Entry> _byTech;

        private static void EnsureIndexes()
        {
            if (_byQuantumId != null) return;
            _byQuantumId = new Dictionary<int, Entry>(Entries.Count);
            _byTech = new Dictionary<string, Entry>(Entries.Count);
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                _byQuantumId[e.SpellIdValue] = e;
                if (!string.IsNullOrEmpty(e.SpellIdTech)) _byTech[e.SpellIdTech] = e;
            }
        }

        /// <summary>
        /// Retourne l'entree Bible pour un SpellId Quantum (int). Cast <c>(int)spellId</c>
        /// depuis Combat HUD. Retourne false si non trouve (devrait jamais arriver sur
        /// les 80 sorts de prod).
        /// </summary>
        public static bool TryGetByQuantumId(int spellIdValue, out Entry entry)
        {
            EnsureIndexes();
            return _byQuantumId.TryGetValue(spellIdValue, out entry);
        }

        public static bool TryGetByTech(string spellIdTech, out Entry entry)
        {
            EnsureIndexes();
            entry = default;
            return !string.IsNullOrEmpty(spellIdTech) && _byTech.TryGetValue(spellIdTech, out entry);
        }
    }
}
