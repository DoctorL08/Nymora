using System.Collections.Generic;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// Spawn des VFX one-shot quand un combatant cast un sort visible (mapping
    /// VFXSpriteLibrary). Detection via polling de Combatant.LastCastOnTurn / LastCastSpellId
    /// — meme mecanisme que CombatantRenderer.DispatchAnimTriggers.
    ///
    /// Position : case du caster (resolue via GridRenderer.GetTileView). Pour Bible V7.1
    /// Ame Laceree (frappe melee), le visuel montre le caster + impact a ses pieds, donc
    /// case du caster = OK. Pour les sorts a distance (futurs sheets), pourra etre raffine
    /// en ajoutant LastCastTargetX/Y au DSL Quantum.
    ///
    /// Cycle de vie : le GameObject VFX se detruit automatiquement quand SpriteAnimator
    /// (loop=false) declenche son evenement Completed.
    ///
    /// Pure View — n'affecte pas la simulation.
    /// </summary>
    public class CombatVFXView : MonoBehaviour
    {
        [Header("Dependances")]
        [SerializeField] private GridRenderer _gridRenderer;
        [SerializeField] private VFXSpriteLibrary _library;

        [Header("Rendu")]
        [Tooltip("FPS de lecture des VFX (10 frames a 12 fps = ~0.83s pour Ame Laceree).")]
        [SerializeField, Min(1f)] private float _framesPerSecond = 12f;

        [Tooltip("Scale du GameObject VFX. 1.0 = 1 tile de large (VFX 128x128 @ PPU 128). " +
                 "Augmente si le sprite parait trop petit, baisse si trop encombrant.")]
        [SerializeField, Min(0.1f)] private float _vfxScale = 1.5f;

        [Tooltip("SortingOrder du VFX. 2000 = au-dessus des combattants (qui tournent " +
                 "autour de 1000) et au-dessus de tout overlay terrain.")]
        [SerializeField] private int _sortingOrder = 2000;

        [Tooltip("Offset Y monde applique sur le VFX (le sprite affiche un perso + impact " +
                 "au sol, donc on remonte legerement pour aligner le perso au-dessus de la case).")]
        [SerializeField] private float _yOffset = 0.25f;

        // 2.13.e : tracking via LastCastSequence (compteur monotone) au lieu de LastCastOnTurn
        // pour detecter plusieurs casts dans le meme tour.
        private readonly Dictionary<EntityRef, int> _lastCastSequenceByEntity = new Dictionary<EntityRef, int>(4);
        private bool _ready;

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
        }

        private void OnGameStarted(QuantumGame game)
        {
            if (_gridRenderer == null)
            {
                Debug.LogError("[Nymora.CombatVFX] GridRenderer manquant — drag le composant.", this);
                return;
            }
            if (_library == null)
            {
                Debug.LogError("[Nymora.CombatVFX] VFXSpriteLibrary manquant — drag l'asset.", this);
                return;
            }
            _lastCastSequenceByEntity.Clear();
            // Init : capte la sequence initiale (0 au spawn) pour ne pas declencher de VFX
            // au demarrage du combat.
            var frame = game.Frames.Verified;
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant combatant))
            {
                _lastCastSequenceByEntity[entity] = combatant.LastCastSequence;
            }
            _ready = true;
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (!_ready) return;
            var frame = game.Frames.Verified;
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant combatant))
            {
                int prevSeq = _lastCastSequenceByEntity.TryGetValue(entity, out var s) ? s : 0;
                int currSeq = combatant.LastCastSequence;
                if (currSeq != prevSeq)
                {
                    Debug.Log($"[Nymora.CombatVFX] DETECT cast P{combatant.PlayerIndex} spell={combatant.LastCastSpellId} seq {prevSeq}->{currSeq} target=({combatant.LastCastTargetX},{combatant.LastCastTargetY})");
                    TrySpawnVFX(combatant);
                }
                _lastCastSequenceByEntity[entity] = currSeq;
            }
        }

        private void TrySpawnVFX(in Combatant caster)
        {
            Sprite[] frames = _library.GetFrames(caster.LastCastSpellId);
            if (frames == null || frames.Length == 0) return; // pas de VFX defini pour ce sort

            // 2.13.e : position du VFX = case ciblee par le cast (pas la case du caster).
            // Pour les sorts self-target, TargetX/Y = caster pos (CombatInputController redirige).
            int targetX = caster.LastCastTargetX;
            int targetY = caster.LastCastTargetY;
            var tile = _gridRenderer.GetTileView(targetX, targetY);
            if (tile == null)
            {
                Debug.LogWarning($"[Nymora.CombatVFX] Tile null pour target=({targetX},{targetY}) caster=({caster.GridX},{caster.GridY}) spell={caster.LastCastSpellId}");
                return;
            }

            Vector3 worldPos = tile.transform.position + new Vector3(0f, _yOffset, 0f);
            Debug.Log($"[Nymora.CombatVFX] Spawn {caster.LastCastSpellId} : target grid=({targetX},{targetY}) world={worldPos} caster=({caster.GridX},{caster.GridY})");

            var go = new GameObject($"VFX_{caster.LastCastSpellId}");
            go.transform.SetParent(transform, false);
            go.transform.position = worldPos;
            go.transform.localScale = new Vector3(_vfxScale, _vfxScale, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = tile.SortingLayerName;
            sr.sortingOrder = _sortingOrder;

            var anim = go.AddComponent<SpriteAnimator>();
            anim.Completed += () => Destroy(go);
            anim.SetFrames(frames, _framesPerSecond, loop: false);
        }
    }
}
