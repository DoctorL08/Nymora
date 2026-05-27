using System.Collections;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// VFX procéduraux BESPOKE par sort Soulrender (identité Bible V7.1 — thème SANG/viscéral).
    /// Chaque sort a une gestuelle distincte (slash lourd / double-entaille / bélier / explosion
    /// en croix / chaîne qui happe / dôme de fer / cautérisation feu→soin / renaissance...).
    ///
    /// La signature (Âme Lacérée) est gérée par ProceduralSpellVfx.PlaySignature (+ frames Kyami).
    /// 100% View. TryPlay renvoie false si le SpellId n'est pas Soulrender-mappé (fallback archétype).
    /// </summary>
    public static class SoulrenderVfx
    {
        private static readonly Color Blood = Hex("#D6303A");
        private static readonly Color BloodDark = Hex("#7A0E14");
        private static readonly Color Heal = Hex("#5BE08A");
        private static readonly Color Fire = Hex("#FF7A2A");
        private static readonly Color Iron = Hex("#C8C8D0");
        private static readonly Color Gold = Hex("#FFD36B");

        private const float BodyY = 0.45f, GroundY = 0.08f, BaseY = 0.25f;
        private const int Order = 2000;

        public static bool TryPlay(MonoBehaviour host, SpellId id, Vector3 casterPos, Vector3 targetPos,
                                   string layer, Vector2 dir)
        {
            var parent = host.transform;
            Vector3 up = Vector3.up;
            Vector3 cb = casterPos + up * BodyY, tb = targetPos + up * BodyY;
            Vector3 cg = casterPos + up * GroundY, tg = targetPos + up * GroundY;

            switch (id)
            {
                // ----- OFFENSIFS -----
                case SpellId.SoulrenderTrancheAme: // frappe lourde unique
                    ProceduralVfx.CastCharge(parent, cb, Blood, 0.12f, layer, Order + 2);
                    ProceduralVfx.Slash(parent, tb, dir, Blood, layer, Order + 4);
                    ProceduralVfx.Burst(parent, tb, B(Blood, 24, 5.5f, 0.16f, 0.45f, 0.06f, 1.2f, layer, Order + 1));
                    ProceduralVfx.Shockwave(parent, tg, BloodDark, 1.3f, 0.3f, layer, Order - 1);
                    return true;

                case SpellId.SoulrenderOuvrePlaie: // double entaille en croix + sceau anti-soin
                    ProceduralVfx.Slash(parent, tb, Rotate(dir, 28f), Blood, layer, Order + 4);
                    ProceduralVfx.Slash(parent, tb, Rotate(dir, -28f), Blood, layer, Order + 4);
                    ProceduralVfx.RingHold(parent, tb, A(BloodDark, 0.7f), new Vector2(1.0f, 0.6f), 0.5f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, tb, B(Blood, 16, 6f, 0.1f, 0.35f, 0.05f, 1.0f, layer, Order + 1));
                    return true;

                case SpellId.SoulrenderChargeBrutale: // bélier : trait + couloir de vapeur + impact
                    ProceduralVfx.Beam(parent, cb, tb, Blood, 0.18f, 0.16f, layer, Order + 2, retract: false);
                    for (int i = 1; i <= 4; i++)
                    {
                        Vector3 p = Vector3.Lerp(casterPos, targetPos, i / 5f) + up * GroundY;
                        ProceduralVfx.Zone(parent, p, A(Blood, 0.7f), 1.0f, layer, Order - 2);
                    }
                    ProceduralVfx.Flash(parent, tb, Blood, 0.9f, 0.16f, layer, Order + 4);
                    ProceduralVfx.Shockwave(parent, tg, Blood, 1.6f, 0.35f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, tb, B(Blood, 22, 6f, 0.14f, 0.4f, 0.06f, 1.0f, layer, Order + 1));
                    return true;

                case SpellId.SoulrenderDetonationSanglante: // explosion en CROIX + sang coagulé au sol
                    ProceduralVfx.Flash(parent, tb, Color.white, 1.2f, 0.18f, layer, Order + 5);
                    ProceduralVfx.Shockwave(parent, tg, Blood, 2.2f, 0.45f, layer, Order - 1);
                    ProceduralVfx.Shockwave(parent, tg, BloodDark, 3.0f, 0.55f, layer, Order - 2);
                    ProceduralVfx.Burst(parent, tb, B(Blood, 34, 7f, 0.16f, 0.5f, 0.12f, 0.8f, layer, Order + 1));
                    foreach (var a in CrossArms)
                        ProceduralVfx.Burst(parent, tb + (Vector3)(a * 0.7f), B(Blood, 10, 4f, 0.12f, 0.4f, 0.05f, 1.0f, layer, Order + 1));
                    ProceduralVfx.Zone(parent, tg, BloodDark, 2.0f, layer, Order - 2);
                    return true;

                case SpellId.SoulrenderCuree: // curée : triple lacération sauvage
                    ProceduralVfx.CastCharge(parent, cb, Blood, 0.10f, layer, Order + 2);
                    ProceduralVfx.Slash(parent, tb, Rotate(dir, 32f), Blood, layer, Order + 4);
                    ProceduralVfx.Slash(parent, tb, dir, Blood, layer, Order + 4);
                    ProceduralVfx.Slash(parent, tb, Rotate(dir, -32f), Blood, layer, Order + 4);
                    ProceduralVfx.Flash(parent, tb, Blood, 0.7f, 0.15f, layer, Order + 5);
                    ProceduralVfx.Burst(parent, tb, B(Blood, 26, 7f, 0.15f, 0.45f, 0.06f, 1.2f, layer, Order + 1));
                    return true;

                // ----- TACTIQUES -----
                case SpellId.SoulrenderEmpoignade: // chaîne de sang qui happe la cible
                    ProceduralVfx.Beam(parent, cb, tb, BloodDark, 0.14f, 0.30f, layer, Order + 2, retract: true);
                    ProceduralVfx.Burst(parent, tb, B(Blood, 12, 5f, 0.1f, 0.3f, 0.05f, 0.6f, layer, Order + 1));
                    return true;

                case SpellId.SoulrenderPacteDeSang: // self : draine son propre sang -> surge
                    host.StartCoroutine(PacteRoutine(parent, cb, casterPos, layer));
                    return true;

                case SpellId.SoulrenderMarqueDeCarnage: // sceau : croix de sang projetée sur la cible
                {
                    var p = parent; var l = layer; var dst = tb;
                    ProceduralVfx.Projectile(parent, cb, tb, Blood, 0.18f, layer, Order + 2, onArrive: () =>
                    {
                        ProceduralVfx.Slash(p, dst, new Vector2(1f, 0f), Blood, l, Order + 4);
                        ProceduralVfx.Slash(p, dst, new Vector2(0f, 1f), Blood, l, Order + 4);
                        ProceduralVfx.RingHold(p, dst, A(Blood, 0.8f), new Vector2(0.9f, 0.6f), 0.6f, l, Order - 1);
                        ProceduralVfx.Burst(p, dst, B(Blood, 10, 4f, 0.1f, 0.3f, 0.04f, 0.8f, l, Order + 1));
                    });
                    return true;
                }

                case SpellId.SoulrenderRugissement: // cri primal : ondes concentriques autour du caster
                    host.StartCoroutine(RoarRoutine(parent, cb, cg, layer));
                    return true;

                case SpellId.SoulrenderRageInsatiable: // self : rage bouillonnante pulsée
                    ProceduralVfx.Flash(parent, cb, Blood, 0.8f, 0.2f, layer, Order + 3);
                    ProceduralVfx.Aura(parent, null, casterPos + up * BaseY, Blood, 1.3f, layer, Order + 1);
                    ProceduralVfx.RingHold(parent, cg, A(Blood, 0.7f), new Vector2(1.3f, 0.7f), 0.6f, layer, Order - 1);
                    ProceduralVfx.Shockwave(parent, cg, BloodDark, 1.8f, 0.5f, layer, Order - 2);
                    return true;

                // ----- SURVIE -----
                case SpellId.SoulrenderRiposteCarmin: // bait défensif : anneau de lames
                    ProceduralVfx.RingHold(parent, cg, A(Blood, 0.85f), new Vector2(1.5f, 0.85f), 0.8f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, cb, B(Blood, 18, 3.5f, 0.12f, 0.5f, 0.5f, 0f, layer, Order + 1));
                    ProceduralVfx.Flash(parent, cb, Blood, 0.6f, 0.2f, layer, Order + 3);
                    return true;

                case SpellId.SoulrenderCauterisation: // brûle ses plaies (feu) -> soin
                    host.StartCoroutine(CauterRoutine(parent, cb, casterPos, cg, layer));
                    return true;

                case SpellId.SoulrenderPeauDeFer: // dôme de fer
                    ProceduralVfx.RingHold(parent, cb, A(Iron, 0.85f), new Vector2(1.4f, 1.4f), 0.7f, layer, Order + 3);
                    ProceduralVfx.RingHold(parent, cg, A(Iron, 0.7f), new Vector2(1.6f, 0.9f), 0.7f, layer, Order - 1);
                    ProceduralVfx.Flash(parent, cb, Iron, 0.7f, 0.2f, layer, Order + 4);
                    ProceduralVfx.Burst(parent, cb, B(Iron, 12, 3f, 0.12f, 0.4f, 0.3f, 0.3f, layer, Order + 1));
                    return true;

                case SpellId.SoulrenderSeveVive: // micro-heal doux (vert)
                    ProceduralVfx.Flash(parent, cb, Heal, 0.5f, 0.25f, layer, Order + 3);
                    ProceduralVfx.Aura(parent, null, casterPos + up * BaseY, Heal, 0.9f, layer, Order + 1);
                    return true;

                case SpellId.SoulrenderDernierSouffle: // renaissance : colonne dorée
                    host.StartCoroutine(RebirthRoutine(parent, cb, casterPos, cg, layer));
                    return true;

                default:
                    return false; // pas un sort Soulrender mappé -> fallback archétype générique
            }
        }

        // ----- Coroutines (effets séquencés) -----

        private static IEnumerator PacteRoutine(Transform parent, Vector3 cb, Vector3 casterPos, string layer)
        {
            ProceduralVfx.CastCharge(parent, cb, BloodDark, 0.25f, layer, Order + 2); // draine vers soi
            yield return new WaitForSecondsRealtime(0.22f);
            ProceduralVfx.Flash(parent, cb, Blood, 1.0f, 0.2f, layer, Order + 5);
            ProceduralVfx.Burst(parent, cb, B(Blood, 28, 7f, 0.15f, 0.5f, 0.08f, 0.5f, layer, Order + 1)); // surge
            ProceduralVfx.Aura(parent, null, casterPos + Vector3.up * BaseY, BloodDark, 0.8f, layer, Order + 1);
            ProceduralVfx.Shockwave(parent, casterPos + Vector3.up * GroundY, Blood, 1.6f, 0.4f, layer, Order - 1);
        }

        private static IEnumerator RoarRoutine(Transform parent, Vector3 cb, Vector3 cg, string layer)
        {
            ProceduralVfx.Flash(parent, cb, Blood, 0.9f, 0.18f, layer, Order + 3);
            float[] diam = { 1.6f, 2.4f, 3.2f };
            for (int i = 0; i < diam.Length; i++)
            {
                Color c = (i % 2 == 0) ? Blood : BloodDark;
                ProceduralVfx.Shockwave(parent, cg, c, diam[i], 0.45f, layer, Order - 1 - i);
                ProceduralVfx.Burst(parent, cb, B(Blood, 8, 3.5f, 0.1f, 0.35f, 0.4f, 0.2f, layer, Order + 1));
                yield return new WaitForSecondsRealtime(0.10f);
            }
        }

        private static IEnumerator CauterRoutine(Transform parent, Vector3 cb, Vector3 casterPos, Vector3 cg, string layer)
        {
            ProceduralVfx.Flash(parent, cb, Fire, 0.8f, 0.2f, layer, Order + 4);
            ProceduralVfx.Burst(parent, cb, B(Fire, 20, 4.5f, 0.13f, 0.5f, 0.18f, -0.5f, layer, Order + 1)); // flammes montantes
            ProceduralVfx.Shockwave(parent, cg, Fire, 1.2f, 0.3f, layer, Order - 1);
            yield return new WaitForSecondsRealtime(0.2f);
            ProceduralVfx.Flash(parent, cb, Heal, 0.5f, 0.2f, layer, Order + 3);
            ProceduralVfx.Aura(parent, null, casterPos + Vector3.up * BaseY, Heal, 0.9f, layer, Order + 1);
        }

        private static IEnumerator RebirthRoutine(Transform parent, Vector3 cb, Vector3 casterPos, Vector3 cg, string layer)
        {
            ProceduralVfx.Flash(parent, cb, Color.white, 1.3f, 0.22f, layer, Order + 6);
            ProceduralVfx.Shockwave(parent, cg, Gold, 2.0f, 0.5f, layer, Order - 1);
            ProceduralVfx.Burst(parent, cb, B(Gold, 30, 2.0f, 0.16f, 0.8f, 0.2f, -1.5f, layer, Order + 1)); // colonne montante
            ProceduralVfx.Aura(parent, null, casterPos + Vector3.up * BaseY, Gold, 1.2f, layer, Order + 1);
            yield return new WaitForSecondsRealtime(0.18f);
            ProceduralVfx.Flash(parent, cb, Gold, 0.8f, 0.25f, layer, Order + 5);
        }

        // ----- Helpers -----

        private static readonly Vector2[] CrossArms = { new Vector2(0, 1), new Vector2(1, 0), new Vector2(0, -1), new Vector2(-1, 0) };

        private static ProceduralVfx.BurstParams B(Color c, int count, float speed, float size, float life,
                                                   float radius, float gravity, string layer, int order)
            => new ProceduralVfx.BurstParams
            { Color = c, Count = count, Speed = speed, Size = size, Lifetime = life, Radius = radius, Gravity = gravity, SortingLayer = layer, SortingOrder = order };

        private static Color A(Color c, float alpha) { c.a = alpha; return c; }

        private static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
    }
}
