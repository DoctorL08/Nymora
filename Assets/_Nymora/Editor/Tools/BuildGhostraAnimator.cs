using System.IO;
using Nymora.Combat.View;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Phase 3 Bloc D — Editor tool : automatise le setup des animations Ghostra (stages 0/1/2).
    /// Mirror de <see cref="BuildNecramAnimator"/> / <see cref="BuildColossarAnimator"/>
    /// / <see cref="BuildNightseerAnimator"/> / <see cref="BuildSoulrenderAnimator"/>.
    ///
    /// Stages Bible V7.1 — mapping decide 16 mai 2026 : stage = Angle Mort (passif).
    ///   - stage 0 : Angle 1 (0 leurre actif) — passif neutre
    ///   - stage 1 : Angle 2 (1-2 leurres)    — sorts dorsaux +50 + plaie ouverte auto
    ///   - stage 2 : Angle 3 (3 leurres)      — sorts dorsaux +80 + permutation 0 PA
    /// La transition est pilotee par <c>CombatantRenderer.ComputeStage</c> qui lit
    /// <c>DecoyHelpers.CountActive</c> au lieu de <c>combatant.Resource</c> pour Ghostra.
    ///
    /// Inputs (livres par le designer dans Downloads/ghostra/Ghostra/Base/sources/) :
    ///   Assets/_Nymora/Art/Sprites/Ghostra/Base/sources/GHOSTRA_animation_stage{0,1,2}_{NE,SE}.aseprite
    ///   Frame tags Aseprite attendus : idle / walk / attack / cast / hurt / death.
    ///
    /// Outputs :
    ///   Assets/_Nymora/Animations/Ghostra/GhostraStage{0,1,2}_{NE,SE}.controller (6 controllers)
    ///   Update du prefab Combatant_Ghostra :
    ///     - Add Animator si absent
    ///     - Bind CombatantView : _animator + _stage{0,1,2}Controller{NE,SE}
    ///     - Fallback sprite : 1er Sprite du .aseprite stage0 SE
    ///
    /// Usage : Menu Nymora > Setup > Build Ghostra Animator
    /// Idempotent : ecrase les AnimatorController existants.
    /// </summary>
    public static class BuildGhostraAnimator
    {
        private const string AseSE0 = "Assets/_Nymora/Art/Sprites/Ghostra/Base/sources/GHOSTRA_animation_stage0_SE.aseprite";
        private const string AseNE0 = "Assets/_Nymora/Art/Sprites/Ghostra/Base/sources/GHOSTRA_animation_stage0_NE.aseprite";
        private const string AseSE1 = "Assets/_Nymora/Art/Sprites/Ghostra/Base/sources/GHOSTRA_animation_stage1_SE.aseprite";
        private const string AseNE1 = "Assets/_Nymora/Art/Sprites/Ghostra/Base/sources/GHOSTRA_animation_stage1_NE.aseprite";
        private const string AseSE2 = "Assets/_Nymora/Art/Sprites/Ghostra/Base/sources/GHOSTRA_animation_stage2_SE.aseprite";
        private const string AseNE2 = "Assets/_Nymora/Art/Sprites/Ghostra/Base/sources/GHOSTRA_animation_stage2_NE.aseprite";

        private const string AnimFolder = "Assets/_Nymora/Animations/Ghostra";

        // Indices : 0=SE/s0, 1=NE/s0, 2=SE/s1, 3=NE/s1, 4=SE/s2, 5=NE/s2.
        // Le default Animator tombe sur SE stage 0 (controllers[0]) = Angle 1 neutre.
        private static readonly string[] AsepritePaths = { AseSE0, AseNE0, AseSE1, AseNE1, AseSE2, AseNE2 };
        private static readonly string[] CtrlPaths =
        {
            AnimFolder + "/GhostraStage0_SE.controller",
            AnimFolder + "/GhostraStage0_NE.controller",
            AnimFolder + "/GhostraStage1_SE.controller",
            AnimFolder + "/GhostraStage1_NE.controller",
            AnimFolder + "/GhostraStage2_SE.controller",
            AnimFolder + "/GhostraStage2_NE.controller",
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

        private const string PrefabPath = "Assets/_Nymora/Prefabs/Combat/Combatants/Combatant_Ghostra.prefab";

        // Doivent rester aligned avec BuildSoulrenderAnimator / BuildNightseerAnimator /
        // BuildColossarAnimator / BuildNecramAnimator (CombatantView lit les memes Animator.StringToHash).
        public const string ParamMoveSpeed = "MoveSpeed";
        public const string ParamCastSpeed = "CastSpeed";
        public const string ParamCast = "Cast";
        public const string ParamAttack = "Attack";
        public const string ParamHurt = "Hurt";
        public const string ParamDeath = "Death";

        private const float IdleSpeed = 0.4f;

        [MenuItem("Nymora/Setup/Build Ghostra Animator")]
        public static void Run()
        {
            int n = AsepritePaths.Length;

            var clipSets = new ClipSet[n];
            for (int i = 0; i < n; i++)
            {
                if (!File.Exists(AsepritePaths[i]))
                {
                    Debug.LogError($"[BuildGhostraAnimator] Fichier introuvable : {AsepritePaths[i]}");
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
                Debug.Log($"[BuildGhostraAnimator] Cree {CtrlPaths[i]} (state machine complete)");
            }

            if (!File.Exists(PrefabPath))
            {
                Debug.LogError($"[BuildGhostraAnimator] Prefab introuvable : {PrefabPath}");
                AssetDatabase.SaveAssets();
                return;
            }

            GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[BuildGhostraAnimator] Impossible de charger : {PrefabPath}");
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
                    Debug.Log("[BuildGhostraAnimator] Animator ajoute au prefab.");
                }
                animator.runtimeAnimatorController = controllers[0]; // default = stage 0 SE
                animator.applyRootMotion = false;

                if (sr != null)
                {
                    var fallbackSprite = LoadFirstSpriteFromAseprite(AseSE0);
                    if (fallbackSprite != null)
                    {
                        sr.sprite = fallbackSprite;
                        Debug.Log($"[BuildGhostraAnimator] SpriteRenderer fallback sprite : {fallbackSprite.name}");
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
                    Debug.Log("[BuildGhostraAnimator] CombatantView : Animator + 6 controllers stage0/1/2 × NE/SE binds.");
                }
                else
                {
                    Debug.LogWarning("[BuildGhostraAnimator] CombatantView introuvable sur le prefab. Bind a faire manuellement.");
                }

                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                Debug.Log($"[BuildGhostraAnimator] Prefab sauvegarde : {PrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CtrlPaths[0]);
            Debug.Log("[BuildGhostraAnimator] DONE. 6 controllers stage0/1/2 × NE/SE generes et binds sur le prefab.");
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
            Debug.Log($"[BuildGhostraAnimator] {name} clips : {list}");
            int missing = 0;
            if (set.Idle == null) missing++;
            if (set.Walk == null) missing++;
            if (set.Attack == null) missing++;
            if (set.Cast == null) missing++;
            if (set.Hurt == null) missing++;
            if (set.Death == null) missing++;
            if (missing > 0)
            {
                Debug.LogWarning($"[BuildGhostraAnimator] {name} : {missing} clip(s) manquant(s) — fallback Idle. " +
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
                Debug.LogWarning($"[BuildGhostraAnimator] Champ '{propertyName}' introuvable sur CombatantView.");
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
