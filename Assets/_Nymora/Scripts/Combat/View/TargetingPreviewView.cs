using System.Collections.Generic;
using Nymora.Combat.Grid;
using Nymora.Combat.View.HUD;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Preview de targeting cote View.
    ///
    /// Priorite a chaque OnUpdateView :
    ///   1. Si _hudController.ArmedSpell est set (2.13.b mode armed) -> resolve la def via
    ///      SpellRegistry.TryGet et utilise Shape/Filter/RangeMin/RangeMax.
    ///   2. Sinon si _inputController.DebugShowTargeting (2.6 mode dev) -> utilise les
    ///      valeurs DebugShape/DebugFilter/DebugRangeMin/DebugRangeMax exposees par
    ///      CombatInputController.
    ///   3. Sinon : aucune preview, clear.
    ///
    /// Toujours : caster = combattant du joueur actif. Highlight bleu pour range castable,
    /// rouge clair pour zone d'effet au hover.
    /// </summary>
    public class TargetingPreviewView : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GridSettings _gridSettings;
        [SerializeField] private GridRenderer _gridRenderer;
        [SerializeField] private CombatInputController _inputController;
        [SerializeField] private Camera _camera;

        [Tooltip("HUD controller (2.13.b). Si set, sa propriete ArmedSpell pilote la preview " +
                 "en priorite (devant le mode debug du CombatInputController).")]
        [SerializeField] private CombatHUDController _hudController;

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

            if (_camera == null) return;

            // Resolution de la source de preview : armed spell > debug mode > rien.
            // Note 2.13.b : on n'utilise plus le Filter cote View pour le highlight bleu.
            // La portee Manhattan complete est affichee ; Quantum filtre au cast.
            TargetingShape shape;
            int rangeMin, rangeMax;
            if (_hudController != null && _hudController.ArmedSpell.HasValue
                && SpellRegistry.TryGet(_hudController.ArmedSpell.Value, out SpellDef def))
            {
                shape = def.Shape;
                rangeMin = def.RangeMin;
                rangeMax = def.RangeMax;
            }
            else if (_inputController != null && _inputController.DebugShowTargeting)
            {
                shape = _inputController.DebugShape;
                rangeMin = _inputController.DebugRangeMin;
                rangeMax = _inputController.DebugRangeMax;
            }
            else
            {
                return; // rien a montrer
            }

            var frame = game.Frames.Verified;
            if (!frame.TryGetSingleton<CombatState>(out var state)) return;

            // 4.14.f hotfix — En PvP, la range castable doit etre AUTOUR DU LOCAL player
            // (pas du joueur actif). Sinon P1 pendant le tour de P0 voit la range autour de
            // P0 = confusion totale. Lorenzo : "pas la range sur P0 mais plutot la range sur P1".
            // En IA (IsBotMatch=1), garder comportement legacy (ActivePlayer pour testing P0+P1).
            int casterPlayerIndex = state.ActivePlayerIndex;
            bool isPvp = frame.RuntimeConfig != null && !frame.RuntimeConfig.IsBotMatch;
            if (isPvp)
            {
                var bootstrap = Nymora.Combat.Bootstrap.CombatBootstrapCasual.Instance;
                if (bootstrap != null && bootstrap.LocalPlayerSlot >= 0)
                {
                    casterPlayerIndex = bootstrap.LocalPlayerSlot;
                }
            }

            // Trouve le caster = combattant du joueur (actif en IA, local en PvP).
            int casterX = -1, casterY = -1;
            bool hasCaster = false;
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef _, out Combatant combatant))
            {
                if (combatant.PlayerIndex == casterPlayerIndex)
                {
                    casterX = combatant.GridX;
                    casterY = combatant.GridY;
                    hasCaster = true;
                    break;
                }
            }
            if (!hasCaster) return;

            // 1) Castable cells (range Manhattan) — wrapper safe int[].
            // 2.13.b : on affiche TOUTE la range Manhattan, peu importe le Filter du sort.
            // Le filter est evalue cote Quantum au moment du cast (case sans ennemi pour
            // un sort Filter=Enemy = cast rejete silencieusement). Lorenzo voit ainsi
            // toujours sa portee, ce qui est plus lisible que filtrer cote View.
            int[] castableBuffer = new int[GridConstants.Count];
            TargetingResolver.ResolveCastableCells(frame, casterX, casterY, rangeMin, rangeMax, castableBuffer, out int castableCount);

            var visibleCastable = new HashSet<int>();
            for (int i = 0; i < castableCount; i++)
            {
                int idx = castableBuffer[i];
                int gx = idx % GridConstants.Width;
                int gy = idx / GridConstants.Width;
                var tile = _gridRenderer.GetTileView(gx, gy);
                if (tile != null)
                {
                    tile.ApplyHighlight(_castableColor);
                    _highlighted.Add(idx);
                    visibleCastable.Add(idx);
                }
            }

            // 2) Hover : effect cells au survol de la case visee.
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

            // Effect zone affichee uniquement si on survole une case dans la range castable.
            if (hoverIdx >= 0 && visibleCastable.Contains(hoverIdx))
            {
                int[] effectBuffer = new int[GridConstants.Count];
                TargetingResolver.ResolveEffectCells(frame, casterX, casterY, hoverGx, hoverGy, shape, effectBuffer, out int effectCount);

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
