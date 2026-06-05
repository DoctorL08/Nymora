using System.Collections;
using Nymora.Combat.View;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// Joue l'effet procédural d'un sort : résout l'archétype + couleurs (SpellVfxCatalog),
    /// les positions monde caster/cible (GridRenderer), et compose les primitives ProceduralVfx
    /// pour un feel "vrai cast" : wind-up à la main du caster -> release (flash) -> impact
    /// (onde de choc + éclats) à la cible.
    ///
    /// Les SIGNATURES passent par <see cref="PlaySignature"/> (séquence multi-phases spectaculaire),
    /// en plus de l'art peint de Kyami s'il existe. 100% View.
    /// </summary>
    public static class ProceduralSpellVfx
    {
        private const float BodyY = 0.45f;   // torse
        private const float GroundY = 0.08f; // sol
        private const float BaseY = 0.25f;   // pieds
        private const int Order = 2000;

        public static void Play(MonoBehaviour host, GridRenderer grid, in Combatant caster)
        {
            if (host == null || grid == null) return;
            var def = SpellVfxCatalog.Resolve(caster.LastCastSpellId);
            if (def.Archetype == SpellVfxArchetype.None) return;

            if (def.Archetype == SpellVfxArchetype.Signature) { PlaySignature(host, grid, caster); return; }

            if (!ResolvePositions(grid, caster, out var casterPos, out var targetPos, out var layer)) return;
            var parent = host.transform;

            Vector2 dir = (Vector2)(targetPos - casterPos);
            if (dir.sqrMagnitude < 0.0001f) dir = FacingDir(caster.Facing);
            dir = dir.normalized;

            // #13 (5 juin) — en phase 3, les pièges du Nightseer sont INVISIBLES pour l'adversaire
            //   (TrapView). Mais le VFX de POSE de Filet de Ronces / Champ de Mines joue À LA POSITION
            //   du/des piège(s) -> il révélait l'emplacement à l'ennemi. On le supprime côté ADVERSAIRE
            //   quand le poseur est en phase 3 ; le propriétaire (viewer == caster) continue de le voir.
            //   (Le Piège Bondissant joue autour du caster, pas sur le piège -> non concerné.)
            if ((caster.LastCastSpellId == SpellId.NightseerFiletDeRonces
                 || caster.LastCastSpellId == SpellId.NightseerChampDeMines)
                && NightseerPassif.TrapsInvisible(caster.Resource)
                && LocalPlayerResolver.Resolve() != caster.PlayerIndex)
            {
                return;
            }

            // Recettes BESPOKE par sort (par classe). Si la classe du sort est mappée finement,
            // on joue son effet dédié et on s'arrête ; sinon on retombe sur l'archétype générique.
            if (SoulrenderVfx.TryPlay(host, caster.LastCastSpellId, casterPos, targetPos, layer, dir)) return;
            if (NightseerVfx.TryPlay(host, caster.LastCastSpellId, casterPos, targetPos, layer, dir)) return;
            if (ColossarVfx.TryPlay(host, caster.LastCastSpellId, casterPos, targetPos, layer, dir)) return;
            if (NecramVfx.TryPlay(host, caster.LastCastSpellId, casterPos, targetPos, layer, dir)) return;
            if (GhostraVfx.TryPlay(host, caster.LastCastSpellId, casterPos, targetPos, layer, dir)) return;

            Vector3 casterBody = casterPos + Vector3.up * BodyY;
            Vector3 targetBody = targetPos + Vector3.up * BodyY;

            switch (def.Archetype)
            {
                case SpellVfxArchetype.Slash:
                    ProceduralVfx.CastCharge(parent, casterBody, def.Primary, 0.16f, layer, Order + 2);
                    ProceduralVfx.Slash(parent, targetBody, dir, def.Primary, layer, Order + 3);
                    ProceduralVfx.Burst(parent, targetBody, ImpactSmall(def.Secondary, layer));
                    ProceduralVfx.Shockwave(parent, targetPos + Vector3.up * GroundY, def.Primary, 1.2f, 0.3f, layer, Order - 1);
                    break;

                case SpellVfxArchetype.Projectile:
                    ProceduralVfx.CastCharge(parent, casterBody, def.Primary, 0.16f, layer, Order + 2);
                    {
                        var sec = def.Secondary; var l = layer; var p = parent; var tb = targetBody;
                        ProceduralVfx.Projectile(parent, casterBody, targetBody, def.Primary, 0.20f, layer, Order + 2,
                            onArrive: () =>
                            {
                                ProceduralVfx.Flash(p, tb, sec, 0.8f, 0.16f, l, Order + 4);
                                ProceduralVfx.Shockwave(p, tb + Vector3.down * (BodyY - GroundY), sec, 1.5f, 0.35f, l, Order - 1);
                                ProceduralVfx.Burst(p, tb, ImpactBig(sec, l));
                            });
                    }
                    break;

                case SpellVfxArchetype.Impact:
                    ProceduralVfx.CastCharge(parent, casterBody, def.Primary, 0.16f, layer, Order + 2);
                    ProceduralVfx.Flash(parent, targetBody, def.Primary, 0.9f, 0.16f, layer, Order + 4);
                    ProceduralVfx.Shockwave(parent, targetPos + Vector3.up * GroundY, def.Primary, 1.7f, 0.38f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, targetBody, ImpactBig(def.Primary, layer));
                    ProceduralVfx.Burst(parent, targetBody, ImpactSmall(def.Secondary, layer));
                    break;

                case SpellVfxArchetype.Zone:
                    ProceduralVfx.Shockwave(parent, targetPos + Vector3.up * GroundY, def.Primary, 1.3f, 0.35f, layer, Order - 1);
                    ProceduralVfx.Zone(parent, targetPos + Vector3.up * GroundY, def.Primary, 2.4f, layer, Order - 2);
                    break;

                case SpellVfxArchetype.Buff:
                    ProceduralVfx.Flash(parent, casterPos + Vector3.up * BaseY, def.Primary, 0.7f, 0.2f, layer, Order + 3);
                    ProceduralVfx.Shockwave(parent, casterPos + Vector3.up * GroundY, def.Primary, 1.2f, 0.4f, layer, Order - 1);
                    ProceduralVfx.Aura(parent, /*follow*/ null, casterPos + Vector3.up * BaseY, def.Primary, 1.0f, layer, Order + 1);
                    break;

                case SpellVfxArchetype.Nova:
                    ProceduralVfx.CastCharge(parent, casterBody, def.Primary, 0.18f, layer, Order + 2);
                    ProceduralVfx.Flash(parent, casterBody, def.Primary, 1.0f, 0.18f, layer, Order + 4);
                    ProceduralVfx.Shockwave(parent, casterPos + Vector3.up * GroundY, def.Primary, 2.4f, 0.45f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, casterBody, Nova(def.Primary, layer));
                    break;
            }
        }

        /// <summary>
        /// Séquence signature SPECTACULAIRE (waaaw) : wind-up qui charge -> flash de release ->
        /// projectile -> gros impact (double onde de choc + éclats denses + braises montantes).
        /// </summary>
        public static void PlaySignature(MonoBehaviour host, GridRenderer grid, in Combatant caster)
        {
            if (host == null || grid == null) return;
            if (!ResolvePositions(grid, caster, out var casterPos, out var targetPos, out var layer)) return;
            var def = SpellVfxCatalog.Resolve(caster.LastCastSpellId);
            Color primary = def.Primary, secondary = def.Secondary;
            host.StartCoroutine(SignatureRoutine(host.transform, casterPos, targetPos, primary, secondary, layer));
        }

        private static IEnumerator SignatureRoutine(Transform parent, Vector3 casterPos, Vector3 targetPos,
                                                     Color primary, Color secondary, string layer)
        {
            Vector3 casterBody = casterPos + Vector3.up * BodyY;
            Vector3 targetBody = targetPos + Vector3.up * BodyY;

            // Phase 0 — wind-up : l'énergie converge vers les mains, le cœur grossit.
            ProceduralVfx.CastCharge(parent, casterBody, primary, 0.40f, layer, Order + 3);
            ProceduralVfx.Shockwave(parent, casterPos + Vector3.up * GroundY, primary, 1.4f, 0.4f, layer, Order - 1);
            yield return new WaitForSecondsRealtime(0.36f);

            // Phase 1 — release : flash éclatant au caster + projectile vers la cible.
            ProceduralVfx.Flash(parent, casterBody, primary, 1.3f, 0.22f, layer, Order + 5);
            ProceduralVfx.Projectile(parent, casterBody, targetBody, primary, 0.16f, layer, Order + 3, onArrive: null);
            yield return new WaitForSecondsRealtime(0.16f);

            // Phase 2 — gros impact : flash blanc + double onde + éclats denses.
            ProceduralVfx.Flash(parent, targetBody, Color.white, 1.7f, 0.20f, layer, Order + 6);
            ProceduralVfx.Shockwave(parent, targetPos + Vector3.up * GroundY, primary, 2.8f, 0.5f, layer, Order - 1);
            ProceduralVfx.Burst(parent, targetBody, new ProceduralVfx.BurstParams
            { Color = primary, Count = 40, Speed = 8.5f, Size = 0.16f, Lifetime = 0.55f, Radius = 0.12f, Gravity = 0.8f, SortingLayer = layer, SortingOrder = Order + 1 });
            ProceduralVfx.Burst(parent, targetBody, new ProceduralVfx.BurstParams
            { Color = Color.white, Count = 22, Speed = 11f, Size = 0.10f, Lifetime = 0.35f, Radius = 0.05f, Gravity = 0.3f, SortingLayer = layer, SortingOrder = Order + 2 });
            yield return new WaitForSecondsRealtime(0.10f);

            // Phase 3 — résonance : seconde onde plus large + braises qui montent.
            ProceduralVfx.Shockwave(parent, targetPos + Vector3.up * GroundY, secondary, 3.6f, 0.6f, layer, Order - 2);
            ProceduralVfx.Aura(parent, null, targetPos + Vector3.up * BaseY, primary, 0.9f, layer, Order + 1);
        }

        // ---------- Helpers ----------

        private static bool ResolvePositions(GridRenderer grid, in Combatant caster,
                                             out Vector3 casterPos, out Vector3 targetPos, out string layer)
        {
            casterPos = targetPos = Vector3.zero; layer = null;
            var casterTile = grid.GetTileView(caster.GridX, caster.GridY);
            var targetTile = grid.GetTileView(caster.LastCastTargetX, caster.LastCastTargetY);
            if (casterTile == null && targetTile == null) return false;
            if (casterTile == null) casterTile = targetTile;
            if (targetTile == null) targetTile = casterTile;
            casterPos = casterTile.transform.position;
            targetPos = targetTile.transform.position;
            layer = targetTile.SortingLayerName;
            return true;
        }

        private static Vector2 FacingDir(Quantum.IsoFacing f)
        {
            switch (f)
            {
                case Quantum.IsoFacing.NE: return new Vector2(1f, 0.5f);
                case Quantum.IsoFacing.SE: return new Vector2(1f, -0.5f);
                case Quantum.IsoFacing.NW: return new Vector2(-1f, 0.5f);
                default:                   return new Vector2(-1f, -0.5f);
            }
        }

        private static ProceduralVfx.BurstParams ImpactBig(Color c, string layer) => new ProceduralVfx.BurstParams
        { Color = c, Count = 22, Speed = 5.5f, Size = 0.14f, Lifetime = 0.4f, Radius = 0.06f, Gravity = 1.0f, SortingLayer = layer, SortingOrder = Order };

        private static ProceduralVfx.BurstParams ImpactSmall(Color c, string layer) => new ProceduralVfx.BurstParams
        { Color = c, Count = 12, Speed = 8f, Size = 0.09f, Lifetime = 0.3f, Radius = 0.04f, Gravity = 0.3f, SortingLayer = layer, SortingOrder = Order + 1 };

        private static ProceduralVfx.BurstParams Nova(Color c, string layer) => new ProceduralVfx.BurstParams
        { Color = c, Count = 36, Speed = 8f, Size = 0.13f, Lifetime = 0.45f, Radius = 0.12f, Gravity = 0f, SortingLayer = layer, SortingOrder = Order };
    }
}
