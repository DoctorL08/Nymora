using Quantum;
using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// MonoBehaviour leger attache a chaque GameObject combattant cote View.
    /// Pas de logique gameplay — uniquement la representation visuelle.
    /// Le CombatantRenderer pousse la position iso a chaque update verifie.
    /// </summary>
    public class CombatantView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _sprite;

        public EntityRef Entity { get; private set; }
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public NymoraClass Class { get; private set; }

        public void Bind(EntityRef entity, NymoraClass nymoraClass)
        {
            Entity = entity;
            Class = nymoraClass;
            if (_sprite == null) _sprite = GetComponentInChildren<SpriteRenderer>();
        }

        public void UpdateGridPosition(int gx, int gy, Vector3 worldPosition)
        {
            GridX = gx;
            GridY = gy;
            transform.position = worldPosition;

            if (_sprite != null)
            {
                // Base 1000 pour garantir que les combattants passent toujours devant
                // les tiles (max sortingOrder tile = 0). Multiplicateur 10 sur (gx + gy)
                // pour preserver l'ordre iso entre combattants (celui qui a (gx+gy) plus
                // petit est plus pres de la camera, donc devant).
                // Range pour une grille 15x17 : 1000 - 30*10 = 700 (min). Toujours > 0.
                _sprite.sortingOrder = 1000 - (gx + gy) * 10;
            }
        }
    }
}
