using System.Collections.Generic;
using Nymora.Core.Data;
using Quantum;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Brique S-STATS.b — Collecteur de stats de combat du joueur local, 100% VIEW
    /// (n'affecte JAMAIS la simulation Quantum -> pas de bump CombatRulesVersion).
    ///
    /// Observe la Frame verified par polling (meme mecanisme que CombatAudioView) et
    /// accumule, pour le joueur local :
    ///   - degats infliges  = pertes de HP des combattants NON-locaux
    ///   - degats subis     = pertes de HP du combattant local
    ///   - sorts lances     = increments de LastCastSequence du combattant local
    ///   - tours            = CombatState.TurnNumber courant (= round, cf turn-semantics)
    ///
    /// Pousse en continu dans MatchBridge (Core) ; HubMatchResultDisplay lit ces stats au
    /// moment du report ranked (POST /ranked/report-result) puis les reset. NON-autoritatif
    /// (auto-declare, double-accord alpha) — coherent avec le backend S-STATS.a.
    ///
    /// Auto-instancie sur toute scene "Combat" (aucune manip Unity, pas de ref de scene).
    /// </summary>
    public sealed class CombatStatsCollector : MonoBehaviour
    {
        private static CombatStatsCollector _current;

        private readonly Dictionary<EntityRef, int> _lastCastSequence = new Dictionary<EntityRef, int>(4);
        private readonly Dictionary<EntityRef, int> _lastHP = new Dictionary<EntityRef, int>(4);

        private int _damageDealt;
        private int _damageTaken;
        private int _spellsCast;
        private int _turns;
        private bool _ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreateForActiveScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreateForActiveScene();

        private static void TryCreateForActiveScene()
        {
            if (_current != null) return;
            if (!SceneManager.GetActiveScene().name.Contains("Combat")) return;
            var go = new GameObject("CombatStatsCollector");
            _current = go.AddComponent<CombatStatsCollector>();
        }

        private void Awake()
        {
            _current = this;
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
        }

        private void OnDestroy()
        {
            if (_current == this) _current = null;
        }

        private void OnGameStarted(QuantumGame game)
        {
            _lastCastSequence.Clear();
            _lastHP.Clear();
            _damageDealt = 0;
            _damageTaken = 0;
            _spellsCast = 0;
            _turns = 0;
            MatchBridge.ResetCombatStats();

            var frame = game.Frames.Verified;
            if (frame == null) return;

            // Baseline : etat initial pour ne pas compter le spawn comme degat/cast.
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant c))
            {
                _lastCastSequence[entity] = c.LastCastSequence;
                _lastHP[entity] = c.HP;
            }
            _ready = true;
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (!_ready) return;
            var frame = game.Frames.Verified;
            if (frame == null) return;

            // Le slot local (PlayerRef global Quantum) est resolu par le bootstrap apres
            // AddPlayer. Tant qu'il vaut -1, on tient les baselines a jour mais on n'attribue
            // pas (les premiers ticks de spawn n'ont de toute facon pas de degats).
            int localSlot = Nymora.Combat.Bootstrap.CombatBootstrapCasual.Instance != null
                ? Nymora.Combat.Bootstrap.CombatBootstrapCasual.Instance.LocalPlayerSlot
                : -1;

            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant c))
            {
                bool isLocal = localSlot >= 0 && c.PlayerIndex == localSlot;

                // --- Sorts lances (local uniquement) ---
                if (_lastCastSequence.TryGetValue(entity, out int prevSeq))
                {
                    int castDelta = c.LastCastSequence - prevSeq;
                    if (castDelta > 0 && isLocal && localSlot >= 0) _spellsCast += castDelta;
                }
                _lastCastSequence[entity] = c.LastCastSequence;

                // --- Degats (perte de HP) ---
                if (_lastHP.TryGetValue(entity, out int prevHP))
                {
                    int delta = c.HP - prevHP;
                    if (delta < 0 && localSlot >= 0)
                    {
                        if (isLocal) _damageTaken += -delta;
                        else _damageDealt += -delta; // perte de HP d'un combattant adverse
                    }
                }
                _lastHP[entity] = c.HP;
            }

            if (frame.TryGetSingleton<CombatState>(out var state))
            {
                _turns = state.TurnNumber;
            }

            // Push continu : a la fin du match, MatchBridge tient les valeurs finales.
            if (localSlot >= 0)
            {
                MatchBridge.SetCombatStats(_damageDealt, _damageTaken, _spellsCast, _turns);
            }
        }
    }
}
