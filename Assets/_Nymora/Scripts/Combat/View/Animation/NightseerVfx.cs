using System.Collections;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// VFX procéduraux BESPOKE par sort Nightseer (Bible V7.1 — prédateur manipulateur :
    /// flèches de précision, ronces vertes, ombre violette, givre, vent, pièges).
    /// Signature (Traquenard) = ProceduralSpellVfx.PlaySignature. 100% View.
    /// </summary>
    public static class NightseerVfx
    {
        private static readonly Color Thorn = Hex("#3FE08A");   // ronces / nature
        private static readonly Color ThornDark = Hex("#1E5E47");
        private static readonly Color Shadow = Hex("#6E4FB0");  // ombre
        private static readonly Color Ice = Hex("#9FE8FF");     // givre
        private static readonly Color Arrow = Hex("#4FD0C0");   // flèche acier-teal
        private static readonly Color Dream = Hex("#B07AE0");   // onirique
        private static readonly Color Wind = Hex("#CFE8F0");    // vent
        private static readonly Color Heal = Hex("#5BE08A");

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
                case SpellId.NightseerTirPrecis: // flèche unique rapide et nette
                {
                    var p = parent; var l = layer; var dst = tb;
                    ProceduralVfx.CastCharge(parent, cb, Arrow, 0.10f, layer, Order + 2);
                    ProceduralVfx.Projectile(parent, cb, tb, Arrow, 0.14f, layer, Order + 2, onArrive: () =>
                    {
                        ProceduralVfx.Flash(p, dst, Arrow, 0.6f, 0.14f, l, Order + 4);
                        ProceduralVfx.Burst(p, dst, B(Arrow, 10, 7f, 0.08f, 0.3f, 0.04f, 0.6f, l, Order + 1));
                    });
                    return true;
                }

                case SpellId.NightseerVoleeDEpines: // volée d'épines en ligne + filet au bout
                    ProceduralVfx.Beam(parent, cb, tb, Thorn, 0.12f, 0.16f, layer, Order + 2, retract: false);
                    for (int i = 1; i <= 3; i++)
                    {
                        Vector3 p = Vector3.Lerp(casterPos, targetPos, i / 3f) + up * GroundY;
                        ProceduralVfx.Burst(parent, p, B(Thorn, 10, 3f, 0.1f, 0.4f, 0.06f, -0.8f, layer, Order + 1)); // épines qui jaillissent
                    }
                    ProceduralVfx.RingHold(parent, tg, A(ThornDark, 0.7f), new Vector2(1.0f, 0.6f), 0.8f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, tb, B(Thorn, 16, 5f, 0.12f, 0.4f, 0.05f, 0.4f, layer, Order + 1));
                    return true;

                case SpellId.NightseerDetonationOnirique: // implosion -> explosion onirique (violet)
                    host.StartCoroutine(DreamRoutine(parent, tb, tg, layer));
                    return true;

                case SpellId.NightseerFrappeDeLOmbre: // frappe d'ombre
                {
                    var p = parent; var l = layer; var dst = tb; var d = dir;
                    ProceduralVfx.Projectile(parent, cb, tb, Shadow, 0.15f, layer, Order + 2, onArrive: () =>
                    {
                        ProceduralVfx.Slash(p, dst, d, Shadow, l, Order + 4);
                        ProceduralVfx.Flash(p, dst, Shadow, 0.6f, 0.15f, l, Order + 4);
                        ProceduralVfx.Burst(p, dst, B(Shadow, 16, 6f, 0.12f, 0.4f, 0.05f, 0.5f, l, Order + 1));
                    });
                    return true;
                }

                case SpellId.NightseerSalveMortelle: // pluie de flèches en croix
                    host.StartCoroutine(SalveRoutine(parent, targetPos, layer));
                    return true;

                // ----- TACTIQUES -----
                case SpellId.NightseerMarqueDuChasseur: // verrou : crosshair projeté
                {
                    var p = parent; var l = layer; var dst = tb;
                    ProceduralVfx.Projectile(parent, cb, tb, Thorn, 0.16f, layer, Order + 2, onArrive: () =>
                    {
                        ProceduralVfx.RingHold(p, dst, A(Thorn, 0.85f), new Vector2(1.0f, 0.62f), 0.7f, l, Order - 1);
                        ProceduralVfx.Slash(p, dst, new Vector2(1f, 0f), Thorn, l, Order + 4);
                        ProceduralVfx.Slash(p, dst, new Vector2(0f, 1f), Thorn, l, Order + 4);
                    });
                    return true;
                }

                case SpellId.NightseerFiletDeRonces: // ronces qui jaillissent du sol
                    ProceduralVfx.RingHold(parent, tg, A(ThornDark, 0.8f), new Vector2(1.1f, 0.65f), 0.9f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, tg, B(Thorn, 22, 3.5f, 0.12f, 0.55f, 0.18f, -1.2f, layer, Order + 1)); // épines vers le haut
                    return true;

                case SpellId.NightseerChampDeMines: // 3 embûches dispersées (3x3)
                    foreach (var off in MineOffsets)
                    {
                        Vector3 p = tg + (Vector3)(off * 0.85f);
                        ProceduralVfx.RingHold(parent, p, A(ThornDark, 0.7f), new Vector2(0.7f, 0.45f), 0.9f, layer, Order - 1);
                        ProceduralVfx.Burst(parent, p, B(Thorn, 9, 3f, 0.1f, 0.45f, 0.08f, -0.9f, layer, Order + 1));
                    }
                    return true;

                case SpellId.NightseerBourrasque: // bourrasque de vent directionnelle
                    ProceduralVfx.Beam(parent, cb, tb, Wind, 0.20f, 0.18f, layer, Order + 2, retract: false);
                    ProceduralVfx.Slash(parent, tb, dir, Wind, layer, Order + 3);
                    ProceduralVfx.Burst(parent, tb, B(Wind, 18, 7.5f, 0.13f, 0.4f, 0.06f, -0.2f, layer, Order + 1));
                    return true;

                case SpellId.NightseerSouffleGlacial: // nova de givre autour du caster
                    ProceduralVfx.Flash(parent, cb, Ice, 0.8f, 0.18f, layer, Order + 3);
                    ProceduralVfx.Shockwave(parent, cg, Ice, 1.8f, 0.4f, layer, Order - 1);
                    ProceduralVfx.Shockwave(parent, cg, Hex("#E8FBFF"), 2.5f, 0.5f, layer, Order - 2);
                    ProceduralVfx.Burst(parent, cb, B(Ice, 30, 7f, 0.12f, 0.45f, 0.12f, 0.2f, layer, Order + 1)); // éclats de glace
                    return true;

                // ----- SURVIE -----
                case SpellId.NightseerVoileDOmbre: // disparition dans l'ombre (implosion)
                    host.StartCoroutine(VanishRoutine(parent, cb, casterPos, layer, Shadow, healFlash: false));
                    return true;

                case SpellId.NightseerPasFurtif: // blink furtif (poof violet)
                    ProceduralVfx.Flash(parent, cb, Shadow, 0.6f, 0.18f, layer, Order + 3);
                    ProceduralVfx.Burst(parent, cb, B(Shadow, 16, 4f, 0.11f, 0.4f, 0.1f, -0.3f, layer, Order + 1));
                    return true;

                case SpellId.NightseerCamouflageRonces: // dôme d'épines
                    ProceduralVfx.RingHold(parent, cb, A(Thorn, 0.8f), new Vector2(1.4f, 1.4f), 0.7f, layer, Order + 3);
                    ProceduralVfx.RingHold(parent, cg, A(ThornDark, 0.7f), new Vector2(1.6f, 0.9f), 0.7f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, cb, B(Thorn, 16, 3f, 0.12f, 0.5f, 0.55f, -0.4f, layer, Order + 1)); // épines vers l'extérieur
                    return true;

                case SpellId.NightseerSeveSauvage: // soin nature
                    ProceduralVfx.Flash(parent, cb, Heal, 0.5f, 0.25f, layer, Order + 3);
                    ProceduralVfx.RingHold(parent, cg, A(Heal, 0.6f), new Vector2(1.2f, 0.7f), 0.6f, layer, Order - 1);
                    ProceduralVfx.Aura(parent, null, casterPos + up * BaseY, Heal, 0.9f, layer, Order + 1);
                    return true;

                case SpellId.NightseerEvanescence: // évasion d'ombre + soin (panic)
                    host.StartCoroutine(VanishRoutine(parent, cb, casterPos, layer, Shadow, healFlash: true));
                    return true;

                default:
                    return false;
            }
        }

        // ----- Coroutines -----

        private static IEnumerator DreamRoutine(Transform parent, Vector3 tb, Vector3 tg, string layer)
        {
            ProceduralVfx.CastCharge(parent, tb, Dream, 0.22f, layer, Order + 2); // implosion onirique
            yield return new WaitForSecondsRealtime(0.20f);
            ProceduralVfx.Flash(parent, tb, Dream, 1.1f, 0.2f, layer, Order + 5);
            ProceduralVfx.Shockwave(parent, tg, Dream, 2.0f, 0.45f, layer, Order - 1);
            ProceduralVfx.Burst(parent, tb, B(Dream, 30, 6.5f, 0.14f, 0.5f, 0.12f, 0.3f, layer, Order + 1));
            foreach (var off in QuadOffsets)
                ProceduralVfx.Burst(parent, tb + (Vector3)(off * 0.5f), B(Dream, 8, 4f, 0.1f, 0.4f, 0.04f, 0.3f, layer, Order + 1));
        }

        private static IEnumerator SalveRoutine(Transform parent, Vector3 targetPos, string layer)
        {
            Vector3 up = Vector3.up;
            Vector3[] cells =
            {
                targetPos, targetPos + (Vector3)(new Vector2(1, 0) * 0.9f), targetPos + (Vector3)(new Vector2(-1, 0) * 0.9f),
                targetPos + (Vector3)(new Vector2(0, 1) * 0.9f), targetPos + (Vector3)(new Vector2(0, -1) * 0.9f)
            };
            ProceduralVfx.Flash(parent, targetPos + up * BodyY, Arrow, 0.9f, 0.18f, layer, Order + 4);
            ProceduralVfx.Shockwave(parent, targetPos + up * GroundY, Arrow, 2.2f, 0.45f, layer, Order - 1);
            for (int i = 0; i < cells.Length; i++)
            {
                Vector3 dst = cells[i] + up * BodyY;
                Vector3 from = cells[i] + up * 4.0f; // pluie depuis le haut
                var p = parent; var l = layer; var d = dst;
                ProceduralVfx.Projectile(parent, from, dst, Arrow, 0.18f, layer, Order + 2, onArrive: () =>
                    ProceduralVfx.Burst(p, d, B(Arrow, 12, 6f, 0.1f, 0.32f, 0.05f, 0.8f, l, Order + 1)));
                yield return new WaitForSecondsRealtime(0.04f);
            }
        }

        private static IEnumerator VanishRoutine(Transform parent, Vector3 cb, Vector3 casterPos, string layer,
                                                 Color shadow, bool healFlash)
        {
            ProceduralVfx.CastCharge(parent, cb, shadow, 0.26f, layer, Order + 2); // l'ombre se referme
            ProceduralVfx.RingHold(parent, casterPos + Vector3.up * GroundY, A(shadow, 0.7f), new Vector2(1.3f, 0.75f), 0.4f, layer, Order - 1);
            yield return new WaitForSecondsRealtime(0.22f);
            ProceduralVfx.Burst(parent, cb, B(shadow, 22, 5f, 0.12f, 0.45f, 0.12f, -0.2f, layer, Order + 1));
            if (healFlash)
            {
                ProceduralVfx.Flash(parent, cb, Heal, 0.6f, 0.22f, layer, Order + 4);
                ProceduralVfx.Aura(parent, null, casterPos + Vector3.up * BaseY, Heal, 0.8f, layer, Order + 1);
            }
        }

        // ----- Helpers -----

        private static readonly Vector2[] MineOffsets = { new Vector2(-1, -0.6f), new Vector2(1, -0.3f), new Vector2(0.2f, 1f) };
        private static readonly Vector2[] QuadOffsets = { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) };

        private static ProceduralVfx.BurstParams B(Color c, int count, float speed, float size, float life,
                                                   float radius, float gravity, string layer, int order)
            => new ProceduralVfx.BurstParams
            { Color = c, Count = count, Speed = speed, Size = size, Lifetime = life, Radius = radius, Gravity = gravity, SortingLayer = layer, SortingOrder = order };

        private static Color A(Color c, float alpha) { c.a = alpha; return c; }
        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
    }
}
