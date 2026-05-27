using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// VFX procéduraux BESPOKE par sort Necram (Bible V7.1 — venin/putréfaction/brume toxique).
    /// Thème vert toxique + nécrotique violet. Signature (Virus Fatal) = PlaySignature. 100% View.
    /// </summary>
    public static class NecramVfx
    {
        private static readonly Color Venom = Hex("#8FD43A");
        private static readonly Color VenomDark = Hex("#3C5A1E");
        private static readonly Color Acid = Hex("#C8E04A");
        private static readonly Color Necro = Hex("#7E5AA0"); // nécrotique violet
        private static readonly Color Slime = Hex("#6FB83A");
        private static readonly Color Heal = Hex("#7BE060");

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
                case SpellId.NecramCrachatAcide: // crachat acide projeté
                {
                    var p = parent; var l = layer; var dst = tb; var g = tg;
                    ProceduralVfx.Projectile(parent, cb, tb, Acid, 0.18f, layer, Order + 2, onArrive: () =>
                    {
                        ProceduralVfx.Flash(p, dst, Acid, 0.6f, 0.14f, l, Order + 4);
                        ProceduralVfx.Burst(p, dst, B(Acid, 18, 4.5f, 0.13f, 0.45f, 0.06f, 1.4f, l, Order + 1)); // éclaboussure qui retombe
                        ProceduralVfx.Zone(p, g, A(Venom, 0.7f), 1.2f, l, Order - 2);
                    });
                    return true;
                }

                case SpellId.NecramMorsurePutride: // morsure : mâchoires qui se referment
                    ProceduralVfx.CastCharge(parent, cb, Venom, 0.10f, layer, Order + 2);
                    ProceduralVfx.Slash(parent, tb, Rotate(dir, 42f), Venom, layer, Order + 4);
                    ProceduralVfx.Slash(parent, tb, Rotate(dir, -42f), Venom, layer, Order + 4);
                    ProceduralVfx.Burst(parent, tb, B(VenomDark, 22, 5f, 0.15f, 0.45f, 0.06f, 1.0f, layer, Order + 1));
                    ProceduralVfx.Flash(parent, tb, Venom, 0.6f, 0.14f, layer, Order + 5);
                    return true;

                case SpellId.NecramDetonationVirulente: // détonation virulente
                    ProceduralVfx.Flash(parent, tb, Venom, 1.0f, 0.18f, layer, Order + 5);
                    ProceduralVfx.Shockwave(parent, tg, Venom, 2.0f, 0.45f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, tb, B(Venom, 32, 7f, 0.14f, 0.5f, 0.12f, 0.4f, layer, Order + 1));
                    ProceduralVfx.Burst(parent, tb, B(Acid, 16, 5f, 0.1f, 0.45f, 0.06f, -0.6f, layer, Order + 1)); // spores qui montent
                    return true;

                case SpellId.NecramFauxDecharnee: // faux qui balaie (mêlée AoE 1)
                    ProceduralVfx.Slash(parent, tb, dir, VenomDark, layer, Order + 4);
                    ProceduralVfx.Slash(parent, tb, Rotate(dir, 60f), Venom, layer, Order + 4);
                    ProceduralVfx.Shockwave(parent, tg, Venom, 1.5f, 0.32f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, tb, B(VenomDark, 20, 5.5f, 0.14f, 0.45f, 0.18f, 0.6f, layer, Order + 1));
                    return true;

                case SpellId.NecramBrumeToxique: // brume toxique 3x3
                    ProceduralVfx.Zone(parent, tg, A(Venom, 0.8f), 2.8f, layer, Order - 2);
                    foreach (var off in QuadOffsets)
                        ProceduralVfx.Zone(parent, tg + (Vector3)(off * 0.9f), A(VenomDark, 0.7f), 2.6f, layer, Order - 2);
                    ProceduralVfx.Shockwave(parent, tg, Venom, 1.8f, 0.4f, layer, Order - 1);
                    return true;

                // ----- TACTIQUES -----
                case SpellId.NecramInoculation: // aiguille / dard d'inoculation
                {
                    var p = parent; var l = layer; var dst = tb;
                    ProceduralVfx.Projectile(parent, cb, tb, Acid, 0.14f, layer, Order + 2, onArrive: () =>
                    {
                        ProceduralVfx.RingHold(p, dst, A(Venom, 0.8f), new Vector2(0.7f, 0.45f), 0.6f, l, Order - 1);
                        ProceduralVfx.Burst(p, dst, B(Venom, 8, 4f, 0.09f, 0.3f, 0.03f, 0.4f, l, Order + 1));
                    });
                    return true;
                }

                case SpellId.NecramContagion: // propagation : éclate et se répand
                {
                    var p = parent; var l = layer; var dst = tb;
                    ProceduralVfx.Projectile(parent, cb, tb, Venom, 0.18f, layer, Order + 2, onArrive: () =>
                    {
                        ProceduralVfx.Burst(p, dst, B(Venom, 18, 5f, 0.12f, 0.45f, 0.06f, 0.3f, l, Order + 1));
                        foreach (var off in QuadOffsets)
                        {
                            Vector3 nb = dst + (Vector3)(off * 0.85f);
                            ProceduralVfx.Beam(p, dst, nb, A(Venom, 0.8f), 0.08f, 0.22f, l, Order + 1, retract: false); // vrilles
                            ProceduralVfx.Burst(p, nb, B(VenomDark, 7, 3f, 0.1f, 0.4f, 0.05f, 0.3f, l, Order + 1));
                        }
                    });
                    return true;
                }

                case SpellId.NecramMarqueSacrificielle: // sceau sacrificiel (nécrotique)
                {
                    var p = parent; var l = layer; var dst = tb;
                    ProceduralVfx.Projectile(parent, cb, tb, Necro, 0.18f, layer, Order + 2, onArrive: () =>
                    {
                        ProceduralVfx.RingHold(p, dst, A(Necro, 0.85f), new Vector2(1.0f, 0.62f), 0.7f, l, Order - 1);
                        ProceduralVfx.Slash(p, dst, new Vector2(1f, 0f), Necro, l, Order + 4);
                        ProceduralVfx.Slash(p, dst, new Vector2(0f, 1f), Necro, l, Order + 4);
                    });
                    return true;
                }

                case SpellId.NecramPasSpectral: // phase spectrale (self)
                    ProceduralVfx.CastCharge(parent, cb, Venom, 0.22f, layer, Order + 2);
                    ProceduralVfx.RingHold(parent, cg, A(VenomDark, 0.7f), new Vector2(1.2f, 0.7f), 0.4f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, cb, B(Venom, 18, 4f, 0.12f, 0.45f, 0.12f, -0.2f, layer, Order + 1));
                    return true;

                case SpellId.NecramSymbioseMorbide: // symbiose : aura de lifesteal pulsée
                    ProceduralVfx.Flash(parent, cb, Venom, 0.6f, 0.2f, layer, Order + 3);
                    ProceduralVfx.Aura(parent, null, casterPos + up * BaseY, Venom, 1.2f, layer, Order + 1);
                    ProceduralVfx.RingHold(parent, cg, A(Necro, 0.7f), new Vector2(1.3f, 0.75f), 0.6f, layer, Order - 1);
                    return true;

                // ----- SURVIE -----
                case SpellId.NecramVoilePestilence: // aura pestilentielle défensive
                    ProceduralVfx.Aura(parent, null, casterPos + up * BaseY, Venom, 1.2f, layer, Order + 1);
                    ProceduralVfx.RingHold(parent, cg, A(VenomDark, 0.75f), new Vector2(1.6f, 0.9f), 0.7f, layer, Order - 1);
                    ProceduralVfx.Zone(parent, cg, A(Venom, 0.6f), 1.4f, layer, Order - 2);
                    return true;

                case SpellId.NecramCarapaceVisqueuse: // carapace visqueuse (dôme de slime)
                    ProceduralVfx.RingHold(parent, cb, A(Slime, 0.8f), new Vector2(1.4f, 1.4f), 0.7f, layer, Order + 3);
                    ProceduralVfx.RingHold(parent, cg, A(VenomDark, 0.7f), new Vector2(1.6f, 0.9f), 0.7f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, cb, B(Slime, 14, 2.5f, 0.14f, 0.5f, 0.3f, 1.0f, layer, Order + 1)); // gouttes qui coulent
                    return true;

                case SpellId.NecramDrainVital: // drain : la vie est aspirée vers le Necram
                    ProceduralVfx.Beam(parent, cb, tb, Venom, 0.14f, 0.30f, layer, Order + 2, retract: true); // aspire vers le caster
                    ProceduralVfx.Burst(parent, tb, B(Necro, 14, 4f, 0.11f, 0.35f, 0.06f, 0.4f, layer, Order + 1));
                    ProceduralVfx.Flash(parent, cb, Heal, 0.5f, 0.2f, layer, Order + 4);
                    ProceduralVfx.Aura(parent, null, casterPos + up * BaseY, Heal, 0.7f, layer, Order + 1);
                    return true;

                case SpellId.NecramPulseSanguinVert: // Régénération Nécrotique (heal self)
                    ProceduralVfx.Flash(parent, cb, Heal, 0.5f, 0.25f, layer, Order + 3);
                    ProceduralVfx.RingHold(parent, cg, A(Heal, 0.55f), new Vector2(1.2f, 0.7f), 0.6f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, cb, B(Venom, 14, 2.5f, 0.12f, 0.6f, 0.18f, -1.0f, layer, Order + 1)); // spores curatives montantes
                    ProceduralVfx.Aura(parent, null, casterPos + up * BaseY, Heal, 0.9f, layer, Order + 1);
                    return true;

                case SpellId.NecramCoconPutride: // cocon putride (encapsule + soigne)
                    ProceduralVfx.RingHold(parent, cb, A(VenomDark, 0.85f), new Vector2(1.2f, 1.2f), 1.0f, layer, Order + 3);
                    ProceduralVfx.RingHold(parent, cg, A(Venom, 0.6f), new Vector2(1.3f, 0.75f), 1.0f, layer, Order - 1);
                    ProceduralVfx.Flash(parent, cb, Venom, 0.6f, 0.25f, layer, Order + 4);
                    ProceduralVfx.Burst(parent, cb, B(Slime, 12, 2f, 0.13f, 0.6f, 0.25f, 0.6f, layer, Order + 1));
                    return true;

                default:
                    return false;
            }
        }

        // ----- Helpers -----

        private static readonly Vector2[] QuadOffsets = { new Vector2(1, 0.4f), new Vector2(-1, 0.4f), new Vector2(0.4f, -1), new Vector2(-0.4f, 1) };

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
