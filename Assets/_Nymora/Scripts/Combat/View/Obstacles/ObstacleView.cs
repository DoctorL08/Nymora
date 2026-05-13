using Quantum;
using TMPro;
using UnityEngine;

namespace Nymora.Combat.View.Obstacles
{
    /// <summary>
    /// 3.1 — MonoBehaviour leger attache a chaque GameObject Obstacle cote View.
    /// Bind a une entity Quantum + display HP via TMP_Text en world space.
    ///
    /// Pas d'animation, pas de logique gameplay — uniquement la representation
    /// visuelle de l'obstacle. Le ObstacleRenderer pousse les data a chaque
    /// CallbackUpdateView.
    ///
    /// Sera enrichi en 3.3.b avec :
    ///   - Sprites par ObstacleKind (Pilier/Mur visuels distincts)
    ///   - Anim destruction (poof particle quand HP=0)
    ///   - Highlight si selectionne par un sort de targeting
    /// </summary>
    public class ObstacleView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _sprite;
        [Tooltip("HP label (TMP world space). Optionnel — si null, on n'affiche pas le HP.")]
        [SerializeField] private TextMeshPro _hpLabel;

        public EntityRef Entity { get; private set; }
        public int GridX { get; private set; }
        public int GridY { get; private set; }

        public void Bind(EntityRef entity)
        {
            Entity = entity;
            if (_sprite == null) _sprite = GetComponentInChildren<SpriteRenderer>();
            if (_hpLabel == null) _hpLabel = GetComponentInChildren<TextMeshPro>();
        }

        /// <summary>
        /// Update les data visuelles depuis le composant Obstacle Quantum (lu par valeur).
        /// Appele par ObstacleRenderer a chaque CallbackUpdateView.
        /// </summary>
        public void UpdateData(Obstacle data, Vector3 worldPos)
        {
            GridX = data.GridX;
            GridY = data.GridY;
            transform.position = worldPos;
            if (_hpLabel != null)
            {
                _hpLabel.text = $"{data.HP}/{data.MaxHP}";
            }
            if (_sprite != null)
            {
                // Sorting order : pareille convention que CombatantView (1000 - (gx+gy)*10).
                // Les obstacles partagent la meme couche que les combattants.
                _sprite.sortingOrder = 1000 - (data.GridX + data.GridY) * 10;
            }
        }
    }
}
