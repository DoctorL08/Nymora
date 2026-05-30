using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// VFX procéduraux BESPOKE par sort Ghostra (Bible V7.1 — spectral/lames/leurres/téléports).
    /// Thème cyan spectral. Signature (Exécution Spectrale) = PlaySignature (+ frames Kyami). 100% View.
    /// </summary>
    public static class GhostraVfx
    {
        private static readonly Color Spectral = Hex("#67C7F0");
        private static readonly Color SpectralDark = Hex("#2C5C77");
        private static readonly Color Pale = Hex("#BFE8FF");
        private static readonly Color Shadow = Hex("#3A6E8C");
        private static readonly Color Bleed = Hex("#C85A6A");

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
                case SpellId.GhostraLameSpectrale: // lame spectrale (mêlée)
                    ProceduralVfx.CastCharge(parent, cb, Spectral, 0.10f, layer, Order + 2);
                    ProceduralVfx.Slash(parent, tb, dir, Spectral, layer, Order + 4);
                    ProceduralVfx.Burst(parent, tb, B(Pale, 18, 5.5f, 0.13f, 0.4f, 0.06f, 0.4f, layer, Order + 1));
                    ProceduralVfx.Shockwave(parent, tg, SpectralDark, 1.2f, 0.3f, layer, Order - 1);
                    return true;

                case SpellId.GhostraFrappeFantome: // frappe fantôme : surgit de nulle part
                    ProceduralVfx.CastCharge(parent, tb, Spectral, 0.14f, layer, Order + 2); // matérialisation
                    ProceduralVfx.Flash(parent, tb, Pale, 0.9f, 0.16f, layer, Order + 5);
                    ProceduralVfx.Slash(parent, tb, dir, Spectral, layer, Order + 4);
                    ProceduralVfx.Burst(parent, tb, B(Spectral, 22, 6.5f, 0.13f, 0.45f, 0.08f, 0.4f, layer, Order + 1));
                    return true;

                case SpellId.GhostraLameVoraceSpectrale: // combo bleed : double lame
                    ProceduralVfx.Slash(parent, tb, Rotate(dir, 22f), Spectral, layer, Order + 4);
                    ProceduralVfx.Slash(parent, tb, Rotate(dir, -22f), Spectral, layer, Order + 4);
                    ProceduralVfx.Burst(parent, tb, B(Bleed, 12, 5f, 0.1f, 0.45f, 0.05f, 1.0f, layer, Order + 1)); // saignement
                    ProceduralVfx.Burst(parent, tb, B(Pale, 12, 6f, 0.1f, 0.35f, 0.05f, 0.3f, layer, Order + 1));
                    return true;

                case SpellId.GhostraSaigneAme: // saigne-âme : lance l'âme (range 2)
                {
                    var p = parent; var l = layer; var dst = tb; var d = dir;
                    ProceduralVfx.Projectile(parent, cb, tb, Spectral, 0.16f, layer, Order + 2, onArrive: () =>
                    {
                        ProceduralVfx.Slash(p, dst, d, Spectral, l, Order + 4);
                        ProceduralVfx.Burst(p, dst, B(Bleed, 16, 5f, 0.11f, 0.45f, 0.06f, 0.8f, l, Order + 1));
                        ProceduralVfx.Flash(p, dst, Pale, 0.5f, 0.14f, l, Order + 5);
                    });
                    return true;
                }

                case SpellId.GhostraDanseDesLames: // danse des lames : tourbillon (AoE 1 autour)
                    for (int a = 0; a < 5; a++)
                        ProceduralVfx.Slash(parent, cb, AngleDir(a * 72f), Spectral, layer, Order + 4);
                    ProceduralVfx.Shockwave(parent, cg, Spectral, 1.8f, 0.4f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, cb, B(Pale, 26, 7f, 0.12f, 0.45f, 0.1f, 0.2f, layer, Order + 1));
                    return true;

                // ----- TACTIQUES -----
                case SpellId.GhostraRepliqueFantome: // un leurre se matérialise
                    ProceduralVfx.Flash(parent, tb, Spectral, 0.7f, 0.2f, layer, Order + 4);
                    ProceduralVfx.RingHold(parent, tg, A(SpectralDark, 0.7f), new Vector2(1.0f, 0.6f), 0.6f, layer, Order - 1);
                    ProceduralVfx.Aura(parent, null, targetPos + up * BaseY, Spectral, 0.8f, layer, Order + 1); // silhouette qui monte
                    return true;

                case SpellId.GhostraPasDansLOmbre: // blink + leurre
                    ProceduralVfx.Flash(parent, cb, Spectral, 0.6f, 0.18f, layer, Order + 3);
                    ProceduralVfx.RingHold(parent, cg, A(SpectralDark, 0.6f), new Vector2(1.1f, 0.65f), 0.45f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, cb, B(Spectral, 16, 4f, 0.11f, 0.4f, 0.1f, -0.2f, layer, Order + 1));
                    return true;

                case SpellId.GhostraPermutation: // swap avec un de ses leurres : blink spectral (invisible adversaire)
                    ProceduralVfx.Flash(parent, cb, Spectral, 0.5f, 0.16f, layer, Order + 3);
                    ProceduralVfx.RingHold(parent, cg, A(SpectralDark, 0.55f), new Vector2(1.0f, 0.6f), 0.4f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, cb, B(Spectral, 12, 4f, 0.10f, 0.38f, 0.09f, -0.1f, layer, Order + 1));
                    return true;

                case SpellId.GhostraDagueLancee: // dague spectrale lancée
                {
                    var p = parent; var l = layer; var dst = tb;
                    ProceduralVfx.Projectile(parent, cb, tb, Pale, 0.13f, layer, Order + 2, onArrive: () =>
                    {
                        ProceduralVfx.Slash(p, dst, new Vector2(1f, 0.3f), Spectral, l, Order + 4);
                        ProceduralVfx.Burst(p, dst, B(Spectral, 10, 6f, 0.09f, 0.3f, 0.04f, 0.5f, l, Order + 1));
                    });
                    return true;
                }

                case SpellId.GhostraMarqueDeLOmbre: // marque de l'ombre (sceau)
                {
                    var p = parent; var l = layer; var dst = tb;
                    ProceduralVfx.Projectile(parent, cb, tb, Shadow, 0.16f, layer, Order + 2, onArrive: () =>
                    {
                        ProceduralVfx.RingHold(p, dst, A(Shadow, 0.85f), new Vector2(1.0f, 0.62f), 0.7f, l, Order - 1);
                        ProceduralVfx.Slash(p, dst, new Vector2(1f, 0f), Shadow, l, Order + 4);
                        ProceduralVfx.Slash(p, dst, new Vector2(0f, 1f), Shadow, l, Order + 4);
                    });
                    return true;
                }

                // ----- SURVIE -----
                case SpellId.GhostraVoileSpectral: // voile spectral : purge (reset DoT)
                    ProceduralVfx.Flash(parent, cb, Pale, 0.7f, 0.22f, layer, Order + 4);
                    ProceduralVfx.Shockwave(parent, cb, Spectral, 1.6f, 0.4f, layer, Order + 1); // vague qui lave
                    ProceduralVfx.Aura(parent, null, casterPos + up * BaseY, Spectral, 0.8f, layer, Order + 1);
                    return true;

                case SpellId.GhostraLinceulDOmbres: // linceul d'ombres (bouclier épineux)
                    ProceduralVfx.RingHold(parent, cb, A(SpectralDark, 0.85f), new Vector2(1.4f, 1.4f), 0.7f, layer, Order + 3);
                    ProceduralVfx.RingHold(parent, cg, A(Shadow, 0.7f), new Vector2(1.6f, 0.9f), 0.7f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, cb, B(Spectral, 16, 3f, 0.12f, 0.5f, 0.5f, -0.3f, layer, Order + 1)); // pointes spectrales
                    return true;

                case SpellId.GhostraRepliqueProtectrice: // réplique protectrice (dôme fantôme)
                    ProceduralVfx.RingHold(parent, cb, A(Spectral, 0.8f), new Vector2(1.4f, 1.4f), 0.7f, layer, Order + 3);
                    ProceduralVfx.Flash(parent, cb, Pale, 0.6f, 0.2f, layer, Order + 4);
                    ProceduralVfx.Aura(parent, null, casterPos + up * BaseY, Spectral, 0.8f, layer, Order + 1);
                    return true;

                case SpellId.GhostraDernierPas: // dernier pas (dash fantôme)
                    ProceduralVfx.Flash(parent, cb, Pale, 0.5f, 0.16f, layer, Order + 3);
                    ProceduralVfx.Burst(parent, cb, B(Spectral, 14, 5f, 0.1f, 0.35f, 0.08f, -0.1f, layer, Order + 1));
                    return true;

                case SpellId.GhostraPasDeLAuDela: // pas de l'au-delà (phase défensive)
                    ProceduralVfx.CastCharge(parent, cb, Spectral, 0.2f, layer, Order + 2);
                    ProceduralVfx.RingHold(parent, cg, A(SpectralDark, 0.7f), new Vector2(1.2f, 0.7f), 0.4f, layer, Order - 1);
                    ProceduralVfx.Burst(parent, cb, B(Pale, 16, 4f, 0.11f, 0.4f, 0.1f, -0.2f, layer, Order + 1));
                    return true;

                default:
                    return false;
            }
        }

        // ----- Helpers -----

        private static Vector2 AngleDir(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r) * 0.6f); // léger squash iso
        }

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
