using System.Collections.Generic;
using Nymora.Core.Audio;
using Quantum;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// Brique A2 — SFX combat pilotés par la simulation Quantum. Centralise en UN seul endroit
    /// les sons déclenchés par la sim (cast de sort, dégâts, mort, soin, début de match), sur
    /// le même mécanisme de polling que CombatVFXView / CombatantHPWatcher (LastCastSequence
    /// et HP par entité). 100% View — n'affecte jamais la simulation.
    ///
    /// Auto-instancié au chargement de toute scène de combat (nom contient "Combat") : aucune
    /// manip Unity, aucune référence de scène (les SFX sont 2D/non-spatialisés). Les sons de
    /// tour / pile-ou-face / fin de match / timer vivent côté HUD (déclencheurs UI dédiés).
    /// </summary>
    public sealed class CombatAudioView : MonoBehaviour
    {
        private static CombatAudioView _current;

        private readonly Dictionary<EntityRef, int> _lastCastSequence = new Dictionary<EntityRef, int>(4);
        private readonly Dictionary<EntityRef, int> _lastHP = new Dictionary<EntityRef, int>(4);
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
            var go = new GameObject("CombatAudioView");
            _current = go.AddComponent<CombatAudioView>();
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
            var frame = game.Frames.Verified;
            if (frame == null) return;

            // Baseline : on capte l'état initial pour ne PAS jouer de SFX au démarrage.
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant c))
            {
                _lastCastSequence[entity] = c.LastCastSequence;
                _lastHP[entity] = c.HP;
            }
            _ready = true;

            NymoraAudioManager.Instance?.PlaySfx(SoundId.CombatStart);
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (!_ready) return;
            var audio = NymoraAudioManager.Instance;
            if (audio == null) return;
            var frame = game.Frames.Verified;
            if (frame == null) return;

            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant c))
            {
                // --- Cast de sort ---
                int prevSeq = _lastCastSequence.TryGetValue(entity, out var s) ? s : c.LastCastSequence;
                if (c.LastCastSequence != prevSeq)
                {
                    audio.PlaySfx(SoundId.SpellCastGeneric);
                }
                _lastCastSequence[entity] = c.LastCastSequence;

                // --- HP : dégâts / mort / soin ---
                if (_lastHP.TryGetValue(entity, out int prevHP))
                {
                    int delta = c.HP - prevHP;
                    if (delta < 0)
                    {
                        // Mort = on franchit 0 ce tick (prevHP > 0). Sinon dégâts normaux.
                        if (c.HP <= 0 && prevHP > 0) audio.PlaySfx(SoundId.Death);
                        else audio.PlaySfx(SoundId.Damage);
                    }
                    else if (delta > 0)
                    {
                        audio.PlaySfx(SoundId.Heal);
                    }
                }
                _lastHP[entity] = c.HP;
            }
        }
    }
}
