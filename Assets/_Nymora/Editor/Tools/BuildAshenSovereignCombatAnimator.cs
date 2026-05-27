using System.Collections.Generic;
using System.IO;
using Nymora.Core.ScriptableObjects;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Brique 5.10 (A1) — Génère la variante COMBAT du skin Ashen Sovereign (Soulrender).
    ///
    /// Inputs (copiés du livrable Kyami dans le projet) :
    ///   Assets/_Nymora/Art/Cosmetics/AshenSovereign/sources/Ashen_stage{0,1,2}_{NE,SE}.aseprite
    ///   Frame tags Aseprite attendus : idle / walk / attack / cast / hurt / death (substring match,
    ///   fallback idle si un tag manque — mêmes règles que BuildSoulrenderAnimator).
    ///
    /// Étapes :
    ///   1. Aligne les réglages d'import Aseprite sur ceux du Soulrender de base (PPU 96, pivot
    ///      custom 0.5/0.1, Point filter, Animated + Generate Clips) → le skin combat se cale
    ///      pixel-perfect sur la classe. Idempotent (SaveAndReimport).
    ///   2. Génère 6 AnimatorController (stage 0/1/2 × NE/SE) avec la MÊME state machine que le
    ///      Soulrender (Idle lent / Walk / Cast / Attack / Hurt / Death + transitions) dans
    ///      Assets/_Nymora/Animations/Cosmetics/AshenSovereign/.
    ///   3. Câble les 6 controllers + offsets sur l'asset CosmeticSkinDefinition AshenSovereign
    ///      (champs StageXControllerYY). CombatantView les piochera quand le skin est équipé (A2/A3).
    ///
    /// NB : les directions NW/SW sont obtenues runtime par flipX (cf CombatantView.SetStageAndFacing).
    /// La logique state machine est volontairement dupliquée de BuildSoulrenderAnimator (tool
    /// éditeur autonome) pour ne pas risquer de toucher au pipeline classe qui marche.
    ///
    /// Menu : Nymora > Setup > Build Ashen Sovereign Combat Animator.
    /// </summary>
    public static class BuildAshenSovereignCombatAnimator
    {
        private const string SrcDir = "Assets/_Nymora/Art/Cosmetics/AshenSovereign/sources";
        private const string AnimFolder = "Assets/_Nymora/Animations/Cosmetics/AshenSovereign";
        private const string SkinAssetPath = "Assets/_Nymora/ScriptableObjects/Cosmetics/AshenSovereign.asset";

        // Calibration d'import reprise du Soulrender (cf SR_animation_stage0_SE.aseprite.meta) :
        // PPU 96 + pivot custom (0.5, 0.1) + Point. Pilotée par les .meta des .aseprite (méthode
        // Unity normale, versionnée) ; ce const sert juste à la vérification d'import.
        private const float Ppu = 96f;

        // Idle lent : respiration (cf BuildSoulrenderAnimator.IdleSpeed).
        private const float IdleSpeed = 0.4f;

        private const string ParamMoveSpeed = "MoveSpeed";
        private const string ParamCastSpeed = "CastSpeed";
        private const string ParamCast = "Cast";
        private const string ParamAttack = "Attack";
        private const string ParamHurt = "Hurt";
        private const string ParamDeath = "Death";

        // Indices : 0..2 = SE par stage, 3..5 = NE par stage (même ordre que CombatantView bind).
        private static readonly string[] AsepritePaths =
        {
            SrcDir + "/Ashen_stage0_SE.aseprite",
            SrcDir + "/Ashen_stage1_SE.aseprite",
            SrcDir + "/Ashen_stage2_SE.aseprite",
            SrcDir + "/Ashen_stage0_NE.aseprite",
            SrcDir + "/Ashen_stage1_NE.aseprite",
            SrcDir + "/Ashen_stage2_NE.aseprite",
        };
        private static readonly string[] CtrlPaths =
        {
            AnimFolder + "/AshenStage0_SE.controller",
            AnimFolder + "/AshenStage1_SE.controller",
            AnimFolder + "/AshenStage2_SE.controller",
            AnimFolder + "/AshenStage0_NE.controller",
            AnimFolder + "/AshenStage1_NE.controller",
            AnimFolder + "/AshenStage2_NE.controller",
        };
        // Champs cible sur CosmeticSkinDefinition (même ordre).
        private static readonly string[] SkinFieldNames =
        {
            "Stage0ControllerSE",
            "Stage1ControllerSE",
            "Stage2ControllerSE",
            "Stage0ControllerNE",
            "Stage1ControllerNE",
            "Stage2ControllerNE",
        };

        [MenuItem("Nymora/Setup/Build Ashen Sovereign Combat Animator", priority = 46)]
        public static void Run()
        {
            int n = AsepritePaths.Length;
            var actions = new List<string>();

            // 1. Pré-flight + alignement des réglages d'import.
            for (int i = 0; i < n; i++)
            {
                if (!File.Exists(AsepritePaths[i]))
                {
                    EditorUtility.DisplayDialog("Build Ashen Combat",
                        $"Fichier introuvable :\n{AsepritePaths[i]}\n\nAs-tu copié les .aseprite dans {SrcDir} ?", "OK");
                    return;
                }
            }
            // Vérifie que l'import (piloté par les .meta, alignés sur le Soulrender : PPU 96 +
            // pivot custom 0.5/0.1) est cohérent. Si un sprite revient en PPU != 96, le .meta a
            // été écrasé/regénéré aux defaults → le skin sera mal calé en Y (cf incident 27 mai).
            int ppuDrift = VerifyImportPpu();
            if (ppuDrift > 0)
                actions.Add($"⚠ {ppuDrift}/{n} fichier(s) avec PPU != {Ppu} — vérifie les .meta (skin mal calé en Y sinon)");
            else
                actions.Add($"import vérifié : PPU {Ppu} OK sur les 6 fichiers");

            // 2. Collecte des clips par tag.
            var clipSets = new ClipSet[n];
            for (int i = 0; i < n; i++)
            {
                clipSets[i] = LoadClipSet(AsepritePaths[i]);
                LogClipSetSummary(AsepritePaths[i], clipSets[i]);
            }

            // 3. Génère les 6 controllers.
            EnsureFolderRecursive(AnimFolder);
            var controllers = new RuntimeAnimatorController[n];
            for (int i = 0; i < n; i++)
            {
                if (File.Exists(CtrlPaths[i])) AssetDatabase.DeleteAsset(CtrlPaths[i]); // overwrite
                var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPaths[i]);
                BuildStateMachine(ctrl, clipSets[i]);
                controllers[i] = ctrl;
            }
            actions.Add($"6 AnimatorController générés dans {AnimFolder}");

            // 4. Câble les controllers sur l'asset skin.
            var skin = AssetDatabase.LoadAssetAtPath<CosmeticSkinDefinition>(SkinAssetPath);
            if (skin == null)
            {
                EditorUtility.DisplayDialog("Build Ashen Combat",
                    $"CosmeticSkinDefinition introuvable :\n{SkinAssetPath}\n\nLance d'abord 'Patch Ashen Sovereign Skin' (crée l'asset hub).", "OK");
                return;
            }
            var so = new SerializedObject(skin);
            for (int i = 0; i < n; i++)
            {
                var prop = so.FindProperty(SkinFieldNames[i]);
                if (prop != null) prop.objectReferenceValue = controllers[i];
                else Debug.LogWarning($"[BuildAshenCombat] Champ '{SkinFieldNames[i]}' introuvable sur CosmeticSkinDefinition.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            actions.Add("CosmeticSkinDefinition AshenSovereign : 6 controllers combat câblés");

            Debug.Log("[BuildAshenCombat] " + string.Join("\n", actions));
            Selection.activeObject = skin;
            EditorUtility.DisplayDialog("Build Ashen Combat",
                string.Join("\n", actions) +
                "\n\nProchaine étape (A2) : CombatantView swappe ces controllers quand le skin est équipé.", "OK");
        }

        /// <summary>
        /// Vérifie que les 6 .aseprite sont importés au bon PPU (96, aligné Soulrender). Retourne
        /// le nombre de fichiers en dérive. La calibration réelle vit dans les .meta (méthode Unity
        /// normale, versionnée) — ce tool ne fait que la contrôler, pas la forcer.
        /// </summary>
        private static int VerifyImportPpu()
        {
            int drift = 0;
            foreach (var path in AsepritePaths)
            {
                float ppu = -1f;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (a is Sprite sp) { ppu = sp.pixelsPerUnit; break; }
                }
                if (ppu > 0f && !Mathf.Approximately(ppu, Ppu))
                {
                    Debug.LogWarning($"[BuildAshenCombat] {Path.GetFileName(path)} importé en PPU {ppu} (attendu {Ppu}). Le .meta a été régénéré aux defaults ?");
                    drift++;
                }
            }
            return drift;
        }

        // ===================================================================
        // State machine (dupliqué de BuildSoulrenderAnimator — voir ce fichier
        // pour les commentaires détaillés). Si tu modifies l'un, modifie l'autre.
        // ===================================================================
        private struct ClipSet
        {
            public AnimationClip Idle, Walk, Attack, Cast, Hurt, Death;
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
            int missing = 0;
            if (set.Idle == null) missing++;
            if (set.Walk == null) missing++;
            if (set.Attack == null) missing++;
            if (set.Cast == null) missing++;
            if (set.Hurt == null) missing++;
            if (set.Death == null) missing++;
            Debug.Log($"[BuildAshenCombat] {name} : idle={Has(set.Idle)} walk={Has(set.Walk)} attack={Has(set.Attack)} cast={Has(set.Cast)} hurt={Has(set.Hurt)} death={Has(set.Death)}");
            if (missing > 0)
                Debug.LogWarning($"[BuildAshenCombat] {name} : {missing} clip(s) manquant(s) — fallback Idle. Vérifie les tags Aseprite.");
        }

        private static string Has(AnimationClip c) => c != null ? c.name : "(null)";

        private static void BuildStateMachine(AnimatorController ctrl, ClipSet clips)
        {
            ctrl.AddParameter(ParamMoveSpeed, AnimatorControllerParameterType.Float);
            ctrl.AddParameter(ParamCastSpeed, AnimatorControllerParameterType.Float);
            var parameters = ctrl.parameters;
            for (int i = 0; i < parameters.Length; i++)
                if (parameters[i].name == ParamCastSpeed) parameters[i].defaultFloat = 1f;
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
            // Death : latché, pas de retour.
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
