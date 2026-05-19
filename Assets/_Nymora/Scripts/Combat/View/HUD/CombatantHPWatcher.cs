using System.Collections.Generic;
using Nymora.Combat.Grid;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Surveille les HP des combattants tick par tick. Quand un HP change, demande au
    /// FloatingTextManager de spawner un "-X" rouge (damage) ou "+X" vert (heal) au-dessus
    /// du sprite.
    ///
    /// Approche pollee (vs Signal Quantum) :
    ///   - Simple : aucune modif DSL Quantum, pas de regen code
    ///   - Suffisant pour 2.13.c : couvre tous les sorts (Pacte de Sang -80 HP self,
    ///     Sang Coagule -30 HP, Charge Brutale 180 dgts, Curee Heal 50%, etc.)
    ///   - Limite : ne distingue PAS shield absorb vs HP loss. Si Peau de Fer absorbe
    ///     200 dgts, le HP ne bouge pas, donc pas de texte (mais la status line montre
    ///     le shield qui decremente). Phase ulterieure : signal Quantum dedie.
    ///
    /// Position spawn : iso projection depuis GridX/GridY (+ offset Y au-dessus du sprite).
    /// Pas de couplage a CombatantRenderer.
    /// </summary>
    public class CombatantHPWatcher : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private FloatingTextManager _manager;
        [SerializeField] private GridSettings _gridSettings;

        [Tooltip("Offset Y monde au-dessus de la case (1 = environ au-dessus de la tete du sprite).")]
        [SerializeField] private float _spawnYOffsetWorld = 1.1f;

        private readonly Dictionary<EntityRef, int> _lastHP = new Dictionary<EntityRef, int>(4);
        private Vector3 _centerOffset;
        private bool _gridReady;

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
        }

        private void OnGameStarted(QuantumGame game)
        {
            _lastHP.Clear();
            if (_gridSettings == null)
            {
                Debug.LogWarning("[CombatantHPWatcher] GridSettings manquant — cable dans l'Inspector.", this);
                _gridReady = false;
                return;
            }

            var frame = game.Frames.Verified;
            if (frame.TryGetSingleton<GridSingleton>(out var grid))
            {
                _centerOffset = _gridSettings.CenterGrid
                    ? IsoProjection.CenterOffset(grid.Width, grid.Height, _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight)
                    : Vector3.zero;
            }
            _gridReady = true;
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (!_gridReady || _manager == null) return;

            var frame = game.Frames.Verified;
            if (frame == null) return;

            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant c))
            {
                int currentHP = c.HP;
                if (_lastHP.TryGetValue(entity, out int prevHP))
                {
                    int delta = currentHP - prevHP;
                    if (delta != 0)
                    {
                        Vector3 worldPos = IsoProjection.GridToWorld(
                            c.GridX, c.GridY,
                            _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight) + _centerOffset;
                        worldPos.y += _spawnYOffsetWorld;
                        // 19 mai POLISH-6h — Si un sort signature vient d'etre cast (dans la fenetre
                        // SignatureCastBridge ~1.5s), spawn le texte EPIQUE (gros or bounce). Sinon
                        // texte standard rouge/vert. Limite aux degats (delta < 0) — un signature ne
                        // soigne jamais en l'etat actuel mais defensif.
                        if (delta < 0 && SignatureCastBridge.IsSignatureRecent())
                        {
                            _manager.SpawnSignatureHit(worldPos, delta);
                        }
                        else
                        {
                            _manager.Spawn(worldPos, delta);
                        }
                    }
                }
                _lastHP[entity] = currentHP;
            }
        }
    }
}
