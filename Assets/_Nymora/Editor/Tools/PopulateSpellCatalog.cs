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

                // Effects : list inchange (descriptions / lore preserves)
                // Description / LoreFlavor / IconSprite : preserves (NE PAS overwrite)
                if (entry.Effects == null) entry.Effects = new List<SpellEffect>();

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
