using System.Collections.Generic;
using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.3.b — Consomme un path A* et deplace HubAvatar tile par tile en lerp world space.
    /// Vitesse exprimee en tiles/sec : chaque step prend 1/_tilesPerSecond.
    /// </summary>
    public sealed class HubMovementController : MonoBehaviour
    {
        [SerializeField] private HubAvatar _avatar;
        [SerializeField, Range(0.5f, 16f)] private float _tilesPerSecond = 4f;

        private Queue<(int gx, int gy)> _pending = new Queue<(int gx, int gy)>();
        private bool _stepInProgress;
        private float _stepElapsed;
        private float _stepDuration;
        private Vector3 _stepFromWorld;
        private Vector3 _stepToWorld;
        private int _stepToGx, _stepToGy;

        public bool IsMoving => _stepInProgress || _pending.Count > 0;

        public void Follow(List<(int gx, int gy)> path)
        {
            _pending.Clear();
            _stepInProgress = false;
            if (path == null) return;
            foreach (var step in path) _pending.Enqueue(step);
        }

        public void StopImmediate()
        {
            _pending.Clear();
            _stepInProgress = false;
        }

        private void Update()
        {
            if (_avatar == null || _avatar.Grid == null) return;

            if (!_stepInProgress)
            {
                if (_pending.Count == 0) return;
                BeginNextStep();
            }

            _stepElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_stepElapsed / _stepDuration);
            Vector3 lerped = Vector3.Lerp(_stepFromWorld, _stepToWorld, t);
            _avatar.SetWorldPositionInterpolated(_stepToGx, _stepToGy, lerped);

            if (t >= 1f)
            {
                _avatar.SetGridPosition(_stepToGx, _stepToGy);
                _stepInProgress = false;
            }
        }

        private void BeginNextStep()
        {
            var grid = _avatar.Grid;
            var next = _pending.Dequeue();
            _stepToGx = next.gx;
            _stepToGy = next.gy;
            _stepFromWorld = _avatar.transform.position;
            _stepToWorld = IsoProjection.GridToWorld(_stepToGx, _stepToGy, grid.TileWorldWidth, grid.TileWorldHeight) + grid.CenterOffset;
            _stepDuration = 1f / Mathf.Max(0.01f, _tilesPerSecond);
            _stepElapsed = 0f;
            _stepInProgress = true;
        }
    }
}
