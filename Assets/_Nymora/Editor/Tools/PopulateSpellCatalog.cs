using System.Collections.Generic;
using System.IO;
using Nymora.Core.Data;
using Nymora.Core.ScriptableObjects;
using Quantum;
using UnityEditor;
using UnityEngine;
// Aliases : NymoraClass / TargetingFilter / TargetingShape / SpellCategory existent
// AUSSI dans Quantum (codegen) — on prend Nymora.Core.Enums comme source-of-truth UI.
using TargetingFilter = Nymora.Core.Enums.TargetingFilter;
using TargetingShape = Nymora.Core.Enums.TargetingShape;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Editor tool 5.3.b — Populate SpellCatalog.asset depuis SpellRegistry runtime
    /// (PA / Range / Filter / Shape / Damage) + <see cref="SpellBibleTexts.Entries"/>
    /// (DisplayName / Class / Category / Description / LoreFlavor — Bible V7.1 patchee).
    ///
    /// Menu : Nymora &gt; Setup &gt; Populate Spell Catalog.
    ///
    /// Le tool est IDEMPOTENT : re-runnable, met a jour les entries existantes au lieu
    /// de dupliquer.
    ///
    /// Refacto 5.4 (18 mai 2026) : les 80 entries Bible (mappings + descriptions +
    /// loreFlavors) vivaient ici en local. Migrees dans <see cref="SpellBibleTexts"/>
    /// (Nymora.Core) pour partager avec le tooltip combat (qui faisait "(Description
    /// Bible non disponible)" pour 4 classes sur 5).
    /// </summary>
    public static class PopulateSpellCatalog
    {
        private const string CATALOG_PATH = "Assets/_Nymora/ScriptableObjects/Spells/SpellCatalog.asset";

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

            // 3. Iterate Bible entries (source-of-truth Nymora.Core), fill ou update
            foreach (var bible in SpellBibleTexts.Entries)
            {
                var spellId = (SpellId)bible.SpellIdValue;
                if (!SpellRegistry.TryGet(spellId, out SpellDef def))
                {
                    Debug.LogWarning($"[Nymora.PopulateSpellCatalog] SpellRegistry.TryGet RATE pour {spellId} ({bible.DisplayName}) — sort skip.");
                    missing++;
                    continue;
                }

                bool isExisting = existing.TryGetValue(bible.SpellIdTech, out SpellDefinition entry);
                if (!isExisting)
                {
                    entry = new SpellDefinition();
                    created++;
                }
                else
                {
                    updated++;
                }

                // Identity (overwrite : source-of-truth Bible + Quantum)
                entry.SpellId = bible.SpellIdTech;
                entry.DisplayName = bible.DisplayName;
                entry.ClassId = bible.ClassId;
                entry.Category = bible.Category;
                entry.QuantumSpellIdValue = bible.SpellIdValue;

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

                // Description / LoreFlavor : OVERWRITE depuis Bible V7.1 (source unique Nymora.Core).
                entry.Description = bible.Description;
                entry.LoreFlavor = bible.LoreFlavor;

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
                Debug.LogWarning($"[Nymora.PopulateSpellCatalog] ATTENTION : seulement {newList.Count}/80 sorts populates. Verifier SpellBibleTexts.Entries ou SpellRegistry.");
            }
        }

    }
}
