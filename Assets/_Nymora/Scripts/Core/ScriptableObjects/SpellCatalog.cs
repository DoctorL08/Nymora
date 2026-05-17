using System.Collections.Generic;
using UnityEngine;

namespace Nymora.Core.ScriptableObjects
{
    /// <summary>
    /// Catalogue centralise des 80 sorts du jeu (5 classes x 16 sorts).
    /// Source-of-truth UI pour le Deck Builder et les tooltips de combat.
    ///
    /// 1 SpellCatalog.asset unique dans Assets/_Nymora/ScriptableObjects/Spells/.
    /// Cree 5.3.b (17 mai 2026) en remplacement du pattern "1 asset par sort"
    /// (cf [[project-phase5-plan]] decision Lorenzo).
    ///
    /// IMPORTANT : modifier une valeur ici = incrementer GameVersion.CombatRulesVersion.
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Spell Catalog", fileName = "SpellCatalog", order = 105)]
    public class SpellCatalog : ScriptableObject
    {
        [Tooltip("Liste des 80 sorts du jeu. Generee par Editor tool PopulateSpellCatalog " +
                 "depuis SpellRegistry.cs (stats runtime) + Bible V7.1 patchee (descriptions).")]
        public List<SpellDefinition> Spells = new List<SpellDefinition>();

        /// <summary>
        /// Recherche un sort par son SpellId technique (ex : "ghostra_lame_spectrale").
        /// Retourne null si introuvable.
        /// </summary>
        public SpellDefinition FindBySpellId(string spellId)
        {
            if (string.IsNullOrEmpty(spellId)) return null;
            for (int i = 0; i < Spells.Count; i++)
            {
                if (Spells[i] != null && Spells[i].SpellId == spellId)
                    return Spells[i];
            }
            return null;
        }

        /// <summary>
        /// Liste les sorts d'une classe donnee (en excluant le signature si voulu).
        /// </summary>
        public List<SpellDefinition> FindByClass(Nymora.Core.Enums.NymoraClass cls, bool includeSignature = true)
        {
            var result = new List<SpellDefinition>();
            for (int i = 0; i < Spells.Count; i++)
            {
                var s = Spells[i];
                if (s == null || s.ClassId != cls) continue;
                if (!includeSignature && s.Category == Nymora.Core.Enums.SpellCategory.Signature) continue;
                result.Add(s);
            }
            return result;
        }
    }
}
