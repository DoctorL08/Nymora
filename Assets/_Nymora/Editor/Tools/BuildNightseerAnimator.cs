using System.IO;
using Nymora.Combat.View;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Editor tool : automatise le setup des animations Nightseer (2.12.bis Nightseer).
    /// Mirror exact du <see cref="BuildSoulrenderAnimator"/> — meme state machine, memes
    /// parametres, meme convention NE/SE + flipX pour NW/SW.
    ///
    /// Inputs (livres par le designer) :
    ///   Assets/_Nymora/Art/Sprites/Nightseer/Base/sources/NS_animation_stage{0,1,2}_{NE,SE}.aseprite
    ///   Frame tags Aseprite attendus dans chaque fichier : idle / walk / attack / cast / hurt / death.
    ///   Si un tag manque, fallback sur idle (warning console mais pas d'erreur).
    ///
    /// Outputs (genere automatiquement) :
    ///   Assets/_Nymora/Animations/Nightseer/NightseerStage{0,1,2}_{NE,SE}.controller (6 controllers)
    ///     State machine identique au Soulrender (Idle / Walk / Cast / Attack / Hurt / Death).
    ///     Parametres : MoveSpeed (float), CastSpeed (float, default 1), Cast/Attack/Hurt/Death (triggers).
    ///   Update du prefab Combatant_Nightseer :
    ///     - Add Animator si absent
    ///     - Bind CombatantView : _animator + 6 fields _stage{0,1,2}Controller{NE,SE}
    ///
    /// Les directions NW et SW sont obtenues runtime par flipX (cf CombatantView.SetStageAndFacing).
    ///
    /// Usage : Menu Nymora > Setup > Build Nightseer Animator
    /// Le tool est idempotent : si les AnimatorController existent deja, ecrases.
    /// </summary>
    public static class BuildNightseerAnimator
    {
        private const string AseSE0 = "Assets/_Nymora/Art/Sprites/Nightseer/Base/sources/NS_animation_stage0_SE.aseprite";
        private const string AseSE1 = "Assets/_Nymora/Art/Sprites/Nightseer/Base/sources/NS_animation_stage1_SE.aseprite";
        private const string AseSE2 = "Assets/_Nymora/Art/Sprites/Nightseer/Base/sources/NS_animation_stage2_SE.aseprite";
        private const string AseNE0 = "Assets/_Nymora/Art/Sprites/Nightseer/Base/sources/NS_animation_stage0_NE.aseprite";
        private const string AseNE1 = "Assets/_Nymora/Art/Sprites/Nightseer/Base/sources/NS_animation_stage1_NE.aseprite";
        private const string AseNE2 = "Assets/_Nymora/Art/Sprites/Nightseer/Base/sources/NS_animation_stage2_NE.aseprite";

        private const string AnimFolder = "Assets/_Nymora/Animations/Nightseer";

        // Indices : 0..2 = SE par stage, 3..5 = NE par stage.
        private static readonly string[] AsepritePaths = { AseSE0, AseSE1, AseSE2, AseNE0, AseNE1, AseNE2 };
        private static readonly string[] CtrlPaths =
        {
            AnimFolder + "/NightseerStage0_SE.controller",
            AnimFolder + "/NightseerStage1_SE.controller",
            AnimFolder + "/NightseerStage2_SE.controller",
            AnimFolder + "/NightseerStage0_NE.controller",
            AnimFolder + "/NightseerStage1_NE.controller",
            AnimFolder + "/NightseerStage2_NE.controller",
        };
        private static readonly string[] BindFieldNames =
        {
            "_stage0ControllerSE",
            "_stage1ControllerSE",
            "_stage2ControllerSE",
            "_stage0ControllerNE",
            "_stage1ControllerNE",
            "_stage2ControllerNE",
        };

        private const string PrefabPath = "Assets/_Nymora/Prefabs/Combat/Combatants/Combatant_Nightseer.prefab";

        // Parametres et noms d'etats : doivent rester aligned avec BuildSoulrenderAnimator
        // (CombatantView lit les memes Animator.StringToHash). Si tu modifies ici, modifie
        // aussi la-bas et dans CombatantView (Param*Hash).
        public const string ParamMoveSpeed = "MoveSpeed";
        public const string ParamCastSpeed = "CastSpeed";
        public const string ParamCast = "Cast";
        public const string ParamAttack = "Attack";
        public const string ParamHurt = "Hurt";
        public const string ParamDeath = "Death";

        // Idle speed bas (~2-3 cycles/s) : meme valeur que Soulrender pour coherence.
        private const float IdleSpeed = 0.4f;

        [MenuItem("Nymora/Setup/Build Nightseer Animator")]
        public static void Run()
        {
            int n = AsepritePaths.Length;

            // 1. Pre-flight : verifier que les .aseprite existent et collecter les clips par tag.
            var clipSets = new ClipSet[n];
            for (int i = 0; i < n; i++)
            {
                if (!File.Exists(AsepritePaths[i]))
                {
                    Debug.LogError($"[BuildNightseerAnimator] Fichier introuvable : {AsepritePaths[i]}");
                    return;
                }
                clipSets[i] = LoadClipSet(AsepritePaths[i]);
                LogClipSetSummary(AsepritePaths[i], clipSets[i]);
            }

            // 2. Cree le dossier Animations/Nightseer si absent.
            EnsureFolderRecursive(AnimFolder);

            // 3. Cree les 6 AnimatorController avec state machine complete.
            var controllers = new RuntimeAnimatorController[n];
            for (int i = 0; i < n; i++)
            {
                if (File.Exists(CtrlPaths[i]))
                {
                    AssetDatabase.DeleteAsset(CtrlPaths[i]); // overwrite
                }
                var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPaths[i]);
                BuildStateMachine(ctrl, clipSets[i]);
                controllers[i] = ctrl;
                Debug.Log($"[BuildNightseerAnimator] Cree {CtrlPaths[i]} (state machine complete)");
            }

            // 4. Update le prefab Combatant_Nightseer.
            if (!File.Exists(PrefabPath))
            {
                Debug.LogError($"[BuildNightseerAnimator] Prefab introuvable : {PrefabPath}");
                AssetDatabase.SaveAssets();
                return;
            }

            GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[BuildNightseerAnimator] Impossible de charger : {PrefabPath}");
                return;
            }

            try
            {
                // Add Animator si absent (on le met sur le meme GO que le SpriteRenderer).
                var sr = prefab.GetComponentInChildren<SpriteRenderer>();
                GameObject host = sr != null ? sr.gameObject : prefab;
                var animator = host.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = host.AddComponent<Animator>();
                    Debug.Log("[BuildNightseerAnimator] Animator ajoute au prefab.");
                }
                animator.runtimeAnimatorController = controllers[0]; // default = stage 0 SE
                animator.applyRootMotion = false;

                // Fallback : si l'anim ne fire pas (tags Aseprite mal nommes / clip ciblant un autre
                // path), le SpriteRenderer affichait le placeholder violet historique. On force le
                // sprite statique au 1er frame du .aseprite Stage 0 SE pour avoir au moins le bon
                // visuel par defaut.
                if (sr != null)
                {
                    var fallbackSprite = LoadFirstSpriteFromAseprite(AseSE0);
                    if (fallbackSprite != null)
                    {
                        sr.sprite = fallbackSprite;
                        Debug.Log($"[BuildNightseerAnimator] SpriteRenderer fallback sprite : {fallbackSprite.name}");
                    }
                    else
                    {
                        Debug.LogWarning("[BuildNightseerAnimator] Aucun Sprite extrait du .aseprite Stage 0 SE — le prefab garde son sprite courant.");
                    }
                }

                // Bind CombatantView.
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
                    Debug.Log("[BuildNightseerAnimator] CombatantView : Animator + 6 controllers (NE+SE) binds.");
                }
                else
                {
                    Debug.LogWarning("[BuildNightseerAnimator] CombatantView introuvable sur le prefab. Bind a faire manuellement.");
                }

                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                Debug.Log($"[BuildNightseerAnimator] Prefab sauvegarde : {PrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CtrlPaths[0]);
            Debug.Log("[BuildNightseerAnimator] DONE. Idle lent, walk/cast/attack/hurt/death pretes a etre triggerees.");
        }

        /// <summary>
        /// Set des AnimationClip pour 1 .aseprite (1 stage + 1 direction).
        /// </summary>
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
            Debug.Log($"[BuildNightseerAnimator] {name} clips : {list}");
            int missing = 0;
            if (set.Idle == null) missing++;
            if (set.Walk == null) missing++;
            if (set.Attack == null) missing++;
            if (set.Cast == null) missing++;
            if (set.Hurt == null) missing++;
            if (set.Death == null) missing++;
            if (missing > 0)
            {
                Debug.LogWarning($"[BuildNightseerAnimator] {name} : {missing} clip(s) manquant(s) — fallback Idle. " +
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
            // Death : latched, pas de transition retour.
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
                Debug.LogWarning($"[BuildNightseerAnimator] Champ '{propertyName}' introuvable sur CombatantView.");
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
