using System.Collections.Generic;
using Quantum;
using TMPro;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// Affiche les marques visuelles (overlay sprite anime) au-dessus de chaque combatant
    /// porteur d'un Status couvert par MarkSpriteLibrary.
    ///
    /// Architecture :
    ///   - Subscribe a CallbackUpdateView pour poll l'etat des Statuses chaque frame view.
    ///   - Maintient un cache EntityRef -> CombatantView (rebuild lazy si mismatch).
    ///   - Pour chaque combatant : ajoute/retire des overlays GameObject enfants selon les
    ///     marques actives. Empile horizontalement si plusieurs marques en meme temps.
    ///
    /// Pure View — n'affecte pas la simulation. Reagit aux changements de Statuses sans
    /// instrumentation cote sim.
    /// </summary>
    public class CombatantMarksView : MonoBehaviour
    {
        [Header("Dependances")]
        [SerializeField] private MarkSpriteLibrary _library;

        [Header("Rendu")]
        [Tooltip("FPS de lecture des marques (4 frames a 4 fps = ~1s par cycle).")]
        [SerializeField, Min(1f)] private float _framesPerSecond = 4f;

        [Tooltip("B7 (22 mai) — Taille MONDE cible (dimension max) commune a TOUTES les marques. " +
                 "Le scale est calcule par marque = cette taille / dimension native du sprite, " +
                 "pour des marques UNIFORMES quelle que soit leur taille/PPU native. " +
                 "0.45 unit = lisible sans etre trop gros (avant : scale fixe 1.2 -> tailles inegales).")]
        [SerializeField, Min(0.05f)] private float _markTargetWorldSize = 0.45f;

        [Tooltip("Offset local par rapport au combatant. Y positif = au-dessus de la tete. " +
                 "POLISH-5b (17 mai) : bumped de 0.7 -> 1.4 car les sprites Phase 3 sont scales " +
                 "1.16x avec child Visual Y -0.22 (cf RestructureGhostraPrefabTool), donc la tete " +
                 "atteint maintenant ~0.9-1.0 unit. Marque a Y=1.4 = au-dessus de la tete, lisible.")]
        [SerializeField] private Vector2 _baseOffset = new Vector2(0f, 1.4f);

        [Tooltip("Espacement horizontal quand plusieurs marques sont actives sur la meme cible.")]
        [SerializeField, Min(0f)] private float _markSpacingX = 0.4f;

        [Tooltip("SortingOrder des marques. 1500 = devant les combattants (~1000), derriere VFX one-shot (~2000).")]
        [SerializeField] private int _sortingOrder = 1500;

        [Tooltip("POLISH-5c (17 mai) : log verbeux a chaque spawn/clear d'overlay de marque. " +
                 "A desactiver une fois le bug 'marks invisibles' resolu.")]
        [SerializeField] private bool _verboseLog = true;

        [Header("Venin Necram (compteur de marques)")]
        [Tooltip("Taille de police du compteur de marques de venin (nombre 1-4 sous l'icone). Monde.")]
        [SerializeField, Min(0.1f)] private float _veninCountFontSize = 2.2f;
        [Tooltip("Couleur du compteur de venin.")]
        [SerializeField] private Color _veninCountColor = new Color(0.7f, 1f, 0.4f, 1f); // vert venin

        // Etat runtime. Key encoding : pour eviter de melanger StatusKind et MarkKind dans le meme
        // dict, on prefixe StatusKind avec 0x1000 (Status) et MarkKind avec 0x2000 (Mark). Resultat :
        // un int unique par "type de marque visuelle" sans collisions entre enums.
        private const int KeyStatus = 0x1000;
        private const int KeyMark = 0x2000;
        private const int KeyVenin = 0x3000; // Necram : compteur de marques de venin (combatant.VeninStacks, hors Statuses[])
        private readonly Dictionary<EntityRef, CombatantView> _viewByEntity = new Dictionary<EntityRef, CombatantView>(4);
        private readonly Dictionary<EntityRef, Dictionary<int, GameObject>> _overlays =
            new Dictionary<EntityRef, Dictionary<int, GameObject>>(4);
        // Reuse buffers pour eviter les allocations dans OnUpdateView.
        private readonly List<int> _activeBuffer = new List<int>(4);
        private readonly List<int> _toRemoveBuffer = new List<int>(4);

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
        }

        private void OnGameStarted(QuantumGame game)
        {
            ClearAll();
            _viewByEntity.Clear();
            // RebuildViewCache sera fait au 1er OnUpdateView (l'ordre de spawn des CombatantView
            // n'est pas garanti par rapport a notre Awake).
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (_library == null) return;

            var frame = game.Frames.Verified;
            var filter = frame.Filter<Combatant>();
            bool needCacheRebuild = _viewByEntity.Count == 0;

            while (filter.Next(out EntityRef entity, out Combatant combatant))
            {
                if (!_viewByEntity.TryGetValue(entity, out var view) || view == null)
                {
                    needCacheRebuild = true;
                }
            }

            if (needCacheRebuild) RebuildViewCache();

            filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant combatant))
            {
                if (!_viewByEntity.TryGetValue(entity, out var view) || view == null) continue;
                UpdateMarksForCombatant(entity, combatant, view);
            }
        }

        private void RebuildViewCache()
        {
            _viewByEntity.Clear();
            var allViews = Object.FindObjectsByType<CombatantView>(FindObjectsSortMode.None);
            foreach (var v in allViews)
            {
                if (v == null) continue;
                if (v.Entity != EntityRef.None) _viewByEntity[v.Entity] = v;
            }
        }

        private void UpdateMarksForCombatant(EntityRef entity, Combatant combatant, CombatantView view)
        {
            // 1) Liste les marques actives sur ce combattant.
            //    a) StatusKind (Soulrender AntiHealShield, MarkedByCarnage, etc.) — 8 slots Statuses.
            _activeBuffer.Clear();
            for (int i = 0; i < 8; i++)
            {
                var s = combatant.Statuses[i];
                if (s.Kind == StatusKind.None || s.TurnsLeft <= 0) continue;
                if (_library.GetFrames(s.Kind) == null) continue;
                _activeBuffer.Add(KeyStatus | (int)s.Kind);
            }
            //    b) MarkKind (Nightseer Traque/Empreinte) — 1 marque max active a la fois (Bible).
            if (combatant.CurrentMark != MarkKind.None && combatant.MarkTurnsLeft > 0)
            {
                if (_library.GetMarkFrames(combatant.CurrentMark) != null)
                {
                    _activeBuffer.Add(KeyMark | (int)combatant.CurrentMark);
                }
            }
            //    c) Venin Necram (combatant.VeninStacks, cumulable 1-4) — overlay icone + compteur.
            //       Hors Statuses[] : c'est un compteur entier dedie. Une seule cle (KeyVenin), le
            //       nombre est affiche dans le label de l'overlay (mis a jour en etape 5).
            if (combatant.VeninStacks > 0 && _library.GetVeninFrames() != null)
            {
                _activeBuffer.Add(KeyVenin);
            }

            // 2) Recupere ou cree le dictionnaire d'overlays pour ce combattant.
            if (!_overlays.TryGetValue(entity, out var perEntity))
            {
                perEntity = new Dictionary<int, GameObject>(2);
                _overlays[entity] = perEntity;
            }

            // 3) Detruit les overlays pour les marques qui ne sont plus actives.
            _toRemoveBuffer.Clear();
            foreach (var kvp in perEntity)
            {
                if (!_activeBuffer.Contains(kvp.Key))
                {
                    if (kvp.Value != null) Destroy(kvp.Value);
                    _toRemoveBuffer.Add(kvp.Key);
                }
            }
            foreach (var key in _toRemoveBuffer) perEntity.Remove(key);

            // 4) Cree les overlays manquants.
            for (int i = 0; i < _activeBuffer.Count; i++)
            {
                int key = _activeBuffer[i];
                if (!perEntity.ContainsKey(key))
                {
                    var go = SpawnOverlay(view, key);
                    if (go != null) perEntity[key] = go;
                }
            }

            // 5) Repositionne tous les overlays actifs (centre + spacing horizontal).
            int count = _activeBuffer.Count;
            for (int i = 0; i < count; i++)
            {
                int key = _activeBuffer[i];
                if (!perEntity.TryGetValue(key, out var go) || go == null) continue;
                float xOffset = (i - (count - 1) * 0.5f) * _markSpacingX;
                go.transform.localPosition = new Vector3(_baseOffset.x + xOffset, _baseOffset.y, 0f);

                // Venin : met a jour le compteur (le nombre de marques varie de 1 a 4).
                if (key == KeyVenin)
                {
                    var countLabel = go.transform.Find("Count");
                    if (countLabel != null)
                    {
                        var tmp = countLabel.GetComponent<TextMeshPro>();
                        if (tmp != null) tmp.text = combatant.VeninStacks.ToString();
                    }
                }
            }
        }

        private GameObject SpawnOverlay(CombatantView view, int key)
        {
            // Venin : overlay dedie (icone animee + compteur TMP). Geometrie differente des autres
            // marques (root non scale, enfants Icon + Count), d'ou un chemin separe.
            if (key == KeyVenin) return SpawnVeninOverlay(view);

            // Decode (StatusKind ou MarkKind) selon le prefixe.
            Sprite[] frames;
            string nameSuffix;
            if ((key & 0xF000) == KeyMark)
            {
                MarkKind mk = (MarkKind)(key & 0xFFF);
                frames = _library.GetMarkFrames(mk);
                nameSuffix = $"Mark_{mk}";
            }
            else
            {
                StatusKind sk = (StatusKind)(key & 0xFFF);
                frames = _library.GetFrames(sk);
                nameSuffix = $"Status_{sk}";
            }
            if (frames == null || frames.Length == 0)
            {
                if (_verboseLog) Debug.LogWarning($"[Nymora.Marks] SKIP spawn {nameSuffix} on {view.name} : frames=null/empty (SO field non rempli — relance CombatAssetsNormalizer).");
                return null;
            }

            // POLISH-5c diag : detecte les frames non-null mais avec sprite null (asset fantome,
            // typique apres suppression des .gif si le SO n'a pas ete re-bind).
            int nullCount = 0;
            for (int i = 0; i < frames.Length; i++) if (frames[i] == null) nullCount++;
            if (_verboseLog)
            {
                string firstName = frames[0] != null ? frames[0].name : "<null>";
                Debug.Log($"[Nymora.Marks] Spawn {nameSuffix} on {view.name} : {frames.Length} frame(s), nullCount={nullCount}, first='{firstName}', pos={_baseOffset}, targetSize={_markTargetWorldSize}, sortingOrder={_sortingOrder}");
                if (nullCount > 0)
                {
                    Debug.LogError($"[Nymora.Marks] /!\\ {nameSuffix} a {nullCount}/{frames.Length} sprite null (asset fantome). Relance Nymora > Validation > Normalize Combat Assets > Do All.");
                }
            }

            var go = new GameObject(nameSuffix);
            go.transform.SetParent(view.transform, false);
            go.transform.localPosition = new Vector3(_baseOffset.x, _baseOffset.y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Default";
            sr.sortingOrder = _sortingOrder;
            sr.sprite = frames[0]; // pose la 1ere frame pour lire ses bounds (taille native monde)

            // B7 — normalise la taille : toutes les marques sont rendues a _markTargetWorldSize
            // (dimension max), independamment de la taille/PPU native du sprite -> UNIFORMES.
            float nativeMax = Mathf.Max(frames[0].bounds.size.x, frames[0].bounds.size.y);
            float uniformScale = nativeMax > 0.0001f ? _markTargetWorldSize / nativeMax : 1f;
            go.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);

            var anim = go.AddComponent<SpriteAnimator>();
            anim.SetFrames(frames, _framesPerSecond, loop: true);
            return go;
        }

        /// <summary>
        /// Necram (2 juin) — overlay de venin : une icone animee (marque_venin_putrefaction) + un
        /// compteur du nombre de marques actives (combatant.VeninStacks, 1-4). Le root n'est PAS scale
        /// (l'icone est scalee a part), pour que le compteur garde une taille lisible. Le texte est
        /// mis a jour chaque frame dans UpdateMarksForCombatant (etape 5).
        /// </summary>
        private GameObject SpawnVeninOverlay(CombatantView view)
        {
            var frames = _library.GetVeninFrames();
            if (frames == null || frames.Length == 0)
            {
                if (_verboseLog) Debug.LogWarning($"[Nymora.Marks] SKIP venin on {view.name} : _veninStacksFrames vide (relance Nymora > ... > Populate Combat Sprite Libraries).");
                return null;
            }

            var root = new GameObject("Venin");
            root.transform.SetParent(view.transform, false);
            root.transform.localPosition = new Vector3(_baseOffset.x, _baseOffset.y, 0f);

            // Icone animee, scalee a la taille monde commune des marques.
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(root.transform, false);
            var sr = iconGO.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Default";
            sr.sortingOrder = _sortingOrder;
            sr.sprite = frames[0];
            float nativeMax = Mathf.Max(frames[0].bounds.size.x, frames[0].bounds.size.y);
            float uniformScale = nativeMax > 0.0001f ? _markTargetWorldSize / nativeMax : 1f;
            iconGO.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
            var anim = iconGO.AddComponent<SpriteAnimator>();
            anim.SetFrames(frames, _framesPerSecond, loop: true);

            // Compteur (coin bas-droit de l'icone), TMP world space.
            var countGO = new GameObject("Count");
            countGO.transform.SetParent(root.transform, false);
            countGO.transform.localPosition = new Vector3(_markTargetWorldSize * 0.4f, -_markTargetWorldSize * 0.4f, 0f);
            var tmp = countGO.AddComponent<TextMeshPro>();
            tmp.text = "1";
            tmp.fontSize = _veninCountFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = _veninCountColor;
            tmp.fontStyle |= FontStyles.Bold;
            tmp.sortingOrder = _sortingOrder + 1; // devant l'icone
            // Contour noir lisible, via materiau instancie (n'affecte aucun autre TMP).
            _ = tmp.fontMaterial;
            tmp.outlineColor = new Color(0f, 0f, 0f, 1f);
            tmp.outlineWidth = 0.3f;
            var rect = countGO.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = new Vector2(1f, 0.6f);

            if (_verboseLog) Debug.Log($"[Nymora.Marks] Spawn Venin on {view.name} : {frames.Length} frame(s), targetSize={_markTargetWorldSize}, sortingOrder={_sortingOrder}");
            return root;
        }

        private void ClearAll()
        {
            foreach (var perEntity in _overlays.Values)
            {
                foreach (var go in perEntity.Values)
                {
                    if (go != null) Destroy(go);
                }
            }
            _overlays.Clear();
        }

        private void OnDestroy()
        {
            ClearAll();
        }
    }
}
