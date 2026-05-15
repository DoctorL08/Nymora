using System.IO;
using Nymora.Combat.View;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Phase 3 Bloc C — Editor tool : automatise le setup des animations Necram (stages 0/1/2).
    /// Mirror du <see cref="BuildColossarAnimator"/>, <see cref="BuildNightseerAnimator"/>
    /// et <see cref="BuildSoulrenderAnimator"/> — meme state machine, memes parametres,
    /// meme convention NE/SE + flipX pour NW/SW.
    ///
    /// Stages Bible V7.1 : Necram atteint stage1/2 via La Floraison (passif marques).
    /// Le tool genere les 6 controllers (stage0/1/2 × NE/SE) et bind les 6 fields
    /// _stage{0,1,2}Controller{NE,SE} du CombatantView. CombatantView.SetStageAndFacing
    /// switche entre les 3 controllers selon le stage courant du combattant.
    ///
    /// Inputs (livres par le designer) :
    ///   Assets/_Nymora/Art/Sprites/Necram/Base/sources/NECRAM_animation_stage{0,1,2}_{NE,SE}.aseprite
    ///   Frame tags Aseprite attendus : idle / walk / attack / cast / hurt / death.
    ///
    /// Outputs (genere automatiquement) :
    ///   Assets/_Nymora/Animations/Necram/NecramStage{0,1,2}_{NE,SE}.controller (6 controllers)
    ///   Update du prefab Combatant_Necram :
    ///     - Add Animator si absent
    ///     - Bind CombatantView : _animator + _stage{0,1,2}Controller{NE,SE}
    ///     - Fallback sprite : 1er Sprite du .aseprite stage0 SE
    ///
    /// Usage : Menu Nymora > Setup > Build Necram Animator
    /// Le tool est idempotent : si les AnimatorController existent deja, ecrases.
    /// </summary>
    public static class BuildNecramAnimator
    {
        private const string AseSE0 = "Assets/_Nymora/Art/Sprites/Necram/Base/sources/NECRAM_animation_stage0_SE.aseprite";
        private const string AseNE0 = "Assets/_Nymora/Art/Sprites/Necram/Base/sources/NECRAM_animation_stage0_NE.aseprite";
        private const string AseSE1 = "Assets/_Nymora/Art/Sprites/Necram/Base/sources/NECRAM_animation_stage1_SE.aseprite";
        private const string AseNE1 = "Assets/_Nymora/Art/Sprites/Necram/Base/sources/NECRAM_animation_stage1_NE.aseprite";
        private const string AseSE2 = "Assets/_Nymora/Art/Sprites/Necram/Base/sources/NECRAM_animation_stage2_SE.aseprite";
        private const string AseNE2 = "Assets/_Nymora/Art/Sprites/Necram/Base/sources/NECRAM_animation_stage2_NE.aseprite";

        private const string AnimFolder = "Assets/_Nymora/Animations/Necram";

        // Indices : 0=SE/s0, 1=NE/s0, 2=SE/s1, 3=NE/s1, 4=SE/s2, 5=NE/s2.
        // L'ordre SE-then-NE par stage est volontaire pour que le default Animator
        // tombe sur SE stage 0 (controllers[0]).
        private static readonly string[] AsepritePaths = { AseSE0, AseNE0, AseSE1, AseNE1, AseSE2, AseNE2 };
        private static readonly string[] CtrlPaths =
        {
            AnimFolder + "/NecramStage0_SE.controller",
            AnimFolder + "/NecramStage0_NE.controller",
            AnimFolder + "/NecramStage1_SE.controller",
            AnimFolder + "/NecramStage1_NE.controller",
            AnimFolder + "/NecramStage2_SE.controller",
            AnimFolder + "/NecramStage2_NE.controller",
        };
        private static readonly string[] BindFieldNames =
        {
            "_stage0ControllerSE",
            "_stage0ControllerNE",
            "_stage1ControllerSE",
            "_stage1ControllerNE",
            "_stage2ControllerSE",
            "_stage2ControllerNE",
        };

        private const string PrefabPath = "Assets/_Nymora/Prefabs/Combat/Combatants/Combatant_Necram.prefab";

        // Doivent rester aligned avec BuildSoulrenderAnimator / BuildNightseerAnimator / BuildColossarAnimator
        // (CombatantView lit les memes Animator.StringToHash).
        public const string ParamMoveSpeed = "MoveSpeed";
        public const string ParamCastSpeed = "CastSpeed";
        public const string ParamCast = "Cast";
        public const string ParamAttack = "Attack";
        public const string ParamHurt = "Hurt";
        public const string ParamDeath = "Death";

        private const float IdleSpeed = 0.4f;

        [MenuItem("Nymora/Setup/Build Necram Animator")]
        public static void Run()
        {
            int n = AsepritePaths.Length;

            var clipSets = new ClipSet[n];
            for (int i = 0; i < n; i++)
            {
                if (!File.Exists(AsepritePaths[i]))
                {
                    Debug.LogError($"[BuildNecramAnimator] Fichier introuvable : {AsepritePaths[i]}");
                    return;
                }
                clipSets[i] = LoadClipSet(AsepritePaths[i]);
                LogClipSetSummary(AsepritePaths[i], clipSets[i]);
            }

            EnsureFolderRecursive(AnimFolder);

            var controllers = new RuntimeAnimatorController[n];
            for (int i = 0; i < n; i++)
            {
                if (File.Exists(CtrlPaths[i]))
                {
                    AssetDatabase.DeleteAsset(CtrlPaths[i]);
                }
                var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPaths[i]);
                BuildStateMachine(ctrl, clipSets[i]);
                controllers[i] = ctrl;
                Debug.Log($"[BuildNecramAnimator] Cree {CtrlPaths[i]} (state machine complete)");
            }

            if (!File.Exists(PrefabPath))
            {
                Debug.LogError($"[BuildNecramAnimator] Prefab introuvable : {PrefabPath}");
                AssetDatabase.SaveAssets();
                return;
            }

            GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[BuildNecramAnimator] Impossible de charger : {PrefabPath}");
                return;
            }

            try
            {
                var sr = prefab.GetComponentInChildren<SpriteRenderer>();
                GameObject host = sr != null ? sr.gameObject : prefab;
                var animator = host.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = host.AddComponent<Animator>();
                    Debug.Log("[BuildNecramAnimator] Animator ajoute au prefab.");
                }
                animator.runtimeAnimatorController = controllers[0]; // default = stage 0 SE
                animator.applyRootMotion = false;

                if (sr != null)
                {
                    var fallbackSprite = LoadFirstSpriteFromAseprite(AseSE0);
                    if (fallbackSprite != null)
                    {
                        sr.sprite = fallbackSprite;
                        Debug.Log($"[BuildNecramAnimator] SpriteRenderer fallback sprite : {fallbackSprite.name}");
                    }
                }

                var view = prefab.GetComponentInChildren<CombatantView>();
                if (view != null)
                {
                    var so = new SerializedObject(view);
                    SetObjectRef(so, "_animator", animator);
                    for (int i = 0; i < n; i++)
                    {
                        SetObjectRef(so, BindFieldNames[i], controllers[i]);
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("[BuildNecramAnimator] CombatantView : Animator + 6 controllers stage0/1/2 × NE/SE binds.");
                }
                else
                {
                    Debug.LogWarning("[BuildNecramAnimator] CombatantView introuvable sur le prefab. Bind a faire manuellement.");
                }

                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                Debug.Log($"[BuildNecramAnimator] Prefab sauvegarde : {PrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CtrlPaths[0]);
            Debug.Log("[BuildNecramAnimator] DONE. 6 controllers stage0/1/2 × NE/SE generes et binds sur le prefab.");
        }

        private struct ClipSet
        {
            public AnimationClip Idle;
            public AnimationClip Walk;
            public AnimationClip Attack;
            public AnimationClip Cast;
            public AnimationClip Hurt;
            public AnimationClip Death;
        }

        private static ClipSet LoadClipSet(string asepritePath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(asepritePath);
            var set = new ClipSet();
            foreach (var a in assets)
            {
                if (!(a is AnimationClip clip)) continue;
                string lower = clip.name.ToLowerInvariant();
                if (set.Idle == null && lower.Contains("idle")) set.Idle = clip;
                else if (set.Walk == null && lower.Contains("walk")) set.Walk = clip;
                else if (set.Attack == null && lower.Contains("attack")) set.Attack = clip;
                else if (set.Cast == null && lower.Contains("cast")) set.Cast = clip;
                else if (set.Hurt == null && lower.Contains("hurt")) set.Hurt = clip;
                else if (set.Death == null && lower.Contains("death")) set.Death = clip;
            }
            if (set.Idle == null)
            {
                foreach (var a in assets)
                {
                    if (a is AnimationClip clip) { set.Idle = clip; break; }
                }
            }
            return set;
        }

        private static void LogClipSetSummary(string asepritePath, ClipSet set)
        {
            string name = Path.GetFileNameWithoutExtension(asepritePath);
            string list =
                $"idle={ClipName(set.Idle)} walk={ClipName(set.Walk)} attack={ClipName(set.Attack)} " +
                $"cast={ClipName(set.Cast)} hurt={ClipName(set.Hurt)} death={ClipName(set.Death)}";
            Debug.Log($"[BuildNecramAnimator] {name} clips : {list}");
            int missing = 0;
            if (set.Idle == null) missing++;
            if (set.Walk == null) missing++;
            if (set.Attack == null) missing++;
            if (set.Cast == null) missing++;
            if (set.Hurt == null) missing++;
            if (set.Death == null) missing++;
            if (missing > 0)
            {
                Debug.LogWarning($"[BuildNecramAnimator] {name} : {missing} clip(s) manquant(s) — fallback Idle. " +
                    "Verifie les tags Aseprite (idle/walk/attack/cast/hurt/death) et 'Generate Animation Clips'.");
            }
        }

        private static string ClipName(AnimationClip c) => c != null ? c.name : "(null)";

        private static Sprite LoadFirstSpriteFromAseprite(string asepritePath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(asepritePath);
            foreach (var a in assets)
            {
                if (a is Sprite sprite) return sprite;
            }
            return null;
        }

        private static void BuildStateMachine(AnimatorController ctrl, ClipSet clips)
        {
            ctrl.AddParameter(ParamMoveSpeed, AnimatorControllerParameterType.Float);
            ctrl.AddParameter(ParamCastSpeed, AnimatorControllerParameterType.Float);
            var parameters = ctrl.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == ParamCastSpeed) parameters[i].defaultFloat = 1f;
            }
            ctrl.parameters = parameters;
            ctrl.AddParameter(ParamCast, AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter(ParamAttack, AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter(ParamHurt, AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter(ParamDeath, AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;

            var idle = sm.AddState("Idle");
            idle.motion = clips.Idle;
            idle.speed = IdleSpeed;

            var walk = sm.AddState("Walk");
            walk.motion = clips.Walk != null ? clips.Walk : clips.Idle;
            walk.speedParameter = ParamMoveSpeed;
            walk.speedParameterActive = true;

            var cast = sm.AddState("Cast");
            cast.motion = clips.Cast != null ? clips.Cast : clips.Idle;
            cast.speedParameter = ParamCastSpeed;
            cast.speedParameterActive = true;

            var attack = sm.AddState("Attack");
            attack.motion = clips.Attack != null ? clips.Attack : clips.Idle;

            var hurt = sm.AddState("Hurt");
            hurt.motion = clips.Hurt != null ? clips.Hurt : clips.Idle;

            var death = sm.AddState("Death");
            death.motion = clips.Death != null ? clips.Death : clips.Idle;

            sm.defaultState = idle;

            var idleToWalk = idle.AddTransition(walk);
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.1f;
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.01f, ParamMoveSpeed);

            var walkToIdle = walk.AddTransition(idle);
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.1f;
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.01f, ParamMoveSpeed);

            AddTriggerFromAny(sm, cast, ParamCast);
            AddTriggerFromAny(sm, attack, ParamAttack);
            AddTriggerFromAny(sm, hurt, ParamHurt);
            AddTriggerFromAny(sm, death, ParamDeath);

            AddBackToIdle(cast, idle);
            AddBackToIdle(attack, idle);
            AddBackToIdle(hurt, idle);
        }

        private static void AddTriggerFromAny(AnimatorStateMachine sm, AnimatorState target, string trigger)
        {
            var t = sm.AddAnyStateTransition(target);
            t.hasExitTime = false;
            t.duration = 0.05f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        private static void AddBackToIdle(AnimatorState from, AnimatorState idle)
        {
            var t = from.AddTransition(idle);
            t.hasExitTime = true;
            t.exitTime = 0.95f;
            t.duration = 0.1f;
        }

        private static void SetObjectRef(SerializedObject so, string propertyName, Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[BuildNecramAnimator] Champ '{propertyName}' introuvable sur CombatantView.");
                return;
            }
            prop.objectReferenceValue = value;
        }

        private static void EnsureFolderRecursive(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolderRecursive(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
