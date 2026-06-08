using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        // Tokens de valeurs dynamiques (chantier "valeurs buffees en combat", 6 juin).
        // Une description encode son nombre de base PRIMAIRE de degats/soin via un token
        // auto-porte : "{DMG:170}" / "{HEAL:150}" (la valeur de base est DANS le token, pas de
        // champ separe). Deux resolutions :
        //   - ResolvePlain (ci-dessous) : remplace le token par sa valeur brute -> deck builder,
        //     PopulateSpellCatalog, et tout consommateur SANS contexte de caster.
        //   - Cote combat (Nymora.Combat.View.HUD.SpellTooltipText) : remplace par la valeur
        //     BUFFEE selon les bonus actifs du caster, coloree en vert si modifiee.
        // Les sorts sans token gardent leur nombre en clair (retro-compatible, rollout par classe).
        // ------------------------------------------------------------------
        private static readonly Regex TokenRegex = new Regex(@"\{(?:DMG|HEAL):(\d+)\}", RegexOptions.Compiled);

        /// <summary>Remplace les tokens {DMG:N}/{HEAL:N} par leur valeur brute N (texte sans couleur).</summary>
        public static string ResolvePlain(string description)
        {
            if (string.IsNullOrEmpty(description) || description.IndexOf('{') < 0) return description;
            return TokenRegex.Replace(description, m => m.Groups[1].Value);
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
                Description = "{DMG:220} dégâts.\nSi le coup tue : recule de 2 cases gratuitement.",
                LoreFlavor  = "Le sort signature de base. Lent (3 PA), prévisible — et c'est ce qui le rend terrifiant. L'adversaire SAIT qu'il arrive. Il ne peut pas l'arrêter." },
            new Entry { SpellIdValue = 11, SpellIdTech = "soulrender_ouvre_plaie",          ClassId = NymoraClass.Soulrender, Category = SpellCategory.Offensive, DisplayName = "Ouvre-Plaie",
                Description = "{DMG:110} dégâts.\nAvec 1 HG : 230 dégâts + soins/boucliers de la cible ÷2 (1 tour).\n1×/tour.",
                LoreFlavor  = "L'anti-sustain. La simple existence de ce sort dans le deck Soulrender suffit à interdire à l'adversaire de poser un Carapace ou Soin Lourd sans préparation." },
            new Entry { SpellIdValue = 12, SpellIdTech = "soulrender_charge_brutale",       ClassId = NymoraClass.Soulrender, Category = SpellCategory.Offensive, DisplayName = "Charge Brutale",
                Description = "Fonce en ligne droite jusqu'au 1er obstacle.\n{DMG:180} dégâts à la cible touchée.\nLaisse de la Vapeur Carmin sur le trajet (1 tour).\n1×/tour.",
                LoreFlavor  = "Le bélier. Charge Brutale ne fait pas seulement entrer le Soulrender — elle CRÉE un couloir de pression qui restera après son passage." },
            new Entry { SpellIdValue = 13, SpellIdTech = "soulrender_detonation_sanglante", ClassId = NymoraClass.Soulrender, Category = SpellCategory.Offensive, DisplayName = "Détonation Sanglante",
                Description = "AoE croix 3. {DMG:60} dégâts + 40 par HG (2 à 5 HG).\nAvec 5 HG : 260 dégâts.\nSang Coagulé sous le centre (2 tours).\n5 HG ici → Âme Lacérée interdite (cooldown reset).",
                LoreFlavor  = "Le payoff total. Détoner 5 HG est un acte de FOI — le Soulrender renonce à son finisher pour un coup massif." },
            new Entry { SpellIdValue = 14, SpellIdTech = "soulrender_curee",                ClassId = NymoraClass.Soulrender, Category = SpellCategory.Offensive, DisplayName = "Éventration",
                Description = "{DMG:220} dégâts + PLAIE OUVERTE : 50/tour pendant 3 tours.\n1×/tour.",
                LoreFlavor  = "Le tout ou rien. Curée est une lecture pure : si tu calcules juste, le match s'enchaîne. Si tu calcules mal, tu donnes un tempo entier à l'adversaire." },
            new Entry { SpellIdValue = 15, SpellIdTech = "soulrender_pacte_de_sang",        ClassId = NymoraClass.Soulrender, Category = SpellCategory.Tactical,  DisplayName = "Pacte de Sang",
                Description = "Subis 80 dégâts, gagne +2 HG.\nProchain sort offensif ce tour : +25% dégâts.\n1×/match.",
                LoreFlavor  = "Le bouton clutch. Quand l'adversaire pense être safe, le Soulrender saigne lui-même pour ouvrir une fenêtre de burst. Décision à très haut risque." },
            new Entry { SpellIdValue = 16, SpellIdTech = "soulrender_marque_de_carnage",    ClassId = NymoraClass.Soulrender, Category = SpellCategory.Tactical,  DisplayName = "Marque de Carnage",
                Description = "Marque la cible 3 tours.\nTes sorts sur elle : +1 HG.\n1×/tour.",
                LoreFlavor  = "Le sceau. Marque de Carnage transforme une cible en machine à fabriquer de la ressource. Plus l'adversaire reçoit de coups, plus le Soulrender accélère." },
            new Entry { SpellIdValue = 17, SpellIdTech = "soulrender_empoignade",           ClassId = NymoraClass.Soulrender, Category = SpellCategory.Tactical,  DisplayName = "Empoignade",
                Description = "En ligne droite. 90 dégâts.\nTire la cible au contact + -2 PM (prochain tour).\n1×/tour.",
                LoreFlavor  = "L'arrachement. Empoignade défait la map des classes-kite. Une Nightseer qui pensait son setup safe se retrouve au corps à corps, son Évanescence verrouillée." },
            new Entry { SpellIdValue = 18, SpellIdTech = "soulrender_rugissement",          ClassId = NymoraClass.Soulrender, Category = SpellCategory.Tactical,  DisplayName = "Rugissement",
                Description = "AoE rayon 3. Ennemis : -1 PM + pas de téléport au tour suivant.\nCible sous 50% PV : -2 PM.\n1×/tour, relance 2 tours.",
                LoreFlavor  = "Le cri primal. Rugissement ne tue pas — il fige. Combiné à Charge Brutale derrière, c'est un piège géométrique. Anti-Ghostra par excellence." },
            new Entry { SpellIdValue = 19, SpellIdTech = "soulrender_rage_insatiable",      ClassId = NymoraClass.Soulrender, Category = SpellCategory.Tactical,  DisplayName = "Frénésie",
                Description = "2 tours : tes sorts offensifs gagnent +5% dégâts et +1 HG.\nRecast = rafraîchit (pas de cumul).",
                LoreFlavor  = "L'emballement. Frénésie transforme chaque coup en carburant : plus le Soulrender frappe, plus sa jauge monte, plus ça fait mal." },
            new Entry { SpellIdValue = 20, SpellIdTech = "soulrender_riposte_carmin",       ClassId = NymoraClass.Soulrender, Category = SpellCategory.Survival,  DisplayName = "Riposte Carmin",
                Description = "1 tour : chaque attaque mêlée subie renvoie 100 dégâts + -1 PM à l'attaquant.",
                LoreFlavor  = "Le piège du chasseur. Riposte Carmin n'est pas une défense — c'est une invitation. Elle dit à l'adversaire : 'Viens me frapper.'" },
            new Entry { SpellIdValue = 21, SpellIdTech = "soulrender_cauterisation",        ClassId = NymoraClass.Soulrender, Category = SpellCategory.Survival,  DisplayName = "Sang Bouillant",
                Description = "2 tours : à chaque dégât subi, +1 HG et prochaine frappe +15 dégâts.\nRecast = rafraîchit (pas de cumul).",
                LoreFlavor  = "Le sang qui bout. Plus on le frappe, plus il accumule de rage et de hémoglyphe — chaque coup reçu nourrit le prochain coup rendu." },
            new Entry { SpellIdValue = 22, SpellIdTech = "soulrender_peau_de_fer",          ClassId = NymoraClass.Soulrender, Category = SpellCategory.Survival,  DisplayName = "Peau de Fer",
                Description = "Bouclier 200 HP, 2 tours.\nPendant ce temps : sorts mêlée +10 dégâts.\nRelance 2 tours.",
                LoreFlavor  = "Le mur viandard. Peau de Fer ne fait pas que protéger — elle ENCOURAGE l'engagement. Anti-Colossar/Nightseer qui zone à distance." },
            new Entry { SpellIdValue = 23, SpellIdTech = "soulrender_seve_vive",            ClassId = NymoraClass.Soulrender, Category = SpellCategory.Survival,  DisplayName = "Sève Vive",
                Description = "Soigne 100 HP (160 avec 1 HG).\nSi tu saignes : +50 HP.\n1×/tour.",
                LoreFlavor  = "Le rapide. Sève Vive est le micro-heal qui maintient le Soulrender en vie sans qu'il quitte le combat." },
            new Entry { SpellIdValue = 24, SpellIdTech = "soulrender_dernier_souffle",      ClassId = NymoraClass.Soulrender, Category = SpellCategory.Survival,  DisplayName = "Dernier Souffle",
                Description = "Soigne 200 HP + gagne 3 HG.\nSous 50% PV · 1×/match.",
                LoreFlavor  = "L'ultime. Dernier Souffle n'est pas un heal — c'est une renaissance. Le Soulrender qui aurait dû mourir au tour 5 revient à 50% HP avec 3 HG en main, prêt pour un Âme Lacérée." },
            new Entry { SpellIdValue = 25, SpellIdTech = "soulrender_ame_laceree",          ClassId = NymoraClass.Soulrender, Category = SpellCategory.Signature, DisplayName = "Âme Lacérée",
                Description = "{DMG:320} dégâts. Soigne 50% des dégâts infligés.\nSi le coup tue : Sang Coagulé en croix 5 cases.",
                LoreFlavor  = "L'exécution rituelle. Âme Lacérée n'est pas un simple finisher — c'est l'aboutissement d'un cycle. Le Soulrender a saigné, fait saigner, accumulé. Maintenant il récolte." },

            // ===== NIGHTSEER (30-45) =====
            new Entry { SpellIdValue = 30, SpellIdTech = "nightseer_tir_precis",            ClassId = NymoraClass.Nightseer, Category = SpellCategory.Offensive, DisplayName = "Tir Précis",
                Description = "{DMG:150} dégâts.\nCible Traqué : 210 dégâts.\n1×/tour.",
                LoreFlavor  = "Le sniper. Tir Précis n'a pas besoin de surprendre — sa simple existence à 4 cases force l'adversaire à toujours regarder en l'air." },
            new Entry { SpellIdValue = 31, SpellIdTech = "nightseer_volee_epines",          ClassId = NymoraClass.Nightseer, Category = SpellCategory.Offensive, DisplayName = "Volée d'Épines",
                Description = "Tir en ligne. {DMG:130} dégâts à toutes les cibles.\nPose un Filet de Ronces derrière la dernière cible (disparaît après 6 tours).\n1×/tour.",
                LoreFlavor  = "Le double effet. Volée d'Épines fait des dégâts ET pose un piège. L'adversaire qui survit doit décider : foncer dans le filet ou contourner et perdre du tempo." },
            new Entry { SpellIdValue = 32, SpellIdTech = "nightseer_detonation_onirique",   ClassId = NymoraClass.Nightseer, Category = SpellCategory.Offensive, DisplayName = "Détonation Onirique",
                Description = "Croix 5 cases. {DMG:170} dégâts.\nDéclenche tes pièges sous la zone : +30 dégâts, TRAQUÉ et +1 PR par piège.",
                LoreFlavor  = "L'œil qui frappe à travers le brouillard. Détonation Onirique punit la lecture. Si l'adversaire pensait être hors de portée, il ne l'était pas — le Nightseer voyait à travers." },
            new Entry { SpellIdValue = 33, SpellIdTech = "nightseer_frappe_ombre",          ClassId = NymoraClass.Nightseer, Category = SpellCategory.Offensive, DisplayName = "Frappe de l'Ombre",
                Description = "{DMG:160} dégâts.\nCible TRAQUÉ : +120 dégâts (280) et consomme TRAQUÉ.",
                LoreFlavor  = "L'exécution. Frappe de l'Ombre est le couperet du chasseur : elle ne sert qu'à achever une proie déjà marquée. Tout le setup converge ici." },
            new Entry { SpellIdValue = 34, SpellIdTech = "nightseer_salve_mortelle",        ClassId = NymoraClass.Nightseer, Category = SpellCategory.Offensive, DisplayName = "Salve Mortelle",
                Description = "Carré 3×3 : {DMG:160} dégâts au centre, 90 autour.\nChaque piège sous la zone : +40 dégâts (non consommé).\nCoûte 3 PR · relance 2 tours.",
                LoreFlavor  = "Le moment où la map révèle sa vérité. Salve Mortelle déchire toutes les illusions du Nightseer en même temps." },
            new Entry { SpellIdValue = 35, SpellIdTech = "nightseer_marque_chasseur",       ClassId = NymoraClass.Nightseer, Category = SpellCategory.Tactical,  DisplayName = "Affût",
                Description = "2 tours : +2 portée et +10% dégâts sur tes sorts à distance.\n+1 PR.\nRelance 3 tours · indisponible au tour 1.",
                LoreFlavor  = "Se poster. L'Affût n'est pas une attaque — c'est l'instant où le chasseur s'immobilise, mesure le vent et la distance. Ce qui suit ne rate pas." },
            new Entry { SpellIdValue = 36, SpellIdTech = "nightseer_filet_ronces",          ClassId = NymoraClass.Nightseer, Category = SpellCategory.Tactical,  DisplayName = "Filet de Ronces",
                Description = "Pose une embûche (invisible en phase 3).\nEnnemi qui entre : 100 dégâts, -1 PM, TRAQUÉ.\nDisparaît après 6 tours.\n1×/tour.",
                LoreFlavor  = "Le piège classique, mais ré-ingéniéré. Le Filet est lisible pour le Nightseer — il SAIT où il l'a posé. Il l'utilise pour pousser l'adversaire ailleurs." },
            new Entry { SpellIdValue = 37, SpellIdTech = "nightseer_champ_mines",           ClassId = NymoraClass.Nightseer, Category = SpellCategory.Tactical,  DisplayName = "Champ de Mines",
                Description = "Pose 3 mines en zone 3×3 (invisibles en phase 3).\nChaque mine : 70 dégâts + TRAQUÉ.\nDisparaissent après 6 tours.\n1×/tour.",
                LoreFlavor  = "Le terrain miné. Champ de Mines transforme une zone en no-go. L'adversaire doit faire un détour OU absorber 3 mines pour passer." },
            new Entry { SpellIdValue = 38, SpellIdTech = "nightseer_bourrasque",            ClassId = NymoraClass.Nightseer, Category = SpellCategory.Tactical,  DisplayName = "Bourrasque",
                Description = "Pousse la cible 2 cases (2e clic = direction).\nSi elle finit sur un piège/Sang Coagulé : déclenché.\n2×/tour.",
                LoreFlavor  = "L'arme du conducteur. Bourrasque n'est pas une frappe — c'est un volant. Le Nightseer décide où l'adversaire VA, pas où il EST." },
            new Entry { SpellIdValue = 39, SpellIdTech = "nightseer_souffle_glacial",       ClassId = NymoraClass.Nightseer, Category = SpellCategory.Tactical,  DisplayName = "Piège Bondissant",
                Description = "Pose un piège-catapulte (2e clic = direction d'éjection).\nL'ennemi qui entre est éjecté de 3 cases + TRAQUÉ.\nDisparaît après 6 tours.\n1×/tour.",
                LoreFlavor  = "Le tremplin piégé. Le Nightseer ne repousse pas l'ennemi — il le PROJETTE là où il l'a décidé : dans un autre piège, un coin, ou hors de portée." },
            new Entry { SpellIdValue = 40, SpellIdTech = "nightseer_voile_ombre",           ClassId = NymoraClass.Nightseer, Category = SpellCategory.Survival,  DisplayName = "Repli Épineux",
                Description = "Repousse de 2 cases tous les ennemis adjacents.\nSoigne 100 HP.\nRelance 2 tours.",
                LoreFlavor  = "Le décrochage. Quand l'étau se referme, le Nightseer explose en ronces : l'ennemi recule, et l'archer retrouve l'air dont il a besoin pour rouvrir l'angle." },
            new Entry { SpellIdValue = 41, SpellIdTech = "nightseer_pas_furtif",            ClassId = NymoraClass.Nightseer, Category = SpellCategory.Survival,  DisplayName = "Pas Furtif",
                Description = "Téléporte jusqu'à 3 cases.\nAvec 1 PR : pose un Filet de Ronces sur la case quittée (6 tours).\n1×/tour, relance 1 tour · indisponible au tour 1.",
                LoreFlavor  = "Le coup le plus frustrant pour l'adversaire. Le Nightseer disparaît littéralement. L'adversaire doit deviner où il est." },
            new Entry { SpellIdValue = 42, SpellIdTech = "nightseer_camouflage_ronces",     ClassId = NymoraClass.Nightseer, Category = SpellCategory.Survival,  DisplayName = "Camouflage de Ronces",
                Description = "Bouclier 130 HP, 2 tours.\nPendant ce temps : ennemi adjacent en fin de tour subit 70 dégâts + TRAQUÉ.",
                LoreFlavor  = "L'épine défensive. Camouflage Ronces dit à l'adversaire : 'Approche-toi, vois ce qui se passe.' Anti-engage parfait contre Soulrender et Ghostra mêlée." },
            new Entry { SpellIdValue = 43, SpellIdTech = "nightseer_seve_sauvage",          ClassId = NymoraClass.Nightseer, Category = SpellCategory.Survival,  DisplayName = "Sève Sauvage",
                Description = "Soigne 130 HP.\nSi un de tes pièges s'est déclenché (ce tour ou le précédent) : +60 HP.",
                LoreFlavor  = "Le heal de récolte. Sève Sauvage récompense le Nightseer qui a déjà fait son setup. Plus la map est piégée, plus il survit." },
            new Entry { SpellIdValue = 44, SpellIdTech = "nightseer_evanescence",           ClassId = NymoraClass.Nightseer, Category = SpellCategory.Survival,  DisplayName = "Évanescence",
                Description = "Téléporte jusqu'à 7 cases. Soigne 150 HP.\nPose un Filet de Ronces sur la case quittée (6 tours).\nSous 50% PV · 1×/match.",
                LoreFlavor  = "L'évasion totale. Évanescence permet au Nightseer de quitter complètement le combat le temps d'un tour." },
            new Entry { SpellIdValue = 45, SpellIdTech = "nightseer_traquenard",            ClassId = NymoraClass.Nightseer, Category = SpellCategory.Signature, DisplayName = "Traquenard",
                Description = "280 dégâts. Pousse la cible 2 cases (2e clic) ; le NS prend sa case d'origine.\nPARALYSIE : -2 PM, -2 PA (prochain tour).\nCible TRAQUÉ : +80 dégâts et +2 PR.\nCoûte 5 PR (jauge pleine).",
                LoreFlavor  = "L'embuscade pure. Traquenard n'est pas un finisher de DPS — c'est l'aboutissement d'un piège mental. La paralysie verrouille le tour adverse, le Nightseer peut décrocher ou enchaîner." },

            // ===== COLOSSAR (50-65) =====
            new Entry { SpellIdValue = 50, SpellIdTech = "colossar_frappe_lourde",          ClassId = NymoraClass.Colossar, Category = SpellCategory.Offensive, DisplayName = "Frappe Lourde",
                Description = "180 dégâts.\nCible épinglée (contre un mur/Pilier/bord) : 280 dégâts.\n2×/tour.",
                LoreFlavor  = "Le coup signature. La cible doit littéralement éviter d'avoir un mur derrière elle pour exister. Le Colossar transforme les bords de map en pièges." },
            new Entry { SpellIdValue = 51, SpellIdTech = "colossar_onde_de_choc",           ClassId = NymoraClass.Colossar, Category = SpellCategory.Offensive, DisplayName = "Onde de Choc",
                Description = "AoE adjacente : 80 dégâts + pousse de 2 cases.\nPoussée contre un mur/Pilier/bord : +80 dégâts + TRAUMA (-1 PM, -1 PA, 1 tour).\n2×/tour.",
                LoreFlavor  = "Le sort qui transforme un Pilier en arme. Sans Onde, un Pilier est juste décoratif. Avec, c'est un mur sur lequel l'adversaire va s'écraser." },
            new Entry { SpellIdValue = 52, SpellIdTech = "colossar_marteau_punisseur",      ClassId = NymoraClass.Colossar, Category = SpellCategory.Offensive, DisplayName = "Marteau Punisseur",
                Description = "160 dégâts.\nCible sous 4 PA (a déjà agi) : 240 dégâts + TRAUMA (-2 PA prochain tour).",
                LoreFlavor  = "L'anti-tempo. Marteau Punisseur punit les classes qui spam — Soulrender, Necram. Le Colossar dit : 'Tu as fini ton tour ? Tant mieux. Maintenant tu prends.'" },
            new Entry { SpellIdValue = 53, SpellIdTech = "colossar_choc_sismique",          ClassId = NymoraClass.Colossar, Category = SpellCategory.Offensive, DisplayName = "Choc Sismique",
                Description = "Frappe en ligne (4 cases). 130 dégâts à toutes les cibles.\n-1 PM au prochain tour.\nTraverse tes Piliers/Murs : +50 dégâts à la cible suivante.",
                LoreFlavor  = "L'onde tellurique. Choc Sismique passe à travers ses propres murs comme un piston. Le Colossar tire à travers ses fortifications — il est le seul." },
            new Entry { SpellIdValue = 54, SpellIdTech = "colossar_represailles",           ClassId = NymoraClass.Colossar, Category = SpellCategory.Survival, DisplayName = "Représailles",
                Description = "Soigne 200 HP.\n1 tour : renvoie 50 dégâts/attaque mêlée subie (max 2).\nSous 50% PV · 1×/match.",
                LoreFlavor  = "Le sursaut du colosse. Acculé, blessé, le Colossar puise dans sa rage une dernière vague de vigueur — et qui le frappe encore se blesse sur sa carapace." },
            new Entry { SpellIdValue = 55, SpellIdTech = "colossar_pilier",                 ClassId = NymoraClass.Colossar, Category = SpellCategory.Tactical,  DisplayName = "Pilier",
                Description = "Pose un Pilier (200 HP, bloque mouvement et lignes de vue/tir).\nReste jusqu'à destruction. +1 FD.\nMax 6 cases d'obstacle : au-delà, le plus ancien (n°1) est détruit.",
                LoreFlavor  = "L'outil. À lui seul, Pilier ne menace personne. En combinaison avec push/pull, il devient un instrument de meurtre." },
            new Entry { SpellIdValue = 56, SpellIdTech = "colossar_mur_de_pierre",          ClassId = NymoraClass.Colossar, Category = SpellCategory.Tactical,  DisplayName = "Mur de Pierre",
                Description = "Mur de 3 cases (5 avec 1 FD). Bloque tout : mouvement, ciblage, lignes de tir.\nReste jusqu'à destruction. +2 FD.\nChaque case compte dans le max 6 (le plus ancien détruit au-delà).\n1×/tour, relance 2 tours.",
                LoreFlavor  = "Le grand séparateur. Un Mur bien posé peut couper la map en deux et forcer l'adversaire à choisir : il fait demi-tour ou il détruit le mur." },
            new Entry { SpellIdValue = 57, SpellIdTech = "colossar_ancrage",                ClassId = NymoraClass.Colossar, Category = SpellCategory.Tactical,  DisplayName = "Ancrage",
                Description = "Cible : -1 PM pendant 2 tours.\nNe peut pas être déplacée (push/pull/téléport) au prochain tour.\n1×/tour, relance 3 tours.",
                LoreFlavor  = "Le gel. Ancrage est l'anti-mobilité ultime. Une Ghostra ancrée ne peut plus se téléporter. C'est un sort qui DÉSACTIVE des kits entiers." },
            new Entry { SpellIdValue = 58, SpellIdTech = "colossar_provocation",            ClassId = NymoraClass.Colossar, Category = SpellCategory.Tactical,  DisplayName = "Provocation",
                Description = "1 tour : tous les sorts de la cible coûtent +1 PA.\nSi elle n'est pas adjacente au Colossar en fin de tour : 100 dégâts.\nRelance 2 tours.",
                LoreFlavor  = "L'humiliation. Provocation force l'adversaire à venir au Colossar — qui l'attend avec Représailles posé. Le Colossar dicte les engagements directement par sort." },
            new Entry { SpellIdValue = 59, SpellIdTech = "colossar_brisure",                ClassId = NymoraClass.Colossar, Category = SpellCategory.Tactical,  DisplayName = "Brisure",
                Description = "90 dégâts. Retire un buff/bouclier de la cible.\nSi elle n'en a pas : TRAUMA (-2 PA prochain tour).",
                LoreFlavor  = "Le briseur de mur. Brisure est l'anti-tank, l'anti-tortue. Aucune classe ne peut se reposer derrière un bouclier face au Colossar — il les casse explicitement." },
            new Entry { SpellIdValue = 60, SpellIdTech = "colossar_stoicisme",              ClassId = NymoraClass.Colossar, Category = SpellCategory.Survival,  DisplayName = "Stoïcisme",
                Description = "Bouclier 200 HP, 2 tours.\n1 tour : ne peut pas être déplacé (push/pull/téléport).\nSi le bouclier survit aux 2 tours : +80 HP.",
                LoreFlavor  = "Le rocher. Stoïcisme est le contraire d'un panic-button — c'est une déclaration. Le Colossar plante les pieds." },
            new Entry { SpellIdValue = 61, SpellIdTech = "colossar_garde_protectrice",      ClassId = NymoraClass.Colossar, Category = SpellCategory.Survival,  DisplayName = "Garde Protectrice",
                Description = "2 tours : -15% de dégâts subis (toutes sources).\nCumul avec Densité Inerte plafonné à -45%.",
                LoreFlavor  = "L'armure mobile. Garde Protectrice est le bouclier qui ne casse pas. Il n'a pas de HP — il a un timer. Le Colossar peut traverser une zone hostile sans se faire démolir." },
            new Entry { SpellIdValue = 62, SpellIdTech = "colossar_ressac_vital",           ClassId = NymoraClass.Colossar, Category = SpellCategory.Survival,  DisplayName = "Ressac Vital",
                Description = "Soigne 80 HP + 30 par attaque subie au tour précédent (max 200).",
                LoreFlavor  = "Le contre-tank. Ressac Vital récompense le Colossar qui s'est fait taper. Plus l'adversaire l'agresse, plus il se soigne. Anti-burst implacable." },
            new Entry { SpellIdValue = 63, SpellIdTech = "colossar_renvoi_bouclier",        ClassId = NymoraClass.Colossar, Category = SpellCategory.Survival,  DisplayName = "Renvoi du Bouclier",
                Description = "1 tour : chaque attaque subie (mêlée ou distance) renvoie 60 dégâts (max 2).\nRelance 1 tour.",
                LoreFlavor  = "Le miroir. Renvoi du Bouclier est l'anti-Nightseer — un sort à distance qui frappe le Colossar lui revient direct." },
            new Entry { SpellIdValue = 64, SpellIdTech = "colossar_soin_lourd",             ClassId = NymoraClass.Colossar, Category = SpellCategory.Offensive, DisplayName = "Éboulement",
                Description = "Détruit un de tes obstacles : 150 dégâts en zone (rayon 1) + pousse les ennemis.\nSi c'est un Pilier : +30 HP.\n1×/tour.",
                LoreFlavor  = "Le sacrifice de pierre. Le Colossar transforme sa propre fortification en bombe : la défense devient l'attaque." },
            new Entry { SpellIdValue = 65, SpellIdTech = "colossar_effondrement",           ClassId = NymoraClass.Colossar, Category = SpellCategory.Signature, DisplayName = "Effondrement",
                Description = "Rayon 2 : 200 dégâts + éjection des ennemis. Les cases deviennent impraticables 2 tours.\n2 tours : tes sorts coûtent -1 PA et -30% dégâts subis.\nCoûte 5 FD (jauge pleine) · relance 4 tours.",
                LoreFlavor  = "L'arme tellurique. Plus d'annonce : le sol cède SOUS l'ennemi à l'instant où le Colossar le décide. Pas de fenêtre pour fuir." },

            // ===== NECRAM (70-85) =====
            new Entry { SpellIdValue = 70, SpellIdTech = "necram_crachat_acide",            ClassId = NymoraClass.Necram, Category = SpellCategory.Offensive, DisplayName = "Crachat Acide",
                Description = "100 dégâts + 2 marques de venin.\nMax 4 marques par cible.",
                LoreFlavor  = "Le sort de base, mais redoutable. Crachat Acide combine dégâts directs et setup en 1 PA-efficace. C'est l'arme à 80% de l'utilisation Necram en early." },
            new Entry { SpellIdValue = 71, SpellIdTech = "necram_morsure_putride",          ClassId = NymoraClass.Necram, Category = SpellCategory.Offensive, DisplayName = "Morsure Putride",
                Description = "120 dégâts + 10 par marque sur la cible (max 160).\nSi la cible meurt : ses marques passent à l'ennemi le plus proche.",
                LoreFlavor  = "L'embrasement. Morsure Putride est le finisher qui propage. Tuer une cible avec elle ne stoppe pas le DoT — elle migre. Anti-team, mais aussi outil pour cycler en 1v1." },
            new Entry { SpellIdValue = 72, SpellIdTech = "necram_detonation_virulente",     ClassId = NymoraClass.Necram, Category = SpellCategory.Offensive, DisplayName = "Détonation Virulente",
                Description = "Inflige d'un coup tous les dégâts de venin de la cible (ignore boucliers et réductions).\nMarques non consommées.\n1×/tour.",
                LoreFlavor  = "Le détonateur. Plus besoin de tout dépenser d'un coup : le Necram fait pulser son poison à la demande, tour après tour." },
            new Entry { SpellIdValue = 73, SpellIdTech = "necram_faux_decharnee",           ClassId = NymoraClass.Necram, Category = SpellCategory.Offensive, DisplayName = "Faux Décharnée",
                Description = "AoE autour du Necram : 130 dégâts.\nSoigne 30 HP par marque sur les cibles touchées (max 120).",
                LoreFlavor  = "Le moment où le mage devient bête. La Faux est anti-Soulrender, anti-Ghostra : si tu te rapproches du Necram, il en profite pour se soigner sur ton dos." },
            new Entry { SpellIdValue = 74, SpellIdTech = "necram_brume_toxique",            ClassId = NymoraClass.Necram, Category = SpellCategory.Offensive, DisplayName = "Brume Toxique",
                Description = "Zone toxique 3×3, 2 tours (superposable).\nEnnemi dans la zone : +1 marque ; -1 PM s'il y démarre son tour.\nTant qu'il y tient : dégâts de venin majorés (+10 par marque).\n1×/tour, relance 2 tours.",
                LoreFlavor  = "L'air vicié. Brume Toxique ne tue pas — elle CONDAMNE : rester dedans, c'est accélérer sa propre pourriture." },
            new Entry { SpellIdValue = 75, SpellIdTech = "necram_inoculation",              ClassId = NymoraClass.Necram, Category = SpellCategory.Tactical,  DisplayName = "Inoculation",
                Description = "30 dégâts + 2 marques de venin.\nMax 4 marques par cible.\n1×/tour.",
                LoreFlavor  = "Le baiser de la mort. Une morsure légère, puis le venin s'installe : l'adversaire qui prend 2 marques sait que les 3 prochains tours vont être un compte à rebours." },
            new Entry { SpellIdValue = 76, SpellIdTech = "necram_marque_sacrificielle",     ClassId = NymoraClass.Necram, Category = SpellCategory.Tactical,  DisplayName = "Marque Sacrificielle",
                Description = "3 tours : les marques de venin sur la cible infligent +20 dégâts/tour.",
                LoreFlavor  = "L'engrais. Marque Sacrificielle force l'adversaire à se soigner CONSTAMMENT. 70 dégâts de venin par tour ne pardonnent aucun délai." },
            new Entry { SpellIdValue = 77, SpellIdTech = "necram_symbiose_morbide",         ClassId = NymoraClass.Necram, Category = SpellCategory.Tactical,  DisplayName = "Symbiose Morbide",
                Description = "2 tours : quand ton venin inflige ses dégâts, soigne 15 HP.\nRecast = rafraîchit.",
                LoreFlavor  = "Le parasite. Symbiose transforme le Necram en machine à régen : chaque pulsation de poison le nourrit." },
            new Entry { SpellIdValue = 78, SpellIdTech = "necram_contagion",                ClassId = NymoraClass.Necram, Category = SpellCategory.Tactical,  DisplayName = "Contagion",
                Description = "2 tours : la cible prend +1 marque de venin en fin de chacun de ses tours.\n1×/tour.",
                LoreFlavor  = "L'épidémie. La cible devient son propre foyer d'infection : chaque tour qui passe la gangrène un peu plus, sans que le Necram lève le petit doigt." },
            new Entry { SpellIdValue = 79, SpellIdTech = "necram_pas_spectral",             ClassId = NymoraClass.Necram, Category = SpellCategory.Survival, DisplayName = "Échange Spectral",
                Description = "Échange ta place avec un ennemi + 80 dégâts.\n1×/tour.",
                LoreFlavor  = "Le pacte d'os. Le Necram et sa proie permutent dans un éclair spectral : il quitte le danger, elle le subit." },
            new Entry { SpellIdValue = 80, SpellIdTech = "necram_voile_pestilence",         ClassId = NymoraClass.Necram, Category = SpellCategory.Tactical,  DisplayName = "Nuée de Spores",
                Description = "2 tours : chacun de tes sorts sur un ennemi pose +1 marque bonus.\nRecast = rafraîchit (pas de cumul).",
                LoreFlavor  = "L'essaim. Le Necram s'enveloppe de spores : chaque sort qu'il lance ensemence un peu plus la chair de sa proie." },
            new Entry { SpellIdValue = 81, SpellIdTech = "necram_carapace_visqueuse",       ClassId = NymoraClass.Necram, Category = SpellCategory.Survival,  DisplayName = "Carapace Visqueuse",
                Description = "Bouclier 160 HP, 2 tours.\nAttaquant mêlée qui frappe : +1 marque.\nRelance 2 tours.",
                LoreFlavor  = "L'épine pourrie. Carapace Visqueuse n'est pas un mur — c'est un piège défensif. Frapper le Necram en mêlée = signer son arrêt de mort." },
            new Entry { SpellIdValue = 82, SpellIdTech = "necram_drain_vital",              ClassId = NymoraClass.Necram, Category = SpellCategory.Survival,  DisplayName = "Drain Vital",
                Description = "40 dégâts.\nSoigne 40 HP par marque sur la cible (max 160, non consommées).\n1×/tour.",
                LoreFlavor  = "Le siphon. Plus la cible est gangrenée, plus Drain Vital régénère : le poison devient une fontaine de vie." },
            new Entry { SpellIdValue = 83, SpellIdTech = "necram_regeneration_necrotique",  ClassId = NymoraClass.Necram, Category = SpellCategory.Survival,  DisplayName = "Régénération Nécrotique",
                Description = "Soigne 70 HP + 15 par marque ennemie dans rayon 4 (max +90).\nAvec 1 PT : +30 HP.",
                LoreFlavor  = "La récolte. Régénération Nécrotique scale avec le travail accompli. Plus de marques = plus de heal. C'est le heal qui dit : 'J'ai bien semé.'" },
            new Entry { SpellIdValue = 84, SpellIdTech = "necram_cocon_putride",            ClassId = NymoraClass.Necram, Category = SpellCategory.Survival,  DisplayName = "Cocon Putride",
                Description = "Soigne 220 HP + 1 marque à tous les ennemis dans rayon 4.\nSous 50% PV · 1×/match.",
                LoreFlavor  = "L'explosion fongique. Cocon Putride n'est pas qu'un panic-heal — c'est une aspersion. Le Necram à l'agonie devient soudain le Necram avec 6+ marques sur la map." },
            new Entry { SpellIdValue = 85, SpellIdTech = "necram_virus_fatal",              ClassId = NymoraClass.Necram, Category = SpellCategory.Signature, DisplayName = "Virus Fatal",
                Description = "Toutes les marques de la cible infligent leurs dégâts ×1,5 d'un coup (ignore boucliers et réductions).\nÀ 4 marques pleines : ~360 dégâts.\nMarques consommées si la cible survit.\nCoûte 6 PT (jauge pleine) · relance 4 tours.",
                LoreFlavor  = "L'apoptose. Virus Fatal est l'aboutissement absolu de la stratégie Necram : 4-5 tours de setup transformés en 1 tour de mort lente accélérée." },

            // ===== GHOSTRA (86-101). Volte-Face (90) reste class Tactique malgre amendement offensif 16 mai. =====
            new Entry { SpellIdValue = 86, SpellIdTech = "ghostra_lame_spectrale",          ClassId = NymoraClass.Ghostra, Category = SpellCategory.Offensive, DisplayName = "Lame Spectrale",
                Description = "130 dégâts.\nDos : +50 (Angle 2) / +80 (Angle 3).\nRetourne la cible dos à la Ghostra.",
                LoreFlavor  = "La frappe la plus banale du jeu — sauf que personne ne sait d'où elle vient. La banalité du sort est sa force : il sort de partout, depuis n'importe quel leurre." },
            new Entry { SpellIdValue = 87, SpellIdTech = "ghostra_lame_vorace_spectrale",   ClassId = NymoraClass.Ghostra, Category = SpellCategory.Offensive, DisplayName = "Lame Vorace Spectrale",
                Description = "110 dégâts + 60 si PLAIE OUVERTE (non consommée).\nDos : +bonus passif.\n2×/tour.",
                LoreFlavor  = "Le coup qui ronge. Lame Vorace empile sur une plaie ouverte sans la fermer. Légère et rapide, elle se relance dans le tour là où la Lame Spectrale frappe lourd une seule fois." },
            new Entry { SpellIdValue = 88, SpellIdTech = "ghostra_replique_fantome",        ClassId = NymoraClass.Ghostra, Category = SpellCategory.Tactical,  DisplayName = "Réplique Fantôme",
                Description = "Pose un leurre (100 HP, identique à la Ghostra), 4 tours.\nSurvit la durée : +80 HP. Détruit par l'adversaire : +40 HP.\n1×/tour.",
                LoreFlavor  = "Le clone qui paye les frais. Réplique Fantôme FORCE l'adversaire à choisir : 'je frappe ce qui ressemble à la Ghostra ?' Toute lecture coûte. La Ghostra gagne quoi qu'il arrive." },
            new Entry { SpellIdValue = 89, SpellIdTech = "ghostra_pas_dans_ombre",          ClassId = NymoraClass.Ghostra, Category = SpellCategory.Tactical,  DisplayName = "Pas dans l'Ombre",
                Description = "Téléporte jusqu'à 4 cases.\nLes ennemis adjacents à l'arrivée tournent le dos à la Ghostra.\nLaisse un leurre sur la case quittée (cap 3).\n1×/tour, relance 2 tours · indisponible au tour 1.",
                LoreFlavor  = "Le saut de l'absent. Pas dans l'Ombre n'est pas seulement une mobilité — c'est un GÉNÉRATEUR de leurre." },
            new Entry { SpellIdValue = 90, SpellIdTech = "ghostra_permutation",            ClassId = NymoraClass.Ghostra, Category = SpellCategory.Tactical,  DisplayName = "Permutation",
                Description = "Échange ta place avec un de tes leurres (invisible côté adversaire).\nÀ 3 leurres (Angle 3) : coûte 0 PA.\n1×/tour.",
                LoreFlavor  = "Le pas de côté du fantôme. La Ghostra n'est jamais où on la frappe — elle était déjà l'autre silhouette." },
            new Entry { SpellIdValue = 91, SpellIdTech = "ghostra_saigne_ame",              ClassId = NymoraClass.Ghostra, Category = SpellCategory.Offensive, DisplayName = "Saigne-Âme",
                Description = "200 dégâts + 70 si PLAIE OUVERTE (consommée).\nSi le coup tue : +60 HP.",
                LoreFlavor  = "L'aboutissement. Saigne-Âme consomme la plaie pour un payoff massif. Le sort de fin du combo Plaie Ouverte → Lame Vorace → Saigne-Âme." },
            new Entry { SpellIdValue = 92, SpellIdTech = "ghostra_frappe_fantome",          ClassId = NymoraClass.Ghostra, Category = SpellCategory.Offensive, DisplayName = "Frappe Fantôme",
                Description = "Téléporte au contact de la cible + 200 dégâts.\nDos : +bonus passif.\nSi la cible a été retournée ce tour : PLAIE OUVERTE (40/tour, 2 tours).",
                LoreFlavor  = "Le finisseur. Frappe Fantôme arrive de nulle part. Combinée à un retournement de la cible, c'est un combo qui peut shred 350+ HP en un tour." },
            new Entry { SpellIdValue = 93, SpellIdTech = "ghostra_eveil_spectral",          ClassId = NymoraClass.Ghostra, Category = SpellCategory.Tactical,  DisplayName = "Éveil Spectral",
                Description = "Un de tes leurres se place dans le dos de la cible et la poignarde : 100 dégâts.\nDorsal/PLAIE OUVERTE calculés depuis le leurre. Non consommé.\nRequiert 1 leurre + une case libre adjacente.\n2×/tour.",
                LoreFlavor  = "L'illusion qui mord. Un leurre n'est plus seulement un decoy : il se réveille et plante sa lame. La cible ne sait jamais quelle silhouette va frapper." },
            new Entry { SpellIdValue = 94, SpellIdTech = "ghostra_marque_ombre",            ClassId = NymoraClass.Ghostra, Category = SpellCategory.Tactical,  DisplayName = "Marque de l'Ombre",
                Description = "2 tours : tes sorts sur la cible gagnent +20 dégâts.\nHit dorsal pendant la durée : PLAIE OUVERTE automatique.\n1×/tour.",
                LoreFlavor  = "Le sceau. Marque de l'Ombre pré-charge une cible. La Ghostra peut ensuite alterner Réplique → permutation → Lame Spectrale dorsal et l'effet plaie est garanti. Anti-tank par contournement." },
            new Entry { SpellIdValue = 95, SpellIdTech = "ghostra_nuee_spectrale",          ClassId = NymoraClass.Ghostra, Category = SpellCategory.Offensive, DisplayName = "Nuée Spectrale",
                Description = "100 dégâts + 40 par leurre actif + 20 par leurre adjacent à la cible (max 280).\nNe consomme aucun leurre.\n1×/tour.",
                LoreFlavor  = "L'apocalypse en miniature. Plus la Ghostra a semé de silhouettes, plus la nuée déchire. Toutes les lames frappent au même instant." },
            new Entry { SpellIdValue = 96, SpellIdTech = "ghostra_linceul_ombres",          ClassId = NymoraClass.Ghostra, Category = SpellCategory.Survival,  DisplayName = "Linceul d'Ombres",
                Description = "Bouclier 130 HP, 2 tours.\nChaque attaque mêlée subie renvoie 40 dégâts.\n1×/tour.",
                LoreFlavor  = "Le suaire. Linceul d'Ombres est le bouclier qui mord. Anti-Soulrender qui charge." },
            new Entry { SpellIdValue = 97, SpellIdTech = "ghostra_voile_spectral",          ClassId = NymoraClass.Ghostra, Category = SpellCategory.Survival,  DisplayName = "Voile Spectral",
                Description = "Téléporte tous tes leurres autour de la cible.\nChaque leurre adjacent lui inflige 60 dégâts (max 180).\nRequiert 1 leurre.\n1×/tour.",
                LoreFlavor  = "Le voile se resserre. D'un geste, la Ghostra rappelle toutes ses silhouettes autour de sa proie — le piège se referme avant la première lame." },
            new Entry { SpellIdValue = 98, SpellIdTech = "ghostra_replique_protectrice",    ClassId = NymoraClass.Ghostra, Category = SpellCategory.Survival,  DisplayName = "Réplique Protectrice",
                Description = "Pose un leurre protecteur (200 HP) : redirige 30% des dégâts subis, 4 tours.\nS'il est détruit : +80 HP.\nCompte dans le cap 3 leurres.\n1×/tour.",
                LoreFlavor  = "Le clone-bouclier. Réplique Protectrice n'est pas un leurre offensif — c'est un sustain caché. Elle prolonge la vie de la Ghostra de 1-2 tours." },
            new Entry { SpellIdValue = 99, SpellIdTech = "ghostra_dernier_pas",             ClassId = NymoraClass.Ghostra, Category = SpellCategory.Survival,  DisplayName = "Dernier Pas",
                Description = "Soigne 200 HP. Téléporte jusqu'à 5 cases.\nPose un leurre sur la case quittée.\nSous 50% PV · 1×/match.",
                LoreFlavor  = "L'évasion finale. Dernier Pas n'est pas qu'un heal — c'est un tour offert. La Ghostra à 200 HP se retrouve à 50% HP, à 5 cases de l'engagement, avec un leurre fraîchement posé." },
            new Entry { SpellIdValue = 100, SpellIdTech = "ghostra_communion_spectrale",    ClassId = NymoraClass.Ghostra, Category = SpellCategory.Survival,  DisplayName = "Communion Spectrale",
                Description = "Absorbe un de tes leurres et soigne 150 HP.\nRequiert 1 leurre.\n1×/tour.",
                LoreFlavor  = "Le retour à soi. La Ghostra rappelle l'une de ses silhouettes et se nourrit de sa propre illusion pour recoller ses plaies." },
            new Entry { SpellIdValue = 101, SpellIdTech = "ghostra_execution_spectrale",    ClassId = NymoraClass.Ghostra, Category = SpellCategory.Signature, DisplayName = "Exécution Spectrale",
                Description = "Si la cible est dorsale : 350 dégâts + PLAIE OUVERTE (50/tour, 3 tours).\nSi le coup tue : +100 HP et 2 leurres réapparaissent.\nSi la cible n'est PAS dorsale : RATE (les 3 leurres sont quand même consommés).",
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
