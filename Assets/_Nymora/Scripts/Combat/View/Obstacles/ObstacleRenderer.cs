using System.Collections.Generic;
using Nymora.Combat.Grid;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.Obstacles
{
    /// <summary>
    /// 3.1 — Spawn / despawn un GameObject par entity Obstacle cote View, sync chaque
    /// CallbackUpdateView en lisant Filter&lt;Obstacle&gt;.
    ///
    /// Pas de pooling : max ~5 obstacles concurrents typiques en 1v1 (Bible V7.1 cap
    /// FD = 3 piliers, +1 mur ponctuel). Pooling viendra en Phase 6 si necessaire.
    ///
    /// Pattern miroir de CombatantRenderer.OnUpdateView mais simplifie (pas de
    /// tracking facing / HP / cast — un Obstacle est statique).
    /// </summary>
    public class ObstacleRenderer : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GridSettings _gridSettings;

        [Tooltip("Prefab placeholder Pilier (genere par Nymora > Setup > Create Obstacle Prefabs).")]
        [SerializeField] private GameObject _pillarPrefab;
        [Tooltip("Prefab placeholder Mur. Optionnel pour 3.1 (pas encore utilise).")]
        [SerializeField] private GameObject _wallPrefab;

        private readonly Dictionary<EntityRef, ObstacleView> _views = new Dictionary<EntityRef, ObstacleView>();
        // Buffer reutilise pour eviter les allocations dans OnUpdateView.
        private readonly List<EntityRef> _seenThisFrame = new List<EntityRef>(8);
        private readonly List<EntityRef> _toDestroy = new List<EntityRef>(4);

        private Vector3 _centerOffset;
        private bool _gridReady;

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
        }

        private void OnDestroy()
        {
            ClearAll();
        }

        private void OnGameStarted(QuantumGame game)
        {
            if (_gridSettings == null)
            {
                Debug.LogError("[Nymora.ObstacleRenderer] GridSettings manquant.", this);
                return;
            }
            ClearAll();
            var frame = game.Frames.Verified;
            var grid = frame.GetSingleton<GridSingleton>();
            _centerOffset = _gridSettings.CenterGrid
                ? IsoProjection.CenterOffset(grid.Width, grid.Height, _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight)
                : Vector3.zero;
            _gridReady = true;
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (!_gridReady) return;
            var frame = game.Frames.Verified;

            _seenThisFrame.Clear();

            // 1. Iterate tous les Obstacle de la frame, spawn / update les vues.
            var filter = frame.Filter<Obstacle>();
            while (filter.Next(out EntityRef entity, out Obstacle data))
            {
                _seenThisFrame.Add(entity);

                if (!_views.TryGetValue(entity, out var view) || view == null)
                {
                    view = SpawnView(entity, data.Kind);
                    if (view == null) continue; // prefab manquant, log deja sorti
                    _views[entity] = view;
                }

                Vector3 worldPos = IsoProjection.GridToWorld(
                    data.GridX, data.GridY,
                    _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight)
                    + _centerOffset;

                view.UpdateData(data, worldPos);
            }

            // 2. Despawn les vues dont l'entity n'existe plus dans la frame (destroyed).
            _toDestroy.Clear();
            foreach (var kv in _views)
            {
                if (!_seenThisFrame.Contains(kv.Key))
                {
                    _toDestroy.Add(kv.Key);
                }
            }
            for (int i = 0; i < _toDestroy.Count; i++)
            {
                EntityRef key = _toDestroy[i];
                if (_views.TryGetValue(key, out var view) && view != null)
                {
                    Destroy(view.gameObject);
                }
                _views.Remove(key);
            }
        }

        /// <summary>
        /// Polish 3.3.d : retourne l'ObstacleView a la case (gx,gy), ou null si aucun.
        /// Utilise par TileHoverView pour afficher/cacher le HP au survol souris.
        /// O(N) sur le nb d'obstacles concurrents (max ~10), negligeable.
        /// </summary>
        public ObstacleView GetObstacleViewAt(int gx, int gy)
        {
            foreach (var kv in _views)
            {
                if (kv.Value == null) continue;
                if (kv.Value.GridX == gx && kv.Value.GridY == gy) return kv.Value;
            }
            return null;
        }

        private ObstacleView SpawnView(EntityRef entity, ObstacleKind kind)
        {
            GameObject prefab = kind switch
            {
                ObstacleKind.Pillar => _pillarPrefab,
                ObstacleKind.Wall   => _wallPrefab != null ? _wallPrefab : _pillarPrefab,
                _ => _pillarPrefab,
            };
            if (prefab == null)
            {
                Debug.LogWarning($"[Nymora.ObstacleRenderer] Prefab manquant pour kind {kind} — Spawn ignore.", this);
                return null;
            }
            var go = Instantiate(prefab, transform);
            go.name = $"Obstacle_{kind}_{entity}";
            var view = go.GetComponent<ObstacleView>();
            if (view == null) view = go.AddComponent<ObstacleView>();
            view.Bind(entity);
            return view;
        }

        private void ClearAll()
        {
            foreach (var kv in _views)
            {
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            }
            _views.Clear();
            _seenThisFrame.Clear();
            _toDestroy.Clear();
        }
    }
}
