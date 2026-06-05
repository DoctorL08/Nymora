using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// #23 (5 juin) — Outline de SILHOUETTE pour le marquage d'équipe en match miroir.
    ///
    /// Technique : duplique le sprite de l'objet en 8 copies décalées (4 cardinales + 4 diagonales),
    /// teintées de la couleur d'équipe, rendues JUSTE DERRIÈRE l'objet. L'union des copies décalées
    /// dépasse du contour opaque du sprite → un liseré qui épouse la SILHOUETTE exacte (pas la boîte
    /// englobante transparente). Aucune dépendance shader.
    ///
    /// Les copies sont regroupées sous un enfant "MirrorOutline" du GameObject de l'objet, donc elles
    /// suivent automatiquement sa position / son flip / son anim de scale. Allumé/éteint à la demande.
    ///
    /// La palette (2 couleurs) + l'épaisseur sont poussées par MirrorOwnershipOutlineView (composant
    /// de config dans la scène). Pure View.
    /// </summary>
    public static class MirrorOutlineHelper
    {
        private const string OutlineRootName = "MirrorOutline";

        private static Color _teamP0 = new Color(0.32f, 0.66f, 1f, 1f);   // bleu
        private static Color _teamP1 = new Color(1f, 0.55f, 0.16f, 1f);   // orange
        private static float _widthWorld = 0.05f;                         // épaisseur du liseré (unités monde)

        // 8 directions (cardinales + diagonales normalisées) pour un contour régulier.
        private static readonly Vector2[] Dirs =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f),
            new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f),
        };

        public static void SetPalette(Color p0, Color p1) { _teamP0 = p0; _teamP1 = p1; }
        public static void SetWidth(float widthWorld) { _widthWorld = Mathf.Max(0.005f, widthWorld); }

        public static Color ColorForOwner(int owner) => owner == 0 ? _teamP0 : _teamP1;

        /// <summary>
        /// Crée/met à jour (ou cache) l'outline de silhouette sur le SpriteRenderer d'un objet.
        ///   show=false / owner hors {0,1} / sprite null -> outline caché.
        ///   sinon -> 8 copies teintées de la couleur d'équipe de `owner`, derrière l'objet.
        /// Idempotent : appelable chaque frame.
        /// </summary>
        public static void Refresh(SpriteRenderer obj, bool show, int owner)
        {
            if (obj == null) return;
            Transform existing = obj.transform.Find(OutlineRootName);

            if (!show || owner < 0 || owner > 1 || obj.sprite == null)
            {
                if (existing != null && existing.gameObject.activeSelf) existing.gameObject.SetActive(false);
                return;
            }

            Color col = ColorForOwner(owner);
            Transform root = existing != null ? existing : Create(obj);
            if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);

            for (int i = 0; i < Dirs.Length; i++)
            {
                Transform child = root.GetChild(i);
                child.localPosition = new Vector3(Dirs[i].x * _widthWorld, Dirs[i].y * _widthWorld, 0f);
                var sr = child.GetComponent<SpriteRenderer>();
                sr.sprite = obj.sprite;
                sr.color = col;
                sr.flipX = obj.flipX;
                sr.flipY = obj.flipY;
                sr.sortingLayerID = obj.sortingLayerID;
                sr.sortingOrder = obj.sortingOrder - 1; // juste derrière l'objet
            }
        }

        private static Transform Create(SpriteRenderer obj)
        {
            var root = new GameObject(OutlineRootName);
            root.transform.SetParent(obj.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            for (int i = 0; i < Dirs.Length; i++)
            {
                var c = new GameObject("o" + i);
                c.transform.SetParent(root.transform, false);
                c.AddComponent<SpriteRenderer>();
            }
            return root.transform;
        }
    }
}
