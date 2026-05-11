using System.Collections.Generic;
using Nymora.Combat.Grid;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Spawn un GameObject par entity Combatant cote View au demarrage du combat,
    /// puis sync leurs positions a chaque CallbackUpdateView (placement iso depuis
    /// GridX/GridY).
    ///
    /// Pas de pooling en 2.2 (max 2 combattants en 1v1, futile). Pooling viendra
    /// en Phase 6 quand on aura les modes 2v2/3v3 (jusqu'a 6 combattants).
    /// </summary>
    public class CombatantRenderer : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GridSettings _gridSettings;

        [Header("Prefabs par classe (ordre Bible V7.1)")]
        [SerializeField] private GameObject _soulrenderPrefab;
        [SerializeField] private GameObject _nightseerPrefab;
        [SerializeField] private GameObject _colossarPrefab;
        [SerializeField] private GameObject _necramPrefab;
        [SerializeField] private GameObject _ghostraPrefab;

        private readonly Dictionary<EntityRef, CombatantView> _views = new Dictionary<EntityRef, CombatantView>();
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
                Debug.LogError("[Nymora.CombatantRenderer] GridSettings manquant.", this);
                return;
            }

            ClearAll();

            var frame = game.Frames.Verified;

            // Recupere les dimensions de la grille pour calculer le centerOffset (meme que GridRenderer).
            var grid = frame.GetSingleton<GridSingleton>();
            _centerOffset = _gridSettings.CenterGrid
                ? IsoProjection.CenterOffset(grid.Width, grid.Height, _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight)
                : Vector3.zero;
            _gridReady = true;

            // Spawn 1 GameObject par entity Combatant existante (safe API : copie par valeur).
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant combatant))
            {
                SpawnView(entity, combatant);
            }
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (!_gridReady) return;

            var frame = game.Frames.Verified;
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant combatant))
            {
                if (!_views.TryGetValue(entity, out var view) || view == null)
                {
                    // Entity apparue apres GameStarted (ex : invocations futures, leurres Ghostra) — spawn a la volee.
                    SpawnView(entity, combatant);
                    continue;
                }

                Vector3 world = IsoProjection.GridToWorld(
                    combatant.GridX, combatant.GridY,
                    _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight) + _centerOffset + transform.position;

                view.UpdateGridPosition(combatant.GridX, combatant.GridY, world);
            }
        }

        private void SpawnView(EntityRef entity, Combatant combatant)
        {
            GameObject prefab = GetPrefabForClass(combatant.Class);
            if (prefab == null)
            {
                Debug.LogError($"[Nymora.CombatantRenderer] Prefab manquant pour classe {combatant.Class}.", this);
                return;
            }

            Vector3 world = IsoProjection.GridToWorld(
                combatant.GridX, combatant.GridY,
                _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight) + _centerOffset + transform.position;

            var go = Instantiate(prefab, world, Quaternion.identity, transform);
            go.name = $"Combatant_P{combatant.PlayerIndex}_{combatant.Class}";

            var view = go.GetComponent<CombatantView>();
            if (view == null)
            {
                Debug.LogError($"[Nymora.CombatantRenderer] CombatantView manquant sur prefab {prefab.name}.", this);
                Destroy(go);
                return;
            }

            view.Bind(entity, combatant.Class);
            view.UpdateGridPosition(combatant.GridX, combatant.GridY, world);
            _views[entity] = view;

            Debug.Log($"[Nymora.CombatantRenderer] Spawn P{combatant.PlayerIndex} {combatant.Class} en ({combatant.GridX},{combatant.GridY}) HP={combatant.HP}/{combatant.MaxHP} PA={combatant.PA} PM={combatant.PM}");
        }

        private GameObject GetPrefabForClass(NymoraClass nymoraClass)
        {
            switch (nymoraClass)
            {
                case NymoraClass.Soulrender: return _soulrenderPrefab;
                case NymoraClass.Nightseer: return _nightseerPrefab;
                case NymoraClass.Colossar: return _colossarPrefab;
                case NymoraClass.Necram: return _necramPrefab;
                case NymoraClass.Ghostra: return _ghostraPrefab;
                default: return null;
            }
        }

        private void ClearAll()
        {
            foreach (var pair in _views)
            {
                if (pair.Value != null) Destroy(pair.Value.gameObject);
            }
            _views.Clear();
            _gridReady = false;
        }
    }
}
