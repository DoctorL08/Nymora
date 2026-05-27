using UnityEngine;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Generateur procedural d'une ECLABOUSSURE DE SANG (dark fantasy / trash) servant de fond
    /// au flottant de degats signature "SMASH!!" (cf FloatingTextManager.SpawnSignatureHit).
    ///
    /// Compose :
    ///   - une tache centrale organique (bord ondule, pas une etoile),
    ///   - des gouttelettes satellites projetees autour,
    ///   - quelques coulures/gouttes qui pendent vers le bas,
    ///   - un grain "crasseux" (speckle) + assombrissement vers les bords.
    /// Palette cramoisi -> presque noir. 100% View, zero asset (peint dans une Texture2D), cache
    /// de quelques variantes. Leger (256px, 2 variantes) + Prewarm() pour eviter tout hitch.
    /// </summary>
    public static class BloodSplatSprite
    {
        private const int TexSize = 256;
        private const int VariantCount = 2;

        private static Sprite[] _variants;
        private static Sprite _drip;

        /// <summary>Pre-genere le cache (a appeler au chargement de la scene combat). No-op si deja fait.</summary>
        public static void Prewarm()
        {
            EnsureGenerated();
            GetDrip();
        }

        public static Sprite GetRandom()
        {
            EnsureGenerated();
            return _variants[Random.Range(0, _variants.Length)];
        }

        /// <summary>Sprite de COULURE de sang (colonne verticale + goutte au bout), pivot a etirer
        /// vers le bas pour simuler le sang qui coule de la flaque. Genere une fois, cache.</summary>
        public static Sprite GetDrip()
        {
            if (_drip != null) return _drip;
            _drip = GenerateDrip();
            return _drip;
        }

        private static void EnsureGenerated()
        {
            if (_variants != null && _variants[0] != null) return;
            _variants = new Sprite[VariantCount];
            for (int i = 0; i < VariantCount; i++)
                _variants[i] = Generate(2000 + i * 911);
        }

        private static Sprite Generate(int seed)
        {
            const float twoPi = Mathf.PI * 2f;
            int n = TexSize;
            var rng = new System.Random(seed);
            float cx = n * 0.5f;
            float cy = n * 0.5f;
            float rBase = n * 0.30f;

            // Phases des harmoniques du contour organique de la tache.
            float p1 = (float)rng.NextDouble() * twoPi;
            float p2 = (float)rng.NextDouble() * twoPi;
            float p3 = (float)rng.NextDouble() * twoPi;
            float p4 = (float)rng.NextDouble() * twoPi;

            // Gouttelettes satellites.
            const int dropN = 9;
            var dCx = new float[dropN];
            var dCy = new float[dropN];
            var dR = new float[dropN];
            for (int i = 0; i < dropN; i++)
            {
                float ang = (float)rng.NextDouble() * twoPi;
                float dist = n * (0.34f + (float)rng.NextDouble() * 0.13f);
                dCx[i] = cx + Mathf.Cos(ang) * dist;
                dCy[i] = cy + Mathf.Sin(ang) * dist;
                dR[i] = n * (0.018f + (float)rng.NextDouble() * 0.035f);
            }

            // Coulures qui pendent (vers le bas = y decroissant en coord texture).
            const int dripN = 3;
            var pX = new float[dripN];
            var pTop = new float[dripN];
            var pLen = new float[dripN];
            var pW = new float[dripN];
            for (int i = 0; i < dripN; i++)
            {
                pX[i] = cx + (float)(rng.NextDouble() * 2.0 - 1.0) * n * 0.12f;
                pTop[i] = cy - n * 0.16f;
                pLen[i] = n * (0.10f + (float)rng.NextDouble() * 0.13f);
                pW[i] = n * (0.022f + (float)rng.NextDouble() * 0.02f);
            }

            var core = new Color(0.52f, 0.05f, 0.06f, 1f);
            var edge = new Color(0.13f, 0.015f, 0.02f, 1f);
            var clear = new Color(0f, 0f, 0f, 0f);

            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float fx = x + 0.5f;
                    float fy = y + 0.5f;
                    float dx = fx - cx;
                    float dy = fy - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx);
                    if (a < 0f) a += twoPi;

                    bool inside = false;
                    float shade = 0f;   // 0 = coeur, 1 = bord (assombri)
                    float alpha = 1f;

                    // Tache centrale (contour ondule).
                    float b = rBase * (1f
                        + 0.12f * Mathf.Sin(a * 3f + p1)
                        + 0.08f * Mathf.Sin(a * 5f + p2)
                        + 0.05f * Mathf.Sin(a * 7f + p3)
                        + 0.04f * Mathf.Sin(a * 11f + p4));
                    if (r <= b)
                    {
                        inside = true;
                        shade = Mathf.Clamp01(r / b);
                        alpha = Mathf.Clamp01(b - r);
                    }
                    else
                    {
                        // Gouttelettes.
                        for (int i = 0; i < dropN; i++)
                        {
                            float ddx = fx - dCx[i];
                            float ddy = fy - dCy[i];
                            float dist = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                            if (dist < dR[i])
                            {
                                inside = true;
                                shade = 0.6f;
                                alpha = Mathf.Clamp01(dR[i] - dist);
                                break;
                            }
                        }
                        // Coulures.
                        if (!inside)
                        {
                            for (int i = 0; i < dripN; i++)
                            {
                                if (InsideDrip(fx, fy, pX[i], pTop[i], pLen[i], pW[i]))
                                {
                                    inside = true;
                                    shade = 0.78f;
                                    alpha = 1f;
                                    break;
                                }
                            }
                        }
                    }

                    if (inside)
                    {
                        Color col = Color.Lerp(core, edge, shade);
                        // Grain crasseux (speckle) -> assombrissement aleatoire par pixel.
                        float g = 0.80f + 0.20f * Hash(x, y);
                        col.r *= g; col.g *= g; col.b *= g;
                        col.a = alpha;
                        px[y * n + x] = col;
                    }
                    else
                    {
                        px[y * n + x] = clear;
                    }
                }
            }

            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        // Coulure verticale qui pend vers le bas (tip arrondi), s'affinant vers la pointe.
        private static bool InsideDrip(float px, float py, float x0, float yTop, float len, float wTop)
        {
            float yEnd = yTop - len;
            if (py <= yTop && py >= yEnd)
            {
                float tt = (yTop - py) / len; // 0 en haut, 1 a la pointe
                float halfW = Mathf.Lerp(wTop, wTop * 0.18f, tt);
                if (Mathf.Abs(px - x0) < halfW) return true;
            }
            // Goutte au bout.
            float tx = px - x0;
            float ty = py - yEnd;
            float tipR = wTop * 0.55f;
            return tx * tx + ty * ty < tipR * tipR;
        }

        // Colonne de sang qui coule + goutte au bout. Pivot haut + etirement vertical = ecoulement.
        private static Sprite GenerateDrip()
        {
            const int w = 48;
            const int h = 192;
            float cx = w * 0.5f;
            float beadR = w * 0.32f;
            float beadCy = beadR + 3f; // goutte pres du bas (y faible)

            var core = new Color(0.50f, 0.05f, 0.06f, 1f);
            var edge = new Color(0.14f, 0.015f, 0.02f, 1f);
            var clear = new Color(0f, 0f, 0f, 0f);

            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float fx = x + 0.5f;
                    float fy = y + 0.5f;
                    bool inside = false;
                    float shade = 0f;

                    // Goutte (bulbe) au bas.
                    float bx = fx - cx;
                    float by = fy - beadCy;
                    if (bx * bx + by * by < beadR * beadR)
                    {
                        inside = true;
                        shade = Mathf.Sqrt(bx * bx + by * by) / beadR;
                    }
                    // Colonne au-dessus de la goutte (s'elargit legerement vers le bas).
                    if (!inside && fy >= beadCy)
                    {
                        float tcol = Mathf.InverseLerp(h, beadCy, fy); // 0 en haut -> 1 vers la goutte
                        float halfW = Mathf.Lerp(w * 0.13f, w * 0.20f, tcol);
                        if (Mathf.Abs(fx - cx) < halfW)
                        {
                            inside = true;
                            shade = Mathf.Abs(fx - cx) / Mathf.Max(1f, halfW);
                        }
                    }

                    if (inside)
                    {
                        Color col = Color.Lerp(core, edge, shade * 0.85f);
                        float g = 0.82f + 0.18f * Hash(x, y);
                        col.r *= g; col.g *= g; col.b *= g;
                        col.a = 1f;
                        px[y * w + x] = col;
                    }
                    else
                    {
                        px[y * w + x] = clear;
                    }
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float Hash(int x, int y)
        {
            int h = (x * 73856093) ^ (y * 19349663);
            h = (h >> 13) ^ h;
            return (h & 0x7fff) / 32767f;
        }
    }
}
