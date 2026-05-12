using TMPro;
using UnityEngine;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Animation d'un texte flottant (dgts/heals). Translate Y up + fade alpha,
    /// puis se detruit. Cycle de vie : ~1 seconde par defaut.
    ///
    /// Spawne par FloatingTextManager. Tu n'as pas a instancier ce composant manuellement.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        private TMP_Text _label;

        private float _durationSeconds = 1f;
        private float _riseDistancePx = 60f;
        private float _fadeStartSeconds = 0.5f;

        private float _elapsed;
        private Vector2 _startAnchoredPos;
        private RectTransform _rt;

        public void Configure(string text, Color color, float duration, float riseDistance, float fadeStart)
        {
            if (_label == null) _label = GetComponent<TMP_Text>();
            if (_label != null)
            {
                _label.text = text;
                _label.color = color;
            }
            _durationSeconds = duration;
            _riseDistancePx = riseDistance;
            _fadeStartSeconds = fadeStart;

            _rt = transform as RectTransform;
            if (_rt != null) _startAnchoredPos = _rt.anchoredPosition;
            _elapsed = 0f;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = _durationSeconds > 0f ? Mathf.Clamp01(_elapsed / _durationSeconds) : 1f;

            if (_rt != null)
            {
                _rt.anchoredPosition = _startAnchoredPos + new Vector2(0f, _riseDistancePx * t);
            }
            if (_label != null && _elapsed > _fadeStartSeconds && _durationSeconds > _fadeStartSeconds)
            {
                float fadeT = (_elapsed - _fadeStartSeconds) / (_durationSeconds - _fadeStartSeconds);
                var c = _label.color;
                c.a = Mathf.Clamp01(1f - fadeT);
                _label.color = c;
            }
            if (_elapsed >= _durationSeconds)
            {
                Destroy(gameObject);
            }
        }
    }
}
