using System.Collections.Generic;
using Nymora.Combat.Grid;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Preview de targeting cote View (brique 2.6).
    ///
    /// A chaque update verifie :
    ///   1. Clear les highlights precedents
    ///   2. Si mode debug actif sur le CombatInputController :
    ///      - Recupere le combattant du joueur actif (caster)
    ///      - Calcule les CASES VISABLES (range Manhattan) via TargetingResolver
    ///      - Highlight bleu clair
    ///      - Si la souris survole une case visable : highlight rouge clair la ZONE D'EFFET (shape autour de cette case)
    ///
    /// La 2.6 fait juste du visuel — pas d'effet gameplay, pas de cast. Sera utilise par les
    /// vrais sorts en 2.8 (Tranche-Ame Soulrender) en lieu et place du mode debug.
    /// </summary>
    public class TargetingPreviewView : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GridSettings _gridSettings;
        [SerializeField] private GridRenderer _gridRenderer;
        [SerializeField] private CombatInputController _inputController;
        [SerializeField] private Camera _camera;

        [Header("Couleurs de preview")]
        [SerializeField] private Color _castableColor = new Color(0.55f, 0.75f, 1.00f, 1f);
        [SerializeField] private Color _effectColor = new Color(1.00f, 0.55f, 0.55f, 1f);

        private Vector3 _centerOffset;
        private bool _gridReady;
        private readonly HashSet<int> _highlighted = new HashSet<int>();

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
            if (_camera == null) _camera = Camera.main;
        }

        private void OnGameStarted(QuantumGame game)
        {
            if (_gridSettings == null || _gridRenderer == null) return;

            var frame = game.Frames.Verified;
            if (!frame.TryGetSingleton<GridSingleton>(out var grid)) return;

            _centerOffset = _gridSettings.CenterGrid
                ? IsoProjection.CenterOffset(grid.Width, grid.Height, _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight)
                : Vector3.zero;
            _gridReady = true;
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (!_gridReady) return;

            // Clear previous highlights
            ClearHighlights();

            if (_inputController == null || !_inputController.DebugShowTargeting) return;
            if (_camera == null) return;

            var frame = game.Frames.Verified;
            if (!frame.TryGetSingleton<CombatState>(out var state)) return;

            // Trouve le caster = combattant du joueur actif
            int casterX = -1, casterY = -1;
            EntityRef casterEntity = EntityRef.None;
            int casterPlayerIndex = state.ActivePlayerIndex;
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant combatant))
            {
                if (combatant.PlayerIndex == state.ActivePlayerIndex)
                {
                    casterEntity = entity;
                    casterX = combatant.GridX;
                    casterY = combatant.GridY;
                    break;
                }
            }
            if (casterEntity == EntityRef.None) return;

            // 1) Castable cells (range Manhattan) — wrapper safe int[]
            int rangeMin = _inputController.DebugRangeMin;
            int rangeMax = _inputController.DebugRangeMax;
            int[] castableBuffer = new int[GridConstants.Count];
            TargetingResolver.ResolveCastableCells(frame, casterX, casterY, rangeMin, rangeMax, castableBuffer, out int castableCount);

            // Applique le filter sur les castable cells (on n'affiche que celles qui matchent le filter du sort).
            var visibleCastable = new List<int>(castableCount);
            var filterEnum = _inputController.DebugFilter;
            for (int i = 0; i < castableCount; i++)
            {
                int idx = castableBuffer[i];
                int gx = idx % GridConstants.Width;
                int gy = idx / GridConstants.Width;
                if (TargetingResolver.MatchesFilter(frame, gx, gy, filterEnum, casterEntity, casterPlayerIndex))
                {
                    visibleCastable.Add(idx);
                }
            }

            // Highlight castable cells (bleu)
            foreach (var idx in visibleCastable)
            {
                int gx = idx % GridConstants.Width;
                int gy = idx / GridConstants.Width;
                var tile = _gridRenderer.GetTileView(gx, gy);
                if (tile != null)
                {
                    tile.ApplyHighlight(_castableColor);
                    _highlighted.Add(idx);
                }
            }

            // 2) Hover : effect cells
            Vector3 mouseWorld = _camera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            mouseWorld.z = 0f;
            var (hoverGx, hoverGy) = IsoProjection.WorldToGrid(
                mouseWorld,
                _gridSettings.TileWorldWidth,
                _gridSettings.TileWorldHeight,
                _centerOffset);

            int hoverIdx = (hoverGx >= 0 && hoverGx < GridConstants.Width && hoverGy >= 0 && hoverGy < GridConstants.Height)
                ? hoverGy * GridConstants.Width + hoverGx
                : -1;

            // Le hover doit etre sur une castable cell visible pour declencher la preview d'effet.
            if (hoverIdx >= 0 && visibleCastable.Contains(hoverIdx))
            {
                int[] effectBuffer = new int[GridConstants.Count];
                TargetingResolver.ResolveEffectCells(frame, casterX, casterY, hoverGx, hoverGy, _inputController.DebugShape, effectBuffer, out int effectCount);

                for (int i = 0; i < effectCount; i++)
                {
                    int idx = effectBuffer[i];
                    int gx = idx % GridConstants.Width;
                    int gy = idx / GridConstants.Width;
                    var tile = _gridRenderer.GetTileView(gx, gy);
                    if (tile != null)
                    {
                        tile.ApplyHighlight(_effectColor);
                        _highlighted.Add(idx);
                    }
                }
            }
        }

        private void ClearHighlights()
        {
            foreach (var idx in _highlighted)
            {
                int gx = idx % GridConstants.Width;
                int gy = idx / GridConstants.Width;
                var tile = _gridRenderer.GetTileView(gx, gy);
                if (tile != null) tile.ClearHighlight();
            }
            _highlighted.Clear();
        }

        private void OnDestroy()
        {
            ClearHighlights();
        }
    }
}
