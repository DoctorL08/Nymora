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
        [Tooltip("Sprite fallback si _decoyIdleController null. Sert de 1er sprite statique avant que l'animator demarre. Drag-and-drop le 1er frame idle de stage0_SE.")]
        [SerializeField] private Sprite _placeholderSprite;
        [Tooltip("AnimatorController stage 0 (Bible Angle 1 : 0 leurre actif). Default = GhostraStage0_SE.controller. " +
                 "Son etat default est Idle, le leurre boucle en Idle indefiniment.")]
        [SerializeField] private RuntimeAnimatorController _decoyIdleController;
        [Tooltip("AnimatorController stage 1 (Bible Angle 2 : 1-2 leurres actifs). Default = GhostraStage1_SE.controller. " +
                 "Si null, fallback sur _decoyIdleController.")]
        [SerializeField] private RuntimeAnimatorController _decoyStage1Controller;
        [Tooltip("AnimatorController stage 2 (Bible Angle 3 : 3 leurres actifs = au cap). Default = GhostraStage2_SE.controller. " +
                 "Si null, fallback sur _decoyStage1Controller puis _decoyIdleController.")]
        [SerializeField] private RuntimeAnimatorController _decoyStage2Controller;
        [Tooltip("Scale appliquee au GameObject leurre. Default 1.16 (calibre Lorenzo, aligne avec RestructureGhostraPrefabTool).")]
        [SerializeField] private Vector3 _decoyScale = new Vector3(1.16f, 1.16f, 1f);
        [Tooltip("Y offset applique au sprite leurre (aligne avec le Visual.LocalPosition.y du prefab Ghostra : -0.22).")]
        [SerializeField] private float _decoyYOffset = -0.22f;
        [Tooltip("Valeur INITIALE du sorting order a la creation du GameObject leurre. Ecrasee des la 1ere frame par le tri iso dynamique (1000 - (PosX+PosY)*10, identique a CombatantView) pour que le leurre se trie comme un vrai combattant selon sa case.")]
        [SerializeField] private int _decoySortingOrder = 5;
        [Tooltip("Alpha du sprite leurre COTE CASTER (le Ghostra qui a pose). 1 = identique a la vraie Ghostra. Default 0.85 = legerement translucide pour aider le caster a distinguer ses leurres. COTE ADVERSAIRE l'alpha est force a 1 (indiscernable Bible V7.1).")]
        [SerializeField, Range(0f, 1f)] private float _decoyAlpha = 0.85f;
        [Tooltip("Tint applique au sprite leurre COTE CASTER uniquement. Default cyan pale pour aider le caster a distinguer ses leurres. Cote adversaire le tint est force a blanc opaque (indiscernable du vrai Ghostra).")]
        [SerializeField] private Color _decoyTint = new Color(0.7f, 0.88f, 1.0f, 1.0f); // bleu pale spectral

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
                        go = CreateDecoyGameObject(ghostraEntity, slot, d.Kind, ghostra.PlayerIndex);
                        _decoyVisuals[key] = go;
                    }

                    // Position iso depuis (PosX, PosY).
                    Vector3 world = IsoProjection.GridToWorld(
                        d.PosX, d.PosY,
                        _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight) + _centerOffset + transform.position;
                    world.y += _decoyYOffset;
                    go.transform.position = world;

                    // Tri iso : MEME formule que CombatantView (1000 - (gx+gy)*10) pour que le
                    // leurre se trie comme un vrai combattant selon sa case. Sinon le sortingOrder
                    // fixe le fait toujours passer SOUS les combattants (un perso derriere un leurre
                    // s'affichait devant). Recalcule chaque frame : un leurre peut etre deplace/permute.
                    var decoySr = go.GetComponent<SpriteRenderer>();
                    if (decoySr != null)
                    {
                        decoySr.sortingOrder = 1000 - (d.PosX + d.PosY) * 10;
                    }

                    // Sync sprite avec la vraie Ghostra (Bible "indiscernable cote adversaire").
                    SyncSpriteFromGhostra(go, ghostraEntity);

                    // Refresh tint chaque frame : robuste au tweak Inspector en Play Mode et
                    // au cas ou LocalPlayer change (improbable mais safe).
                    ApplyTintForOwnership(go, ghostra.PlayerIndex);

                    // Sync stage chaque frame : le Ghostra parent peut changer de stage
                    // (0/1/2 = Angle 1/2/3 Bible V7.1, base sur nb leurres actifs). Le leurre
                    // suit visuellement le stage du parent (cohenrence indiscernable Bible).
                    SyncStageFromGhostra(go, ghostra);
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

        private GameObject CreateDecoyGameObject(EntityRef ghostra, int slot, DecoyKind kind, int ghostraPlayerIndex)
        {
            var go = new GameObject($"FakeGhostra_E#{ghostra.Index}_slot{slot}_{kind}");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localScale = _decoyScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = _decoySortingOrder;
            // Tint applique selon ownership : caster voit cyan/translucide, adversaire voit
            // blanc opaque indiscernable. Resolu chaque frame dans ApplyTintForOwnership.
            // Init = adversaire-style (blanc opaque) safe par defaut.
            sr.color = Color.white;

            // 3.7.a.i.4 — ajoute Animator avec controller idle-only. Comme on ne trigger
            // jamais Walk/Cast/Attack sur le leurre, il boucle en Idle (default state).
            // Si _decoyIdleController null, fallback sprite statique via _placeholderSprite.
            if (_decoyIdleController != null)
            {
                var anim = go.AddComponent<Animator>();
                anim.runtimeAnimatorController = _decoyIdleController;
                anim.applyRootMotion = false;
            }
            else if (_placeholderSprite != null)
            {
                sr.sprite = _placeholderSprite;
            }

            // Proxy hover : permet a TileHoverView de detecter le survol et d'afficher
            // le tooltip du VRAI Ghostra parent (Bible V7.1 : mindgame indiscernable).
            var proxy = go.AddComponent<DecoyHoverProxy>();
            proxy.GhostraParentEntity = ghostra;

            return go;
        }

        /// <summary>
        /// Sync l'AnimatorController du leurre sur le stage du Ghostra parent (chaque frame,
        /// idempotent : ne touche que si le controller cible a change). Logique identique a
        /// CombatantRenderer.ComputeStage (Ghostra branch) : nb decoys actifs -> stage 0/1/2.
        /// Fallback descendant si le controller stage demande est null (stage 2 -> stage 1 ->
        /// stage 0 / idle).
        /// </summary>
        private void SyncStageFromGhostra(GameObject decoyGo, Combatant ghostra)
        {
            var anim = decoyGo.GetComponent<Animator>();
            if (anim == null) return; // mode fallback sprite statique : pas de stage swap

            int active = 0;
            for (int i = 0; i < 3; i++)
            {
                if (ghostra.Decoys[i].Kind != DecoyKind.None) active++;
            }
            int stage = active >= 3 ? 2 : active >= 1 ? 1 : 0;

            RuntimeAnimatorController target =
                stage == 2 ? (_decoyStage2Controller ?? _decoyStage1Controller ?? _decoyIdleController)
              : stage == 1 ? (_decoyStage1Controller ?? _decoyIdleController)
              :              _decoyIdleController;

            if (target != null && !ReferenceEquals(anim.runtimeAnimatorController, target))
            {
                anim.runtimeAnimatorController = target;
            }
        }

        /// <summary>
        /// Applique le tint au sprite leurre selon LocalPlayer vs Ghostra owner :
        /// - LocalPlayer == ghostraPlayerIndex (le caster Ghostra voit son propre leurre)
        ///   -> tint cyan + alpha 0.85 (aide visuelle a distinguer ses leurres).
        /// - LocalPlayer != ghostraPlayerIndex (l'adversaire voit le leurre)
        ///   -> tint blanc opaque (1,1,1,1) = indiscernable du vrai Ghostra (Bible V7.1).
        /// </summary>
        private void ApplyTintForOwnership(GameObject decoyGo, int ghostraPlayerIndex)
        {
            var sr = decoyGo.GetComponent<SpriteRenderer>();
            if (sr == null) return;
            int localPlayer = LocalPlayerResolver.Resolve();
            Color target = (localPlayer == ghostraPlayerIndex)
                ? new Color(_decoyTint.r, _decoyTint.g, _decoyTint.b, _decoyAlpha)
                : Color.white;
            if (sr.color != target) sr.color = target;
        }

        /// <summary>
        /// 3.7.a.i.4 — No-op si l'Animator du leurre gere lui-meme le sprite (cas standard
        /// avec _decoyIdleController bind). Sinon fallback sur _placeholderSprite statique.
        /// Les leurres NE reproduisent PAS les anims de la vraie Ghostra (walk/cast/attack/hurt)
        /// : ils tournent leur propre Animator idle-only et restent donc figés en pose Idle
        /// (avec la petite anim de respiration idle classique, pas un sprite statique).
        /// </summary>
        private void SyncSpriteFromGhostra(GameObject decoyGo, EntityRef ghostraEntity)
        {
            // Si un Animator est present (idle-only controller bind), il drive le sprite.
            // Sinon fallback sur _placeholderSprite statique.
            if (decoyGo.GetComponent<Animator>() != null) return;
            var sr = decoyGo.GetComponent<SpriteRenderer>();
            if (sr == null) return;
            if (_placeholderSprite != null && sr.sprite != _placeholderSprite)
            {
                sr.sprite = _placeholderSprite;
            }
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
