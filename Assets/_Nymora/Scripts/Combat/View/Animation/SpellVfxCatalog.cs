using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// Archétype visuel d'un sort : décide QUEL effet procédural on joue (et où).
    /// </summary>
    public enum SpellVfxArchetype
    {
        None = 0,
        Slash,      // lame mêlée à la cible, orientée caster->cible
        Projectile, // projectile caster->cible + burst d'impact à l'arrivée
        Impact,     // explosion radiale à la cible
        Zone,       // nappe persistante sur la case cible (brume/flaque/feu)
        Buff,       // aura montante sur le caster
        Nova,       // onde radiale large centrée sur le caster
        Signature,  // séquence multi-phases spectaculaire (1 par classe)
    }

    /// <summary>
    /// Description VFX d'un sort (data : archétype + 2 couleurs). Le rendu réel est fait par
    /// <see cref="ProceduralSpellVfx"/> via <see cref="ProceduralVfx"/>.
    /// </summary>
    public readonly struct SpellVfxDef
    {
        public readonly SpellVfxArchetype Archetype;
        public readonly Color Primary;
        public readonly Color Secondary;

        public SpellVfxDef(SpellVfxArchetype a, Color p, Color s)
        {
            Archetype = a; Primary = p; Secondary = s;
        }
    }

    /// <summary>
    /// Mapping SpellId -> SpellVfxDef. POC : Soulrender mappé finement ; les 4 autres classes
    /// reçoivent un défaut teinté par classe (placeholder propre) en attendant le mapping fin.
    ///
    /// Les SIGNATURES (frames peints par Kyami via VFXSpriteLibrary) ne passent PAS ici : le
    /// CombatVFXView ne tombe sur le procédural que si la library n'a pas de frames pour le sort.
    /// </summary>
    public static class SpellVfxCatalog
    {
        // --- Palettes par classe ---
        private static readonly Color SoulrenderA = Hex("#D6303A"); // sang vif
        private static readonly Color SoulrenderB = Hex("#7A0E14"); // sang sombre
        private static readonly Color NightseerA  = Hex("#3FE08A"); // vert lame/ronces
        private static readonly Color NightseerB  = Hex("#1E5E47");
        private static readonly Color ColossarA   = Hex("#E0A93A"); // ambre/pierre
        private static readonly Color ColossarB   = Hex("#7C6A45");
        private static readonly Color NecramA     = Hex("#8FD43A"); // venin toxique
        private static readonly Color NecramB     = Hex("#3C5A1E");
        private static readonly Color GhostraA    = Hex("#67C7F0"); // spectral cyan
        private static readonly Color GhostraB    = Hex("#2C5C77");

        /// <summary>Les 5 signatures (1 par classe) — déclenchent la séquence VFX spectaculaire.</summary>
        public static bool IsSignature(SpellId id)
        {
            switch (id)
            {
                case SpellId.SoulrenderAmeLaceree:
                case SpellId.NightseerTraquenard:
                case SpellId.ColossarEffondrement:
                case SpellId.NecramVirusFatal:
                case SpellId.GhostraExecutionSpectrale:
                    return true;
                default:
                    return false;
            }
        }

        public static SpellVfxDef Resolve(SpellId id)
        {
            switch (id)
            {
                // ===== SIGNATURES (séquence spectaculaire, couleurs de classe) =====
                case SpellId.SoulrenderAmeLaceree:      return new SpellVfxDef(SpellVfxArchetype.Signature, SoulrenderA, SoulrenderB);
                case SpellId.NightseerTraquenard:       return new SpellVfxDef(SpellVfxArchetype.Signature, NightseerA,  NightseerB);
                case SpellId.ColossarEffondrement:      return new SpellVfxDef(SpellVfxArchetype.Signature, ColossarA,   ColossarB);
                case SpellId.NecramVirusFatal:          return new SpellVfxDef(SpellVfxArchetype.Signature, NecramA,     NecramB);
                case SpellId.GhostraExecutionSpectrale: return new SpellVfxDef(SpellVfxArchetype.Signature, GhostraA,    GhostraB);

                // ===== SOULRENDER (mappé finement) =====
                case SpellId.SoulrenderTrancheAme:        return new SpellVfxDef(SpellVfxArchetype.Slash,   SoulrenderA, SoulrenderB);
                case SpellId.SoulrenderOuvrePlaie:        return new SpellVfxDef(SpellVfxArchetype.Slash,   SoulrenderA, SoulrenderB);
                case SpellId.SoulrenderCuree:             return new SpellVfxDef(SpellVfxArchetype.Slash,   SoulrenderA, SoulrenderB);
                case SpellId.SoulrenderChargeBrutale:     return new SpellVfxDef(SpellVfxArchetype.Projectile, SoulrenderA, SoulrenderB); // charge : trait caster->cible + impact
                case SpellId.SoulrenderEmpoignade:        return new SpellVfxDef(SpellVfxArchetype.Projectile, SoulrenderA, SoulrenderB); // empoignade : trait vers la cible
                case SpellId.SoulrenderDetonationSanglante: return new SpellVfxDef(SpellVfxArchetype.Impact, SoulrenderA, SoulrenderB);
                case SpellId.SoulrenderMarqueDeCarnage:   return new SpellVfxDef(SpellVfxArchetype.Impact,  SoulrenderA, SoulrenderB);
                case SpellId.SoulrenderRugissement:       return new SpellVfxDef(SpellVfxArchetype.Nova,    SoulrenderA, SoulrenderB);
                case SpellId.SoulrenderPacteDeSang:       return new SpellVfxDef(SpellVfxArchetype.Buff,    SoulrenderA, SoulrenderB);
                case SpellId.SoulrenderRageInsatiable:    return new SpellVfxDef(SpellVfxArchetype.Buff,    SoulrenderA, SoulrenderB);
                case SpellId.SoulrenderRiposteCarmin:     return new SpellVfxDef(SpellVfxArchetype.Buff,    SoulrenderA, SoulrenderB);
                case SpellId.SoulrenderPeauDeFer:         return new SpellVfxDef(SpellVfxArchetype.Buff,    Hex("#C8C8D0"), SoulrenderB);
                case SpellId.SoulrenderCauterisation:     return new SpellVfxDef(SpellVfxArchetype.Buff,    Hex("#5BE08A"), SoulrenderA); // heal = vert
                case SpellId.SoulrenderSeveVive:          return new SpellVfxDef(SpellVfxArchetype.Buff,    Hex("#5BE08A"), SoulrenderA); // heal = vert
                case SpellId.SoulrenderDernierSouffle:    return new SpellVfxDef(SpellVfxArchetype.Buff,    Hex("#FFD36B"), SoulrenderA);
                // SoulrenderAmeLaceree = signature (frames Kyami) -> géré par la library, pas ici.

                default:
                    return DefaultForClass(id);
            }
        }

        /// <summary>
        /// Défaut par classe (placeholder propre) pour les sorts non encore mappés finement :
        /// un Impact teinté à la couleur de la classe. Inféré depuis le préfixe du SpellId.
        /// </summary>
        private static SpellVfxDef DefaultForClass(SpellId id)
        {
            string n = id.ToString();
            if (n.StartsWith("Soulrender")) return new SpellVfxDef(SpellVfxArchetype.Impact, SoulrenderA, SoulrenderB);
            if (n.StartsWith("Nightseer"))  return new SpellVfxDef(SpellVfxArchetype.Impact, NightseerA,  NightseerB);
            if (n.StartsWith("Colossar"))   return new SpellVfxDef(SpellVfxArchetype.Impact, ColossarA,   ColossarB);
            if (n.StartsWith("Necram"))     return new SpellVfxDef(SpellVfxArchetype.Impact, NecramA,     NecramB);
            if (n.StartsWith("Ghostra"))    return new SpellVfxDef(SpellVfxArchetype.Impact, GhostraA,    GhostraB);
            return new SpellVfxDef(SpellVfxArchetype.Impact, Color.white, Color.gray);
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
        }
    }
}
