using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Tri iso par profondeur pour un sprite statique du hub (torche, prop, statue...).
    /// 100% View.
    ///
    /// Calque la formule de profondeur de l'avatar (HubAvatar : sortingOrder = baseOrder
    /// - (gx+gy), base 100). Convertit la position monde Y du sprite en "profondeur iso"
    /// avec le meme pas que la grille (TileWorldHeight/2) -> le perso passe DEVANT la torche
    /// quand il est plus bas/devant, et DERRIERE quand il est plus haut/derriere. Automatique.
    ///
    /// Pose ce composant sur le GameObject du sprite torche. Place le pivot du sprite (ou
    /// _depthPivot) au NIVEAU DU SOL de la torche (ses "pieds") pour que la bascule se fasse
    /// au bon endroit.
    ///
    /// _baseOrder DOIT valoir la meme base que l'avatar (100) pour s'interleaver avec lui.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class IsoDepthSort : MonoBehaviour
    {
        [Tooltip("Doit matcher le base sorting order de l'avatar (HubAvatar = 100).")]
        [SerializeField] private int _baseOrder = 100;

        [Tooltip("Point qui definit la profondeur (les 'pieds' au sol). Defaut = ce transform.")]
        [SerializeField] private Transform _depthPivot;

        [Tooltip("Decalage manuel si la bascule devant/derriere est un poil trop tot/tard. " +
                 "Negatif = la torche passe plus facilement DERRIERE le perso.")]
        [SerializeField] private int _orderBias;

        private SpriteRenderer _sr;
        private HubGridRenderer _grid;

        private void Awake() => _sr = GetComponent<SpriteRenderer>();

        private void OnEnable()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            _grid = FindFirstObjectByType<HubGridRenderer>();
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (_sr == null) return;
            if (_grid == null) _grid = FindFirstObjectByType<HubGridRenderer>();
            if (_grid == null) return;

            // Snap sur la case grille exacte avec la MEME inverse-projection que le hub
            // (l'avatar fait sortingOrder = base - (gx+gy)). On calque a l'identique.
            Vector3 p = _depthPivot != null ? _depthPivot.position : transform.position;
            var (gx, gy) = IsoProjection.WorldToGrid(p, _grid.TileWorldWidth, _grid.TileWorldHeight, _grid.CenterOffset);
            _sr.sortingOrder = _baseOrder - (gx + gy) + _orderBias;
        }
    }
}
