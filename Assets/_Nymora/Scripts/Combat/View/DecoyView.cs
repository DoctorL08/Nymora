using System.Collections.Generic;
using Nymora.Combat.Grid;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// 3.6 — Spawn et sync les sprites de leurres Ghostra cote View. Lit
    /// <c>Combatant.Decoys[i]</c> pour chaque Ghostra vivant et maintient un GameObject
    /// "FakeGhostra_P{X}_slot{Y}" avec SpriteRenderer a chaque position de leurre.
    ///
    /// Sprite visuellement IDENTIQUE a la vraie Ghostra cote adversaire (Bible V7.1) :
    /// on copie le sprite courant du SpriteRenderer de la Ghostra parente via le
    /// CombatantRenderer (lookup _views[ghostra]).
    ///
    /// Pattern : pas de pooling (max 2 Ghostra * 3 slots = 6 GameObjects en pratique).
    /// Cleanup auto a chaque frame quand un slot devient None ou la Ghostra meurt.
    /// </summary>
    public class DecoyView : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GridSettings _gridSettings;
        [Tooltip("Reference au CombatantRenderer pour recuperer le SpriteRenderer de la vraie Ghostra (visuel identique). Si null, fallback sur _placeholderSprite.")]
        [SerializeField] private CombatantRenderer _combatantRenderer;
        [Tooltip("Sprite affiche sur les leurres si CombatantRenderer null ou Ghostra introuvable. Drag-and-drop l'avatar Ghostra 128px ou le 1er frame stage0_SE.")]
        [SerializeField] private Sprite _placeholderSprite;
        [Tooltip("Scale appliquee au GameObject leurre. Default 1.16 (calibre Lorenzo, aligne avec RestructureGhostraPrefabTool).")]
        [SerializeField] private Vector3 _decoyScale = new Vector3(1.16f, 1.16f, 1f);
        [Tooltip("Y offset applique au sprite leurre (aligne avec le Visual.LocalPosition.y du prefab Ghostra : -0.22).")]
        [SerializeField] private float _decoyYOffset = -0.22f;
        [Tooltip("Sorting order applique aux leurres. Default 5 (au-dessus des tiles, sous la vraie Ghostra ~10).")]
        [SerializeField] private int _decoySortingOrder = 5;
        [Tooltip("Alpha du sprite leurre. 1 = identique a la vraie Ghostra. <1 utile pour debug visuel (transparence = c'est un leurre).")]
        [SerializeField, Range(0f, 1f)] private float _decoyAlpha = 1f;

        // Cle composite (ghostraEntity, slotIndex) -> GameObject leurre actif.
        private readonly Dictionary<(EntityRef ghostra, int slot), GameObject> _decoyVisuals
            = new Dictionary<(EntityRef, int), GameObject>(6);

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
                Debug.LogError("[Nymora.DecoyView] GridSettings manquant.", this);
                return;
            }
            var frame = game.Frames.Verified;
            var grid = frame.GetSingleton<GridSingleton>();
            _centerOffset = _gridSettings.CenterGrid
                ? IsoProjection.CenterOffset(grid.Width, grid.Height, _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight)
                : Vector3.zero;
            _gridReady = true;

            ClearAll();
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (!_gridReady) return;
            var frame = game.Frames.Predicted;
            if (frame == null) return;

            // Marquer les cles a supprimer (slots qui ne sont plus actifs cette frame).
            var seen = new HashSet<(EntityRef, int)>();

            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef ghostraEntity, out Combatant ghostra))
            {
                if (ghostra.Class != NymoraClass.Ghostra) continue;
                if (ghostra.HP <= 0)
                {
                    // Ghostra morte -> ses leurres aussi disparaissent (slot reste a None
                    // dans le DSL mais on cleanup defensive).
                    continue;
                }

                for (int slot = 0; slot < 3; slot++)
                {
                    var d = ghostra.Decoys[slot];
                    if (d.Kind == DecoyKind.None) continue;

                    var key = (ghostraEntity, slot);
                    seen.Add(key);

                    if (!_decoyVisuals.TryGetValue(key, out var go) || go == null)
                    {
                        go = CreateDecoyGameObject(ghostraEntity, slot, d.Kind);
                        _decoyVisuals[key] = go;
                    }

                    // Position iso depuis (PosX, PosY).
                    Vector3 world = IsoProjection.GridToWorld(
                        d.PosX, d.PosY,
                        _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight) + _centerOffset + transform.position;
                    world.y += _decoyYOffset;
                    go.transform.position = world;

                    // Sync sprite avec la vraie Ghostra (Bible "indiscernable cote adversaire").
                    SyncSpriteFromGhostra(go, ghostraEntity);
                }
            }

            // Cleanup : tous les decoys qui n'ont plus de slot actif cette frame.
            // Buffer pour eviter de modifier _decoyVisuals pendant l'iteration.
            using (var toRemove = new ListBuffer<(EntityRef, int)>())
            {
                foreach (var kvp in _decoyVisuals)
                {
                    if (!seen.Contains(kvp.Key)) toRemove.List.Add(kvp.Key);
                }
                foreach (var key in toRemove.List)
                {
                    if (_decoyVisuals.TryGetValue(key, out var go) && go != null)
                    {
                        Destroy(go);
                    }
                    _decoyVisuals.Remove(key);
                }
            }
        }

        private GameObject CreateDecoyGameObject(EntityRef ghostra, int slot, DecoyKind kind)
        {
            var go = new GameObject($"FakeGhostra_E#{ghostra.Index}_slot{slot}_{kind}");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localScale = _decoyScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = _decoySortingOrder;
            sr.color = new Color(1f, 1f, 1f, _decoyAlpha);
            return go;
        }

        /// <summary>
        /// Copie le sprite courant de la vraie Ghostra sur le GameObject leurre, ou
        /// fallback sur _placeholderSprite si lookup impossible.
        /// </summary>
        private void SyncSpriteFromGhostra(GameObject decoyGo, EntityRef ghostraEntity)
        {
            var sr = decoyGo.GetComponent<SpriteRenderer>();
            if (sr == null) return;

            Sprite source = _placeholderSprite;
            if (_combatantRenderer != null)
            {
                var ghostraSR = FindGhostraSpriteRenderer(ghostraEntity);
                if (ghostraSR != null && ghostraSR.sprite != null)
                {
                    source = ghostraSR.sprite;
                }
            }
            if (sr.sprite != source) sr.sprite = source;
        }

        private SpriteRenderer FindGhostraSpriteRenderer(EntityRef ghostra)
        {
            // CombatantRenderer expose _views en private — pas d'accesseur public actuellement.
            // Fallback : on cherche par nom dans les enfants de scene (les vrais GameObjects
            // Combatant_P{X}_Ghostra sont enfants de CombatantRenderer.transform).
            if (_combatantRenderer == null) return null;
            var t = _combatantRenderer.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (!child.name.Contains("Ghostra")) continue;
                var sr = child.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) return sr;
            }
            return null;
        }

        private void ClearAll()
        {
            foreach (var kvp in _decoyVisuals)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            _decoyVisuals.Clear();
        }

        // Petit helper pour eviter d'allouer une nouvelle List a chaque cleanup.
        private sealed class ListBuffer<T> : System.IDisposable
        {
            public readonly List<T> List = new List<T>(4);
            public void Dispose() { List.Clear(); }
        }
    }
}
