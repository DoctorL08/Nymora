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
using NymoraClass = Nymora.Core.Enums.NymoraClass;

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
                // LoS bridgée depuis la source unique sim (SpellSystem.SpellNeedsLineOfSight) :
                // EXACTEMENT le même switch que la validation du cast + le grisé du preview de
                // ciblage (TargetingPreviewView). Le deck builder lit ensuite ce champ pour
                // afficher "Ligne de vue requise". Re-Populate après tout ajout au switch.
                entry.RequiresLineOfSight = SpellSystem.SpellNeedsLineOfSight(spellId);

                // Effects : list inchange (preserves)
                if (entry.Effects == null) entry.Effects = new List<SpellEffect>();

                // Description / LoreFlavor : OVERWRITE depuis Bible V7.1 (source unique Nymora.Core).
                entry.Description = bible.Description;
                entry.LoreFlavor = bible.LoreFlavor;

                // IconSprite : auto-populate depuis le dossier icones de la classe (18 mai).
                // Convention filename : `icon_<suffix>` ou suffix = SpellIdTech apres le 1er '_'
                // (= apres le prefixe classe). Ex : "soulrender_tranche_ame" -> "icon_tranche_ame".
                // Si non trouve, preserve la valeur existante (drag-and-drop manuel reste possible).
                var icon = ResolveIconSprite(bible.SpellIdTech, bible.ClassId);
                if (icon != null) entry.IconSprite = icon;

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
            int iconsHit = 0, iconsMiss = 0;
            foreach (var s in newList)
            {
                if (s.IconSprite != null) iconsHit++; else iconsMiss++;
            }
            Debug.Log($"[Nymora.PopulateSpellCatalog] Icones : {iconsHit} assignees / {iconsMiss} manquantes (drag manuel ou re-naming designer).");
        }

        // ================ ICON RESOLUTION (18 mai) ================

        private static readonly Dictionary<NymoraClass, string> ClassIconFolders = new Dictionary<NymoraClass, string>
        {
            { NymoraClass.Soulrender, "Assets/_Nymora/Art/Sprites/Soulrender/soulrender_icons" },
            { NymoraClass.Nightseer,  "Assets/_Nymora/Art/Sprites/Nightseer/Icons" },
            { NymoraClass.Colossar,   "Assets/_Nymora/Art/Sprites/Colossar/colossar_icons" },
            { NymoraClass.Necram,     "Assets/_Nymora/Art/Sprites/Necram/necram_icons" },
            { NymoraClass.Ghostra,    "Assets/_Nymora/Art/Sprites/Ghostra/Spell_Icon" },
        };

        // Overrides pour les sorts dont le SpellIdTech (Bible) drop des particules
        // ("de", "du", "l'") alors que le filename designer les conserve. Source :
        // PopulateSpellIconRegistry.FileToSpellId (= verite filename designer).
        private static readonly Dictionary<string, string> FilenameOverrides = new Dictionary<string, string>
        {
            { "nightseer_volee_epines",        "icon_volee_depines" },
            { "nightseer_frappe_ombre",        "icon_frappe_de_lombre" },
            { "nightseer_marque_chasseur",     "icon_marque_du_chasseur" },
            { "nightseer_filet_ronces",        "icon_filet_de_ronces" },
            { "nightseer_champ_mines",         "icon_champ_de_mines" },
            { "nightseer_voile_ombre",         "icon_voile_dombre" },
            { "colossar_renvoi_bouclier",      "icon_renvoi_du_bouclier" },
            { "necram_voile_pestilence",       "icon_voile_de_pestilence" },
            { "ghostra_pas_dans_ombre",        "icon_pas_dans_lombre" },
            { "ghostra_marque_ombre",          "icon_marque_de_lombre" },
            { "ghostra_linceul_ombres",        "icon_linceul_dombres" },
            // Refonte 30 mai : les 4 sorts Ghostra reworkés (Permutation/Éveil/Nuée/Communion) suivent
            //   la convention de nommage par défaut (icon_<suffixe>), pas besoin d'override.
        };

        /// <summary>
        /// Resoud le Sprite icone pour un sort donne. Format attendu :
        ///   filename = "icon_<suffix>" ou suffix = SpellIdTech apres le 1er '_'.
        /// Ex : "nightseer_filet_de_ronces" -> chercher "icon_filet_de_ronces" dans le dossier
        /// Nightseer/Icons. Retourne null si introuvable (le caller preserve la valeur existante).
        /// </summary>
        private static Sprite ResolveIconSprite(string spellIdTech, NymoraClass classId)
        {
            if (string.IsNullOrEmpty(spellIdTech)) return null;
            if (!ClassIconFolders.TryGetValue(classId, out string folder)) return null;

            // 1. Check les overrides explicites (cas particules droppees)
            string filename;
            if (!FilenameOverrides.TryGetValue(spellIdTech, out filename))
            {
                // 2. Convention default : suffix apres le 1er '_' du SpellIdTech
                int sep = spellIdTech.IndexOf('_');
                if (sep < 0 || sep >= spellIdTech.Length - 1) return null;
                filename = "icon_" + spellIdTech.Substring(sep + 1);
            }

            string path = $"{folder}/{filename}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[Nymora.PopulateSpellCatalog] Icone introuvable pour {spellIdTech} (path attendu : {path}). Drag-and-drop manuel via Inspector si necessaire.");
            }
            return sprite;
        }
    }
}
