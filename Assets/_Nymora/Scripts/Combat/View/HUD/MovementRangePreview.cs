using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Preview des cases atteignables au mouvement (2.13.b).
    ///
    /// Pilotage :
    ///   - Si un sort est arme sur le HUD -> skip (priorite au spell preview TargetingPreviewView)
    ///   - Sinon : BFS depuis le combatant actif jusqu'a PM courant, highlight les cases
    ///     atteignables. Coherent avec MovementSystem :
    ///       * Skip non-walkable, occupied, hors bornes
    ///       * Cout 1 par case + 1 supplementaire si entree sur Vapeur Carmin (destination)
    ///
    /// L'algo est SPFA-like (BFS avec relaxation) : variable cost mais grille petite (255 cases),
    /// la complexite reste triviale. Buffers prealloues -> zero allocation par tick.
    /// </summary>
    public class MovementRangePreview : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GridRenderer _gridRenderer;
        [SerializeField] private CombatHUDController _hudController;

        [Header("Couleur de preview")]
        [SerializeField] private Color _reachableColor = new Color(0.65f, 0.92f, 0.65f, 1f);

        // Buffers prealloues — reutilises chaque tick.
        private readonly int[] _bestCost = new int[GridConstants.Count];
        private readonly int[] _queueBuf = new int[GridConstants.Count * 4];
        private readonly int[] _highlightedIdx = new int[GridConstants.Count];
        private int _highlightedCount;
        private bool _gridReady;

        // Offsets 4 cardinales (l'iso projection vit cote View ; logique grille reste rectangulaire).
        private static readonly int[] NeighborDX = { 1, -1, 0, 0 };
        private static readonly int[] NeighborDY = { 0, 0, 1, -1 };

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
        }

        private void OnDestroy()
        {
            ClearHighlights();
        }

        private void OnGameStarted(QuantumGame game)
        {
            _gridReady = _gridRenderer != null;
            if (!_gridReady)
            {
                Debug.LogWarning("[MovementRangePreview] GridRenderer manquant — drag dans l'Inspector.", this);
            }
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (!_gridReady) return;

            ClearHighlights();

            // Priorite spell preview : si un sort est arme, on cache la preview movement.
            if (_hudController != null && _hudController.ArmedSpell.HasValue) return;

            var frame = game.Frames.Verified;
            if (frame == null) return;
            if (!frame.TryGetSingleton<CombatState>(out var state)) return;
            if (state.CurrentPhase != CombatPhase.TurnActive) return;

            // Trouve le combatant actif.
            int casterX = -1, casterY = -1, casterPM = 0;
            bool hasCaster = false;
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef _, out Combatant c))
            {
                if (c.PlayerIndex == state.ActivePlayerIndex)
                {
                    casterX = c.GridX;
                    casterY = c.GridY;
                    casterPM = c.PM;
                    hasCaster = true;
                    break;
                }
            }
            if (!hasCaster || casterPM <= 0) return;

            // BFS / SPFA avec relaxation.
            for (int i = 0; i < _bestCost.Length; i++) _bestCost[i] = int.MaxValue;

            int width = GridConstants.Width;
            int startIdx = casterY * width + casterX;
            _bestCost[startIdx] = 0;

            int head = 0, tail = 0;
            _queueBuf[tail++] = startIdx;

            while (head < tail)
            {
                int idx = _queueBuf[head++];
                int x = idx % width;
                int y = idx / width;
                int curCost = _bestCost[idx];
                if (curCost >= casterPM) continue; // ne peut pas etendre plus loin

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + NeighborDX[d];
                    int ny = y + NeighborDY[d];
                    if (!GridHelpers.InBounds(nx, ny)) continue;
                    if (!GridHelpers.IsWalkable(frame, nx, ny)) continue;
                    if (GridHelpers.GetOccupant(frame, nx, ny) != EntityRef.None) continue;

                    int extra = GridHelpers.GetTerrainKind(frame, nx, ny) == TerrainKind.VapeurCarmin ? 1 : 0;
                    int newCost = curCost + 1 + extra;
                    if (newCost > casterPM) continue;

                    int nidx = ny * width + nx;
                    if (newCost < _bestCost[nidx])
                    {
                        _bestCost[nidx] = newCost;
                        // Borne defensive : si la file devient pleine (cas extreme), on coupe.
                        if (tail < _queueBuf.Length) _queueBuf[tail++] = nidx;
                    }
                }
            }

            // Highlight les cases atteignables (cost > 0 pour exclure la case du caster lui-meme).
            for (int idx = 0; idx < _bestCost.Length; idx++)
            {
                if (_bestCost[idx] == int.MaxValue || _bestCost[idx] == 0) continue;
                int gx = idx % width;
                int gy = idx / width;
                var tile = _gridRenderer.GetTileView(gx, gy);
                if (tile == null) continue;
                tile.ApplyHighlight(_reachableColor);
                _highlightedIdx[_highlightedCount++] = idx;
            }
        }

        private void ClearHighlights()
        {
            if (_gridRenderer == null) { _highlightedCount = 0; return; }
            int width = GridConstants.Width;
            for (int i = 0; i < _highlightedCount; i++)
            {
                int idx = _highlightedIdx[i];
                int gx = idx % width;
                int gy = idx / width;
                var tile = _gridRenderer.GetTileView(gx, gy);
                if (tile != null) tile.ClearHighlight();
            }
            _highlightedCount = 0;
        }
    }
}
