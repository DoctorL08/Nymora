using System.Collections.Generic;
using System.IO;
using Nymora.Core.Data;
using Nymora.Core.ScriptableObjects;
using Quantum;
using UnityEditor;
using UnityEngine;
// Aliases : NymoraClass / TargetingFilter / TargetingShape / SpellCategory existent
// AUSSI dans Quantum (codegen) — on prend Nymora.Core.Enums comme source-of-truth UI.
using NymoraClass = Nymora.Core.Enums.NymoraClass;
using SpellCategory = Nymora.Core.Enums.SpellCategory;
using TargetingFilter = Nymora.Core.Enums.TargetingFilter;
using TargetingShape = Nymora.Core.Enums.TargetingShape;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Editor tool 5.3.b — Populate SpellCatalog.asset depuis SpellRegistry runtime
    /// (PA / Range / Filter / Shape / Damage) + mapping hardcode (DisplayName / Class /
    /// Category) Bible V7.1 patchee 17 mai 2026.
    ///
    /// Menu : Nymora &gt; Setup &gt; Populate Spell Catalog.
    ///
    /// Le tool est IDEMPOTENT : re-runnable, il met a jour les entries existantes
    /// au lieu de dupliquer. Conserve les descriptions textuelles deja remplies
    /// manuellement (champ Description et LoreFlavor).
    /// </summary>
    public static class PopulateSpellCatalog
    {
        private const string CATALOG_PATH = "Assets/_Nymora/ScriptableObjects/Spells/SpellCatalog.asset";

        // ------------------------------------------------------------------
        // Mapping canonique des 80 sorts (Bible V7.1 patchee 17 mai 2026).
        // Source : Spell.qtn enum SpellId + commentaires Bible-aligned.
        //
        // Format : (SpellId numeric, NymoraClass, SpellCategory, DisplayName Bible-friendly, SpellIdTechnique).
        // SpellIdTechnique sert de stable key (snake_case) pour le backend deck save.
        // ------------------------------------------------------------------
        private struct Mapping
        {
            public int SpellIdValue;
            public NymoraClass Class;
            public SpellCategory Category;
            public string DisplayName;
            public string SpellIdTech;
        }

        private static readonly List<Mapping> _mappings = new List<Mapping>
        {
            // SOULRENDER (10-25) : 5 offensifs / 5 tactiques / 5 survie / signature
            new Mapping { SpellIdValue = 10, Class = NymoraClass.Soulrender, Category = SpellCategory.Offensive, DisplayName = "Tranche-Âme",          SpellIdTech = "soulrender_tranche_ame" },
            new Mapping { SpellIdValue = 11, Class = NymoraClass.Soulrender, Category = SpellCategory.Offensive, DisplayName = "Ouvre-Plaie",            SpellIdTech = "soulrender_ouvre_plaie" },
            new Mapping { SpellIdValue = 12, Class = NymoraClass.Soulrender, Category = SpellCategory.Offensive, DisplayName = "Charge Brutale",         SpellIdTech = "soulrender_charge_brutale" },
            new Mapping { SpellIdValue = 13, Class = NymoraClass.Soulrender, Category = SpellCategory.Offensive, DisplayName = "Détonation Sanglante", SpellIdTech = "soulrender_detonation_sanglante" },
            new Mapping { SpellIdValue = 14, Class = NymoraClass.Soulrender, Category = SpellCategory.Offensive, DisplayName = "Curée",              SpellIdTech = "soulrender_curee" },
            new Mapping { SpellIdValue = 15, Class = NymoraClass.Soulrender, Category = SpellCategory.Tactical,  DisplayName = "Pacte de Sang",          SpellIdTech = "soulrender_pacte_de_sang" },
            new Mapping { SpellIdValue = 16, Class = NymoraClass.Soulrender, Category = SpellCategory.Tactical,  DisplayName = "Marque de Carnage",      SpellIdTech = "soulrender_marque_de_carnage" },
            new Mapping { SpellIdValue = 17, Class = NymoraClass.Soulrender, Category = SpellCategory.Tactical,  DisplayName = "Empoignade",             SpellIdTech = "soulrender_empoignade" },
            new Mapping { SpellIdValue = 18, Class = NymoraClass.Soulrender, Category = SpellCategory.Tactical,  DisplayName = "Rugissement",            SpellIdTech = "soulrender_rugissement" },
            new Mapping { SpellIdValue = 19, Class = NymoraClass.Soulrender, Category = SpellCategory.Tactical,  DisplayName = "Rage Insatiable",        SpellIdTech = "soulrender_rage_insatiable" },
            new Mapping { SpellIdValue = 20, Class = NymoraClass.Soulrender, Category = SpellCategory.Survival,  DisplayName = "Riposte Carmin",         SpellIdTech = "soulrender_riposte_carmin" },
            new Mapping { SpellIdValue = 21, Class = NymoraClass.Soulrender, Category = SpellCategory.Survival,  DisplayName = "Cautérisation",     SpellIdTech = "soulrender_cauterisation" },
            new Mapping { SpellIdValue = 22, Class = NymoraClass.Soulrender, Category = SpellCategory.Survival,  DisplayName = "Peau de Fer",            SpellIdTech = "soulrender_peau_de_fer" },
            new Mapping { SpellIdValue = 23, Class = NymoraClass.Soulrender, Category = SpellCategory.Survival,  DisplayName = "Sève Vive",          SpellIdTech = "soulrender_seve_vive" },
            new Mapping { SpellIdValue = 24, Class = NymoraClass.Soulrender, Category = SpellCategory.Survival,  DisplayName = "Dernier Souffle",        SpellIdTech = "soulrender_dernier_souffle" },
            new Mapping { SpellIdValue = 25, Class = NymoraClass.Soulrender, Category = SpellCategory.Signature, DisplayName = "Âme Lacérée", SpellIdTech = "soulrender_ame_laceree" },

            // NIGHTSEER (30-45)
            new Mapping { SpellIdValue = 30, Class = NymoraClass.Nightseer, Category = SpellCategory.Offensive, DisplayName = "Tir Précis",          SpellIdTech = "nightseer_tir_precis" },
            new Mapping { SpellIdValue = 31, Class = NymoraClass.Nightseer, Category = SpellCategory.Offensive, DisplayName = "Volée d'Épines", SpellIdTech = "nightseer_volee_epines" },
            new Mapping { SpellIdValue = 32, Class = NymoraClass.Nightseer, Category = SpellCategory.Offensive, DisplayName = "Détonation Onirique", SpellIdTech = "nightseer_detonation_onirique" },
            new Mapping { SpellIdValue = 33, Class = NymoraClass.Nightseer, Category = SpellCategory.Offensive, DisplayName = "Frappe de l'Ombre",        SpellIdTech = "nightseer_frappe_ombre" },
            new Mapping { SpellIdValue = 34, Class = NymoraClass.Nightseer, Category = SpellCategory.Offensive, DisplayName = "Salve Mortelle",           SpellIdTech = "nightseer_salve_mortelle" },
            new Mapping { SpellIdValue = 35, Class = NymoraClass.Nightseer, Category = SpellCategory.Tactical,  DisplayName = "Marque du Chasseur",       SpellIdTech = "nightseer_marque_chasseur" },
            new Mapping { SpellIdValue = 36, Class = NymoraClass.Nightseer, Category = SpellCategory.Tactical,  DisplayName = "Filet de Ronces",          SpellIdTech = "nightseer_filet_ronces" },
            new Mapping { SpellIdValue = 37, Class = NymoraClass.Nightseer, Category = SpellCategory.Tactical,  DisplayName = "Champ de Mines",           SpellIdTech = "nightseer_champ_mines" },
            new Mapping { SpellIdValue = 38, Class = NymoraClass.Nightseer, Category = SpellCategory.Tactical,  DisplayName = "Bourrasque",               SpellIdTech = "nightseer_bourrasque" },
            new Mapping { SpellIdValue = 39, Class = NymoraClass.Nightseer, Category = SpellCategory.Tactical,  DisplayName = "Souffle Glacial",          SpellIdTech = "nightseer_souffle_glacial" },
            new Mapping { SpellIdValue = 40, Class = NymoraClass.Nightseer, Category = SpellCategory.Survival,  DisplayName = "Voile d'Ombre",            SpellIdTech = "nightseer_voile_ombre" },
            new Mapping { SpellIdValue = 41, Class = NymoraClass.Nightseer, Category = SpellCategory.Survival,  DisplayName = "Pas Furtif",               SpellIdTech = "nightseer_pas_furtif" },
            new Mapping { SpellIdValue = 42, Class = NymoraClass.Nightseer, Category = SpellCategory.Survival,  DisplayName = "Camouflage de Ronces",     SpellIdTech = "nightseer_camouflage_ronces" },
            new Mapping { SpellIdValue = 43, Class = NymoraClass.Nightseer, Category = SpellCategory.Survival,  DisplayName = "Sève Sauvage",         SpellIdTech = "nightseer_seve_sauvage" },
            new Mapping { SpellIdValue = 44, Class = NymoraClass.Nightseer, Category = SpellCategory.Survival,  DisplayName = "Évanescence",          SpellIdTech = "nightseer_evanescence" },
            new Mapping { SpellIdValue = 45, Class = NymoraClass.Nightseer, Category = SpellCategory.Signature, DisplayName = "Traquenard",               SpellIdTech = "nightseer_traquenard" },

            // COLOSSAR (50-65)
            new Mapping { SpellIdValue = 50, Class = NymoraClass.Colossar, Category = SpellCategory.Offensive, DisplayName = "Frappe Lourde",            SpellIdTech = "colossar_frappe_lourde" },
            new Mapping { SpellIdValue = 51, Class = NymoraClass.Colossar, Category = SpellCategory.Offensive, DisplayName = "Onde de Choc",             SpellIdTech = "colossar_onde_de_choc" },
            new Mapping { SpellIdValue = 52, Class = NymoraClass.Colossar, Category = SpellCategory.Offensive, DisplayName = "Marteau Punisseur",        SpellIdTech = "colossar_marteau_punisseur" },
            new Mapping { SpellIdValue = 53, Class = NymoraClass.Colossar, Category = SpellCategory.Offensive, DisplayName = "Choc Sismique",            SpellIdTech = "colossar_choc_sismique" },
            new Mapping { SpellIdValue = 54, Class = NymoraClass.Colossar, Category = SpellCategory.Offensive, DisplayName = "Représailles",         SpellIdTech = "colossar_represailles" },
            new Mapping { SpellIdValue = 55, Class = NymoraClass.Colossar, Category = SpellCategory.Tactical,  DisplayName = "Pilier",                   SpellIdTech = "colossar_pilier" },
            new Mapping { SpellIdValue = 56, Class = NymoraClass.Colossar, Category = SpellCategory.Tactical,  DisplayName = "Mur de Pierre",            SpellIdTech = "colossar_mur_de_pierre" },
            new Mapping { SpellIdValue = 57, Class = NymoraClass.Colossar, Category = SpellCategory.Tactical,  DisplayName = "Ancrage",                  SpellIdTech = "colossar_ancrage" },
            new Mapping { SpellIdValue = 58, Class = NymoraClass.Colossar, Category = SpellCategory.Tactical,  DisplayName = "Provocation",              SpellIdTech = "colossar_provocation" },
            new Mapping { SpellIdValue = 59, Class = NymoraClass.Colossar, Category = SpellCategory.Tactical,  DisplayName = "Brisure",                  SpellIdTech = "colossar_brisure" },
            new Mapping { SpellIdValue = 60, Class = NymoraClass.Colossar, Category = SpellCategory.Survival,  DisplayName = "Stoïcisme",            SpellIdTech = "colossar_stoicisme" },
            new Mapping { SpellIdValue = 61, Class = NymoraClass.Colossar, Category = SpellCategory.Survival,  DisplayName = "Garde Protectrice",        SpellIdTech = "colossar_garde_protectrice" },
            new Mapping { SpellIdValue = 62, Class = NymoraClass.Colossar, Category = SpellCategory.Survival,  DisplayName = "Ressac Vital",             SpellIdTech = "colossar_ressac_vital" },
            new Mapping { SpellIdValue = 63, Class = NymoraClass.Colossar, Category = SpellCategory.Survival,  DisplayName = "Renvoi du Bouclier",       SpellIdTech = "colossar_renvoi_bouclier" },
            new Mapping { SpellIdValue = 64, Class = NymoraClass.Colossar, Category = SpellCategory.Survival,  DisplayName = "Soin Lourd",               SpellIdTech = "colossar_soin_lourd" },
            new Mapping { SpellIdValue = 65, Class = NymoraClass.Colossar, Category = SpellCategory.Signature, DisplayName = "Effondrement",             SpellIdTech = "colossar_effondrement" },

            // NECRAM (70-85). #14 = "Régénération Nécrotique" (Bible) / NecramPulseSanguinVert (code).
            new Mapping { SpellIdValue = 70, Class = NymoraClass.Necram, Category = SpellCategory.Offensive, DisplayName = "Crachat Acide",            SpellIdTech = "necram_crachat_acide" },
            new Mapping { SpellIdValue = 71, Class = NymoraClass.Necram, Category = SpellCategory.Offensive, DisplayName = "Morsure Putride",          SpellIdTech = "necram_morsure_putride" },
            new Mapping { SpellIdValue = 72, Class = NymoraClass.Necram, Category = SpellCategory.Offensive, DisplayName = "Détonation Virulente", SpellIdTech = "necram_detonation_virulente" },
            new Mapping { SpellIdValue = 73, Class = NymoraClass.Necram, Category = SpellCategory.Offensive, DisplayName = "Faux Décharnée",  SpellIdTech = "necram_faux_decharnee" },
            new Mapping { SpellIdValue = 74, Class = NymoraClass.Necram, Category = SpellCategory.Offensive, DisplayName = "Brume Toxique",            SpellIdTech = "necram_brume_toxique" },
            new Mapping { SpellIdValue = 75, Class = NymoraClass.Necram, Category = SpellCategory.Tactical,  DisplayName = "Inoculation",              SpellIdTech = "necram_inoculation" },
            new Mapping { SpellIdValue = 76, Class = NymoraClass.Necram, Category = SpellCategory.Tactical,  DisplayName = "Marque Sacrificielle",     SpellIdTech = "necram_marque_sacrificielle" },
            new Mapping { SpellIdValue = 77, Class = NymoraClass.Necram, Category = SpellCategory.Tactical,  DisplayName = "Symbiose Morbide",         SpellIdTech = "necram_symbiose_morbide" },
            new Mapping { SpellIdValue = 78, Class = NymoraClass.Necram, Category = SpellCategory.Tactical,  DisplayName = "Contagion",                SpellIdTech = "necram_contagion" },
            new Mapping { SpellIdValue = 79, Class = NymoraClass.Necram, Category = SpellCategory.Tactical,  DisplayName = "Pas Spectral",             SpellIdTech = "necram_pas_spectral" },
            new Mapping { SpellIdValue = 80, Class = NymoraClass.Necram, Category = SpellCategory.Survival,  DisplayName = "Voile de Pestilence",      SpellIdTech = "necram_voile_pestilence" },
            new Mapping { SpellIdValue = 81, Class = NymoraClass.Necram, Category = SpellCategory.Survival,  DisplayName = "Carapace Visqueuse",       SpellIdTech = "necram_carapace_visqueuse" },
            new Mapping { SpellIdValue = 82, Class = NymoraClass.Necram, Category = SpellCategory.Survival,  DisplayName = "Drain Vital",              SpellIdTech = "necram_drain_vital" },
            new Mapping { SpellIdValue = 83, Class = NymoraClass.Necram, Category = SpellCategory.Survival,  DisplayName = "Régénération Nécrotique", SpellIdTech = "necram_regeneration_necrotique" },
            new Mapping { SpellIdValue = 84, Class = NymoraClass.Necram, Category = SpellCategory.Survival,  DisplayName = "Cocon Putride",            SpellIdTech = "necram_cocon_putride" },
            new Mapping { SpellIdValue = 85, Class = NymoraClass.Necram, Category = SpellCategory.Signature, DisplayName = "Virus Fatal",              SpellIdTech = "necram_virus_fatal" },

            // GHOSTRA (86-101). Slots Bible-canonical 5/5/5/1 : Volte-Face (90) classee Tactique malgre amendement offensif 16 mai.
            new Mapping { SpellIdValue = 86, Class = NymoraClass.Ghostra, Category = SpellCategory.Offensive, DisplayName = "Lame Spectrale",            SpellIdTech = "ghostra_lame_spectrale" },
            new Mapping { SpellIdValue = 87, Class = NymoraClass.Ghostra, Category = SpellCategory.Offensive, DisplayName = "Lame Vorace Spectrale",     SpellIdTech = "ghostra_lame_vorace_spectrale" },
            new Mapping { SpellIdValue = 88, Class = NymoraClass.Ghostra, Category = SpellCategory.Tactical,  DisplayName = "Réplique Fantôme", SpellIdTech = "ghostra_replique_fantome" },
            new Mapping { SpellIdValue = 89, Class = NymoraClass.Ghostra, Category = SpellCategory.Tactical,  DisplayName = "Pas dans l'Ombre",          SpellIdTech = "ghostra_pas_dans_ombre" },
            new Mapping { SpellIdValue = 90, Class = NymoraClass.Ghostra, Category = SpellCategory.Tactical,  DisplayName = "Volte-Face",                SpellIdTech = "ghostra_volte_face" },
            new Mapping { SpellIdValue = 91, Class = NymoraClass.Ghostra, Category = SpellCategory.Offensive, DisplayName = "Saigne-Âme",           SpellIdTech = "ghostra_saigne_ame" },
            new Mapping { SpellIdValue = 92, Class = NymoraClass.Ghostra, Category = SpellCategory.Offensive, DisplayName = "Frappe Fantôme",       SpellIdTech = "ghostra_frappe_fantome" },
            new Mapping { SpellIdValue = 93, Class = NymoraClass.Ghostra, Category = SpellCategory.Tactical,  DisplayName = "Dague Lancée",         SpellIdTech = "ghostra_dague_lancee" },
            new Mapping { SpellIdValue = 94, Class = NymoraClass.Ghostra, Category = SpellCategory.Tactical,  DisplayName = "Marque de l'Ombre",         SpellIdTech = "ghostra_marque_ombre" },
            new Mapping { SpellIdValue = 95, Class = NymoraClass.Ghostra, Category = SpellCategory.Offensive, DisplayName = "Danse des Lames",           SpellIdTech = "ghostra_danse_des_lames" },
            new Mapping { SpellIdValue = 96, Class = NymoraClass.Ghostra, Category = SpellCategory.Survival,  DisplayName = "Linceul d'Ombres",          SpellIdTech = "ghostra_linceul_ombres" },
            new Mapping { SpellIdValue = 97, Class = NymoraClass.Ghostra, Category = SpellCategory.Survival,  DisplayName = "Voile Spectral",            SpellIdTech = "ghostra_voile_spectral" },
            new Mapping { SpellIdValue = 98, Class = NymoraClass.Ghostra, Category = SpellCategory.Survival,  DisplayName = "Réplique Protectrice", SpellIdTech = "ghostra_replique_protectrice" },
            new Mapping { SpellIdValue = 99, Class = NymoraClass.Ghostra, Category = SpellCategory.Survival,  DisplayName = "Dernier Pas",               SpellIdTech = "ghostra_dernier_pas" },
            new Mapping { SpellIdValue = 100, Class = NymoraClass.Ghostra, Category = SpellCategory.Survival,  DisplayName = "Pas de l'Au-Delà",     SpellIdTech = "ghostra_pas_au_dela" },
            new Mapping { SpellIdValue = 101, Class = NymoraClass.Ghostra, Category = SpellCategory.Signature, DisplayName = "Exécution Spectrale",  SpellIdTech = "ghostra_execution_spectrale" },
        };

        // ------------------------------------------------------------------
        // Descriptions Bible V7.1 (champ EFFET des tableaux Bible).
        // Source : Nymora/_docs/01_BIBLE_V7.1_Combat.md (5 sections classes).
        // Importees 17 mai 2026 (5.3.e.iii completion : tooltip deck builder
        // affichait "Description Bible V7.1 a remplir" — fix Lorenzo).
        //
        // Texte gameplay pur (mecanique). Le champ PRESSION Bible est
        // importe separement dans _loreFlavors (saveur narrative).
        //
        // OVERWRITE a chaque populate : si Lorenzo amende une stat dans la
        // Bible, modifier ici puis re-run le tool.
        // ------------------------------------------------------------------
        private static readonly Dictionary<string, string> _descriptions = new Dictionary<string, string>
        {
            // ===== SOULRENDER =====
            { "soulrender_tranche_ame",          "Inflige 220 dégâts. Si le coup tue, le Soulrender RECULE de 2 cases gratuitement (mouvement non-PM). Effet purement de mise en scène, mais bloque les contre-attaques zone post-kill." },
            { "soulrender_ouvre_plaie",          "Inflige 110 dégâts. SI 1 HG dépensé : 230 dégâts ET la cible ne peut pas se soigner ni recevoir de bouclier pendant 2 tours." },
            { "soulrender_charge_brutale",       "Le Soulrender fonce en ligne droite jusqu'à la première unité ou case bloquante. Inflige 180 dégâts à la cible touchée. Toute case foulée pendant la charge devient Vapeur Carmin pendant 1 tour." },
            { "soulrender_detonation_sanglante", "Centre AoE croix 3. Inflige 60 dégâts de base à toutes les cibles dans la zone, +40 par HG consommé. Avec 5 HG : 260 dégâts. Sang Coagulé créé sous le centre pendant 2 tours. ATTENTION : consommer 5 HG ici interdit Âme Lacérée et reset son cooldown." },
            { "soulrender_curee",                "Inflige 150 dégâts. SI la cible meurt sur ce sort : le Soulrender heal 50% de ses HP max manquants ET récupère 4 PA pour le tour suivant. Si la cible NE MEURT PAS : le Soulrender perd 60 HP." },
            { "soulrender_pacte_de_sang",        "Le Soulrender s'inflige 80 dégâts à lui-même et gagne +3 HG immédiatement. Son prochain sort offensif ce tour gagne +50% de dégâts. UTILISABLE 1 FOIS PAR MATCH." },
            { "soulrender_marque_de_carnage",    "Marque la cible 3 tours. Pendant ce temps, tous les sorts du Soulrender sur cette cible génèrent +1 HG bonus. La marque est visible sur le sprite ennemi (croix de sang)." },
            { "soulrender_empoignade",           "Tire la cible jusqu'à 1 case du Soulrender. La cible ne peut pas être téléportée par un de ses propres sorts au tour suivant. Pas de dégâts." },
            { "soulrender_rugissement",          "AoE rayon 3 autour du Soulrender. Toutes les cibles ennemies subissent -1 PM ET ne peuvent pas téléporter au tour suivant. Si une cible est sous 50% HP : -2 PM au lieu de -1. Pas de dégâts." },
            { "soulrender_rage_insatiable",      "Pendant 2 tours, chaque sort offensif lancé par le Soulrender regenère 1 PA (max 1 par tour). Les sorts coûtent 1 PA de plus pendant ces 2 tours." },
            { "soulrender_riposte_carmin",       "Pendant 1 tour, toute attaque MÊLÉE subie par le Soulrender renvoie 100 dégâts à l'attaquant ET lui coûte 1 PM additionnel pour son prochain mouvement. Le Soulrender prend les dégâts normalement." },
            { "soulrender_cauterisation",        "Retire instantanément tous les DoT actifs sur le Soulrender (poison, plaie ouverte, autres saignements ennemis). Le Soulrender se soigne de 60 HP par DoT retiré (min 60, max 180)." },
            { "soulrender_peau_de_fer",          "Le Soulrender gagne un BOUCLIER de 200 HP pendant 2 tours. Pendant la durée du bouclier, ses sorts à portée 1 (mêlée) gagnent +30 dégâts. Le bouclier se vide normalement aux dégâts subis." },
            { "soulrender_seve_vive",            "Le Soulrender se soigne de 100 HP. Avec 1 HG : 160 HP. Si le Soulrender saigne actuellement (DoT actif sur lui) : +50 HP additionnels." },
            { "soulrender_dernier_souffle",      "Utilisable uniquement à <30% HP. Le Soulrender se soigne de 200 HP ET gagne 3 HG. UTILISABLE 1 FOIS PAR MATCH." },
            { "soulrender_ame_laceree",          "Inflige 320 dégâts. Le Soulrender se soigne de 50% des dégâts qui ont passé (après bouclier). Si la cible meurt sur ce sort : le combat est marqué d'une explosion de sang qui crée du Sang Coagulé en croix 5 cases." },

            // ===== NIGHTSEER =====
            { "nightseer_tir_precis",            "Inflige 200 dégâts. Si la cible est Traqué : 280 dégâts ET le Nightseer regagne 1 PR." },
            { "nightseer_volee_epines",          "Tir en ligne droite. Inflige 130 dégâts à toutes les cibles touchées. Pose un Filet de Ronces (identique au sort tactique : 100 dégâts, -2 PM, applique EMPREINTÉ 2 tours au déclenchement) sur la dernière case touchée." },
            { "nightseer_detonation_onirique",   "AoE 2x2 cases. 170 dégâts dans la zone. Si une case Voilé existe dans la zone, elle se déchire et inflige 80 dégâts supplémentaires. Avec 2 PR : portée passe à 10, peut frapper depuis l'autre côté de la map." },
            { "nightseer_frappe_ombre",          "Inflige 200 dégâts. Si la cible a moins de 50% de ses PM max actuels (donc s'est déjà déplacée) : +100 dégâts ET applique EMPREINTÉ pour 2 tours." },
            { "nightseer_salve_mortelle",        "Cible une case. Centre + 4 cases adjacentes en croix : 220 dégâts au centre, 130 sur les côtés. Toute cible Traqué dans la zone : +60 dégâts. Toute case Voilé dans la zone : déchirée, dévoilée, +50 dégâts à qui s'y trouvait. ATTENTION : consommer 3 PR ici diffère le cooldown du Traquenard." },
            { "nightseer_marque_chasseur",       "Applique TRAQUÉ à la cible pendant 3 tours. Sort très peu cher car le payoff est dans les autres sorts. Coexiste avec Voilé/Empreinté sur d'autres cibles, pas sur la même." },
            { "nightseer_filet_ronces",          "Pose une embûche invisible sur une case (Voilé pour l'adversaire). Toute unité ennemie qui entre : 100 dégâts, -2 PM, et applique EMPREINTÉ pour 2 tours." },
            { "nightseer_champ_mines",           "Pose 3 embûches Voilées dans une zone 3x3 (placement aléatoire pour l'adversaire, choisi par le Nightseer). Chaque embûche : 70 dégâts + applique EMPREINTÉ." },
            { "nightseer_bourrasque",            "Pousse la cible 3 cases dans la direction choisie. Avec 1 PR : 5 cases. Si la cible finit sa course sur un Filet de Ronces, une mine, ou une case Sang Coagulé : effets déclenchés à pleine puissance." },
            { "nightseer_souffle_glacial",       "AoE croix 3 cases autour du Nightseer. Inflige 70 dégâts + push 1 case + applique -1 PM aux cibles. Si une cible est poussée sur un Filet/mine : déclenchement." },
            { "nightseer_voile_ombre",           "Le Nightseer disparaît visuellement de l'écran adversaire pendant 1 tour entier. Il ne peut pas être ciblé directement (les AoE non-ciblées passent toujours). Si on devine sa case par AoE : effet normal." },
            { "nightseer_pas_furtif",            "Téléporte le Nightseer jusqu'à 4 cases. Si 1 PR consommée, la case d'arrivée devient VOILÉE pour l'adversaire pendant 2 tours." },
            { "nightseer_camouflage_ronces",     "Le Nightseer gagne un BOUCLIER de 130 HP pendant 2 tours. Pendant la durée, sa case est entourée d'un Filet de Ronces invisible : tout ennemi adjacent fin de tour subit 70 dégâts + EMPREINTÉ." },
            { "nightseer_seve_sauvage",          "Le Nightseer se soigne de 130 HP. Si une de ses embûches a été déclenchée ce tour ou le tour précédent : +60 HP additionnels. Si une case Voilée existe actuellement sur la map : +30 HP." },
            { "nightseer_evanescence",           "Utilisable uniquement à <30% HP. Le Nightseer se téléporte jusqu'à 7 cases ET se soigne de 150 HP. La case quittée devient Voilée pendant 2 tours. UTILISABLE 1 FOIS PAR MATCH." },
            { "nightseer_traquenard",            "Le Nightseer se téléporte à 1 case de la cible (côté libre, choix du joueur). Inflige 280 dégâts. Applique PARALYSIE (-3 PM, -2 PA) au prochain tour de la cible. Si la cible était Traqué/Voilé/Empreinté avant le sort : +80 dégâts ET la marque est consommée pour générer 2 PR au Nightseer après le coup." },

            // ===== COLOSSAR =====
            { "colossar_frappe_lourde",          "Inflige 180 dégâts. Si la cible est ÉPINGLÉE (adjacente à un Pilier, mur, ou bord de map du côté opposé au Colossar) : 280 dégâts." },
            { "colossar_onde_de_choc",           "AoE autour du Colossar. Inflige 80 dégâts à toutes les unités adjacentes ET les pousse de 2 cases. Si une unité est poussée contre un mur, Pilier, ou bord de map : 80 dégâts supplémentaires + APPLIQUE TRAUMA (-1 PM, -1 PA pendant 1 tour)." },
            { "colossar_marteau_punisseur",      "Inflige 160 dégâts. Si la cible a moins de 4 PA actuels (donc a déjà cast ce tour) : 240 dégâts ET applique TRAUMA (-2 PA prochain tour)." },
            { "colossar_choc_sismique",          "Frappe en ligne droite 4 cases. Inflige 130 dégâts à toutes les cibles touchées. Toutes les cibles touchées : -1 PM au prochain tour. Si une case Pilier ou Mur du Colossar se trouve sur la trajectoire : la frappe traverse, +50 dégâts à la cible suivante." },
            { "colossar_represailles",           "Inflige 100 dégâts immédiatement. Pendant 2 tours après le cast, chaque attaque mêlée subie par le Colossar renvoie 80 dégâts à l'attaquant. Cap à 4 retours." },
            { "colossar_pilier",                 "Pose un Pilier (200 HP, infranchissable, occupe 1 case) sur une case vide. Reste jusqu'à destruction. Le Colossar gagne +1 FD à la pose. Le Pilier bloque les lignes de vue et de tir des sorts directs." },
            { "colossar_mur_de_pierre",          "Crée un mur infranchissable de 3 cases (en ligne) pendant 2 tours. Avec 1 FD : 5 cases. Le Mur bloque tout : déplacements, ciblages directs, lignes de tir." },
            { "colossar_ancrage",                "La cible perd 2 PM pendant 2 tours ET ne peut pas être déplacée par effets externes (push/pull/téléport) au prochain tour. Pas de dégâts." },
            { "colossar_provocation",            "Force la cible à tenter d'attaquer le Colossar pendant 1 tour (ses sorts non-ciblant le Colossar coûtent +2 PA). La cible perd aussi 1 PM. Si la cible n'est pas adjacente au Colossar à la fin de son tour : 100 dégâts auto." },
            { "colossar_brisure",                "Inflige 90 dégâts. Retire un buff/bouclier de la cible (au choix du joueur). Si la cible n'a pas de buff/bouclier : applique TRAUMA (-2 PA prochain tour). Si la cible avait Camouflage Ronces, Linceul d'Ombres, Carapace Visqueuse, Stoïcisme, Peau de Fer : le bouclier est entièrement retiré." },
            { "colossar_stoicisme",              "Le Colossar gagne un BOUCLIER de 200 HP pour 2 tours. Pendant ces 2 tours, il ne peut PAS être déplacé (push/pull/téléport ennemi sans effet). Si le bouclier survit aux 2 tours sans être brisé, le Colossar récupère 80 HP." },
            { "colossar_garde_protectrice",      "Pendant 2 tours, le Colossar subit -30% de dégâts de TOUTES les sources (sauf DoT venin Necram qui ignore les réductions). Ne se cumule pas avec le passif Densité Inerte au-delà du cap -50% total." },
            { "colossar_ressac_vital",           "Le Colossar se soigne de 80 HP + 30 HP par attaque qu'il a subie au tour précédent (max +120 HP, donc cap 200 HP)." },
            { "colossar_renvoi_bouclier",        "Pendant 1 tour, toute attaque (mêlée OU à distance) subie par le Colossar renvoie 60 dégâts à l'attaquant. Cap à 4 retours." },
            { "colossar_soin_lourd",             "Soigne 150 HP sur soi OU sur un allié à 3 cases. UNIQUE HEAL CROSS-CLASSE DU JEU. En 1v1 : self-only, agit comme un heal lourd à 3 PA." },
            { "colossar_effondrement",           "ANNONCE 1 TOUR À L'AVANCE (le sol craque sous le Colossar à la fin du tour de cast — l'ennemi le voit). Au tour suivant : toutes les cases adjacentes au Colossar (rayon 2) deviennent IMPRATICABLES pendant 2 tours. Les ennemis dessus prennent 200 dégâts immédiats et sont éjectés vers la case libre la plus proche. Pendant les 2 tours d'Effondrement, le Colossar gagne +1 PM, ses sorts coûtent -1 PA, et toute attaque qu'il subit est réduite de 30%. À la fin de l'Effondrement, FD revient à 0." },

            // ===== NECRAM =====
            { "necram_crachat_acide",            "Inflige 90 dégâts ET applique 2 marques de venin (au lieu de 1). Cap à 4 marques par cible." },
            { "necram_morsure_putride",          "Inflige 110 dégâts + 22 par marque sur la cible (max +90, donc 200 dégâts max). Si la cible meurt : toutes ses marques sont transférées sur l'unité ennemie la plus proche." },
            { "necram_detonation_virulente",     "Inflige 80 dégâts immédiats. Consomme TOUTES les marques sur la cible : chaque marque consommée inflige 50 dégâts. Avec 4 marques : 280 dégâts totaux. Les marques disparaissent." },
            { "necram_faux_decharnee",           "AoE 1 case (le Necram et ses 8 voisines). Inflige 130 dégâts. Le Necram se SOIGNE de 30 HP par marque active sur toutes les cibles touchées (cap +120 HP)." },
            { "necram_brume_toxique",            "Pose une zone toxique 3x3 pendant 2 tours. Toute unité dans la zone : 60 dégâts immédiats + 1 marque. Toute unité qui ENTRE : 30 dégâts + 1 marque. Toute unité qui FINIT son tour dans la zone : 1 marque additionnelle." },
            { "necram_inoculation",              "Applique 2 marques de venin sur la cible (sans dégâts directs). Cap à 4 marques par cible." },
            { "necram_marque_sacrificielle",     "Pendant 3 tours, les marques sur la cible infligent +20 dégâts par tick. La cible peut recevoir Marque Sacrificielle même si elle n'a pas encore de marques (mais sans marques actives, l'effet est neutre)." },
            { "necram_symbiose_morbide",         "Pendant 2 tours, chaque tick de venin sur les ennemis soigne le Necram de 8 HP. Cap à 4 marques actives qui comptent pour le heal (donc max +32 HP par tour, +64 sur 2 tours)." },
            { "necram_contagion",                "Cible une unité ENNEMIE marquée. Toutes ses marques (cap 3, ou 4 avec PT) sont COPIÉES sur les autres unités ennemies dans un rayon de 3 cases. En 1v1 : la cible reçoit un boost de tick (+1 marque dupliquée sur elle-même)." },
            { "necram_pas_spectral",             "Le Necram gagne +2 PM ce tour ET peut traverser les unités ennemies au prochain déplacement. Ses marques posées par traversée appliquent 1 marque bonus." },
            { "necram_voile_pestilence",         "Pendant 2 tours, toute unité ennemie qui finit son tour à 2 cases ou moins du Necram reçoit 1 marque automatiquement. Pendant ces 2 tours, toute attaque mêlée subie par le Necram applique 1 marque à l'attaquant." },
            { "necram_carapace_visqueuse",       "Le Necram gagne un BOUCLIER de 110 HP pour 2 tours. Tout attaquant mêlée qui frappe le bouclier reçoit 1 marque automatiquement." },
            { "necram_drain_vital",              "Inflige 60 dégâts à la cible. Le Necram se soigne de 30 HP, ou 60 HP si la cible a 3+ marques actives." },
            { "necram_regeneration_necrotique",  "Le Necram se soigne de 70 HP + 15 HP par marque ennemis dans rayon 4 (max +90 HP). Avec 1 PT : +30 HP additionnels." },
            { "necram_cocon_putride",            "Utilisable uniquement à <30% HP. Le Necram se soigne de 220 HP ET applique 1 marque à toutes les unités ennemies dans rayon 4. UTILISABLE 1 FOIS PAR MATCH." },
            { "necram_virus_fatal",              "Cible une unité ennemie. TOUTES les marques sur la cible déclenchent leur tick instantanément X3 (multiplicateur Floraison appliqué). Une cible avec 4 marques de venin (50 dmg/tick × 4 × 3) prend 600 dégâts d'un coup. Les marques sont consommées. Si la cible meurt sur ce sort : les marques ne sont PAS consommées et restent disponibles pour Contagion ou Détonation Virulente sur d'autres cibles." },

            // ===== GHOSTRA (Bible V7.1 patchee 17 mai 2026 : Volte-Face 80 / Dague 40 / Replique Protectrice 4PA 30% 80HP 3r / Replique Fantome 4r) =====
            { "ghostra_lame_spectrale",          "Inflige 170 dégâts. Si dorsal : +50 dégâts (Angle 2) ou +80 (Angle 3) du passif. Si la cible a PLAIE OUVERTE : +60 dégâts." },
            { "ghostra_lame_vorace_spectrale",   "Inflige 130 dégâts + 60 si la cible a PLAIE OUVERTE. Si dorsal : +bonus passif. La Plaie Ouverte n'est PAS consommée." },
            { "ghostra_replique_fantome",        "Pose un Leurre sur une case vide à 4 cases. Le Leurre est visuellement identique à la Ghostra. Dure 4 rounds (amendement 16 mai 2026) ou jusqu'à interaction. Si le Leurre survit la durée complète, la Ghostra regagne 80 HP. Si le Leurre est détruit par un sort adverse, la Ghostra regagne 40 HP." },
            { "ghostra_pas_dans_ombre",          "Téléporte la Ghostra jusqu'à 5 cases. Si une case adjacente à l'arrivée contient une cible ennemie : la cible PIVOTE pour faire face à la Ghostra. Coût optionnel : laisser un leurre sur la case quittée (compte dans le cap 3)." },
            { "ghostra_volte_face",              "Inflige 80 dégâts (amendement 16 mai 2026) + bonus dorsal Angle Mort si applicable. Force la cible ennemie à faire DEMI-TOUR (180°). Sa direction de regard est inversée instantanément. PAS DE VERROU : la cible se réoriente normalement à son prochain tour (walk / cast / push pivots standard). Si elle ne trigger aucun pivot, elle reste dos → dorsal potentiel sur Lame Spec/Saigne-Âme suivant." },
            { "ghostra_saigne_ame",              "Inflige 200 dégâts + 70 si la cible a PLAIE OUVERTE (consomme la plaie). Si la cible meurt : la Ghostra regagne 60 HP." },
            { "ghostra_frappe_fantome",          "La Ghostra se téléporte à 1 case de la cible (côté libre). Inflige 200 dégâts. Si dorsal : +bonus passif. Si la cible avait été VOLTE-FACE ou que sa direction a été modifiée ce tour : APPLIQUE PLAIE OUVERTE (40/tour × 2t)." },
            { "ghostra_dague_lancee",            "Inflige 40 dégâts (amendement 16 mai 2026, nerf 80→40) + bonus dorsal Angle Mort si applicable. Pivot 90° HORAIRE iso de la cible (NE→SE→SW→NW→NE). Flag LastFacingForcedOnTurn posé pour combo Dague→Frappe Fantôme = PlaieOuverte. Cap 2×/tour." },
            { "ghostra_marque_ombre",            "Pendant 2 tours, tous les sorts de la Ghostra sur la cible gagnent +20 dégâts. Si la cible est touchée en dorsal pendant ces 2 tours : applique automatiquement PLAIE OUVERTE." },
            { "ghostra_danse_des_lames",         "AoE 8 cases adjacentes. Inflige 180 dégâts à toutes les cibles. Toute cible touchée subit le bonus dorsal du passif si dorsale OU si un leurre est adjacent à elle (consommation optionnelle : -1 leurre, applique bonus dorsal automatique)." },
            { "ghostra_linceul_ombres",          "La Ghostra gagne un BOUCLIER de 130 HP pendant 2 tours. Toute attaque mêlée subie pendant la durée renvoie 40 dégâts à l'attaquant." },
            { "ghostra_voile_spectral",          "Retire INSTANTANÉMENT tous les DoT actifs sur la Ghostra (saignements, marques venin, plaies). Pendant 1 tour, la Ghostra est immunisée à toute nouvelle application de DoT. UTILISABLE 1x PAR MATCH." },
            { "ghostra_replique_protectrice",    "Pose un Leurre PROTECTEUR (200 HP, redirige 30% des dégâts subis par la Ghostra pendant 3 rounds) (amendement 16 mai 2026 : 4 PA / 30% / 3 rounds). Si le Leurre est détruit, la Ghostra regagne 80 HP. Pas de stack si plusieurs Protective vivants (un seul absorbe par hit). Compte dans le cap 3 leurres." },
            { "ghostra_dernier_pas",             "Utilisable uniquement à <30% HP. La Ghostra se soigne de 200 HP, se téléporte jusqu'à 5 cases, ET pose un leurre sur la case quittée. UTILISABLE 1 FOIS PAR MATCH." },
            { "ghostra_pas_au_dela",             "La Ghostra gagne +2 PM ce tour ET son prochain déplacement ignore les unités (peut traverser ennemis et leurres). Si elle traverse un ennemi, elle déclenche un sort dorsal automatique sur lui (frappe gratuite à 60 dégâts)." },
            { "ghostra_execution_spectrale",     "Inflige 350 dégâts SI la cible est dorsale (regarde ailleurs). Applique PLAIE OUVERTE (50 dégâts/tour × 3 tours). Si la cible meurt sur ce sort, la Ghostra regagne 100 HP ET 2 leurres réapparaissent immédiatement (2 prêts pour le cycle suivant). Si la cible n'est PAS dorsale au moment du cast, le sort RATE et les 3 leurres sont quand même consommés." },
        };

        // ------------------------------------------------------------------
        // Lore flavor Bible V7.1 (champ PRESSION des tableaux Bible).
        // Texte narratif / fantasy. Importe en meme temps que les descriptions.
        // ------------------------------------------------------------------
        private static readonly Dictionary<string, string> _loreFlavors = new Dictionary<string, string>
        {
            // ===== SOULRENDER =====
            { "soulrender_tranche_ame",          "Le sort signature de base. Lent (3 PA), prévisible — et c'est ce qui le rend terrifiant. L'adversaire SAIT qu'il arrive. Il ne peut pas l'arrêter." },
            { "soulrender_ouvre_plaie",          "L'anti-sustain. La simple existence de ce sort dans le deck Soulrender suffit à interdire à l'adversaire de poser un Carapace ou Soin Lourd sans préparation." },
            { "soulrender_charge_brutale",       "Le bélier. Charge Brutale ne fait pas seulement entrer le Soulrender — elle CRÉE un couloir de pression qui restera après son passage." },
            { "soulrender_detonation_sanglante", "Le payoff total. Détoner 5 HG est un acte de FOI — le Soulrender renonce à son finisher pour un coup massif." },
            { "soulrender_curee",                "Le tout ou rien. Curée est une lecture pure : si tu calcules juste, le match s'enchaîne. Si tu calcules mal, tu donnes un tempo entier à l'adversaire." },
            { "soulrender_pacte_de_sang",        "Le bouton clutch. Quand l'adversaire pense être safe, le Soulrender saigne lui-même pour ouvrir une fenêtre de burst. Décision à très haut risque." },
            { "soulrender_marque_de_carnage",    "Le sceau. Marque de Carnage transforme une cible en machine à fabriquer de la ressource. Plus l'adversaire reçoit de coups, plus le Soulrender accélère." },
            { "soulrender_empoignade",           "L'arrachement. Empoignade défait la map des classes-kite. Une Nightseer qui pensait son setup safe se retrouve au corps à corps, son Évanescence verrouillée." },
            { "soulrender_rugissement",          "Le cri primal. Rugissement ne tue pas — il fige. Combiné à Charge Brutale derrière, c'est un piège géométrique. Anti-Ghostra par excellence." },
            { "soulrender_rage_insatiable",      "Le cycle ouvert. Rage Insatiable est un investissement : on accepte de payer plus cher chaque sort, en échange d'un tempo qui ne s'arrête jamais." },
            { "soulrender_riposte_carmin",       "Le piège du chasseur. Riposte Carmin n'est pas une défense — c'est une invitation. Elle dit à l'adversaire : 'Viens me frapper.'" },
            { "soulrender_cauterisation",        "L'auto-cautérisation. Anti-Necram et anti-Ghostra. Quand le bleed devient trop dense, le Soulrender brûle ses propres plaies pour repartir." },
            { "soulrender_peau_de_fer",          "Le mur viandard. Peau de Fer ne fait pas que protéger — elle ENCOURAGE l'engagement. Anti-Colossar/Nightseer qui zone à distance." },
            { "soulrender_seve_vive",            "Le rapide. Sève Vive est le micro-heal qui maintient le Soulrender en vie sans qu'il quitte le combat." },
            { "soulrender_dernier_souffle",      "L'ultime. Dernier Souffle n'est pas un heal — c'est une renaissance. Le Soulrender qui aurait dû mourir au tour 5 revient à 50% HP avec 3 HG en main, prêt pour un Âme Lacérée." },
            { "soulrender_ame_laceree",          "L'exécution rituelle. Âme Lacérée n'est pas un simple finisher — c'est l'aboutissement d'un cycle. Le Soulrender a saigné, fait saigner, accumulé. Maintenant il récolte." },

            // ===== NIGHTSEER =====
            { "nightseer_tir_precis",            "Le sniper. Tir Précis n'a pas besoin de surprendre — sa simple existence à 6 cases force l'adversaire à toujours regarder en l'air." },
            { "nightseer_volee_epines",          "Le double effet. Volée d'Épines fait des dégâts ET pose un piège. L'adversaire qui survit doit décider : foncer dans le filet ou contourner et perdre du tempo." },
            { "nightseer_detonation_onirique",   "L'œil qui frappe à travers le brouillard. Détonation Onirique punit la lecture. Si l'adversaire pensait être hors de portée, il ne l'était pas — le Nightseer voyait à travers." },
            { "nightseer_frappe_ombre",          "L'archer immobile. Frappe de l'Ombre punit le mouvement. Les classes qui sprintent (Ghostra, Soulrender qui charge) se font shred." },
            { "nightseer_salve_mortelle",        "Le moment où la map révèle sa vérité. Salve Mortelle déchire toutes les illusions du Nightseer en même temps." },
            { "nightseer_marque_chasseur",       "L'oeil. Quand l'adversaire prend une Marque du Chasseur, il sait que les 3 prochains tours vont être violents." },
            { "nightseer_filet_ronces",          "Le piège classique, mais ré-ingéniéré. Le Filet est lisible pour le Nightseer — il SAIT où il l'a posé. Il l'utilise pour pousser l'adversaire ailleurs." },
            { "nightseer_champ_mines",           "Le terrain miné. Champ de Mines transforme une zone en no-go. L'adversaire doit faire un détour OU absorber 3 mines pour passer." },
            { "nightseer_bourrasque",            "L'arme du conducteur. Bourrasque n'est pas une frappe — c'est un volant. Le Nightseer décide où l'adversaire VA, pas où il EST." },
            { "nightseer_souffle_glacial",       "Le décrochage. Souffle Glacial est l'outil anti-mêlée. Quand un Soulrender colle au Nightseer, ce sort le repousse ET le pousse potentiellement dans une mine voilée." },
            { "nightseer_voile_ombre",           "Le grand silence. Voile d'Ombre est l'arme du décrochage. Quand le Soulrender le pourchasse, le Nightseer disparaît juste avant le finisher." },
            { "nightseer_pas_furtif",            "Le coup le plus frustrant pour l'adversaire. Le Nightseer disparaît littéralement. L'adversaire doit deviner où il est." },
            { "nightseer_camouflage_ronces",     "L'épine défensive. Camouflage Ronces dit à l'adversaire : 'Approche-toi, vois ce qui se passe.' Anti-engage parfait contre Soulrender et Ghostra mêlée." },
            { "nightseer_seve_sauvage",          "Le heal de récolte. Sève Sauvage récompense le Nightseer qui a déjà fait son setup. Plus la map est piégée, plus il survit." },
            { "nightseer_evanescence",           "L'évasion totale. Évanescence permet au Nightseer de quitter complètement le combat le temps d'un tour." },
            { "nightseer_traquenard",            "L'embuscade pure. Traquenard n'est pas un finisher de DPS — c'est l'aboutissement d'un piège mental. La paralysie verrouille le tour adverse, le Nightseer peut décrocher ou enchaîner." },

            // ===== COLOSSAR =====
            { "colossar_frappe_lourde",          "Le coup signature. La cible doit littéralement éviter d'avoir un mur derrière elle pour exister. Le Colossar transforme les bords de map en pièges." },
            { "colossar_onde_de_choc",           "Le sort qui transforme un Pilier en arme. Sans Onde, un Pilier est juste décoratif. Avec, c'est un mur sur lequel l'adversaire va s'écraser." },
            { "colossar_marteau_punisseur",      "L'anti-tempo. Marteau Punisseur punit les classes qui spam — Soulrender, Necram. Le Colossar dit : 'Tu as fini ton tour ? Tant mieux. Maintenant tu prends.'" },
            { "colossar_choc_sismique",          "L'onde tellurique. Choc Sismique passe à travers ses propres murs comme un piston. Le Colossar tire à travers ses fortifications — il est le seul." },
            { "colossar_represailles",           "Le contre-engage. Représailles est posé AVANT le combat rapproché — c'est un engagement délibéré du Colossar pour dire à un Soulrender ou une Ghostra : 'Vas-y, viens.'" },
            { "colossar_pilier",                 "L'outil. À lui seul, Pilier ne menace personne. En combinaison avec push/pull, il devient un instrument de meurtre." },
            { "colossar_mur_de_pierre",          "Le grand séparateur. Un Mur bien posé peut couper la map en deux et forcer l'adversaire à choisir : il fait demi-tour ou il détruit le mur." },
            { "colossar_ancrage",                "Le gel. Ancrage est l'anti-mobilité ultime. Une Ghostra ancrée ne peut plus se téléporter. C'est un sort qui DÉSACTIVE des kits entiers." },
            { "colossar_provocation",            "L'humiliation. Provocation force l'adversaire à venir au Colossar — qui l'attend avec Représailles posé. Le Colossar dicte les engagements directement par sort." },
            { "colossar_brisure",                "Le briseur de mur. Brisure est l'anti-tank, l'anti-tortue. Aucune classe ne peut se reposer derrière un bouclier face au Colossar — il les casse explicitement." },
            { "colossar_stoicisme",              "Le rocher. Stoïcisme est le contraire d'un panic-button — c'est une déclaration. Le Colossar plante les pieds." },
            { "colossar_garde_protectrice",      "L'armure mobile. Garde Protectrice est le bouclier qui ne casse pas. Il n'a pas de HP — il a un timer. Le Colossar peut traverser une zone hostile sans se faire démolir." },
            { "colossar_ressac_vital",           "Le contre-tank. Ressac Vital récompense le Colossar qui s'est fait taper. Plus l'adversaire l'agresse, plus il se soigne. Anti-burst implacable." },
            { "colossar_renvoi_bouclier",        "Le miroir. Renvoi du Bouclier est l'anti-Nightseer — un sort à distance qui frappe le Colossar lui revient direct." },
            { "colossar_soin_lourd",             "Le seul vrai support du jeu. Inutile en 1v1 sauf comme heal classique, mais son existence définit le rôle du Colossar en team. En 2v2/3v3 c'est un game-changer." },
            { "colossar_effondrement",           "L'arme tellurique. L'annonce 1 tour à l'avance crée un mindgame brutal : l'adversaire DOIT se repositionner ou prendre 200 dégâts. Pas de troisième option. Le Colossar dicte la prochaine demi-minute." },

            // ===== NECRAM =====
            { "necram_crachat_acide",            "Le sort de base, mais redoutable. Crachat Acide combine dégâts directs et setup en 1 PA-efficace. C'est l'arme à 80% de l'utilisation Necram en early." },
            { "necram_morsure_putride",          "L'embrasement. Morsure Putride est le finisher qui propage. Tuer une cible avec elle ne stoppe pas le DoT — elle migre. Anti-team, mais aussi outil pour cycler en 1v1." },
            { "necram_detonation_virulente",     "Le détonateur. Détonation est le moment où le Necram récolte. Décision : 'Maintenant ou plus tard ?' Plus tard = plus de marques = plus de dégâts." },
            { "necram_faux_decharnee",           "Le moment où le mage devient bête. La Faux est anti-Soulrender, anti-Ghostra : si tu te rapproches du Necram, il en profite pour se soigner sur ton dos." },
            { "necram_brume_toxique",            "L'air vicié. Brume Toxique ne tue pas — elle CONDAMNE. Les ennemis voient une zone et savent qu'ils ne peuvent pas y mettre les pieds." },
            { "necram_inoculation",              "Le baiser de la mort. Inoculation ne fait rien d'immédiat. L'adversaire qui prend 2 marques sait que les 3 prochains tours vont être un compte à rebours. La pression vient du SILENCE." },
            { "necram_marque_sacrificielle",     "L'engrais. Marque Sacrificielle force l'adversaire à se soigner CONSTAMMENT. Un tick à 70 dégâts ne pardonne aucun délai." },
            { "necram_symbiose_morbide",         "Le parasite. Symbiose transforme le Necram en machine à régen. Plus il a de marques sur la map, plus il devient incassable. Anti-attrition par excellence." },
            { "necram_contagion",                "L'épidémie. En 2v2/3v3, Contagion est dévastateur. En 1v1, elle reste utilisable comme un boost de DoT sur la cible." },
            { "necram_pas_spectral",             "Le passage du fantôme. Pas Spectral est l'unique vrai outil de mobilité du Necram. Il l'utilise pour se positionner dans la Brume ou s'extraire d'une mêlée." },
            { "necram_voile_pestilence",         "Le linceul. Voile de Pestilence punit l'adjacence. Une Ghostra qui se téléporte derrière le Necram pour un dorsal se retrouve marquée." },
            { "necram_carapace_visqueuse",       "L'épine pourrie. Carapace Visqueuse n'est pas un mur — c'est un piège défensif. Frapper le Necram en mêlée = signer son arrêt de mort." },
            { "necram_drain_vital",              "Le siphon. Drain Vital est le heal qui FAIT mal. Sustain économique anti-Soulrender qui presse trop." },
            { "necram_regeneration_necrotique",  "La récolte. Régénération Nécrotique scale avec le travail accompli. Plus de marques = plus de heal. C'est le heal qui dit : 'J'ai bien semé.'" },
            { "necram_cocon_putride",            "L'explosion fongique. Cocon Putride n'est pas qu'un panic-heal — c'est une aspersion. Le Necram à l'agonie devient soudain le Necram avec 6+ marques sur la map." },
            { "necram_virus_fatal",              "L'apoptose. Virus Fatal est l'aboutissement absolu de la stratégie Necram : 4-5 tours de setup transformés en 1 tour de mort lente accélérée." },

            // ===== GHOSTRA =====
            { "ghostra_lame_spectrale",          "La frappe la plus banale du jeu — sauf que personne ne sait d'où elle vient. La banalité du sort est sa force : il sort de partout, depuis n'importe quel leurre." },
            { "ghostra_lame_vorace_spectrale",   "Le coup qui ronge. Lame Vorace empile sur une plaie ouverte sans la fermer. Ghostra qui a posé une Plaie Ouverte au tour précédent peut la rentabiliser pendant 2 tours." },
            { "ghostra_replique_fantome",        "Le clone qui paye les frais. Réplique Fantôme FORCE l'adversaire à choisir : 'je frappe ce qui ressemble à la Ghostra ?' Toute lecture coûte. La Ghostra gagne quoi qu'il arrive." },
            { "ghostra_pas_dans_ombre",          "Le saut de l'absent. Pas dans l'Ombre n'est pas seulement une mobilité — c'est un GÉNÉRATEUR de leurre." },
            { "ghostra_volte_face",              "L'ouverture chirurgicale. Volte-Face est désormais un sort OFFENSIF de setup dorsal — il ouvre la cible pour le combo ET tape pour 80. Il transforme une cible en lapin frappé dans le dos." },
            { "ghostra_saigne_ame",              "L'aboutissement. Saigne-Âme consomme la plaie pour un payoff massif. Le sort de fin du combo Plaie Ouverte → Lame Vorace → Saigne-Âme." },
            { "ghostra_frappe_fantome",          "Le finisseur. Frappe Fantôme arrive de nulle part. Combinée à Volte-Face, c'est un combo qui peut shred 350+ HP en un tour." },
            { "ghostra_dague_lancee",            "Le caillou dans la vitre. Dague Lancée est l'outil le plus subtile : 1 PA, 40 dégâts, ça paraît minuscule. Mais elle MANIPULE le regard de la cible." },
            { "ghostra_marque_ombre",            "Le sceau. Marque de l'Ombre pré-charge une cible. La Ghostra peut ensuite alterner Réplique → permutation → Lame Spectrale dorsal et l'effet plaie est garanti. Anti-tank par contournement." },
            { "ghostra_danse_des_lames",         "L'apocalypse en miniature. Danse des Lames est le moment où la Ghostra cesse d'être un assassin et devient un cyclone. Tout converge en une seconde." },
            { "ghostra_linceul_ombres",          "Le suaire. Linceul d'Ombres est le bouclier qui mord. Anti-Soulrender qui charge." },
            { "ghostra_voile_spectral",          "Le seul anti-DoT du kit Ghostra. Sans lui, la Ghostra se fait fondre par Soulrender et Necram. Avec, elle peut plonger dans le brouillard et ressortir propre." },
            { "ghostra_replique_protectrice",    "Le clone-bouclier. Réplique Protectrice n'est pas un leurre offensif — c'est un sustain caché. Elle prolonge la vie de la Ghostra de 1-2 tours." },
            { "ghostra_dernier_pas",             "L'évasion finale. Dernier Pas n'est pas qu'un heal — c'est un tour offert. La Ghostra à 200 HP se retrouve à 50% HP, à 5 cases de l'engagement, avec un leurre fraîchement posé." },
            { "ghostra_pas_au_dela",             "Le glissement. Pas de l'Au-Delà transforme la Ghostra en fantôme physique. Anti-Empoignade Soulrender, anti-Mur Colossar." },
            { "ghostra_execution_spectrale",     "Le coup le plus risqué du jeu. Exécution Spectrale demande une LECTURE PARFAITE — la cible doit être dorsale. Ratée, la Ghostra perd tout son setup. Réussie, elle finit le match en 1 tour." },
        };

        [MenuItem("Nymora/Setup/Populate Spell Catalog")]
        public static void Run()
        {
            // 1. Charge ou cree SpellCatalog.asset
            var catalog = AssetDatabase.LoadAssetAtPath<SpellCatalog>(CATALOG_PATH);
            if (catalog == null)
            {
                var dir = Path.GetDirectoryName(CATALOG_PATH);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                catalog = ScriptableObject.CreateInstance<SpellCatalog>();
                AssetDatabase.CreateAsset(catalog, CATALOG_PATH);
                Debug.Log($"[Nymora.PopulateSpellCatalog] Cree {CATALOG_PATH}.");
            }

            // 2. Index par SpellIdTech pour preserve descriptions remplies manuellement
            var existing = new Dictionary<string, SpellDefinition>();
            if (catalog.Spells != null)
            {
                foreach (var s in catalog.Spells)
                {
                    if (s != null && !string.IsNullOrEmpty(s.SpellId))
                        existing[s.SpellId] = s;
                }
            }

            var newList = new List<SpellDefinition>();
            int created = 0, updated = 0, missing = 0;

            // 3. Iterate mapping, fill ou update
            foreach (var m in _mappings)
            {
                var spellId = (SpellId)m.SpellIdValue;
                if (!SpellRegistry.TryGet(spellId, out SpellDef def))
                {
                    Debug.LogWarning($"[Nymora.PopulateSpellCatalog] SpellRegistry.TryGet RATE pour {spellId} ({m.DisplayName}) — sort skip.");
                    missing++;
                    continue;
                }

                bool isExisting = existing.TryGetValue(m.SpellIdTech, out SpellDefinition entry);
                if (!isExisting)
                {
                    entry = new SpellDefinition();
                    created++;
                }
                else
                {
                    updated++;
                }

                // Identity (overwrite : source-of-truth mapping + Quantum)
                entry.SpellId = m.SpellIdTech;
                entry.DisplayName = m.DisplayName;
                entry.ClassId = m.Class;
                entry.Category = m.Category;
                entry.QuantumSpellIdValue = m.SpellIdValue;

                // Cost (overwrite depuis SpellRegistry runtime)
                entry.ActionPointCost = def.PACost;
                entry.ClassResourceCost = def.HGCostMandatory;
                // MovementPointCost / CooldownTurns laisses a la valeur existante
                // (pas dans SpellDef Quantum runtime, geres ailleurs par OncePerMatchBit/cooldowns specifiques).

                // Targeting (overwrite depuis SpellRegistry runtime).
                // Enums Quantum.TargetingFilter / TargetingShape ont mêmes valeurs que Nymora.Core
                // (vérifié 17 mai : Targeting.qtn == TargetingFilter.cs / TargetingShape.cs).
                // Cast direct sûr.
                entry.MinRange = def.RangeMin;
                entry.MaxRange = def.RangeMax;
                entry.Filter = (TargetingFilter)(byte)def.Filter;
                entry.Shape = (TargetingShape)(byte)def.Shape;
                entry.RequiresLineOfSight = true; // default — affiner si besoin

                // Effects : list inchange (preserves)
                if (entry.Effects == null) entry.Effects = new List<SpellEffect>();

                // Description / LoreFlavor : OVERWRITE depuis Bible V7.1 (5.3.e.iii fix Lorenzo 17 mai).
                // Si une entree manque dans les dicts, on preserve la valeur existante
                // (permet d'editer manuellement un sort sans que le tool ecrase).
                if (_descriptions.TryGetValue(m.SpellIdTech, out string desc))
                    entry.Description = desc;
                if (_loreFlavors.TryGetValue(m.SpellIdTech, out string lore))
                    entry.LoreFlavor = lore;

                // IconSprite : preserve (NE PAS overwrite — assigne manuellement Inspector).

                // Versioning
                entry.CombatRulesVersion = GameVersion.CombatRulesVersion;

                newList.Add(entry);
            }

            // 4. Save
            catalog.Spells = newList;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Nymora.PopulateSpellCatalog] Population terminee : {created} crees, {updated} mis a jour, {missing} manquants. Total : {newList.Count}/80.");
            if (newList.Count < 80)
            {
                Debug.LogWarning($"[Nymora.PopulateSpellCatalog] ATTENTION : seulement {newList.Count}/80 sorts populates. Verifier mapping ou SpellRegistry.");
            }
        }

    }
}
