using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// POLISH-5e (17 mai) — Outil de calibration de la map background sous la grille de
    /// combat. Pose ce component sur le GameObject "BattleMapBackground" et ajuste les 2
    /// champs `Y Offset` + `Scale` dans l'Inspector pour caler le losange iso de la map
    /// sur les Gizmos de la grille (visibles dans Scene View grace au GridRenderer).
    ///
    /// [ExecuteAlways] : les modifs s'appliquent EN TEMPS REEL en Edit Mode (Inspector
    /// drag) ET en Play Mode (sliders runtime). Les Gizmos du GridRenderer restent visibles
    /// dans les 2 modes, donc le calage se fait sans avoir a lancer le combat.
    ///
    /// Pour persister un calage fait pendant Play vers Edit Mode :
    ///   1. Pendant Play, ajuste Y/Scale via l'Inspector du component (slider).
    ///   2. Click-droit sur le header du component "Battle Map Calibrator" -> "Copy Component".
    ///   3. Stop le Play Mode.
    ///   4. Click-droit -> "Paste Component Values" -> valeurs persistees, sauvegarde scene.
    ///
    /// Le bouton "Apply Now" du Custom Editor force un refresh manuel si jamais l'auto-apply
    /// ne se declenche pas (ex: scene rechargee, prefab modifie).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class BattleMapCalibrator : MonoBehaviour
    {
        [Tooltip("Position Y du GameObject (X et Z restent inchanges). Negatif pour descendre, " +
                 "positif pour monter. Typiquement entre -3 et +3 pour caler l'arene losange.")]
        [SerializeField, Range(-15f, 15f)] private float _yOffset = 0f;

        [Tooltip("Offset X (laisse 0 si la grille est centree). Pour ajustements lateraux fins.")]
        [SerializeField, Range(-15f, 15f)] private float _xOffset = 0f;

        [Tooltip("Scale uniforme applique au GameObject (X et Y du localScale). 1.0 = taille " +
                 "native du sprite a son PPU. Augmente si la map est plus petite que la grille " +
                 "Gizmos, diminue si elle deborde.")]
        [SerializeField, Range(0.1f, 5f)] private float _scale = 1f;

        [Tooltip("Si true, applique les valeurs a chaque frame (recommande). Decoche pour " +
                 "manipuler le Transform a la main sans que ce component l'ecrase.")]
        [SerializeField] private bool _autoApply = true;

        public float YOffset => _yOffset;
        public float XOffset => _xOffset;
        public float Scale => _scale;

        private void OnEnable()
        {
            ApplyToTransform();
        }

        private void OnValidate()
        {
            // Inspector change -> apply direct (Edit Mode + Play Mode).
            if (_autoApply) ApplyToTransform();
        }

        private void LateUpdate()
        {
            if (_autoApply) ApplyToTransform();
        }

        /// <summary>
        /// Applique les valeurs (Y, X, Scale) au transform. Public pour pouvoir etre force
        /// depuis le Custom Editor (bouton "Apply Now").
        /// </summary>
        public void ApplyToTransform()
        {
            var pos = transform.position;
            transform.position = new Vector3(_xOffset, _yOffset, pos.z);
            transform.localScale = new Vector3(_scale, _scale, 1f);
        }

        /// <summary>
        /// Recopie les valeurs courantes du Transform dans les champs serialized.
        /// Utile si Lorenzo a bouge le GameObject via les handles Scene View et veut
        /// recharger les sliders.
        /// </summary>
        public void CopyFromTransform()
        {
            _yOffset = transform.position.y;
            _xOffset = transform.position.x;
            _scale = transform.localScale.x;
        }
    }
}
