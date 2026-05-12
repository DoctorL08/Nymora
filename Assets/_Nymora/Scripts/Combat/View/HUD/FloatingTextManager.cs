using TMPro;
using UnityEngine;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Spawne des FloatingText au-dessus des combatants quand HP change.
    /// Le canvas dedie (ScreenSpaceOverlay, sortingOrder &lt; HUD) parent tous les textes.
    ///
    /// Pour le moment couleurs hardcodees :
    ///   - Damage : rouge vif
    ///   - Heal   : vert pale
    /// (Bleu shield absorb : Phase 2.13.d ou plus tard, necessite un signal Quantum.)
    /// </summary>
    public class FloatingTextManager : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Camera _worldCamera;

        [Header("Style")]
        [SerializeField] private Color _damageColor = new Color(0.91f, 0.29f, 0.29f, 1f);
        [SerializeField] private Color _healColor = new Color(0.48f, 0.82f, 0.42f, 1f);
        [SerializeField] private float _fontSize = 36f;
        [SerializeField] private float _durationSeconds = 1.0f;
        [SerializeField] private float _riseDistancePx = 60f;
        [SerializeField] private float _fadeStartSeconds = 0.5f;

        private void Awake()
        {
            if (_worldCamera == null) _worldCamera = Camera.main;
        }

        /// <summary>
        /// Spawn un texte flottant a `worldPos`. Sign de la valeur :
        ///   - amount &lt; 0 -> damage rouge "-N"
        ///   - amount &gt; 0 -> heal vert "+N"
        ///   - amount == 0 : ignore
        /// </summary>
        public void Spawn(Vector3 worldPos, int amount)
        {
            if (amount == 0 || _canvas == null || _worldCamera == null) return;

            string text = amount < 0 ? amount.ToString() : "+" + amount;
            Color color = amount < 0 ? _damageColor : _healColor;

            var screenPos = _worldCamera.WorldToScreenPoint(worldPos);
            // Pour ScreenSpaceOverlay, position pixel = position de la RectTransform.
            var go = new GameObject("FloatingText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(FloatingText));
            go.transform.SetParent(_canvas.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(160f, 50f);
            rt.position = screenPos;

            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.color = color;
            label.fontSize = _fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableWordWrapping = false;

            var floater = go.GetComponent<FloatingText>();
            // FloatingText fait GetComponent<TMP_Text>() dans Configure, pas besoin de cabler manuellement.
            floater.Configure(text, color, _durationSeconds, _riseDistancePx, _fadeStartSeconds);
        }
    }
}
