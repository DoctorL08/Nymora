using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Kit visuel partagé du HUD combat — aligne la DA du combat sur le menu hub « Échap »
    /// (monochrome, coins arrondis). Vit dans Nymora.Combat (l'asmdef Combat ne référence PAS
    /// Nymora.Hub, donc HubMenuTheme n'est pas accessible ici) : les valeurs ci-dessous sont la
    /// RÉPLIQUE exacte de HubMenuTheme.asset (source de vérité). Si la palette hub change, mettre
    /// à jour ces constantes en miroir.
    ///
    /// Le sprite à coins arrondis est généré par CODE (cohérent avec HubMenuUIFactory) et mis en
    /// cache par rayon, puis teinté via Image.color. 100% View.
    /// </summary>
    public static class CombatUiKit
    {
        // --- Palette monochrome (miroir HubMenuTheme) ---
        public static readonly Color PanelBg       = new Color(0.10f, 0.105f, 0.12f, 0.96f);
        public static readonly Color CardBg        = new Color(0.16f, 0.165f, 0.185f, 0.96f);
        public static readonly Color Accent        = new Color(0.93f, 0.94f, 0.96f, 1f);
        public static readonly Color GhostBg       = new Color(1f, 1f, 1f, 0.06f);
        public static readonly Color GhostBgHover  = new Color(1f, 1f, 1f, 0.12f);
        public static readonly Color TextPrimary   = new Color(0.93f, 0.94f, 0.96f, 1f);
        public static readonly Color TextSecondary = new Color(0.66f, 0.67f, 0.71f, 1f);
        public static readonly Color TextMuted     = new Color(0.45f, 0.46f, 0.50f, 1f);
        public static readonly Color TextOnLight   = new Color(0.08f, 0.085f, 0.10f, 1f);
        public static readonly Color Divider       = new Color(1f, 1f, 1f, 0.08f);

        public const float CornerRadius = 14f;

        /// <summary>Applique un fond arrondi (9-slice) à une Image, en conservant sa couleur.</summary>
        public static void ApplyRounded(Image img, float radius)
        {
            if (img == null) return;
            img.sprite = RoundedSprite(radius);
            img.type = Image.Type.Sliced;
        }

        // ===== Sprite à coins arrondis (généré + caché par rayon) =====

        private static readonly Dictionary<int, Sprite> _roundedCache = new Dictionary<int, Sprite>();

        public static Sprite RoundedSprite(float radius)
        {
            int r = Mathf.Max(2, Mathf.RoundToInt(radius));
            if (_roundedCache.TryGetValue(r, out var cached) && cached != null) return cached;

            int size = r * 2 + 4; // 4px de zone centrale étirable (9-slice)
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte a = (byte)Mathf.RoundToInt(CornerAlpha(x, y, size, r) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();

            var border = new Vector4(r, r, r, r);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = "CombatRounded_" + r;
            _roundedCache[r] = sprite;
            return sprite;
        }

        /// <summary>Alpha d'un pixel : plein partout sauf dans les 4 coins, arrondis avec anti-aliasing 1px.</summary>
        private static float CornerAlpha(int x, int y, int size, int r)
        {
            float cx = x + 0.5f, cy = y + 0.5f;
            float cornerX = -1f, cornerY = -1f;
            if (cx < r) cornerX = r; else if (cx > size - r) cornerX = size - r;
            if (cy < r) cornerY = r; else if (cy > size - r) cornerY = size - r;
            if (cornerX < 0f || cornerY < 0f) return 1f; // bord droit ou centre -> plein

            float dx = cx - cornerX, dy = cy - cornerY;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(r - dist + 0.5f);
        }
    }
}
