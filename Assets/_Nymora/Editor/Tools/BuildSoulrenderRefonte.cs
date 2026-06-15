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
    /// Refonte graphique SOULRENDER (14 juin 2026) — pipeline complet en 1 clic.
    /// Calque EXACT de BuildGhostraRefonte (même modèle visuel, mêmes décisions techniques).
    ///
    /// Modele visuel :
    ///   - 1 SKIN DE BASE unique (idle/walk/attack/cast/hurt/death × SE/NE), joue a TOUS les stages.
    ///   - 2 couches d'AURA qui prennent le perso en sandwich (back derriere / front devant),
    ///     allumees aux stages 1 et 2 (1 aura par stage, stage 2 REMPLACE stage 1).
    /// La montee de stage du Soulrender reste pilotee par CombatantRenderer.ComputeStage via la
    /// RESSOURCE (Carnage) : < 40% -> stage0, < max -> stage1, == max -> stage2. Cote View, c'est
    /// CombatantView.ApplyAura qui allume la bonne couche.
    ///
    /// Sources (PNG livres par le designer, bandes horizontales, cellules 128×128) :
    ///   Desktop/Nymora_Graph/Soulrender/PNG/
    ///     stage0/soulrender_base_{idle,walking,attack,cast,hurt,death}_{SE,NE}.png   (12 sheets)
    ///     stage1/Soulrender_stage1_aura_{back,front}_{SE,NE}.png                     (4 sheets)
    ///     stage2/Soulrender_stage2_aura_{back,front}_{SE,NE}.png                     (4 sheets)
    ///   ⚠ Nommage livre : base en minuscule "soulrender_base_", anim de marche = "walking",
    ///     auras en "Soulrender_stage{n}_aura_...".
    ///
    /// Ce que le tool fait : copie/slice (PPU 96, pivot 0.5/0.1) les 20 PNG -> clips -> 2 controllers
    /// base (SoulrenderBase_SE/NE, reconstruits EN PLACE -> guid preserve) + 8 sets de frames d'aura
    /// -> bind du prefab Combatant_Soulrender + retarget de l'avatar hub (Soulrender.asset).
    ///
    /// View-only : aucun bump CombatRulesVersion. Idempotent (re-run = ecrase/repointe).
    /// Menu : Nymora > Setup > Build Soulrender Refonte.
    /// </summary>
    public static class BuildSoulrenderRefonte
    {
        // --- Sources (hors projet) ---
        private const string Src = "C:/Users/Lorenzo/Desktop/Nymora_Graph/Soulrender/PNG";

        // --- Calibration combat verrouillee (cf BuildGhostraRefonte) ---
        private const int Cell = 128;
        private const float Ppu = 96f;
        private static readonly Vector2 Pivot = new Vector2(0.5f, 0.1f);

        // --- Destinations projet ---
        private const string DstBaseSheets = "Assets/_Nymora/Art/Sprites/Soulrender/Refonte/Base";
        private const string DstAuraSheets = "Assets/_Nymora/Art/Sprites/Soulrender/Refonte/Aura";
        private const string DstClips = "Assets/_Nymora/Animations/Soulrender/Refonte/Clips";
        private const string DstCtrl = "Assets/_Nymora/Animations/Soulrender/Refonte";

        private const string PrefabPath = "Assets/_Nymora/Prefabs/Combat/Combatants/Combatant_Soulrender.prefab";

        private static readonly string[] Dirs = { "SE", "NE" };

        // Skin de base : (token fichier, anim cle hub, state Animator, loop, fps, pingpong).
        // tokenFile = suffixe du PNG ("walking" chez le designer) ; animKey = cle stable pour le hub.
        private static readonly (string file, string animKey, string state, bool loop, float fps, bool pingpong)[] BaseAnims =
        {
            ("idle",    "idle", "Idle",   true,  8f,  true),
            ("walking", "walk", "Walk",   true,  12f, false),
            ("attack",  null,   "Attack", false, 12f, false),
            ("cast",    null,   "Cast",   false, 12f, false),
            ("hurt",    null,   "Hurt",   false, 12f, false),
            ("death",   null,   "Death",  false, 10f, false),
        };

        private static readonly string[] AuraLayers = { "back", "front" };
        private static readonly int[] AuraStages = { 1, 2 };

        // Params Animator (memes hash que CombatantView).
        private const string ParamMoveSpeed = "MoveSpeed";
        private const string ParamCastSpeed = "CastSpeed";
        private const string ParamCast = "Cast";
        private const string ParamAttack = "Attack";
        private const string ParamHurt = "Hurt";
        private const string ParamDeath = "Death";
        private const float IdleSpeed = 1.0f;
        // Garde aligne avec SetAttackSpeed.AttackSpeed (rebuild self-consistent).
        private const float AttackSpeedMultiplier = 1.5f;

        [MenuItem("Nymora/Setup/Build Soulrender Refonte", priority = 41)]
        public static void Run()
        {
            if (!Directory.Exists(Src))
            {
                EditorUtility.DisplayDialog("Build Soulrender Refonte",
                    $"Dossier source introuvable :\n{Src}", "OK");
                return;
            }

            EnsureFolder(DstBaseSheets);
            EnsureFolder(DstAuraSheets);
            EnsureFolder(DstClips);
            EnsureFolder(DstCtrl);

            var report = new List<string>();

            // ----- skin de base -> 12 clips -> 2 controllers -----
            var baseCtrl = new Dictionary<string, AnimatorController>(); // dir -> controller
            var hubFrames = new Dictionary<string, Sprite[]>();          // "idle_SE" / "walk_NE" -> sprites (hub)
            Sprite fallbackSprite = null;
            foreach (var dir in Dirs)
            {
                var set = new ClipSet();
                foreach (var (file, animKey, state, loop, fps, pingpong) in BaseAnims)
                {
                    string fileBase = $"soulrender_base_{file}_{dir}";
                    string srcPng = $"{Src}/stage0/{fileBase}.png";
                    if (!File.Exists(srcPng)) { report.Add($"⚠ manque {fileBase}.png"); continue; }

                    string dstPng = $"{DstBaseSheets}/{fileBase}.png";
                    File.Copy(srcPng, dstPng, true);
                    AssetDatabase.ImportAsset(dstPng, ImportAssetOptions.ForceUpdate);

                    int frames = SliceSheet(dstPng, fileBase);
                    if (frames <= 0) { report.Add($"⚠ slice échoué : {fileBase}"); continue; }

                    var sprites = LoadOrderedSprites(dstPng);
                    if (dir == "SE" && state == "Idle" && sprites.Length > 0) fallbackSprite = sprites[0];
                    // L'avatar du HUB anime idle/walk (SE+NE) — on capture ces sprites (sens aller).
                    if (animKey != null) hubFrames[$"{animKey}_{dir}"] = sprites;

                    Sprite[] clipSprites = pingpong ? PingPong(sprites) : sprites;
                    string clipPath = $"{DstClips}/{fileBase}.anim";
                    var clip = BuildClip(clipSprites, fps, loop, clipPath);
                    set.Assign(state, clip);
                }

                string ctrlPath = $"{DstCtrl}/SoulrenderBase_{dir}.controller";
                baseCtrl[dir] = BuildBaseController(ctrlPath, set);
                report.Add($"base {dir} : controller {Path.GetFileName(ctrlPath)} OK");
            }

            // ----- auras -> 8 sets de Sprite[] (pas de controller : AuraLoopPlayer lit les frames) -----
            var auraFrames = new Dictionary<(int, string, string), Sprite[]>();
            foreach (int stage in AuraStages)
            {
                foreach (var layer in AuraLayers)
                {
                    foreach (var dir in Dirs)
                    {
                        string fileBase = $"Soulrender_stage{stage}_aura_{layer}_{dir}";
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

            // ----- prefab -----
            PatchPrefab(baseCtrl, auraFrames, fallbackSprite, report);

            // ----- hub (avatar = idle/walk SE+NE de la NymoraClassDefinition Soulrender) -----
            RetargetSoulrenderHub(hubFrames, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BuildSoulrenderRefonte]\n" + string.Join("\n", report));
            EditorUtility.DisplayDialog("Build Soulrender Refonte",
                string.Join("\n", report) +
                "\n\nView-only : pas de bump version. Lance un combat IA Soulrender et utilise F10 " +
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

                var baseAnimator = visual.GetComponent<Animator>();
                if (baseAnimator == null) baseAnimator = visual.AddComponent<Animator>();
                baseAnimator.runtimeAnimatorController = baseCtrl["SE"];
                baseAnimator.applyRootMotion = false;

                if (fallbackSprite != null) baseSr.sprite = fallbackSprite;

                var auraBack = EnsureAuraChild(visual, "AuraBack", baseSr);
                var auraFront = EnsureAuraChild(visual, "AuraFront", baseSr);

                var view = prefab.GetComponentInChildren<CombatantView>();
                if (view == null) { report.Add("⚠ CombatantView introuvable — bind a faire a la main."); }
                else
                {
                    var so = new SerializedObject(view);
                    SetRef(so, "_animator", baseAnimator);

                    // Skin de base IDENTIQUE aux 3 stages (garde anti-reset de CombatantView).
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
        /// copie de la base) + AuraLoopPlayer. PAS d'Animator (imbrication interdite -> casse l'idle).
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

            var staleAnim = go.GetComponent<Animator>();
            if (staleAnim != null) Object.DestroyImmediate(staleAnim, true);

            if (go.GetComponent<AuraLoopPlayer>() == null) go.AddComponent<AuraLoopPlayer>();

            return go;
        }

        private static Sprite[] Get(
            Dictionary<(int, string, string), Sprite[]> map, int stage, string layer, string dir)
            => map.TryGetValue((stage, layer, dir), out var s) ? s : null;

        // =====================================================================
        // Hub : repointe les frames idle/walk de la NymoraClassDefinition Soulrender
        // =====================================================================
        private const string SoulrenderClassAsset = "Assets/_Nymora/ScriptableObjects/Classes/Soulrender.asset";

        private static void RetargetSoulrenderHub(Dictionary<string, Sprite[]> hub, List<string> report)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(SoulrenderClassAsset);
            if (asset == null) { report.Add($"⚠ hub : asset introuvable ({SoulrenderClassAsset})"); return; }

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
            if (p == null || !p.isArray) { Debug.LogWarning($"[BuildSoulrenderRefonte] champ '{prop}' introuvable/non-array."); return; }
            if (sprites == null) { p.arraySize = 0; return; }
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

        private static AnimatorController BuildBaseController(string path, ClipSet clips)
        {
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
        // Slice + clips (calque BuildGhostraRefonte)
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

        /// <summary>Boomerang : aller (0..n-1) puis retour (n-2..1) -> cycle continu. n -> 2n-2.</summary>
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
            if (p == null) { Debug.LogWarning($"[BuildSoulrenderRefonte] champ '{prop}' introuvable sur CombatantView."); return; }
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
