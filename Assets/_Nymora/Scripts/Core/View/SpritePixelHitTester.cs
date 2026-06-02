using System.Collections.Generic;
using UnityEngine;

namespace Nymora.Core.View
{
    /// <summary>
    /// Hit-test PIXEL-PARFAIT pour un SpriteRenderer : retourne vrai uniquement si un point
    /// monde tombe sur un pixel OPAQUE du sprite (alpha >= seuil), pas seulement dans sa boite
    /// englobante rectangulaire (AABB).
    ///
    /// Pourquoi : un sprite de perso a beaucoup de transparent autour (et entre) le dessin.
    /// Tester `SpriteRenderer.bounds` (AABB) declenche le hover/tooltip dans tout ce vide ->
    /// "tooltip affiche meme si la souris est loin du sprite" (bug hover hub + combat juin 2026).
    ///
    /// Place dans Nymora.Core pour etre partage par le combat (TileHoverView) ET le hub
    /// (HubAvatarHoverTooltip), sans violer le cloisonnement asmdef (Hub ne reference pas Combat).
    ///
    /// Lecture des pixels SANS Read/Write sur l'import : les atlas Aseprite sont importes
    /// `isReadable: 0`. Plutot que d'activer Read/Write sur ~30 atlas (re-import massif + churn
    /// .meta + piege connu "SerializedObject AsepriteImporter inoperant"), on fabrique une copie
    /// CPU lisible a la volee via un readback GPU (Graphics.Blit -> ReadPixels). Ca marche meme
    /// sur une texture non-readable car le GPU peut la lire. La copie est mise en cache par atlas
    /// (instanceID de la texture) -> un seul readback par atlas, partage par toutes ses frames.
    ///
    /// Hypothese : les sprites ne sont pas tournes (monde 2D, rotation z = 0). Si une rotation
    /// non-identite est detectee, on retombe sur le test AABB (le mapping lineaire bounds ->
    /// textureRect ne tiendrait plus). Pareil si le readback echoue : fallback AABB, donc jamais
    /// de regression par rapport au comportement precedent.
    /// </summary>
    public static class SpritePixelHitTester
    {
        // Alpha minimal pour considerer un pixel comme "plein". 0.1 ignore l'anti-aliasing
        // tres transparent du contour sans rogner le dessin.
        private const float AlphaHitThreshold = 0.1f;

        // Cache des copies CPU lisibles, clef = instanceID de la texture atlas source.
        private static readonly Dictionary<int, Texture2D> _readableCache = new Dictionary<int, Texture2D>();

