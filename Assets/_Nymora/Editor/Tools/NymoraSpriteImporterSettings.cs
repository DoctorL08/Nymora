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

        // 2.13.e — Sprite sheets animes (marques, terrains, VFX). Pivot centre, meme
        // traitement que les icones cote import. Le slicing multiple est applique par
        // AutoSliceFrameSheetsTool (Editor > Nymora > Setup) — pas ici.
        private static readonly string[] SheetRoots =
        {
            "Assets/_Nymora/Art/Sprites/Soulrender/Marks/",
            "Assets/_Nymora/Art/Sprites/Soulrender/Terrains/",
            "Assets/_Nymora/Art/VFX/Soulrender/",
            // Phase 3 : ajouter Nightseer/Colossar/Necram/Ghostra Marks/Terrains/VFX ici.
        };

        void OnPreprocessTexture()
        {
            bool isChar = StartsWithAny(assetPath, CharacterSpriteRoots);
            bool isIcon = StartsWithAny(assetPath, IconRoots);
            bool isSheet = StartsWithAny(assetPath, SheetRoots);
            if (!isChar && !isIcon && !isSheet) return;

            var tex = assetImporter as TextureImporter;
            if (tex == null) return;

            // Settings directement sur le TextureImporter.
            tex.textureType = TextureImporterType.Sprite;
            tex.textureCompression = TextureImporterCompression.Uncompressed;

            // Le reste passe par TextureImporterSettings (API correcte pour spriteMeshType etc).
            var settings = new TextureImporterSettings();
            tex.ReadTextureSettings(settings);
            // POLISH-5c (17 mai) — PPU NON force ici : CombatAssetsNormalizer applique un
            // PPU dynamique = max(W,H)/targetUnits pour que chaque sprite mesure pile la
            // meme taille en monde Unity peu importe sa resolution. Forcer 128 ici sabotait
            // le normalize pour les sheets Soulrender (PNG 64x64 -> 1 unite OK avec PPU=64,
            // 0.5 unite avec PPU=128 hardcode = trop petit). Pour les sprites character/icon
            // hors scope du normalizer, le PPU par defaut Unity (100) suffit ou Lorenzo le
            // configure via Inspector au cas par cas.
            settings.filterMode = FilterMode.Point;
            settings.mipmapEnabled = false;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.alphaIsTransparency = true;
            settings.wrapMode = TextureWrapMode.Clamp;

            // Pivot.
            // Icones : Center (0.5, 0.5) standard.
            // Character (2.13.e) : pivot custom (0.5, 0.30). Mesure : les sprites Soulrender
            //   ont leurs pieds a pixel Y=6 (depuis le bas) dans un frame 128x128. Avec iso 2:1
            //   et TileWorldHeight = 0.5, le bas du diamond est a tile_center - 0.25 unit.
            //   Pivot Y = feet_y_normalized + 0.25 = 0.047 + 0.25 = 0.297 ~= 0.30 met les
            //   pieds pile au bas du diamond du tile.
            //   Si le designer change la convention (perso plus haut dans le frame), ajuster
            //   ici et re-run "Nymora > Setup > Reimport Character Sprites".
            // SpriteAlignment enum int : Center=0, BottomCenter=7, Custom=9.
            if (isChar)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.5f, 0.30f);
            }
            else
            {
                settings.spriteAlignment = (int)SpriteAlignment.Center;
            }

            // POLISH-5c (17 mai) — l'override Multiple historique pour les sheets est
            // RETIRE. Raison : le designer livre maintenant des frames individuelles
            // ({nom}-export[1..N].png), plus de spritesheets a slicer. Le maintien forcait
            // marque_de_carnage et plaie_ouverte (Soulrender/Marks) en Multiple, ce qui
            // empechait LoadAssetAtPath&lt;Sprite&gt; de retourner un sprite principal et
            // sabotait CombatAssetsNormalizer.Apply. Si Lorenzo recoit un jour de vraies
            // spritesheets, reactiver localement via AutoSliceFrameSheetsTool plutot que
            // via un postprocessor global.

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
