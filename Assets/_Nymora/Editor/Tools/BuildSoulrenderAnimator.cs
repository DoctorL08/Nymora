using System.IO;
using Nymora.Combat.View;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Editor tool : automatise le setup des animations Soulrender (2.12 + 2.12.bis).
    ///
    /// Inputs (livres par le designer) :
    ///   Assets/_Nymora/Art/Sprites/Soulrender/Base/sources/SR_animation_stage{0,1,2}_{NE,SE}.aseprite
    ///   Frame tags Aseprite attendus dans chaque fichier : idle / walk / attack / cast / hurt / death.
    ///   Si un tag manque, fallback sur idle (warning console mais pas d'erreur).
    ///
    /// Outputs (genere automatiquement) :
    ///   Assets/_Nymora/Animations/Soulrender/SoulrenderStage{0,1,2}_{NE,SE}.controller (6 controllers)
    ///     Chaque controller a la meme state machine :
    ///       - Idle (default, speed 0.4 — respiration lente, pas hyperactif)
    ///       - Walk (speed = parametre MoveSpeed, anim selon vitesse)
    ///       - Cast (speed = parametre CastSpeed, set par la View selon SpellCategory)
    ///       - Attack (melee, speed 1)
    ///       - Hurt (degats recus, speed 1)
    ///       - Death (HP=0, latched, pas de retour)
    ///     Parametres :
    ///       - MoveSpeed (float, 0) : driver Idle <-> Walk + speed du Walk
    ///       - CastSpeed (float, 1) : module la vitesse du Cast (Survival lent, Offensive rapide)
    ///       - Cast / Attack / Hurt / Death (triggers)
    ///     Transitions :
    ///       - Idle -> Walk si MoveSpeed > 0.01
    ///       - Walk -> Idle si MoveSpeed < 0.01
    ///       - AnyState -> Cast/Attack/Hurt/Death sur trigger
    ///       - Cast/Attack/Hurt -> Idle apres exit time 0.95
    ///       - Death : latched (pas de transition retour)
    ///   Update du prefab Combatant_Soulrender :
    ///     - Add Animator si absent
    ///     - Bind CombatantView : _animator + 6 fields _stage{0,1,2}Controller{NE,SE}
    ///
    /// Les directions NW et SW sont obtenues runtime par flipX (cf CombatantView.SetStageAndFacing).
    ///
    /// Usage : Menu Nymora > Setup > Build Soulrender Animator
    /// Le tool est idempotent : si les AnimatorController existent deja, ecrases.
    /// </summary>
    public static class BuildSoulrenderAnimator
    {
        private const string AseSE0 = "Assets/_Nymora/Art/Sprites/Soulrender/Base/sources/SR_animation_stage0_SE.aseprite";
        private const string AseSE1 = "Assets/_Nymora/Art/Sprites/Soulrender/Base/sources/SR_animation_stage1_SE.aseprite";
        private const string AseSE2 = "Assets/_Nymora/Art/Sprites/Soulrender/Base/sources/SR_animation_stage2_SE.aseprite";
        private const string AseNE0 = "Assets/_Nymora/Art/Sprites/Soulrender/Base/sources/SR_animation_stage0_NE.aseprite";
        private const string AseNE1 = "Assets/_Nymora/Art/Sprites/Soulrender/Base/sources/SR_animation_stage1_NE.aseprite";
        private const string AseNE2 = "Assets/_Nymora/Art/Sprites/Soulrender/Base/sources/SR_animation_stage2_NE.aseprite";

        private const string AnimFolder = "Assets/_Nymora/Animations/Soulrender";

        // Indices : 0..2 = SE par stage, 3..5 = NE par stage.
        private static readonly string[] AsepritePaths = { AseSE0, AseSE1, AseSE2, AseNE0, AseNE1, AseNE2 };
        private static readonly string[] CtrlPaths =
        {
            AnimFolder + "/SoulrenderStage0_SE.controller",
            AnimFolder + "/SoulrenderStage1_SE.controller",
            AnimFolder + "/SoulrenderStage2_SE.controller",
            AnimFolder + "/SoulrenderStage0_NE.controller",
            AnimFolder + "/SoulrenderStage1_NE.controller",
            AnimFolder + "/SoulrenderStage2_NE.controller",
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

        private const string PrefabPath = "Assets/_Nymora/Prefabs/Combat/Combatants/Combatant_Soulrender.prefab";

        // Parametres et noms d'etats : duplique cote CombatantView (Animator.StringToHash).
        // Si tu modifies ici, modifie aussi CombatantView (Param*Hash).
        public const string ParamMoveSpeed = "MoveSpeed";
        public const string ParamCastSpeed = "CastSpeed";
        public const string ParamCast = "Cast";
        public const string ParamAttack = "Attack";
        public const string ParamHurt = "Hurt";
        public const string ParamDeath = "Death";

        // Idle speed bas : Lorenzo a constate que l'idle en speed 1 etait ultra speedy (~6 FPS x10 cycles/s).
        // 0.4 donne une vraie respiration (~2-3 cycles/s). A ajuster si trop lent.
        private const float IdleSpeed = 0.4f;

        [MenuItem("Nymora/Setup/Build Soulrender Animator")]
        public static void Run()
        {
            int n = AsepritePaths.Length;

            // 1. Pre-flight : verifier que les .aseprite existent et collecter les clips par tag.
            var clipSets = new ClipSet[n];
            for (int i = 0; i < n; i++)
            {
                if (!File.Exists(AsepritePaths[i]))
                {
                    Debug.LogError($"[BuildSoulrenderAnimator] Fichier introuvable : {AsepritePaths[i]}");
                    return;
                }
                clipSets[i] = LoadClipSet(AsepritePaths[i]);
                LogClipSetSummary(AsepritePaths[i], clipSets[i]);
            }

            // 2. Cree le dossier Animations/Soulrender si absent.
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
                Debug.Log($"[BuildSoulrenderAnimator] Cree {CtrlPaths[i]} (state machine complete)");
            }

            // 4. Update le prefab Combatant_Soulrender.
            if (!File.Exists(PrefabPath))
            {
                Debug.LogError($"[BuildSoulrenderAnimator] Prefab introuvable : {PrefabPath}");
                AssetDatabase.SaveAssets();
                return;
            }

            GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[BuildSoulrenderAnimator] Impossible de charger : {PrefabPath}");
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
                    Debug.Log("[BuildSoulrenderAnimator] Animator ajoute au prefab.");
                }
                animator.runtimeAnimatorController = controllers[0]; // default = stage 0 SE
                animator.applyRootMotion = false;

                // Fallback : si l'anim ne fire pas (tags Aseprite mal nommes / clip ciblant un autre
                // path), le SpriteRenderer affichait le placeholder rouge historique. On force le
                // sprite statique au 1er frame du .aseprite Stage 0 SE pour avoir au moins le bon
                // visuel par defaut.
                if (sr != null)
                {
                    var fallbackSprite = LoadFirstSpriteFromAseprite(AseSE0);
                    if (fallbackSprite != null)
                    {
                        sr.sprite = fallbackSprite;
                        Debug.Log($"[BuildSoulrenderAnimator] SpriteRenderer fallback sprite : {fallbackSprite.name}");
                    }
                    else
                    {
                        Debug.LogWarning("[BuildSoulrenderAnimator] Aucun Sprite extrait du .aseprite Stage 0 SE — le prefab garde son sprite courant.");
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
                    Debug.Log("[BuildSoulrenderAnimator] CombatantView : Animator + 6 controllers (NE+SE) binds.");
                }
                else
                {
                    Debug.LogWarning("[BuildSoulrenderAnimator] CombatantView introuvable sur le prefab. Bind a faire manuellement.");
                }

                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                Debug.Log($"[BuildSoulrenderAnimator] Prefab sauvegarde : {PrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CtrlPaths[0]);
            Debug.Log("[BuildSoulrenderAnimator] DONE. Idle lent, walk/cast/attack/hurt/death pretes a etre triggerees.");
        }

        /// <summary>
        /// Set des AnimationClip pour 1 .aseprite (1 stage + 1 direction).
        /// Le designer tag chaque sequence avec un nom (idle/walk/attack/cast/hurt/death). On les
        /// retrouve par substring. Si un tag manque, le champ reste null et le fallback Idle est
        /// applique au build de la state machine.
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
            // Fallback : si pas d'Idle trouve mais qu'il y a au moins 1 clip, prend le 1er comme idle.
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
            Debug.Log($"[BuildSoulrenderAnimator] {name} clips : {list}");
            int missing = 0;
            if (set.Idle == null) missing++;
            if (set.Walk == null) missing++;
            if (set.Attack == null) missing++;
            if (set.Cast == null) missing++;
            if (set.Hurt == null) missing++;
            if (set.Death == null) missing++;
            if (missing > 0)
            {
                Debug.LogWarning($"[BuildSoulrenderAnimator] {name} : {missing} clip(s) manquant(s) — fallback Idle. " +
                    "Verifie les tags Aseprite (idle/walk/attack/cast/hurt/death) et 'Generate Animation Clips'.");
            }
        }

        private static string ClipName(AnimationClip c) => c != null ? c.name : "(null)";

        /// <summary>
        /// Recupere le 1er Sprite sub-asset d'un .aseprite (utilise comme fallback pour le
        /// SpriteRenderer du prefab si l'AnimationClip ne fire pas).
        /// </summary>
        private static Sprite LoadFirstSpriteFromAseprite(string asepritePath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(asepritePath);
            foreach (var a in assets)
            {
                if (a is Sprite sprite) return sprite;
            }
            return null;
        }

        /// <summary>
        /// Construit la state machine complete d'un controller (Idle/Walk/Cast/Attack/Hurt/Death)
        /// avec les transitions necessaires. Le default state est Idle (speed 0.4).
        /// </summary>
        private static void BuildStateMachine(AnimatorController ctrl, ClipSet clips)
        {
            // Parametres.
            ctrl.AddParameter(ParamMoveSpeed, AnimatorControllerParameterType.Float);
            // CastSpeed default 1 : on doit explicitement le set car AddParameter ne prend pas de defaultFloat.
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

            // States. Fallback Idle si un clip manque (le state existe mais joue idle).
            var idle = sm.AddState("Idle");
            idle.motion = clips.Idle;
            idle.speed = IdleSpeed; // 0.4 = respiration lente

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

            // Transitions Idle <-> Walk.
            var idleToWalk = idle.AddTransition(walk);
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.1f;
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.01f, ParamMoveSpeed);

            var walkToIdle = walk.AddTransition(idle);
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.1f;
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.01f, ParamMoveSpeed);

            // AnyState -> Cast/Attack/Hurt/Death sur trigger.
            AddTriggerFromAny(sm, cast, ParamCast);
            AddTriggerFromAny(sm, attack, ParamAttack);
            AddTriggerFromAny(sm, hurt, ParamHurt);
            AddTriggerFromAny(sm, death, ParamDeath);

            // Cast/Attack/Hurt -> Idle apres exit time (le clip joue jusqu'au bout puis on revient).
            AddBackToIdle(cast, idle);
            AddBackToIdle(attack, idle);
            AddBackToIdle(hurt, idle);
            // Death : pas de transition retour. Le perso reste sur le dernier frame du clip.
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
                Debug.LogWarning($"[BuildSoulrenderAnimator] Champ '{propertyName}' introuvable sur CombatantView.");
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
