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
        [Tooltip("PATCH 22 mai — cases dans la portee mais derriere un obstacle (ligne de vue " +
                 "bloquee). Grisees pour signaler qu'on ne peut pas taper derriere un mur " +
                 "(sauf sorts qui ignorent la LoS : ligne custom, teleport, melee, self).")]
        [SerializeField] private Color _blockedColor = new Color(0.30f, 0.30f, 0.30f, 0.75f);

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

            // Refonte 29 mai — PRÉVISU DE SÉLECTION DE DIRECTION (2e clic d'un sort directionnel,
            //   ex Bourrasque) : on surligne les 4 axes cardinaux depuis la cible jusqu'à la distance
            //   de push, pour montrer au joueur où cliquer pour choisir le sens. Le sort armé est
            //   déjà consommé (1er clic), donc la range normale ne s'afficherait pas.
            if (_inputController != null && _inputController.AwaitingPushDir)
            {
                int tx = _inputController.PushDirTargetX;
                int ty = _inputController.PushDirTargetY;
                int[] ddx = { 1, -1, 0, 0 };
                int[] ddy = { 0, 0, 1, -1 };
                for (int d = 0; d < 4; d++)
                {
                    // Une seule case adjacente par direction (indicateur de sens, pas la portée du push).
                    int gx = tx + ddx[d];
                    int gy = ty + ddy[d];
                    var dirTile = _gridRenderer.GetTileView(gx, gy);
                    if (dirTile == null) continue;
                    dirTile.ApplyHighlight(_castableColor);
                    _highlighted.Add(gy * GridConstants.Width + gx);
                }
                return;
            }

            // Resolution de la source de preview : armed spell > debug mode > rien.
            // Note 2.13.b : on n'utilise plus le Filter cote View pour le highlight bleu.
            // La portee Manhattan complete est affichee ; Quantum filtre au cast.
            TargetingShape shape;
            int rangeMin, rangeMax;
            // PATCH 22 mai (test designer) — sorts en LIGNE DROITE cardinale : la portee castable
            // est restreinte aux 4 rayons (pas tout le diamant Manhattan) et le survol montre la
            // ligne complete. Pour l'instant : Choc Sismique (Bible "portee 4 ligne").
            bool isStraightLineSpell = false;
            // PATCH 22 mai — grisage des cases hors ligne de vue (derriere un obstacle). Activé
            // uniquement pour les sorts qui requierent une LoS claire cote sim (meme liste).
            bool needsLineOfSight = false;
            if (_hudController != null && _hudController.ArmedSpell.HasValue
                && SpellRegistry.TryGet(_hudController.ArmedSpell.Value, out SpellDef def))
            {
                shape = def.Shape;
                rangeMin = def.RangeMin;
                rangeMax = def.RangeMax;
                // Sorts en LIGNE DROITE cardinale : Choc Sismique (Colossar) + Charge Brutale
                //   (Soulrender, refonte 29 mai : "ligne de 4" — la portee castable se limite aux
                //   cases cardinalement alignees, coherent avec le garde sim).
                isStraightLineSpell = _hudController.ArmedSpell.Value == SpellId.ColossarChocSismique
                                   || _hudController.ArmedSpell.Value == SpellId.SoulrenderChargeBrutale;
                needsLineOfSight = SpellSystem.SpellNeedsLineOfSight(_hudController.ArmedSpell.Value);
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

            // Perspective JOUEUR (PvP comme IA) : la range castable est autour du combattant du
            // joueur LOCAL (slot reseau en PvP, slot 0 en IA). Le sort n'est de toute facon armable
            // que pendant le tour du joueur local (gate CombatHUDController), donc jamais de preview
            // de ciblage cote bot.
            int casterPlayerIndex = LocalPlayerResolver.Resolve();

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
                // Sorts en ligne droite : ne garder que les cases cardinalement alignees avec le
                // caster (meme ligne OU meme colonne). Coherent avec la validation sim.
                if (isStraightLineSpell && gx != casterX && gy != casterY) continue;
                var tile = _gridRenderer.GetTileView(gx, gy);
                if (tile == null) continue;

                // PATCH 22 mai — case derriere un obstacle (LoS bloquee) : grisee + NON cliquable.
                // On reflete exactement le rejet sim (obstacle OWN ne bloque pas, adverse bloque).
                if (needsLineOfSight
                    && !ObstacleHelpers.HasLineOfSight(frame, casterX, casterY, gx, gy, casterPlayerIndex))
                {
                    tile.ApplyHighlight(_blockedColor);
                    _highlighted.Add(idx);
                    // pas ajoute a visibleCastable -> pas de preview d'effet + clic sans effet.
                    continue;
                }

                tile.ApplyHighlight(_castableColor);
                _highlighted.Add(idx);
                visibleCastable.Add(idx);
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
                if (isStraightLineSpell)
                {
                    // Survol d'un sort en ligne : highlight la LIGNE COMPLETE (rangeMax cases) dans
                    // la direction cardinale survolee — matche le handler sim qui tire 4 cases.
                    int ddx = hoverGx - casterX;
                    int ddy = hoverGy - casterY;
                    int stepX = ddx == 0 ? 0 : (ddx > 0 ? 1 : -1);
                    int stepY = ddy == 0 ? 0 : (ddy > 0 ? 1 : -1);
                    int rx = casterX, ry = casterY;
                    for (int s = 0; s < rangeMax; s++)
                    {
                        rx += stepX;
                        ry += stepY;
                        if (rx < 0 || rx >= GridConstants.Width || ry < 0 || ry >= GridConstants.Height) break;
                        var tile = _gridRenderer.GetTileView(rx, ry);
                        if (tile != null)
                        {
                            tile.ApplyHighlight(_effectColor);
                            _highlighted.Add(ry * GridConstants.Width + rx);
                        }
                    }
                }
                else
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
