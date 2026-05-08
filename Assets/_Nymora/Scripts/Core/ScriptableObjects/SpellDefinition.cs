using System.Collections.Generic;
using Nymora.Core.Data;
using Nymora.Core.Enums;
using UnityEngine;

namespace Nymora.Core.ScriptableObjects
{
    /// <summary>
    /// Definition d'un sort de Nymora (Bible V7.1).
    /// 75 sorts au final : 5 classes x 15 sorts (5 Off / 5 Tac / 5 Sur) + 5 signatures.
    ///
    /// IMPORTANT : modifier une valeur ici = incrementer GameVersion.CombatRulesVersion.
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Spell Definition", fileName = "NewSpell", order = 110)]
    public class SpellDefinition : ScriptableObject
    {
        // ---------------------------------------------------------------------
        // Identity
        // ---------------------------------------------------------------------
        [Header("Identity")]
        [Tooltip("Identifiant technique stable (ex : soulrender_carnage_strike). Utilise pour les replays / balance.")]
        public string SpellId;

        [Tooltip("Nom affiche dans l'UI (ex : Frappe Carnage).")]
        public string DisplayName;

        [Tooltip("Classe proprietaire de ce sort.")]
        public NymoraClass ClassId = NymoraClass.None;

        [Tooltip("Categorie du sort dans le deck (Off/Tac/Sur/Signature).")]
        public SpellCategory Category = SpellCategory.None;

        public Sprite IconSprite;

        [TextArea(2, 5)]
        [Tooltip("Description gameplay courte (sera affichee en tooltip).")]
        public string Description;

        [TextArea(2, 4)]
        [Tooltip("Lore / fantasy flavor text (optionnel).")]
        public string LoreFlavor;

        // ---------------------------------------------------------------------
        // Cost
        // ---------------------------------------------------------------------
        [Header("Cost")]
        [Tooltip("Cout en Points d'Action (max 8 par tour).")]
        public int ActionPointCost = 0;

        [Tooltip("Cout en Points de Mouvement (rare — 0 par defaut).")]
        public int MovementPointCost = 0;

        [Tooltip("Cout en ressource de classe (HG, PR, FD, PT, RM). 0 = pas de cout.")]
        public int ClassResourceCost = 0;

        [Tooltip("Cooldown apres usage en nombre de tours. 0 = pas de cooldown.")]
        public int CooldownTurns = 0;

        // ---------------------------------------------------------------------
        // Targeting
        // ---------------------------------------------------------------------
        [Header("Targeting")]
        [Tooltip("Portee minimum en cases (Manhattan). 1 = mini melee.")]
        public int MinRange = 1;

        [Tooltip("Portee maximum en cases (Manhattan).")]
        public int MaxRange = 1;

        [Tooltip("Le sort necessite une ligne de vue (pas d'obstacle entre caster et cible) ?")]
        public bool RequiresLineOfSight = true;

        [Tooltip("Forme de la zone d'effet centree sur la case ciblee.")]
        public TargetingShape Shape = TargetingShape.SingleTile;

        [Tooltip("Filtre de cible accepte (qui peut etre cible par ce sort).")]
        public TargetingFilter Filter = TargetingFilter.Enemy;

        // ---------------------------------------------------------------------
        // Effects
        // ---------------------------------------------------------------------
        [Header("Effects (composables)")]
        [Tooltip("Liste d'effets appliques quand le sort touche. " +
                 "Plusieurs effets se chainent dans l'ordre. " +
                 "Exemple : [Damage 90 Physical] + [ApplyMark Bleed x2].")]
        public List<SpellEffect> Effects = new List<SpellEffect>();

        // ---------------------------------------------------------------------
        // Versioning
        // ---------------------------------------------------------------------
        [Header("Versioning (anti-desync replay)")]
        [Tooltip("Version des regles au moment ou ce sort a ete tune. " +
                 "Doit matcher GameVersion.CombatRulesVersion pour etre joue en ranked.")]
        public int CombatRulesVersion = 1;
    }
}
