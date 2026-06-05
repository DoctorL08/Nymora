using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// #23 (5 juin) — CONTOUR DE CASE pour le marquage d'équipe en match miroir.
    ///
    /// Trace le contour de la CASE iso (losange) en couleur d'équipe via un LineRenderer.
    /// Deux points d'entrée :
    ///   • <see cref="Refresh(TileView,float,float,bool,int)"/> — OBJETS de map STATIQUES
    ///     (pièges, terrain, pilliers) : le contour est enfant de la TILE.
    ///   • <see cref="RefreshOnUnit"/> — UNITÉS MOBILES (combattants, leurres) : le contour est
    ///     enfant de l'UNITÉ, posé au SOL (compense l'offset visuel + annule le scale du parent,
    ///     squash de hit / scale des leurres) pour une taille monde constante, et suit le lerp.
    ///
    /// Rendu propre et CONSTANT quel que soit le sprite posé dessus (zéro dépendance à l'art).
    /// Remplace l'ancienne technique "8 copies décalées du sprite" qui dédoublait le contenu texturé.
    ///
    /// La palette (2 couleurs) + l'épaisseur sont poussées par MirrorOwnershipOutlineView (config
    /// de scène). Pure View, zéro impact simulation.
    /// </summary>
    public static class MirrorOutlineHelper
    {
        private const string OutlineRootName = "MirrorTileOutline";

        // Le contour des objets de map rend AU-DESSUS du surlignage de portée (HighlightSortingOffset)
        // et des overlays au sol (pièges/terrain à +1) -> visible pendant le ciblage, frame le glyphe.
        private const int OutlineSortingBump = 6;

        private static Color _teamP0 = new Color(0.32f, 0.66f, 1f, 1f);   // bleu
        private static Color _teamP1 = new Color(1f, 0.55f, 0.16f, 1f);   // orange
        private static float _widthWorld = 0.06f;                         // épaisseur du trait (unités monde)

        private static Material _lineMat;

        public static void SetPalette(Color p0, Color p1) { _teamP0 = p0; _teamP1 = p1; }
        public static void SetWidth(float widthWorld) { _widthWorld = Mathf.Max(0.005f, widthWorld); }

        public static Color ColorForOwner(int owner) => owner == 0 ? _teamP0 : _teamP1;

        // Matériau partagé du trait : Sprites/Default (built-in, jamais strippé, URP 2D OK). Respecte
        // la vertex color donnée par LineRenderer.startColor/endColor -> une seule instance pour les
        // deux équipes (la couleur ne vient pas du matériau).
        private static Material LineMat
        {
            get
            {
                if (_lineMat == null)
                {
                    _lineMat = new Material(Shader.Find("Sprites/Default"))
                    {
                        name = "MirrorTileOutlineLine (runtime)"
                    };
                }
                return _lineMat;
            }
        }

        // -----------------------------------------------------------------------------------------
        // OBJETS DE MAP STATIQUES — contour enfant de la TILE (centre = position de la tile, scale 1).
        // -----------------------------------------------------------------------------------------
        public static void Refresh(TileView tile, float tileW, float tileH, bool show, int owner)
        {
            if (tile == null) return;
            if (!ShouldShow(show, owner, tileW, tileH)) { HideIfPresent(tile.transform); return; }

            var lr = GetOrCreate(tile.transform);
            var t = lr.transform;
            t.localPosition = Vector3.zero;
            t.localScale = Vector3.one;
            SetDiamond(lr, tileW * 0.5f, tileH * 0.5f);
            ApplyColorWidth(lr, owner);
            lr.sortingLayerName = tile.SortingLayerName;
            lr.sortingOrder = tile.SortingOrder + TileView.HighlightSortingOffset + OutlineSortingBump;
        }

        // -----------------------------------------------------------------------------------------
        // UNITÉS MOBILES — contour enfant de l'UNITÉ, posé au SOL (groundWorld). Compense l'offset
        // visuel du parent (InverseTransformPoint) ET son scale (lossyScale -> localScale inverse)
        // pour que le losange reste à la taille EXACTE de la case malgré le squash de hit ou le
        // scale des leurres. Le sorting est fourni par l'appelant (= juste derrière le sprite).
        // -----------------------------------------------------------------------------------------
        public static void RefreshOnUnit(Transform parent, Vector3 groundWorld, float tileW, float tileH,
                                         bool show, int owner, int sortingLayerId, int sortingOrder)
        {
            if (parent == null) return;
            if (!ShouldShow(show, owner, tileW, tileH)) { HideIfPresent(parent); return; }

            var lr = GetOrCreate(parent);
            var t = lr.transform;
            t.localPosition = parent.InverseTransformPoint(groundWorld);
            Vector3 ls = parent.lossyScale;
            float sx = Mathf.Abs(ls.x) < 1e-4f ? 1f : Mathf.Abs(ls.x);
            float sy = Mathf.Abs(ls.y) < 1e-4f ? 1f : Mathf.Abs(ls.y);
            t.localScale = new Vector3(1f / sx, 1f / sy, 1f); // annule le scale parent -> taille monde constante
            SetDiamond(lr, tileW * 0.5f, tileH * 0.5f);
            ApplyColorWidth(lr, owner);
            lr.sortingLayerID = sortingLayerId;
            lr.sortingOrder = sortingOrder;
        }

        private static bool ShouldShow(bool show, int owner, float tw, float th)
            => show && owner >= 0 && owner <= 1 && tw > 0f && th > 0f;

        private static void HideIfPresent(Transform parent)
        {
            var existing = parent.Find(OutlineRootName);
            if (existing != null && existing.gameObject.activeSelf) existing.gameObject.SetActive(false);
        }

        private static LineRenderer GetOrCreate(Transform parent)
        {
            var existing = parent.Find(OutlineRootName);
            LineRenderer lr = existing != null ? existing.GetComponent<LineRenderer>() : Create(parent);
            if (!lr.gameObject.activeSelf) lr.gameObject.SetActive(true);
            return lr;
        }

        // 4 coins du losange iso d'UNE case, autour de l'origine locale du LineRenderer.
        private static void SetDiamond(LineRenderer lr, float hw, float hh)
        {
            lr.SetPosition(0, new Vector3(hw, 0f, 0f));   // droite
            lr.SetPosition(1, new Vector3(0f, hh, 0f));   // haut
            lr.SetPosition(2, new Vector3(-hw, 0f, 0f));  // gauche
            lr.SetPosition(3, new Vector3(0f, -hh, 0f));  // bas
        }

        private static void ApplyColorWidth(LineRenderer lr, int owner)
        {
            Color col = ColorForOwner(owner);
            lr.startColor = col;
            lr.endColor = col;
            lr.startWidth = _widthWorld;
            lr.endWidth = _widthWorld;
        }

        private static LineRenderer Create(Transform parent)
        {
            var go = new GameObject(OutlineRootName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;   // positions locales -> suit le parent
            lr.loop = true;             // losange fermé
            lr.positionCount = 4;
            lr.alignment = LineAlignment.View;          // billboard plan écran (caméra ortho 2D)
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCornerVertices = 2;   // joints de coins propres
            lr.numCapVertices = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = LineMat;   // partagé (pas .material qui clone 1 matériau/instance)
            return lr;
        }
    }
}