        /// <summary>
        /// True si <paramref name="mouseWorld"/> tombe sur un pixel OPAQUE (alpha >= seuil) du sprite
        /// rendu par <paramref name="sr"/> — c.-à-d. sur le CONTOUR VISIBLE du sprite, jamais dans la
        /// zone transparente autour. Gère position / rotation / scale / flipX / flipY et le TRIM
        /// d'import (Aseprite rogne les bords transparents -> textureRect plus petit que le rect
        /// original). Fallback AABB uniquement si le readback des pixels échoue.
        ///
        /// Refonte ciblage juin 2026 (demande Lorenzo « contours à la perfection ») : on n'utilise
        /// plus la projection bounds->textureRect (fausse dès qu'il y a trim ou mesh Tight). On passe
        /// le point en espace LOCAL du sprite (InverseTransformPoint), puis pivot + PPU pour trouver
        /// le pixel dans le rect ORIGINAL, puis on décale dans le textureRect packé via
        /// textureRectOffset. Un point dans la marge rognée = transparent = miss. Résultat : la zone
        /// cliquable colle exactement au dessin, indépendamment du type de mesh.
        /// </summary>
        public static bool OverlapsOpaque(SpriteRenderer sr, Vector3 mouseWorld)
        {
            if (sr == null || sr.sprite == null) return false;

            // 1) Early-out AABB (cheap) : hors de la boite englobante monde -> jamais sur le sprite.
            //    Superset sûr (la boite englobe toujours le contenu, rotation comprise).
            Bounds b = sr.bounds;
            if (mouseWorld.x < b.min.x || mouseWorld.x > b.max.x) return false;
            if (mouseWorld.y < b.min.y || mouseWorld.y > b.max.y) return false;

            var sprite = sr.sprite;
            var tex = sprite.texture;
            if (tex == null) return true; // pas de texture -> fallback AABB
            float ppu = sprite.pixelsPerUnit;
            if (ppu <= 0f) return true;    // PPU invalide -> fallback AABB

            // 2) Point en espace LOCAL du sprite : annule position, rotation et scale du transform
            //    (le flipX/flipY du SpriteRenderer est une propriété de rendu, PAS du transform :
            //    on le gère à la main plus bas).
            Vector3 local = sr.transform.InverseTransformPoint(mouseWorld);

            // 3) Pixel dans le rect ORIGINAL (non rogné), origine en bas-gauche. pivot est en pixels
            //    relatif à ce bas-gauche, donc local*PPU + pivot recadre correctement.
            Rect rect = sprite.rect;            // rect original complet (dimensions source)
            Vector2 pivot = sprite.pivot;       // pivot en pixels (depuis le bas-gauche du rect)
            float fx = local.x * ppu + pivot.x;
            float fy = local.y * ppu + pivot.y;

            // flipX/flipY : miroir autour du centre du rect (pivot X centré 0.5 sur nos persos, donc
            // miroir-centre == miroir-pivot ; correct aussi pour les pièges/leurres centrés).
            if (sr.flipX) fx = rect.width - fx;
            if (sr.flipY) fy = rect.height - fy;

            // Hors du rect original -> pas sur le sprite.
            if (fx < 0f || fx >= rect.width || fy < 0f || fy >= rect.height) return false;

            // 4) Décalage du trim : le contenu réel vit dans textureRect, positionné dans le rect
            //    original à textureRectOffset. Un point hors de cette sous-zone a été rogné = transparent.
            Rect tr = sprite.textureRect;
            Vector2 trimOffset = sprite.textureRectOffset;
            float localContentX = fx - trimOffset.x;
            float localContentY = fy - trimOffset.y;
            if (localContentX < 0f || localContentX >= tr.width || localContentY < 0f || localContentY >= tr.height)
                return false; // dans la marge rognée -> transparent

            // 5) Pixel atlas correspondant + lecture alpha (copie lisible cachée).
            Texture2D readable = GetReadableCopy(tex);
            if (readable == null) return true; // readback impossible -> fallback AABB

            int px = Mathf.FloorToInt(tr.x + localContentX);
            int py = Mathf.FloorToInt(tr.y + localContentY);
            if (px < 0 || px >= readable.width || py < 0 || py >= readable.height) return false;

            return readable.GetPixel(px, py).a >= AlphaHitThreshold;
        }

        /// <summary>
        /// Retourne une copie CPU lisible de <paramref name="tex"/> (cachee). Construite via un
        /// readback GPU (Blit -> ReadPixels), ce qui fonctionne meme si la texture n'est pas
        /// importee en Read/Write. Retourne null si le readback echoue (ex: contexte sans GPU).
        /// </summary>
        private static Texture2D GetReadableCopy(Texture tex)
        {
            int id = tex.GetInstanceID();
            if (_readableCache.TryGetValue(id, out var cached) && cached != null) return cached;

            RenderTexture rt = RenderTexture.GetTemporary(
                tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture prev = RenderTexture.active;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                var readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false)
                {
                    name = $"ReadableCopy_{tex.name}"
                };
                readable.ReadPixels(new Rect(0f, 0f, tex.width, tex.height), 0, 0);
                readable.Apply(false, false);
                _readableCache[id] = readable;
                return readable;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SpritePixelHitTester] Readback echoue pour '{tex.name}', fallback AABB : {ex.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
