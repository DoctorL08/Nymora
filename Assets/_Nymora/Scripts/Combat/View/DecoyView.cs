using System.Collections.Generic;
using Nymora.Combat.Grid;
using Nymora.Core.ScriptableObjects;
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

        // Voile Spectral (TP des leurres autour de l'ennemi) : suit LastCastSequence de chaque
        // Ghostra pour déclencher un flash de téléportation sur ses leurres au moment du cast.
        private readonly Dictionary<EntityRef, int> _lastVoileCastSeq = new Dictionary<EntityRef, int>(2);
        // Couleur cyan spectral du flash de TP des leurres (aligné GhostraVfx).
        private static readonly Color DecoyTpColor = new Color(0.40f, 0.78f, 0.94f, 1f);

        // Cle composite (ghostraEntity, slotIndex) -> GameObject leurre actif.
        private readonly Dictionary<(EntityRef ghostra, int slot), GameObject> _decoyVisuals
            = new Dictionary<(EntityRef, int), GameObject>(6);

        // PATCH #6/#7 — familier sur CHAQUE leurre (indiscernabilite cote adverse) : meme cle
        // que _decoyVisuals -> la CombatPetView qui colle au leurre.
        private readonly Dictionary<(EntityRef ghostra, int slot), CombatPetView> _decoyPets
            = new Dictionary<(EntityRef, int), CombatPetView>(6);

        private const string PetCatalogResourcePath = "Cosmetics/PetCatalog";
        private PetCatalog _petCatalog;
        private bool _petCatalogLoaded;

        private PetCatalog PetCatalog
        {
            get
            {
                if (!_petCatalogLoaded)
                {
                    _petCatalog = Resources.Load<PetCatalog>(PetCatalogResourcePath);
                    _petCatalogLoaded = true;
                    if (_petCatalog == null)
                        Debug.LogWarning($"[Nymora.DecoyView] PetCatalog introuvable (Resources/{PetCatalogResourcePath}) — familiers de leurres desactives.");
                }
                return _petCatalog;
            }
        }

        // PATCH — skin equipe de la Ghostra : le leurre doit utiliser les MEMES controllers combat
        // (sinon le leurre a l'apparence de base alors que la vraie Ghostra a son skin -> distinguable
        // cote adverse). Catalogue charge depuis Resources (meme path que CombatantRenderer).
        private const string SkinCatalogResourcePath = "Cosmetics/CosmeticSkinCatalog";
        private CosmeticSkinCatalog _skinCatalog;
        private bool _skinCatalogLoaded;

        private CosmeticSkinCatalog SkinCatalog
        {
            get
            {
                if (!_skinCatalogLoaded)
                {
                    _skinCatalog = Resources.Load<CosmeticSkinCatalog>(SkinCatalogResourcePath);
                    _skinCatalogLoaded = true;
                    if (_skinCatalog == null)
                        Debug.LogWarning($"[Nymora.DecoyView] CosmeticSkinCatalog introuvable (Resources/{SkinCatalogResourcePath}) — leurres en sprites de base.");
                }
                return _skinCatalog;
            }
        }

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

            // #23 — contour de case d'équipe : actif UNIQUEMENT en match miroir (Ghostra vs Ghostra).
            bool mirror = MatchViewHelpers.IsMirrorMatch(frame);

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

                // PATCH #6/#7 — familier equipe de la Ghostra (resolu une fois par Ghostra) :
                // il sera replique sur CHAQUE leurre pour rester indiscernable cote adverse.
                var ownerPet = ResolveOwnerPetDef(frame, ghostra.PlayerIndex);

                // Le client LOCAL possede-t-il cette Ghostra ? (source de verite robuste : GetLocalPlayers
                // en online, slot 0 en IA). Si oui = caster -> teinte cyan ; sinon adversaire -> blanc
                // opaque (indiscernable). Corrige le guest PvP qui voyait les leurres ennemis teintes.
                bool localOwnsGhostra = LocalPlayerResolver.LocalOwns(game, ghostra.PlayerIndex);

                // PATCH — skin combat de la Ghostra (resolu une fois) : applique aux leurres pour
                // qu'ils aient la MEME apparence que la vraie Ghostra (indiscernable cote adverse).
                var ownerSkin = ResolveOwnerSkinDef(frame, ghostra);

                // Calibration X/Y/scale des leurres (recalage live via F10) : portee par le SKIN
                // equipe si present, sinon par la NymoraClassDefinition Ghostra de BASE. Resolue une
                // fois par Ghostra et appliquee a chacun de ses leurres ci-dessous.
                float decoyExtraX, decoyExtraY, decoyScaleMul;
                if (ownerSkin != null)
                {
                    decoyExtraX = ownerSkin.DecoyCombatXOffset;
                    decoyExtraY = ownerSkin.DecoyCombatYOffset;
                    decoyScaleMul = ownerSkin.DecoyCombatScale;
                }
                else
                {
                    var ghostraDef = _combatantRenderer != null
                        ? _combatantRenderer.FindClassDef(Nymora.Core.Enums.NymoraClass.Ghostra)
                        : null;
                    decoyExtraX = ghostraDef != null ? ghostraDef.DecoyCombatXOffset : 0f;
                    decoyExtraY = ghostraDef != null ? ghostraDef.DecoyCombatYOffset : 0f;
                    decoyScaleMul = ghostraDef != null ? ghostraDef.DecoyCombatScale : 1f;
                }

                // Voile Spectral : si la Ghostra vient de le caster ce tick, ses leurres ont été
                // téléportés autour de l'ennemi → on joue un flash spectral sur chacun (anim de TP).
                bool voileThisFrame = false;
                {
                    int prevVoile = _lastVoileCastSeq.TryGetValue(ghostraEntity, out var vv) ? vv : ghostra.LastCastSequence;
                    if (ghostra.LastCastSequence != prevVoile)
                    {
                        _lastVoileCastSeq[ghostraEntity] = ghostra.LastCastSequence;
                        if (ghostra.LastCastSpellId == SpellId.GhostraVoileSpectral) voileThisFrame = true;
                    }
                }

                // PATCH — materiau du SpriteRenderer de la vraie Ghostra (materiau 2D Lit) : on le
                // donne aux leurres pour qu'ils reagissent aux lights 2D exactement comme elle
                // (sinon leurre = Sprites-Default UNLIT -> rendu plus "plat/opaque" cote adverse).
                Material ghostraMat = _combatantRenderer != null
                    ? _combatantRenderer.GetCombatantSpriteMaterial(ghostraEntity)
                    : null;

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
                    // Recalage horizontal (skin ou base, réglable en live via F10) appliqué AVANT la
                    // capture du root pour que le familier du leurre suive le décalage X.
                    world.x += decoyExtraX;
                    Vector3 decoyBaseWorld = world; // position "root" (avant offset Y visuel), pour placer le familier
                    // Offset Y de base + offset additionnel (skin équipé OU classe Ghostra de base, réglable F10).
                    world.y += _decoyYOffset + decoyExtraY;
                    go.transform.position = world;

                    // Échelle de base du leurre × multiplicateur (skin OU classe de base, réglable en live
                    // via F10, persisté sur l'asset → toutes scènes). Réappliqué chaque frame (idempotent)
                    // pour que le tuner prenne effet immédiatement et que les leurres skinnés suivent.
                    Vector3 targetDecoyScale = _decoyScale * decoyScaleMul;
                    if (go.transform.localScale != targetDecoyScale) go.transform.localScale = targetDecoyScale;

                    // Tri iso : MEME formule que CombatantView (1000 - (gx+gy)*10) pour que le
                    // leurre se trie comme un vrai combattant selon sa case. Sinon le sortingOrder
                    // fixe le fait toujours passer SOUS les combattants (un perso derriere un leurre
                    // s'affichait devant). Recalcule chaque frame : un leurre peut etre deplace/permute.
                    var decoySr = go.GetComponent<SpriteRenderer>();
                    if (decoySr != null)
                    {
                        decoySr.sortingOrder = 1000 - (d.PosX + d.PosY) * 10;
                        // Meme materiau que la vraie Ghostra (2D Lit) -> rendu identique sous les lights.
                        if (ghostraMat != null && decoySr.sharedMaterial != ghostraMat)
                            decoySr.sharedMaterial = ghostraMat;
                    }

                    // #23 — contour de CASE d'équipe sous le leurre, centré sur la CASE (sans l'offset
                    //   de calibration X/Y du sprite) -> IDENTIQUE à celui du vrai Ghostra (même
                    //   couleur/taille/sorting) -> indiscernable côté adverse (Bible V7.1).
                    if (decoySr != null)
                    {
                        Vector3 decoyCellGround = IsoProjection.GridToWorld(d.PosX, d.PosY,
                            _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight)
                            + _centerOffset + transform.position;
                        MirrorOutlineHelper.RefreshOnUnit(go.transform, decoyCellGround,
                            _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight,
                            mirror, ghostra.PlayerIndex, decoySr.sortingLayerID, decoySr.sortingOrder - 1);
                    }

                    // Flash spectral de téléportation si le Voile vient d'être casté (leurre qui surgit).
                    if (voileThisFrame)
                    {
                        string lname = decoySr != null ? decoySr.sortingLayerName : "Default";
                        int lorder = (decoySr != null ? decoySr.sortingOrder : 5) + 1;
                        Animation.ProceduralVfx.Flash(transform, world, DecoyTpColor, 0.9f, 0.18f, lname, lorder);
                    }

                    // Sync sprite avec la vraie Ghostra (Bible "indiscernable cote adversaire").
                    SyncSpriteFromGhostra(go, ghostraEntity);

                    // Refresh tint chaque frame : robuste au tweak Inspector en Play Mode et
                    // au cas ou LocalPlayer change (improbable mais safe).
                    ApplyTintForOwnership(go, localOwnsGhostra);

                    // Sync stage chaque frame : le Ghostra parent peut changer de stage
                    // (0/1/2 = Angle 1/2/3 Bible V7.1, base sur nb leurres actifs). Le leurre
                    // suit visuellement le stage du parent (cohenrence indiscernable Bible).
                    // ownerSkin != null -> le leurre utilise les controllers du SKIN equipe.
                    SyncStageFromGhostra(go, ghostra, ownerSkin);

                    // PATCH #6/#7 — familier colle au leurre (meme pet que la Ghostra), teinte
                    // comme le leurre. Indiscernable cote adverse (la vraie Ghostra a aussi le sien).
                    UpdateDecoyPet(key, go, ownerPet, localOwnsGhostra, decoyBaseWorld);

                    // Patch 5 juin — pousse l'HP du leurre à son proxy (barre d'HP affichée au survol,
                    //   cf DecoyHoverProxy). max = HP de spawn du kind (0 = leurre 1-hit -> barre pleine).
                    var hoverProxy = go.GetComponent<DecoyHoverProxy>();
                    if (hoverProxy != null)
                    {
                        hoverProxy.SlotIndex = slot;
                        hoverProxy.SetHp(d.HP, DecoyHelpers.GetDecoyMaxHp(d.Kind));
                    }
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

                    // Familier du leurre supprime en meme temps.
                    if (_decoyPets.TryGetValue(key, out var deadPet) && deadPet != null)
                    {
                        Destroy(deadPet.gameObject);
                    }
                    _decoyPets.Remove(key);
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
        private void SyncStageFromGhostra(GameObject decoyGo, Combatant ghostra, CosmeticSkinDefinition skin)
        {
            var anim = decoyGo.GetComponent<Animator>();
            if (anim == null) return; // mode fallback sprite statique : pas de stage swap

            int active = 0;
            for (int i = 0; i < 3; i++)
            {
                if (ghostra.Decoys[i].Kind != DecoyKind.None) active++;
            }
            int stage = active >= 3 ? 2 : active >= 1 ? 1 : 0;

            RuntimeAnimatorController target;
            if (skin != null && skin.HasCombatControllers)
            {
                // PATCH — la Ghostra a un SKIN equipe : le leurre doit l'avoir aussi (indiscernable
                // cote adverse). Les leurres sont SE -> controllers SE du skin par stage (fallback
                // descendant si un stage du skin manque).
                target = stage == 2 ? (skin.Stage2ControllerSE ?? skin.Stage1ControllerSE ?? skin.Stage0ControllerSE)
                       : stage == 1 ? (skin.Stage1ControllerSE ?? skin.Stage0ControllerSE)
                       :              skin.Stage0ControllerSE;
            }
            else
            {
                target = stage == 2 ? (_decoyStage2Controller ?? _decoyStage1Controller ?? _decoyIdleController)
                       : stage == 1 ? (_decoyStage1Controller ?? _decoyIdleController)
                       :              _decoyIdleController;
            }

            if (target != null && !ReferenceEquals(anim.runtimeAnimatorController, target))
            {
                anim.runtimeAnimatorController = target;
            }
        }

        /// <summary>
        /// Applique le tint au sprite leurre selon que le client LOCAL possede la Ghostra :
        /// - localOwns == true (le caster Ghostra voit son propre leurre)
        ///   -> tint cyan + alpha 0.85 (aide visuelle a distinguer ses leurres).
        /// - localOwns == false (l'adversaire voit le leurre)
        ///   -> tint blanc opaque (1,1,1,1) = indiscernable du vrai Ghostra (Bible V7.1).
        /// </summary>
        private void ApplyTintForOwnership(GameObject decoyGo, bool localOwns)
        {
            var sr = decoyGo.GetComponent<SpriteRenderer>();
            if (sr == null) return;
            Color target = localOwns
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

        /// <summary>
        /// PATCH #6/#7 — Resout la PetDefinition du familier equipe par la Ghostra proprietaire
        /// (depuis RuntimePlayer.PetId, sync Quantum AddPlayer). Null si pas de familier equipe
        /// ou catalogue absent. Meme pattern que CombatantRenderer.SpawnPetFor.
        /// </summary>
        private PetDefinition ResolveOwnerPetDef(Frame frame, int playerIndex)
        {
            var catalog = PetCatalog;
            if (catalog == null || frame == null) return null;
            var rp = frame.GetPlayerData((PlayerRef)playerIndex);
            string petId = rp != null ? rp.PetId : null;
            return string.IsNullOrEmpty(petId) ? null : catalog.Resolve(petId);
        }

        /// <summary>
        /// PATCH — Resout le skin combat equipe par la Ghostra (depuis RuntimePlayer.SkinId, sync
        /// Quantum AddPlayer local+adverse). Garde-fou class-lock identique a CombatantRenderer.
        /// Null si pas de skin / catalogue absent / classe non concordante. Sert a donner au leurre
        /// les MEMES controllers combat que la vraie Ghostra (indiscernable cote adverse).
        /// </summary>
        private CosmeticSkinDefinition ResolveOwnerSkinDef(Frame frame, Combatant ghostra)
        {
            var catalog = SkinCatalog;
            if (catalog == null || frame == null) return null;
            var rp = frame.GetPlayerData((PlayerRef)ghostra.PlayerIndex);
            string skinId = rp != null ? rp.SkinId : null;
            if (string.IsNullOrEmpty(skinId)) return null;
            var skin = catalog.Resolve(skinId);
            if (skin == null) return null;
            if (skin.ClassId != Nymora.Core.Enums.NymoraClass.None
                && skin.ClassId.ToString() != ghostra.Class.ToString())
                return null;
            return skin;
        }

        /// <summary>
        /// PATCH #6/#7 — Cree/maintient le familier qui colle a un leurre (memes frames idle que
        /// le familier de la vraie Ghostra), teinte comme le leurre (cyan/translucide cote caster,
        /// opaque cote adverse). Si la Ghostra n'a pas de familier (petDef null), cleanup eventuel.
        /// Pose idle SE (comme le corps du leurre). View-only.
        /// </summary>
        private void UpdateDecoyPet((EntityRef ghostra, int slot) key, GameObject decoyGo,
            PetDefinition petDef, bool localOwns, Vector3 ownerBaseWorld)
        {
            bool hasPet = _decoyPets.TryGetValue(key, out var pet) && pet != null;

            if (petDef == null)
            {
                if (hasPet) { Destroy(pet.gameObject); _decoyPets.Remove(key); }
                return;
            }

            var decoySr = decoyGo.GetComponent<SpriteRenderer>();
            int order = decoySr != null ? decoySr.sortingOrder : 0;
            int layer = decoySr != null ? decoySr.sortingLayerID : 0;

            if (!hasPet)
            {
                var petGo = new GameObject($"DecoyPet_{decoyGo.name}");
                petGo.transform.SetParent(transform, worldPositionStays: false);
                pet = petGo.AddComponent<CombatPetView>();
                pet.Init(petDef, layer);
                _decoyPets[key] = pet;
            }

            // isLocal:false -> ne pousse PAS LastLocalFacingIndex (reserve au familier du joueur local).
            pet.Drive(ownerBaseWorld, IsoFacing.SE, order, layer, isLocal: false, dt: Time.deltaTime);

            // Teinte identique au leurre (cf ApplyTintForOwnership) : indiscernable cote adverse.
            Color tint = localOwns
                ? new Color(_decoyTint.r, _decoyTint.g, _decoyTint.b, _decoyAlpha)
                : Color.white;
            pet.SetTint(tint);
        }

        private void ClearAll()
        {
            foreach (var kvp in _decoyVisuals)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            _decoyVisuals.Clear();

            foreach (var kvp in _decoyPets)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            _decoyPets.Clear();
        }

        // Petit helper pour eviter d'allouer une nouvelle List a chaque cleanup.
        private sealed class ListBuffer<T> : System.IDisposable
        {
            public readonly List<T> List = new List<T>(4);
            public void Dispose() { List.Clear(); }
        }
    }
}
