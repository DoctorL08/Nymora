using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// VFX procéduraux BESPOKE par sort Colossar (Bible V7.1 — le colosse : pierre, marteau,
    /// piliers/murs, séisme, boucliers). Thème terre/ambre, lourd et massif.
    /// Signature (Effondrement) = ProceduralSpellVfx.PlaySignature. 100% View.
    /// </summary>
    public static class ColossarVfx
    {
        private static readonly Color Stone = Hex("#B8A888");
        private static readonly Color StoneDark = Hex("#6E5E45");
        private static readonly Color Amber = Hex("#E0A93A");
        private static readonly Color Dust = Hex("#C6B79C");
        private static readonly Color Iron = Hex("#C8C8D0");
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
                case SpellId.ColossarFrappeLourde: // frappe lourde mêlée
                    ProceduralVfx.CastCharge(parent, cb, Amber, 0.12f, layer, Order + 2);
                    ProceduralVfx.Slash(parent, tb, dir, Stone, layer, Order + 4);
                    ProceduralVfx.Burst(parent, tb, B(StoneDark, 22, 4f, 0.18f, 0.5f, 0.08f, 1.6f, layer, Order + 1)); // gravats lourds
                    ProceduralVfx.Shockwave(parent, tg, StoneDark, 1.6f, 0.35f, layer, Order - 1);
                    return true;

                case SpellId.ColossarOndeDeChoc: // onde de choc / push
                    ProceduralVfx.Shockwave(parent, tg, Stone, 1.6f, 0.35f, layer, Order - 1);
                    ProceduralVfx.Shockwave(parent, tg, StoneDark, 2.4f, 0.45f, layer, Order - 2);
                    ProceduralVfx.Slash(parent, tb, dir, Dust, layer, Order + 3);
                    ProceduralVfx.Burst(parent, tg, B(Dust, 18, 3.5f, 0.14f, 0.45f, 0.2f, 0.4f, layer, Order + 1));
                    return true;

                case SpellId.ColossarMarteauPunisseur: // marteau qui s'abat d'en haut
                {
                    var p = parent; var l = layer; var dst = tb; var g = tg;
                    ProceduralVfx.Projectile(parent, tb + up * 3.0f, tb, Amber, 0.18f, layer, Order + 3, onArrive: () =>
                    {
                        ProceduralVfx.Flash(p, dst, Amber, 1.0f, 0.16f, l, Order + 5);
                        ProceduralVfx.Shockwave(p, g, StoneDark, 1.8f, 0.4f, l, Order - 1);
                        ProceduralVfx.Burst(p, dst, B(StoneDark, 26, 5f, 0.16f, 0.5f, 0.08f, 1.4f, l, Order + 1));
                    });
                    return true;
                }

                case SpellId.ColossarChocSismique: // fissure sismique en ligne
                    ProceduralVfx.Beam(parent, cg, tg, StoneDark, 0.22f, 0.22f, layer, Order - 1, retract: false);
                    for (int i = 1; i <= 4; i++)
                    {
                        Vector3 pp = Vector3.Lerp(casterPos, targetPos, i / 4f) + up * GroundY;
                        ProceduralVfx.Burst(parent, pp, B(Dust, 12, 3.5f, 0.14f, 0.5f, 0.05f, -1.0f, layer, Order + 1)); // poussière qui monte
                    }
                    ProceduralVfx.Shockwave(parent, tg, Stone, 1.6f, 0.35f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, tb, B(StoneDark, 18, 5f, 0.15f, 0.45f, 0.06f, 1.2f, layer, Order + 1));
                    return true;

                case SpellId.ColossarRepresailles: // posture défensive (carapace de roche)
                    ProceduralVfx.RingHold(parent, cg, A(Stone, 0.85f), new Vector2(1.5f, 0.85f), 0.8f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, cb, B(StoneDark, 16, 3f, 0.15f, 0.5f, 0.5f, 0.2f, layer, Order + 1)); // pics de pierre
                    ProceduralVfx.Flash(parent, cb, Amber, 0.6f, 0.2f, layer, Order + 3);
                    return true;

                // ----- TACTIQUES -----
                case SpellId.ColossarPilier: // pilier de pierre qui jaillit
                    Pillar(parent, targetPos, layer);
                    return true;

                case SpellId.ColossarMurDePierre: // mur de pierre (rangée de blocs)
                {
                    Vector2 perp = Rotate(dir.sqrMagnitude < 0.0001f ? new Vector2(1, 0) : dir.normalized, 90f);
                    for (int s = -1; s <= 1; s++)
                        Pillar(parent, targetPos + (Vector3)(perp * (s * 0.9f)), layer);
                    return true;
                }

                case SpellId.ColossarAncrage: // ancre la cible au sol (entraves de pierre)
                    ProceduralVfx.RingHold(parent, tg, A(StoneDark, 0.85f), new Vector2(1.2f, 0.7f), 0.9f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, tb, B(StoneDark, 18, 4f, 0.14f, 0.5f, 0.1f, 2.0f, layer, Order + 1)); // pierres qui retombent (ancrent)
                    ProceduralVfx.Slash(parent, tg, new Vector2(1f, 0f), Stone, layer, Order + 3);
                    return true;

                case SpellId.ColossarProvocation: // provocation / taunt
                {
                    var p = parent; var l = layer; var dst = tb;
                    ProceduralVfx.Beam(parent, cb, tb, Amber, 0.16f, 0.20f, layer, Order + 2, retract: false);
                    ProceduralVfx.RingHold(parent, tb, A(Amber, 0.8f), new Vector2(1.0f, 0.62f), 0.55f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, tb, B(Amber, 12, 4f, 0.12f, 0.35f, 0.05f, 0.3f, layer, Order + 1));
                    return true;
                }

                case SpellId.ColossarBrisure: // brisure / dispel (éclats tranchants)
                    ProceduralVfx.Flash(parent, tb, Hex("#EFE6D0"), 0.9f, 0.14f, layer, Order + 5);
                    ProceduralVfx.Burst(parent, tb, B(Iron, 28, 9f, 0.10f, 0.3f, 0.04f, 0.2f, layer, Order + 1)); // éclats nets rapides
                    ProceduralVfx.Slash(parent, tb, new Vector2(1f, 0.4f), Iron, layer, Order + 4);
                    ProceduralVfx.Slash(parent, tb, new Vector2(-1f, 0.4f), Iron, layer, Order + 4);
                    return true;

                // ----- SURVIE -----
                case SpellId.ColossarStoicisme: // dôme de pierre (bouclier)
                    ProceduralVfx.RingHold(parent, cb, A(Stone, 0.85f), new Vector2(1.4f, 1.4f), 0.8f, layer, Order + 3);
                    ProceduralVfx.RingHold(parent, cg, A(StoneDark, 0.7f), new Vector2(1.6f, 0.9f), 0.8f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, cb, B(Stone, 12, 3f, 0.15f, 0.45f, 0.35f, 0.2f, layer, Order + 1));
                    ProceduralVfx.Flash(parent, cb, Amber, 0.6f, 0.2f, layer, Order + 4);
                    return true;

                case SpellId.ColossarGardeProtectrice: // réduction continue (garde de pierre)
                    ProceduralVfx.Aura(parent, null, casterPos + up * BaseY, Stone, 1.1f, layer, Order + 1);
                    ProceduralVfx.RingHold(parent, cg, A(StoneDark, 0.7f), new Vector2(1.3f, 0.75f), 0.7f, layer, Order - 1);
                    ProceduralVfx.Flash(parent, cb, Stone, 0.5f, 0.2f, layer, Order + 3);
                    return true;

                case SpellId.ColossarRessacVital: // heal réactionnel (terre vivifiante)
                    ProceduralVfx.Flash(parent, cb, Heal, 0.5f, 0.25f, layer, Order + 3);
                    ProceduralVfx.RingHold(parent, cg, A(Heal, 0.55f), new Vector2(1.2f, 0.7f), 0.6f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, cb, B(Stone, 12, 2.5f, 0.13f, 0.6f, 0.18f, -0.9f, layer, Order + 1)); // motes de terre montants
                    ProceduralVfx.Aura(parent, null, casterPos + up * BaseY, Heal, 0.9f, layer, Order + 1);
                    return true;

                case SpellId.ColossarRenvoiDuBouclier: // renvoi du bouclier (onde de garde)
                    ProceduralVfx.RingHold(parent, cb, A(Iron, 0.8f), new Vector2(1.3f, 1.3f), 0.5f, layer, Order + 3);
                    ProceduralVfx.Shockwave(parent, cb, Iron, 2.0f, 0.4f, layer, Order + 1);
                    ProceduralVfx.Flash(parent, cb, Iron, 0.7f, 0.2f, layer, Order + 4);
                    return true;

                case SpellId.ColossarSoinLourd: // heal à distance (faisceau vivifiant)
                {
                    var p = parent; var l = layer; var dst = tb; var dg = tg;
                    ProceduralVfx.Beam(parent, cb, tb, Heal, 0.14f, 0.25f, layer, Order + 2, retract: false);
                    ProceduralVfx.Flash(parent, tb, Heal, 0.6f, 0.22f, layer, Order + 4);
                    ProceduralVfx.Aura(parent, null, dst, Heal, 0.9f, layer, Order + 1);
                    ProceduralVfx.RingHold(parent, dg, A(Heal, 0.5f), new Vector2(1.0f, 0.6f), 0.6f, l, Order - 1);
                    return true;
                }

                default:
                    return false;
            }
        }

        /// <summary>Pilier de pierre qui jaillit du sol (réutilisé par Pilier + Mur de Pierre).</summary>
        private static void Pillar(Transform parent, Vector3 cellPos, string layer)
        {
            Vector3 up = Vector3.up;
            ProceduralVfx.RingHold(parent, cellPos + up * GroundY, A(Dust, 0.7f), new Vector2(1.1f, 0.65f), 0.5f, layer, Order - 1);
            ProceduralVfx.Slash(parent, cellPos + up * 0.55f, new Vector2(0f, 1f), Stone, layer, Order + 4);       // fût vertical
            ProceduralVfx.Burst(parent, cellPos + up * 0.2f, B(StoneDark, 16, 4.5f, 0.14f, 0.5f, 0.06f, -1.6f, layer, Order + 1)); // gravats qui montent
            ProceduralVfx.Burst(parent, cellPos + up * GroundY, B(Dust, 10, 3f, 0.14f, 0.4f, 0.18f, 0.3f, layer, Order + 1));      // poussière au sol
        }

        // ----- Helpers -----

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
