using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nymora.Hub
{
    /// <summary>
    /// Ombre "blob" directionnelle d'un avatar de hub. 100% View (aucune sim Quantum).
    ///
    /// Un ovale doux sous les pieds, TOUJOURS visible, qui penche a l'oppose de la point light
    /// la plus influente. Le decalage est PLAFONNE (_maxOffset) -> l'ombre ne devient jamais
    /// trop longue, tout en reagissant a la position des lumieres. Resout le compromis
    /// "pas d'ombre / ombre trop longue" des ShadowCaster2D URP.
    ///
    /// Reglages :
    /// - Position du Transform : position de base sous les pieds (reglee dans le prefab).
    /// - Scale du Transform    : TAILLE de l'ombre (garde X > Y pour l'ovale).
    /// - _maxOffset    : longueur max du decalage (borne dure).
    /// - _offsetGain   : sensibilite au deplacement des lights.
    /// - _lightRange   : portee d'influence d'une light.
    /// - _min/_maxAlpha: opacite selon l'eclairage recu.
    /// - _stretch*     : leger etirement dans la direction de l'ombre.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class HubAvatarShadow : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _maxOffset = 0.35f;
        [SerializeField, Min(0f)] private float _offsetGain = 0.5f;
        [SerializeField, Min(0.1f)] private float _lightRange = 6f;
        [SerializeField, Range(0f, 1f)] private float _minAlpha = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _maxAlpha = 0.5f;
        [SerializeField] private bool _stretchTowardShadow = true;
        [SerializeField, Min(1f)] private float _maxStretch = 1.35f;
        [SerializeField, Min(0.25f)] private float _refreshInterval = 1f;
        [Tooltip("Lissage de la reaction au flicker des lumieres : plus haut = l'ombre vacille " +
                 "beaucoup moins (suit la MOYENNE des lights, pas le scintillement des torches).")]
        [SerializeField, Min(0f)] private float _smoothTime = 0.6f;

        private SpriteRenderer _sr;
        private Vector3 _baseScale;
        private Vector3 _basePosition;
        private readonly List<Light2D> _lights = new List<Light2D>();

        // Valeurs lissees (decouplent l'ombre du flicker des torches).
        private Vector3 _curOffset;
        private Vector3 _curOffsetVel;
        private float _curStretch = 1f;
        private float _curAlpha;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            _basePosition = transform.localPosition; // base = position reglee dans le prefab
            _curAlpha = _minAlpha;
        }

        private void OnEnable() => StartCoroutine(RefreshLightsLoop());

        // FindObjectsByType est hors du chemin par-frame (coroutine throttlee), pas dans Update.
        private IEnumerator RefreshLightsLoop()
        {
            var wait = new WaitForSeconds(_refreshInterval);
            while (true)
            {
                RefreshLights();
                yield return wait;
            }
        }

        private void RefreshLights()
        {
            _lights.Clear();
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
                if (l.lightType == Light2D.LightType.Point)
                    _lights.Add(l);
        }

        private void LateUpdate()
        {
            Vector3 worldFeet = transform.parent != null
                ? transform.parent.TransformPoint(_basePosition)
                : _basePosition;

            // Direction moyenne "loin des lights", ponderee par leur influence.
            Vector2 accumDir = Vector2.zero;
            float totalInfluence = 0f;
            for (int i = 0; i < _lights.Count; i++)
            {
                var l = _lights[i];
                if (l == null || !l.isActiveAndEnabled) continue;

                Vector2 fromLight = (Vector2)(worldFeet - l.transform.position);
                float dist = fromLight.magnitude;
                if (dist < 1e-3f || dist > _lightRange) continue;

                float infl = l.intensity * (1f - dist / _lightRange);
                if (infl <= 0f) continue;

                accumDir += fromLight.normalized * infl;
                totalInfluence += infl;
            }

            Vector3 targetOffset = Vector3.zero;
            float targetStretch = 1f;
            if (totalInfluence > 1e-3f)
            {
                Vector2 dir = accumDir / totalInfluence;
                Vector2 off = Vector2.ClampMagnitude(dir * _offsetGain, _maxOffset);
                targetOffset = new Vector3(off.x, off.y, 0f);
                if (_stretchTowardShadow)
                    targetStretch = Mathf.Lerp(1f, _maxStretch, Mathf.Clamp01(off.magnitude / _maxOffset));
            }
            float targetAlpha = Mathf.Lerp(_minAlpha, _maxAlpha, Mathf.Clamp01(totalInfluence));

            // Lissage : l'ombre suit la MOYENNE des lumieres, pas leur flicker frame-a-frame
            // -> elle vacille beaucoup moins que les torches.
            _curOffset = Vector3.SmoothDamp(_curOffset, targetOffset, ref _curOffsetVel, _smoothTime);
            float k = _smoothTime > 1e-4f ? 1f - Mathf.Exp(-Time.deltaTime / _smoothTime) : 1f;
            _curStretch = Mathf.Lerp(_curStretch, targetStretch, k);
            _curAlpha = Mathf.Lerp(_curAlpha, targetAlpha, k);

            transform.localPosition = _basePosition + _curOffset;

            if (_stretchTowardShadow && _curOffset.sqrMagnitude > 1e-5f)
            {
                float ang = Mathf.Atan2(_curOffset.y, _curOffset.x) * Mathf.Rad2Deg;
                transform.localRotation = Quaternion.Euler(0f, 0f, ang);
                transform.localScale = new Vector3(_baseScale.x * _curStretch, _baseScale.y, _baseScale.z);
            }
            else
            {
                transform.localRotation = Quaternion.identity;
                transform.localScale = _baseScale;
            }

            if (_sr != null)
            {
                var c = _sr.color;
                c.a = _curAlpha;
                _sr.color = c;
            }
        }
    }
}
