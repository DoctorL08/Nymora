using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// AssetPostprocessor qui applique automatiquement les settings sprite Nymora a
    /// l'import :
    ///   - Texture Type : Sprite (2D and UI)
    ///   - Filter Mode  : Point (pixel art, pas de filtre bilineaire)
    ///   - Compression  : None (pas de perte de qualite)
    ///   - PPU          : 128 (1 tile = 1 unite Unity)
    ///   - Mip Maps     : OFF (2D, pas de zoom out)
    ///   - Mesh Type    : FullRect (safe pour pixel art, evite glitches sur transparents)
    ///   - Alpha        : Is Transparency = true
    ///   - Pivot        : BottomCenter pour les sprites perso (doc designer §2.1), Center pour icones
    ///
    /// SCOPE RESTREINT : s'applique UNIQUEMENT aux sprites de classes pixel-art
    /// (Sprites/{Class}/Base/...) et aux icones. Ne touche PAS :
    ///   - Sprites/Combatants/*_Placeholder.png (placeholders historiques, settings differents)
    ///   - Sprites/TilePlaceholder.png (PPU 64 + Tight mesh, cassait la grille sinon)
    ///   - Tout autre dossier hors Classes/Icons
    ///
    /// Pour les .aseprite : Lorenzo configure le PPU = 128 et le pivot BottomCenter
    /// manuellement dans l'Inspector AsepriteImporter (le package com.unity.2d.aseprite
    /// a sa propre API d'import qu'on laisse en defaut hors PPU/pivot).
    /// </summary>
    public class NymoraSpriteImporterSettings : AssetPostprocessor
    {
        private const float PixelsPerUnit = 128f;

        // Sprites character : pivot bas-centre (les pieds touchent la case).
        // Ajouter Nightseer/Colossar/Necram/Ghostra ici quand le designer les livre.
        private static readonly string[] CharacterSpriteRoots =
        {
            "Assets/_Nymora/Art/Sprites/Soulrender/Base/",
            // "Assets/_Nymora/Art/Sprites/Nightseer/Base/",
            // "Assets/_Nymora/Art/Sprites/Colossar/Base/",
            // "Assets/_Nymora/Art/Sprites/Necram/Base/",
            // "Assets/_Nymora/Art/Sprites/Ghostra/Base/",
        };

        // Icones : pivot centre.
        private static readonly string[] IconRoots =
        {
            "Assets/_Nymora/Art/Sprites/Soulrender/soulrender_icons/",
            // "Assets/_Nymora/Art/Sprites/Nightseer/nightseer_icons/",
            "Assets/_Nymora/Art/UI/Icons/",
        };

        void OnPreprocessTexture()
        {
            bool isChar = StartsWithAny(assetPath, CharacterSpriteRoots);
            bool isIcon = StartsWithAny(assetPath, IconRoots);
            if (!isChar && !isIcon) return;

            var tex = assetImporter as TextureImporter;
            if (tex == null) return;

            // Settings directement sur le TextureImporter.
            tex.textureType = TextureImporterType.Sprite;
            tex.textureCompression = TextureImporterCompression.Uncompressed;

            // Le reste passe par TextureImporterSettings (API correcte pour spriteMeshType etc).
            var settings = new TextureImporterSettings();
            tex.ReadTextureSettings(settings);
            settings.spritePixelsPerUnit = PixelsPerUnit;
            settings.filterMode = FilterMode.Point;
            settings.mipmapEnabled = false;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.alphaIsTransparency = true;
            settings.wrapMode = TextureWrapMode.Clamp;

            // Pivot.
            // Icones : Center (0.5, 0.5) standard.
            // Character : pivot custom (0.5, 0.15) pour compenser l'espace transparent
            //             au bas de l'image (sprite 128x128 mais perso au centre). Valeur
            //             determinee empiriquement par test visuel.
            // SpriteAlignment enum int : Center=0, BottomCenter=7, Custom=9.
            if (isChar)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
            }
            else
            {
                settings.spriteAlignment = (int)SpriteAlignment.Center;
            }

            tex.SetTextureSettings(settings);
        }

        private static bool StartsWithAny(string path, string[] prefixes)
        {
            if (string.IsNullOrEmpty(path)) return false;
            for (int i = 0; i < prefixes.Length; i++)
            {
                if (path.StartsWith(prefixes[i])) return true;
            }
            return false;
        }
    }
}
