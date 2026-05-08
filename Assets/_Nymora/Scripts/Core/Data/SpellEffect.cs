using System;
using Nymora.Core.Enums;
using UnityEngine;

namespace Nymora.Core.Data
{
    /// <summary>
    /// Entree de la liste Effects[] d'un SpellDefinition.
    /// Sérialisable Unity — apparait directement dans l'Inspector du SO Spell.
    /// Plusieurs effets peuvent etre chaines (ex : Damage 90 + ApplyMark Venin x2).
    /// </summary>
    [Serializable]
    public struct SpellEffect
    {
        [Tooltip("Type d'effet applique.")]
        public SpellEffectType Type;

        [Tooltip("Valeur principale (degats, heal, stacks de marque, dur. push en cases, etc.).")]
        public int Amount;

        [Tooltip("Type de degats (utile uniquement pour SpellEffectType.Damage).")]
        public DamageType DamageType;

        [Tooltip("Sous-type de marque (utilise par ApplyMark/ConsumeMark — sera enrichi en Phase 2).")]
        public int MarkSubtype;

        [Tooltip("Stacks appliques pour ApplyMark.")]
        public int Stacks;

        [Tooltip("Duree en tours (buffs, shield, hazard zone, etc.). 0 = instantane.")]
        public int DurationTurns;

        [Tooltip("Ignore les reductions de degats (ex : DoT venin Necram).")]
        public bool IgnoresReduction;
    }
}
