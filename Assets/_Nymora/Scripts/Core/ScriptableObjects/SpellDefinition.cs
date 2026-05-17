using System;
using System.Collections.Generic;
using Nymora.Core.Data;
using Nymora.Core.Enums;
using UnityEngine;

namespace Nymora.Core.ScriptableObjects
{
    /// <summary>
    /// Definition d'un sort de Nymora (Bible V7.1).
    /// 80 sorts au final : 5 classes x (15 sorts + 1 signature). Stocke INLINE dans
    /// SpellCatalog.asset (single source of truth) — pas de .asset par sort.
    ///
    /// Refactor 5.3.b (17 mai 2026) : etait ScriptableObject par sort, devenu
    /// [Serializable] class inline dans SpellCatalog. Choix Lorenzo : 1 fichier
    /// editable d'un coup vs 80 .asset granulaires.
    ///
    /// IMPORTANT : modifier une valeur ici = incrementer GameVersion.CombatRulesVersion.
    /// </summary>
    [Serializable]
    public class SpellDefinition
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
        // Mapping Quantum (5.3.g — wiring combat)
        // ---------------------------------------------------------------------
        [Header("Quantum mapping (5.3.g)")]
        [Tooltip("Valeur numerique correspondant a Quantum.SpellId enum (ex : 10 = SoulrenderTrancheAme). " +
                 "Permet la conversion SpellIdTech (snake_case) -> Quantum.SpellId (byte enum) sans dupliquer " +
                 "le mapping cote Runtime. Auto-populee par Nymora > Setup > Populate Spell Catalog.")]
        public int QuantumSpellIdValue;

        // ---------------------------------------------------------------------
        // Versioning
        // ---------------------------------------------------------------------
        [Header("Versioning (anti-desync replay)")]
        [Tooltip("Version des regles au moment ou ce sort a ete tune. " +
                 "Doit matcher GameVersion.CombatRulesVersion pour etre joue en ranked.")]
        public int CombatRulesVersion = 1;
    }
}
