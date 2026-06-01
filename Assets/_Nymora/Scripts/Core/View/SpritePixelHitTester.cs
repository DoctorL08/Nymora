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
        /// True si <paramref name="mouseWorld"/> tombe sur un pixel opaque du sprite rendu par
        /// <paramref name="sr"/>. Gere position/scale/flip. Fallback AABB si rotation non triviale
        /// ou si la lecture des pixels echoue.
        /// </summary>
        public static bool OverlapsOpaque(SpriteRenderer sr, Vector3 mouseWorld)
        {
            if (sr == null || sr.sprite == null) return false;

            // 1) Early-out AABB : hors de la boite englobante -> jamais sur le sprite.
            Bounds b = sr.bounds;
            if (mouseWorld.x < b.min.x || mouseWorld.x > b.max.x) return false;
            if (mouseWorld.y < b.min.y || mouseWorld.y > b.max.y) return false;

            // 2) Rotation non triviale -> le mapping lineaire ne tient plus, on garde l'AABB.
            float zAngle = sr.transform.rotation.eulerAngles.z % 360f;
            if (zAngle > 0.5f && zAngle < 359.5f)
            {
                return true;
            }

            // 3) Copie lisible de l'atlas (cache par texture).
            var sprite = sr.sprite;
            var tex = sprite.texture;
            if (tex == null) return true; // pas de texture -> fallback AABB
            Texture2D readable = GetReadableCopy(tex);
            if (readable == null) return true; // readback impossible -> fallback AABB

            // 4) Position normalisee dans l'AABB (== etendue du contenu trimme du sprite, car
            //    mesh "Tight" : bounds == textureRect en monde). Inverse selon flipX/flipY.
            float u = (mouseWorld.x - b.min.x) / b.size.x;
            float v = (mouseWorld.y - b.min.y) / b.size.y;
            if (sr.flipX) u = 1f - u;
            if (sr.flipY) v = 1f - v;

            // 5) Pixel correspondant dans l'atlas via textureRect (rect du sprite dans la texture).
            Rect tr = sprite.textureRect;
            int px = Mathf.FloorToInt(tr.x + u * tr.width);
            int py = Mathf.FloorToInt(tr.y + v * tr.height);
            px = Mathf.Clamp(px, Mathf.FloorToInt(tr.x), Mathf.FloorToInt(tr.x + tr.width) - 1);
            py = Mathf.Clamp(py, Mathf.FloorToInt(tr.y), Mathf.FloorToInt(tr.y + tr.height) - 1);
            if (px < 0 || px >= readable.width || py < 0 || py >= readable.height) return true;

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
