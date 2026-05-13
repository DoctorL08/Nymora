using System.Collections.Generic;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// Affiche les marques visuelles (overlay sprite anime) au-dessus de chaque combatant
    /// porteur d'un Status couvert par MarkSpriteLibrary.
    ///
    /// Architecture :
    ///   - Subscribe a CallbackUpdateView pour poll l'etat des Statuses chaque frame view.
    ///   - Maintient un cache EntityRef -> CombatantView (rebuild lazy si mismatch).
    ///   - Pour chaque combatant : ajoute/retire des overlays GameObject enfants selon les
    ///     marques actives. Empile horizontalement si plusieurs marques en meme temps.
    ///
    /// Pure View — n'affecte pas la simulation. Reagit aux changements de Statuses sans
    /// instrumentation cote sim.
    /// </summary>
    public class CombatantMarksView : MonoBehaviour
    {
        [Header("Dependances")]
        [SerializeField] private MarkSpriteLibrary _library;

        [Header("Rendu")]
        [Tooltip("FPS de lecture des marques (4 frames a 4 fps = ~1s par cycle).")]
        [SerializeField, Min(1f)] private float _framesPerSecond = 4f;

        [Tooltip("Scale du GameObject de la marque. 1.0 = 64x64 @ PPU 128 = 0.5 unit. " +
                 "Augmente pour la lisibilite (1.5 ou 2.0 pour des marques bien visibles).")]
        [SerializeField, Min(0.1f)] private float _markScale = 1.2f;

        [Tooltip("Offset local par rapport au combatant. Y positif = au-dessus de la tete. " +
                 "Le combatant a son sprite centre en Y=0, sprite 1 unit donc top = 0.5.")]
        [SerializeField] private Vector2 _baseOffset = new Vector2(0f, 0.7f);

        [Tooltip("Espacement horizontal quand plusieurs marques sont actives sur la meme cible.")]
        [SerializeField, Min(0f)] private float _markSpacingX = 0.4f;

        [Tooltip("SortingOrder des marques. 1500 = devant les combattants (~1000), derriere VFX one-shot (~2000).")]
        [SerializeField] private int _sortingOrder = 1500;

        // Etat runtime.
        private readonly Dictionary<EntityRef, CombatantView> _viewByEntity = new Dictionary<EntityRef, CombatantView>(4);
        private readonly Dictionary<EntityRef, Dictionary<StatusKind, GameObject>> _overlays =
            new Dictionary<EntityRef, Dictionary<StatusKind, GameObject>>(4);
        // Reuse buffers pour eviter les allocations dans OnUpdateView.
        private readonly List<StatusKind> _activeBuffer = new List<StatusKind>(4);
        private readonly List<StatusKind> _toRemoveBuffer = new List<StatusKind>(4);

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
        }

        private void OnGameStarted(QuantumGame game)
        {
            ClearAll();
            _viewByEntity.Clear();
            // RebuildViewCache sera fait au 1er OnUpdateView (l'ordre de spawn des CombatantView
            // n'est pas garanti par rapport a notre Awake).
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (_library == null) return;

            var frame = game.Frames.Verified;
            var filter = frame.Filter<Combatant>();
            bool needCacheRebuild = _viewByEntity.Count == 0;

            while (filter.Next(out EntityRef entity, out Combatant combatant))
            {
                if (!_viewByEntity.TryGetValue(entity, out var view) || view == null)
                {
                    needCacheRebuild = true;
                }
            }

            if (needCacheRebuild) RebuildViewCache();

            filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant combatant))
            {
                if (!_viewByEntity.TryGetValue(entity, out var view) || view == null) continue;
                UpdateMarksForCombatant(entity, combatant, view);
            }
        }

        private void RebuildViewCache()
        {
            _viewByEntity.Clear();
            var allViews = Object.FindObjectsByType<CombatantView>(FindObjectsSortMode.None);
            foreach (var v in allViews)
            {
                if (v == null) continue;
                if (v.Entity != EntityRef.None) _viewByEntity[v.Entity] = v;
            }
        }

        private void UpdateMarksForCombatant(EntityRef entity, Combatant combatant, CombatantView view)
        {
            // 1) Liste les marques actives sur ce combattant.
            _activeBuffer.Clear();
            for (int i = 0; i < 8; i++)
            {
                var s = combatant.Statuses[i];
                if (s.Kind == StatusKind.None || s.TurnsLeft <= 0) continue;
                if (_library.GetFrames(s.Kind) == null) continue;
                _activeBuffer.Add(s.Kind);
            }

            // 2) Recupere ou cree le dictionnaire d'overlays pour ce combattant.
            if (!_overlays.TryGetValue(entity, out var perEntity))
            {
                perEntity = new Dictionary<StatusKind, GameObject>(2);
                _overlays[entity] = perEntity;
            }

            // 3) Detruit les overlays pour les marques qui ne sont plus actives.
            _toRemoveBuffer.Clear();
            foreach (var kvp in perEntity)
            {
                if (!_activeBuffer.Contains(kvp.Key))
                {
                    if (kvp.Value != null) Destroy(kvp.Value);
                    _toRemoveBuffer.Add(kvp.Key);
                }
            }
            foreach (var kind in _toRemoveBuffer) perEntity.Remove(kind);

            // 4) Cree les overlays manquants.
            for (int i = 0; i < _activeBuffer.Count; i++)
            {
                var kind = _activeBuffer[i];
                if (!perEntity.ContainsKey(kind))
                {
                    var go = SpawnOverlay(view, kind);
                    if (go != null) perEntity[kind] = go;
                }
            }

            // 5) Repositionne tous les overlays actifs (centre + spacing horizontal).
            int count = _activeBuffer.Count;
            for (int i = 0; i < count; i++)
            {
                var kind = _activeBuffer[i];
                if (!perEntity.TryGetValue(kind, out var go) || go == null) continue;
                float xOffset = (i - (count - 1) * 0.5f) * _markSpacingX;
                go.transform.localPosition = new Vector3(_baseOffset.x + xOffset, _baseOffset.y, 0f);
            }
        }

        private GameObject SpawnOverlay(CombatantView view, StatusKind kind)
        {
            var frames = _library.GetFrames(kind);
            if (frames == null || frames.Length == 0) return null;

            var go = new GameObject($"Mark_{kind}");
            go.transform.SetParent(view.transform, false);
            go.transform.localPosition = new Vector3(_baseOffset.x, _baseOffset.y, 0f);
            go.transform.localScale = new Vector3(_markScale, _markScale, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Default";
            sr.sortingOrder = _sortingOrder;

            var anim = go.AddComponent<SpriteAnimator>();
            anim.SetFrames(frames, _framesPerSecond, loop: true);
            return go;
        }

        private void ClearAll()
        {
            foreach (var perEntity in _overlays.Values)
            {
                foreach (var go in perEntity.Values)
                {
                    if (go != null) Destroy(go);
                }
            }
            _overlays.Clear();
        }

        private void OnDestroy()
        {
            ClearAll();
        }
    }
}
