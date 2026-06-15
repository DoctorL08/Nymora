using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nymora.Combat.View;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Refonte graphique GHOSTRA (juin 2026) — pipeline complet en 1 clic.
    ///
    /// Nouveau modele visuel (vs l'ancien BuildGhostraAnimator a 3 skins dessines) :
    ///   - 1 SKIN DE BASE unique (idle/walk/attack/cast/hurt/death × SE/NE), joue a TOUS les stages.
    ///   - 2 couches d'AURA qui prennent le perso en sandwich (back derriere / front devant),
    ///     allumees aux stages 1 et 2 (1 aura par stage, stage 2 REMPLACE stage 1).
    /// La montee de stage reste pilotee par CombatantRenderer.ComputeStage (leurres actifs :
    /// 0 -> stage0, 1-2 -> stage1, 3 -> stage2). Cote View, c'est CombatantView.ApplyAura qui
    /// allume la bonne couche.
    ///
    /// Sources (PNG livres par le designer, bandes horizontales, cellules 128×128) :
    ///   Desktop/Nymora_Graph/Ghostra/Ghostra_refonte/PNG/
    ///     stage0/Ghostra_base_{idle,walk,attack,cast,hurt,death}_{SE,NE}.png   (12 sheets)
    ///     stage1/Ghostra_stage1_aura_{back,front}_{SE,NE}.png                  (4 sheets)
    ///     stage2/Ghostra_stage2_aura_{back,front}_{SE,NE}.png                  (4 sheets)
    ///
    /// Ce que le tool fait :
    ///   1. Copie les 20 PNG dans Assets/_Nymora/Art/Sprites/Ghostra/Refonte/{Base,Aura}/.
    ///   2. Slice grille 128, PPU 96, pivot custom 0.5/0.1 (calibration combat verrouillee).
    ///   3. Genere les clips : 12 base (idle/walk en loop) + 8 aura (toutes en loop).
    ///   4. Genere 2 controllers de base (GhostraBase_SE/NE, state machine complete avec params
    ///      MoveSpeed/CastSpeed/Cast/Attack/Hurt/Death) + 8 mini-controllers d'aura (1 state loop).
    ///   5. Restructure le prefab Combatant_Ghostra :
    ///        - le meme controller de base est bind sur les 3 slots de stage (le skin ne change
    ///          plus par stage) ;
    ///        - 2 child "AuraBack"/"AuraFront" sous "Visual" (SpriteRenderer + Animator), tri
    ///          sandwich, materiau copie du sprite de base ;
    ///        - bind des 12 nouveaux champs d'aura de CombatantView.
    ///
    /// View-only : aucun bump CombatRulesVersion. Idempotent (re-run = ecrase/repointe).
    /// Menu : Nymora > Setup > Build Ghostra Refonte.
    /// </summary>
    public static class BuildGhostraRefonte
    {
        // --- Sources (hors projet) ---
        private const string Src = "C:/Users/Lorenzo/Desktop/Nymora_Graph/Ghostra/Ghostra_refonte/PNG";

        // --- Calibration combat verrouillee (cf ImportColossarSpritesheetsTool) ---
        private const int Cell = 128;
        private const float Ppu = 96f;
        private static readonly Vector2 Pivot = new Vector2(0.5f, 0.1f);

        // --- Destinations projet ---
        private const string DstBaseSheets = "Assets/_Nymora/Art/Sprites/Ghostra/Refonte/Base";
        private const string DstAuraSheets = "Assets/_Nymora/Art/Sprites/Ghostra/Refonte/Aura";
        private const string DstClips = "Assets/_Nymora/Animations/Ghostra/Refonte/Clips";
        private const string DstCtrl = "Assets/_Nymora/Animations/Ghostra/Refonte";

        private const string PrefabPath = "Assets/_Nymora/Prefabs/Combat/Combatants/Combatant_Ghostra.prefab";

        private static readonly string[] Dirs = { "SE", "NE" };

        // Skin de base : anim -> (state Animator, loop, fps, pingpong).
        // pingpong = jouer la sequence aller PUIS retour (boomerang) pour un cycle parfaitement
        // continu. idle 7 frames -> 0..6 puis 5..1 = 12 frames (sans doubler les frames extremes).
        private static readonly (string anim, string state, bool loop, float fps, bool pingpong)[] BaseAnims =
        {
            ("idle",   "Idle",   true,  8f,  true),
            ("walk",   "Walk",   true,  12f, false),
            ("attack", "Attack", false, 12f, false),
            ("cast",   "Cast",   false, 12f, false),
            ("hurt",   "Hurt",   false, 12f, false),
            ("death",  "Death",  false, 10f, false),
        };

        // Aura : boucle ambiante calme (cadence portee par CombatantView._auraFps, lue par AuraLoopPlayer).
        private static readonly string[] AuraLayers = { "back", "front" };
        private static readonly int[] AuraStages = { 1, 2 };

        // Params Animator (memes hash que CombatantView / BuildGhostraAnimator).
        private const string ParamMoveSpeed = "MoveSpeed";
        private const string ParamCastSpeed = "CastSpeed";
        private const string ParamCast = "Cast";
        private const string ParamAttack = "Attack";
        private const string ParamHurt = "Hurt";
        private const string ParamDeath = "Death";
        // Vitesse du state Idle = 1.0 : l'idle tourne a la cadence pleine du clip (8 fps), comme
        // dans le hub. (L'ancien 0.4 ralentissait l'idle combat a ~3 fps -> moins fluide que le hub.)
        private const float IdleSpeed = 1.0f;
        // Garde aligne avec SetAttackSpeed.AttackSpeed (rebuild self-consistent).
        private const float AttackSpeedMultiplier = 1.5f;

        [MenuItem("Nymora/Setup/Build Ghostra Refonte", priority = 40)]
        public static void Run()
        {
            if (!Directory.Exists(Src))
            {
                EditorUtility.DisplayDialog("Build Ghostra Refonte",
                    $"Dossier source introuvable :\n{Src}", "OK");
                return;
            }

            EnsureFolder(DstBaseSheets);
            EnsureFolder(DstAuraSheets);
            EnsureFolder(DstClips);
            EnsureFolder(DstCtrl);

            var report = new List<string>();

            // ----- 1+2+3+4a : skin de base -> 12 clips -> 2 controllers -----
            var baseCtrl = new Dictionary<string, AnimatorController>(); // dir -> controller
            var hubFrames = new Dictionary<string, Sprite[]>();          // "idle_SE" / "walk_NE" -> sprites (hub)
            Sprite fallbackSprite = null;
            foreach (var dir in Dirs)
            {
                var set = new ClipSet();
                foreach (var (anim, state, loop, fps, pingpong) in BaseAnims)
                {
                    string fileBase = $"Ghostra_base_{anim}_{dir}";
                    string srcPng = $"{Src}/stage0/{fileBase}.png";
                    if (!File.Exists(srcPng)) { report.Add($"⚠ manque {fileBase}.png"); continue; }

                    string dstPng = $"{DstBaseSheets}/{fileBase}.png";
                    File.Copy(srcPng, dstPng, true);
                    AssetDatabase.ImportAsset(dstPng, ImportAssetOptions.ForceUpdate);

                    int frames = SliceSheet(dstPng, fileBase);
                    if (frames <= 0) { report.Add($"⚠ slice échoué : {fileBase}"); continue; }

                    var sprites = LoadOrderedSprites(dstPng);
                    if (dir == "SE" && anim == "idle" && sprites.Length > 0) fallbackSprite = sprites[0];
                    // L'avatar du HUB anime idle/walk (SE+NE) — on capture ces sprites (sens aller)
                    // pour repointer les tableaux Idle/WalkFrames de la NymoraClassDefinition Ghostra.
                    if (anim == "idle" || anim == "walk") hubFrames[$"{anim}_{dir}"] = sprites;

                    // Sequence du clip : boomerang aller-retour pour l'idle (cycle continu).
                    Sprite[] clipSprites = pingpong ? PingPong(sprites) : sprites;
                    string clipPath = $"{DstClips}/{fileBase}.anim";
                    var clip = BuildClip(clipSprites, fps, loop, clipPath);
                    set.Assign(state, clip);
                }

                string ctrlPath = $"{DstCtrl}/GhostraBase_{dir}.controller";
                baseCtrl[dir] = BuildBaseController(ctrlPath, set);
                report.Add($"base {dir} : controller {Path.GetFileName(ctrlPath)} OK");
            }

            // ----- 1+2+3+4b : auras -> 8 sets de Sprite[] (pas de controller : un AuraLoopPlayer
            //   lit directement les frames ; un Animator imbrique casserait l'idle de base) -----
            // cle = (stage, layer, dir) -> Sprite[]
            var auraFrames = new Dictionary<(int, string, string), Sprite[]>();
            foreach (int stage in AuraStages)
            {
                foreach (var layer in AuraLayers)
                {
                    foreach (var dir in Dirs)
                    {
                        string fileBase = $"Ghostra_stage{stage}_aura_{layer}_{dir}";
                        string srcPng = $"{Src}/stage{stage}/{fileBase}.png";
                        if (!File.Exists(srcPng)) { report.Add($"⚠ manque {fileBase}.png"); continue; }

                        string dstPng = $"{DstAuraSheets}/{fileBase}.png";
                        File.Copy(srcPng, dstPng, true);
                        AssetDatabase.ImportAsset(dstPng, ImportAssetOptions.ForceUpdate);

                        int frames = SliceSheet(dstPng, fileBase);
                        if (frames <= 0) { report.Add($"⚠ slice échoué : {fileBase}"); continue; }

                        auraFrames[(stage, layer, dir)] = LoadOrderedSprites(dstPng);
                    }
                }
            }
            report.Add($"auras : {auraFrames.Count}/8 sets de frames OK");

            // ----- 5 : prefab -----
            PatchPrefab(baseCtrl, auraFrames, fallbackSprite, report);

            // ----- 6 : hub (avatar = idle/walk SE+NE de la NymoraClassDefinition Ghostra) -----
            RetargetGhostraHub(hubFrames, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BuildGhostraRefonte]\n" + string.Join("\n", report));
            EditorUtility.DisplayDialog("Build Ghostra Refonte",
                string.Join("\n", report) +
                "\n\nView-only : pas de bump version. Lance un combat IA Ghostra et utilise F10 " +
                "(preview stage 0/1/2) pour valider les auras en SE/NE/NW/SW.",
                "OK");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PrefabPath);
        }

        // =====================================================================
        // Prefab : 2 child d'aura sous "Visual" + bind CombatantView
        // =====================================================================
        private static void PatchPrefab(
            Dictionary<string, AnimatorController> baseCtrl,
            Dictionary<(int, string, string), Sprite[]> auraFrames,
            Sprite fallbackSprite,
            List<string> report)
        {
            if (!File.Exists(PrefabPath)) { report.Add($"⚠ prefab introuvable : {PrefabPath}"); return; }

            GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var baseSr = prefab.GetComponentInChildren<SpriteRenderer>();
                if (baseSr == null) { report.Add("⚠ SpriteRenderer de base introuvable sur le prefab."); return; }
                GameObject visual = baseSr.gameObject;

                // Animator de base : repointe sur le controller de base SE (default).
                var baseAnimator = visual.GetComponent<Animator>();
                if (baseAnimator == null) baseAnimator = visual.AddComponent<Animator>();
                baseAnimator.runtimeAnimatorController = baseCtrl["SE"];
                baseAnimator.applyRootMotion = false;

                if (fallbackSprite != null) baseSr.sprite = fallbackSprite;

                // 2 couches d'aura, enfants de "Visual" (heritent offset/scale/squash de base).
                // PAS d'Animator dessus : un Animator imbrique sous l'Animator de base casse l'idle.
                // Un AuraLoopPlayer (MonoBehaviour) pilote la boucle de Sprite[].
                var auraBack = EnsureAuraChild(visual, "AuraBack", baseSr);
                var auraFront = EnsureAuraChild(visual, "AuraFront", baseSr);

                // Bind CombatantView.
                var view = prefab.GetComponentInChildren<CombatantView>();
                if (view == null) { report.Add("⚠ CombatantView introuvable — bind a faire a la main."); }
                else
                {
                    var so = new SerializedObject(view);
                    SetRef(so, "_animator", baseAnimator);

                    // Skin de base IDENTIQUE aux 3 stages (le garde anti-reset de CombatantView evite
                    // le restart d'anim quand le stage change).
                    SetRef(so, "_stage0ControllerSE", baseCtrl["SE"]);
                    SetRef(so, "_stage1ControllerSE", baseCtrl["SE"]);
                    SetRef(so, "_stage2ControllerSE", baseCtrl["SE"]);
                    SetRef(so, "_stage0ControllerNE", baseCtrl["NE"]);
                    SetRef(so, "_stage1ControllerNE", baseCtrl["NE"]);
                    SetRef(so, "_stage2ControllerNE", baseCtrl["NE"]);

                    SetRef(so, "_auraBack", auraBack.GetComponent<SpriteRenderer>());
                    SetRef(so, "_auraFront", auraFront.GetComponent<SpriteRenderer>());
                    SetRef(so, "_auraBackPlayer", auraBack.GetComponent<AuraLoopPlayer>());
                    SetRef(so, "_auraFrontPlayer", auraFront.GetComponent<AuraLoopPlayer>());
                    var fpsProp = so.FindProperty("_auraFps");
                    if (fpsProp != null && fpsProp.floatValue <= 0f) fpsProp.floatValue = 8f;

                    SetArray(so, "_auraStage1BackSE", Get(auraFrames, 1, "back", "SE"));
                    SetArray(so, "_auraStage1BackNE", Get(auraFrames, 1, "back", "NE"));
                    SetArray(so, "_auraStage1FrontSE", Get(auraFrames, 1, "front", "SE"));
                    SetArray(so, "_auraStage1FrontNE", Get(auraFrames, 1, "front", "NE"));
                    SetArray(so, "_auraStage2BackSE", Get(auraFrames, 2, "back", "SE"));
                    SetArray(so, "_auraStage2BackNE", Get(auraFrames, 2, "back", "NE"));
                    SetArray(so, "_auraStage2FrontSE", Get(auraFrames, 2, "front", "SE"));
                    SetArray(so, "_auraStage2FrontNE", Get(auraFrames, 2, "front", "NE"));

                    so.ApplyModifiedPropertiesWithoutUndo();
                    report.Add("CombatantView : base (3 slots) + auras (2 players + 8 sets de frames) bindes.");
                }

                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                report.Add($"prefab sauvegarde : {Path.GetFileName(PrefabPath)}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        /// <summary>
        /// Cree (ou reutilise) un child d'aura sous "Visual" avec SpriteRenderer (disabled, materiau
        /// copie de la base) + AuraLoopPlayer. PAS d'Animator (imbrication interdite sous l'Animator
        /// de base -> casse l'idle). Nettoie un eventuel Animator herite d'une version precedente.
        /// </summary>
        private static GameObject EnsureAuraChild(GameObject visual, string name, SpriteRenderer baseSr)
        {
            Transform t = visual.transform.Find(name);
            GameObject go = t != null ? t.gameObject : new GameObject(name);
            if (t == null)
            {
                go.transform.SetParent(visual.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one;
            }

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = baseSr.sharedMaterial;       // meme rendu (2D lights) que la base
            sr.sortingLayerID = baseSr.sortingLayerID;
            sr.enabled = false;                              // stage 0 par defaut

            // Retire un Animator herite d'une version anterieure du tool (imbrication = idle cassee).
            var staleAnim = go.GetComponent<Animator>();
            if (staleAnim != null) Object.DestroyImmediate(staleAnim, true);

            if (go.GetComponent<AuraLoopPlayer>() == null) go.AddComponent<AuraLoopPlayer>();

            return go;
        }

        private static Sprite[] Get(
            Dictionary<(int, string, string), Sprite[]> map, int stage, string layer, string dir)
            => map.TryGetValue((stage, layer, dir), out var s) ? s : null;

        // =====================================================================
        // Hub : repointe les frames idle/walk de la NymoraClassDefinition Ghostra
        // sur le nouveau skin de base (l'avatar hub n'utilise PAS le prefab combat ;
        // il anime IdleFrames/IdleFramesNE/WalkFrames/WalkFramesNE — cf RetargetHubFrames).
        // Pas de stages dans le hub -> pas d'aura ici, juste le skin de base.
        // =====================================================================
        private const string GhostraClassAsset = "Assets/_Nymora/ScriptableObjects/Classes/Ghostra.asset";

        private static void RetargetGhostraHub(Dictionary<string, Sprite[]> hub, List<string> report)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(GhostraClassAsset);
            if (asset == null) { report.Add($"⚠ hub : asset introuvable ({GhostraClassAsset})"); return; }

            // Idle hub en PING-PONG (aller-retour) comme en combat : le hub boucle le tableau, donc
            // un tableau aller-retour produit le boomerang. Walk reste en boucle simple (aller).
            Sprite[] idleSE = PingPong(hub.TryGetValue("idle_SE", out var a) ? a : null);
            Sprite[] idleNE = PingPong(hub.TryGetValue("idle_NE", out var b) ? b : null);
            Sprite[] walkSE = hub.TryGetValue("walk_SE", out var c) ? c : null;
            Sprite[] walkNE = hub.TryGetValue("walk_NE", out var d) ? d : null;
            if (idleSE == null || idleSE.Length == 0) { report.Add("⚠ hub : pas de sprites idle SE -> repointage hub sauté"); return; }

            var so = new SerializedObject(asset);
            SetArray(so, "IdleFrames", idleSE);
            SetArray(so, "IdleFramesNE", idleNE != null && idleNE.Length > 0 ? idleNE : idleSE);
            SetArray(so, "WalkFrames", walkSE != null && walkSE.Length > 0 ? walkSE : idleSE);
            SetArray(so, "WalkFramesNE", walkNE != null && walkNE.Length > 0 ? walkNE
                : (idleNE != null && idleNE.Length > 0 ? idleNE : idleSE));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            report.Add($"hub : idle {idleSE.Length}/{(idleNE?.Length ?? 0)} · walk {(walkSE?.Length ?? 0)}/{(walkNE?.Length ?? 0)} repointes");
        }

        private static void SetArray(SerializedObject so, string prop, Sprite[] sprites)
        {
            var p = so.FindProperty(prop);
            if (p == null || !p.isArray) { Debug.LogWarning($"[BuildGhostraRefonte] champ hub '{prop}' introuvable/non-array."); return; }
            p.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        // =====================================================================
        // Controllers
        // =====================================================================
        private struct ClipSet
        {
            public AnimationClip Idle, Walk, Attack, Cast, Hurt, Death;
            public void Assign(string state, AnimationClip clip)
            {
                if (clip == null) return;
                switch (state)
                {
                    case "Idle": Idle = clip; break;
                    case "Walk": Walk = clip; break;
                    case "Attack": Attack = clip; break;
                    case "Cast": Cast = clip; break;
                    case "Hurt": Hurt = clip; break;
                    case "Death": Death = clip; break;
                }
            }
        }

        /// <summary>State machine complete (idle/walk/cast/attack/hurt/death) — calque de
        /// BuildGhostraAnimator.BuildStateMachine, params identiques lus par CombatantView.</summary>
        private static AnimatorController BuildBaseController(string path, ClipSet clips)
        {
            // Reconstruit EN PLACE (preserve le guid) au lieu de delete+recreate : le delete/recreate
            // au sein d'un run laisse le prefab pointer un controller pas encore re-enregistre dans
            // l'AssetDatabase -> refs "Missing" transitoires dans l'inspecteur. En place = pas de churn.
            var ctrl = GetOrCreateClearedController(path);

            ctrl.AddParameter(ParamMoveSpeed, AnimatorControllerParameterType.Float);
            ctrl.AddParameter(ParamCastSpeed, AnimatorControllerParameterType.Float);
            var ps = ctrl.parameters;
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].name == ParamCastSpeed) ps[i].defaultFloat = 1f;
            ctrl.parameters = ps;
            ctrl.AddParameter(ParamCast, AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter(ParamAttack, AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter(ParamHurt, AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter(ParamDeath, AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            AnimationClip Or(AnimationClip c) => c != null ? c : clips.Idle;

            var idle = sm.AddState("Idle");
            idle.motion = clips.Idle;
            idle.speed = IdleSpeed;

            var walk = sm.AddState("Walk");
            walk.motion = Or(clips.Walk);
            walk.speedParameter = ParamMoveSpeed;
            walk.speedParameterActive = true;

            var cast = sm.AddState("Cast");
            cast.motion = Or(clips.Cast);
            cast.speedParameter = ParamCastSpeed;
            cast.speedParameterActive = true;

            var attack = sm.AddState("Attack");
            attack.motion = Or(clips.Attack);
            attack.speed = AttackSpeedMultiplier;

            var hurt = sm.AddState("Hurt");
            hurt.motion = Or(clips.Hurt);

            var death = sm.AddState("Death");
            death.motion = Or(clips.Death);

            sm.defaultState = idle;

            var i2w = idle.AddTransition(walk);
            i2w.hasExitTime = false; i2w.duration = 0.1f;
            i2w.AddCondition(AnimatorConditionMode.Greater, 0.01f, ParamMoveSpeed);
            var w2i = walk.AddTransition(idle);
            w2i.hasExitTime = false; w2i.duration = 0.1f;
            w2i.AddCondition(AnimatorConditionMode.Less, 0.01f, ParamMoveSpeed);

            AnyTo(sm, cast, ParamCast);
            AnyTo(sm, attack, ParamAttack);
            AnyTo(sm, hurt, ParamHurt);
            AnyTo(sm, death, ParamDeath);
            BackToIdle(cast, idle);
            BackToIdle(attack, idle);
            BackToIdle(hurt, idle);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssetIfDirty(ctrl);
            return ctrl;
        }

        /// <summary>
        /// Charge le controller existant et le VIDE (params + states + transitions AnyState/Entry)
        /// pour le reconstruire en place SANS le supprimer -> preserve le guid -> aucune ref Missing
        /// transitoire dans les prefabs/scenes qui le referencent. Cree l'asset s'il n'existe pas.
        /// </summary>
        private static AnimatorController GetOrCreateClearedController(string path)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl == null) return AnimatorController.CreateAnimatorControllerAtPath(path);

            foreach (var p in ctrl.parameters) ctrl.RemoveParameter(p);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
            foreach (var t in sm.entryTransitions) sm.RemoveEntryTransition(t);
            foreach (var cs in sm.states) sm.RemoveState(cs.state);
            return ctrl;
        }

        private static void AnyTo(AnimatorStateMachine sm, AnimatorState target, string trigger)
        {
            var t = sm.AddAnyStateTransition(target);
            t.hasExitTime = false; t.duration = 0.05f; t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        private static void BackToIdle(AnimatorState from, AnimatorState idle)
        {
            var t = from.AddTransition(idle);
            t.hasExitTime = true; t.exitTime = 0.95f; t.duration = 0.1f;
        }

        // =====================================================================
        // Slice + clips (calque ImportColossarSpritesheetsTool)
        // =====================================================================
#pragma warning disable 0618
        private static int SliceSheet(string assetPath, string spriteBaseName)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter ti) return 0;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            int width = tex != null ? tex.width : 0;
            int height = tex != null ? tex.height : Cell;
            if (width <= 0) return 0;

            int frames = Mathf.Max(1, width / Cell);
            var metas = new SpriteMetaData[frames];
            for (int i = 0; i < frames; i++)
            {
                metas[i] = new SpriteMetaData
                {
                    name = $"{spriteBaseName}_{i}",
                    rect = new Rect(i * Cell, 0, Cell, Mathf.Min(Cell, height)),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = Pivot,
                };
            }

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritePixelsPerUnit = Ppu;
            ti.spritesheet = metas;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.isReadable = false;
            ti.sRGBTexture = true;
            ti.SaveAndReimport();
            return frames;
        }
#pragma warning restore 0618

        private static AnimationClip BuildClip(Sprite[] sprites, float fps, bool loop, string clipPath)
        {
            if (sprites == null || sprites.Length == 0 || fps <= 0f) return null;
            var clip = new AnimationClip { frameRate = fps, name = Path.GetFileNameWithoutExtension(clipPath) };
            var binding = new EditorCurveBinding { type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite" };
            var keys = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            if (loop)
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        private static Sprite[] LoadOrderedSprites(string sheetPath)
            => AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>()
                .OrderBy(s => TrailingInt(s.name)).ToArray();

        /// <summary>Sequence boomerang : aller (0..n-1) puis retour (n-2..1) — sans doubler les
        /// frames extremes -> cycle parfaitement continu. n frames -> 2n-2 frames.</summary>
        private static Sprite[] PingPong(Sprite[] s)
        {
            if (s == null || s.Length < 3) return s;
            int n = s.Length;
            var result = new Sprite[2 * n - 2];
            for (int i = 0; i < n; i++) result[i] = s[i];               // 0 .. n-1
            for (int i = 1; i < n - 1; i++) result[n - 1 + i] = s[n - 1 - i]; // n-2 .. 1
            return result;
        }

        private static int TrailingInt(string name)
        {
            int i = name.Length - 1;
            while (i >= 0 && char.IsDigit(name[i])) i--;
            return int.TryParse(name.Substring(i + 1), out var v) ? v : 0;
        }

        private static void SetRef(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning($"[BuildGhostraRefonte] champ '{prop}' introuvable sur CombatantView."); return; }
            p.objectReferenceValue = value;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
