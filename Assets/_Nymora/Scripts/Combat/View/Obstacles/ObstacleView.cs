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

        [Tooltip("Frames pilotees par le ratio HP/MaxHP. 4 frames attendus : 100% > 75% > 50% > 25%. Vide = sprite statique.")]
        [SerializeField] private Sprite[] _hpFrames;

        public EntityRef Entity { get; private set; }
        public int GridX { get; private set; }
        public int GridY { get; private set; }

        public void Bind(EntityRef entity)
        {
            Entity = entity;
            if (_sprite == null) _sprite = GetComponentInChildren<SpriteRenderer>();
            if (_hpLabel == null) _hpLabel = GetComponentInChildren<TextMeshPro>();
            // Polish 3.3.d : HP cache par defaut, affiche uniquement au survol souris (TileHoverView).
            if (_hpLabel != null) _hpLabel.gameObject.SetActive(false);
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
                // Update le text meme si cache : TileHoverView peut l'activer instantanement.
                _hpLabel.text = $"{data.HP}/{data.MaxHP}";
            }
            if (_sprite != null)
            {
                // Sorting order : pareille convention que CombatantView (1000 - (gx+gy)*10).
                // Les obstacles partagent la meme couche que les combattants.
                _sprite.sortingOrder = 1000 - (data.GridX + data.GridY) * 10;

                // Frame piloté par ratio HP/MaxHP (4 paliers : >75% / >50% / >25% / sinon).
                // Pillar : 4 frames de degradation progressive (fissures + cristal qui s'exposent).
                // Wall : pas de frames, sprite statique inchangé.
                if (_hpFrames != null && _hpFrames.Length > 0 && data.MaxHP > 0)
                {
                    int idx;
                    int maxHp = data.MaxHP;
                    int hp = data.HP;
                    // Compare en entiers (4*hp vs maxHp*N) pour éviter tout float côté View.
                    if (4 * hp > 3 * maxHp) idx = 0;        // >75%
                    else if (4 * hp > 2 * maxHp) idx = 1;   // >50%
                    else if (4 * hp > 1 * maxHp) idx = 2;   // >25%
                    else idx = 3;                            // <=25%
                    if (idx >= _hpFrames.Length) idx = _hpFrames.Length - 1;
                    if (_hpFrames[idx] != null) _sprite.sprite = _hpFrames[idx];
                }
            }
        }

        /// <summary>
        /// Polish 3.3.d : affiche ou cache le HP label. Pilote par TileHoverView au survol souris.
        /// </summary>
        public void SetHpVisible(bool visible)
        {
            if (_hpLabel != null) _hpLabel.gameObject.SetActive(visible);
        }
    }
}
