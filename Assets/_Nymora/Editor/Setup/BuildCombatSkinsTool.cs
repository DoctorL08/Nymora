using System.Collections.Generic;
using System.IO;
using Nymora.Core.Enums;
using Nymora.Core.ScriptableObjects;
using Nymora.Hub;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 5.12 — Importe les 4 skins de combat manquants (Colossar / Ghostra / Necram /
    /// Nightseer) en un seul passage, sur le modèle du skin Ashen Sovereign (Soulrender) déjà en
    /// place. Data-driven : ajouter un skin = une ligne dans <see cref="Skins"/>.
    ///
    /// Entrées (déjà copiées + .meta calibrés PPU 96 / pivot custom 0.5/0.1) :
    ///   Assets/_Nymora/Art/Cosmetics/&lt;Folder&gt;/sources/&lt;Prefix&gt;_animation_stage{0,1,2}_{NE,SE}.aseprite
    ///   Tags Aseprite attendus dans chaque fichier : idle / walk / attack / cast / hurt / death.
    ///
    /// Pour CHAQUE skin :
    ///   1. Vérifie l'import (PPU 96 — alerte si dérive, cf incident calibration 27 mai).
    ///   2. Génère 6 AnimatorController combat (stage 0/1/2 × NE/SE) — même state machine que le
    ///      Soulrender (Idle lent / Walk / Cast / Attack / Hurt / Death), dans
    ///      Assets/_Nymora/Animations/Cosmetics/&lt;Folder&gt;/.
    ///   3. Crée/peuple la CosmeticSkinDefinition (cosmeticId backend + classe + nom + 6 controllers
    ///      + frames HUB idle/walk SE/NE extraites du stage 0 + calibration scale/yOffset reprise
    ///      de la classe).
    ///   4. Exporte une vignette boutique PNG (1re frame idle SE) dans Resources/cosmetics/&lt;key&gt;.png.
    ///
    /// Puis câble TOUS les CosmeticSkinDefinition (Ashen inclus) sur HubAvatar._skinDefinitions.
    ///
    /// Ensuite, lancer :
    ///   Nymora > Setup > Build Cosmetic Skin Catalog   (résolution combat local + adverse + réseau)
    ///   Nymora > Setup > Extract Skin Combat Preview   (prévisu animée boutique)
    ///
    /// Menu : Nymora > Setup > Build Combat Skins (4 classes).
    /// </summary>
    public static class BuildCombatSkinsTool
    {
        private const float Ppu = 96f;
        private const float IdleSpeed = 0.4f; // respiration idle (cf BuildSoulrenderAnimator)

        private const string ParamMoveSpeed = "MoveSpeed";
        private const string ParamCastSpeed = "CastSpeed";
        private const string ParamCast = "Cast";
        private const string ParamAttack = "Attack";
        private const string ParamHurt = "Hurt";
        private const string ParamDeath = "Death";

        private const string ArtRoot = "Assets/_Nymora/Art/Cosmetics";
        private const string AnimRoot = "Assets/_Nymora/Animations/Cosmetics";
        private const string SkinDir = "Assets/_Nymora/ScriptableObjects/Cosmetics";
        private const string ResourcesCosmeticsDir = "Assets/_Nymora/Resources/cosmetics";
        private const string ClassDir = "Assets/_Nymora/ScriptableObjects/Classes";
        private const string HubAvatarPrefabPath = "Assets/_Nymora/Prefabs/Hub/HubAvatar.prefab";
        private const string BackendSettingsPath = "Assets/_Nymora/Settings/NymoraBackendSettings.asset";

        private class SkinEntry
        {
            public string Folder;      // dossier projet (PascalCase), ex "ObsidianTitan"
            public string Prefix;      // préfixe des .aseprite, ex "OBSIDIANTITAN"
            public string CosmeticId;  // id backend (catalog.ts), ex "skin_colossar_obsidian_titan"
            public NymoraClass Class;
            public string DisplayName;
            public string VignetteKey; // nom du PNG vignette dans Resources/cosmetics/
        }

        private static readonly SkinEntry[] Skins =
        {
            new SkinEntry { Folder = "ObsidianTitan", Prefix = "OBSIDIANTITAN",  CosmeticId = "skin_colossar_obsidian_titan", Class = NymoraClass.Colossar,  DisplayName = "Obsidian Titan", VignetteKey = "obsidian_titan" },
            new SkinEntry { Folder = "PaleRevenant",  Prefix = "PALE_REVENANT",  CosmeticId = "skin_ghostra_pale_revenant",   Class = NymoraClass.Ghostra,   DisplayName = "Pale Revenant",  VignetteKey = "pale_revenant" },
            new SkinEntry { Folder = "PlagueApostle", Prefix = "PLAGUE_APOSTLE", CosmeticId = "skin_necram_plague_apostle",   Class = NymoraClass.Necram,    DisplayName = "Plague Apostle", VignetteKey = "plague_apostle" },
            new SkinEntry { Folder = "VoidOracle",    Prefix = "VOIDORACLE",     CosmeticId = "skin_nightseer_void_oracle",   Class = NymoraClass.Nightseer, DisplayName = "Void Oracle",    VignetteKey = "void_oracle" },
        };

        // Ordre des 6 fichiers : 0..2 = SE par stage, 3..5 = NE par stage (comme CombatantView bind).
        private static readonly string[] FieldNames =
        {
            "Stage0ControllerSE", "Stage1ControllerSE", "Stage2ControllerSE",
            "Stage0ControllerNE", "Stage1ControllerNE", "Stage2ControllerNE",
        };

        [MenuItem("Nymora/Setup/Build Combat Skins (4 classes)", priority = 48)]
        public static void Run()
        {
            var report = new List<string>();
            EnsureFolderRecursive(SkinDir);
            EnsureFolderRecursive(ResourcesCosmeticsDir);

            int ok = 0;
            foreach (var skin in Skins)
            {
                report.Add($"── {skin.DisplayName} ({skin.Class}) ──");
                if (BuildOne(skin, report)) ok++;
                report.Add("");
            }

            WireHubAvatarPrefab(report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BuildCombatSkins]\n" + string.Join("\n", report));
            EditorUtility.DisplayDialog("Build Combat Skins",
                $"{ok}/{Skins.Length} skin(s) traité(s).\n\n" + string.Join("\n", report) +
                "\nEnchaîne ensuite :\n• Build Cosmetic Skin Catalog\n• Extract Skin Combat Preview", "OK");
        }

        private static bool BuildOne(SkinEntry skin, List<string> report)
        {
            string src = $"{ArtRoot}/{skin.Folder}/sources";
            string[] ase =
            {
                $"{src}/{skin.Prefix}_animation_stage0_SE.aseprite",
                $"{src}/{skin.Prefix}_animation_stage1_SE.aseprite",
                $"{src}/{skin.Prefix}_animation_stage2_SE.aseprite",
                $"{src}/{skin.Prefix}_animation_stage0_NE.aseprite",
                $"{src}/{skin.Prefix}_animation_stage1_NE.aseprite",
                $"{src}/{skin.Prefix}_animation_stage2_NE.aseprite",
            };

            for (int i = 0; i < ase.Length; i++)
            {
                if (!File.Exists(ase[i]))
                {
                    report.Add($"⚠ fichier introuvable : {ase[i]} — skin ignoré");
                    return false;
                }
            }

            int ppuDrift = VerifyImportPpu(ase);
            report.Add(ppuDrift > 0
                ? $"⚠ {ppuDrift}/6 fichier(s) PPU != {Ppu} — vérifie les .meta (skin mal calé en Y sinon)"
                : $"import OK (PPU {Ppu})");

            // Collecte des clips par fichier.
            var clipSets = new ClipSet[ase.Length];
            for (int i = 0; i < ase.Length; i++)
                clipSets[i] = LoadClipSet(ase[i]);

            // Génère les 6 controllers.
            string animFolder = $"{AnimRoot}/{skin.Folder}";
            EnsureFolderRecursive(animFolder);
            var controllers = new RuntimeAnimatorController[ase.Length];
            string[] dirTag = { "SE", "SE", "SE", "NE", "NE", "NE" };
            int[] stageTag = { 0, 1, 2, 0, 1, 2 };
            for (int i = 0; i < ase.Length; i++)
            {
                string ctrlPath = $"{animFolder}/{skin.Folder}Stage{stageTag[i]}_{dirTag[i]}.controller";
                if (File.Exists(ctrlPath)) AssetDatabase.DeleteAsset(ctrlPath);
                var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                BuildStateMachine(ctrl, clipSets[i]);
                controllers[i] = ctrl;
            }
            report.Add($"6 AnimatorController → {animFolder}");

            // Crée/peuple la CosmeticSkinDefinition.
            string defPath = $"{SkinDir}/{skin.Folder}.asset";
            var def = AssetDatabase.LoadAssetAtPath<CosmeticSkinDefinition>(defPath);
            bool created = def == null;
            if (created)
            {
                def = ScriptableObject.CreateInstance<CosmeticSkinDefinition>();
                AssetDatabase.CreateAsset(def, defPath);
            }
            def.CosmeticId = skin.CosmeticId;
            def.ClassId = skin.Class;
            def.DisplayName = skin.DisplayName;

            // Frames HUB : idle/walk SE depuis stage0 SE (clipSets[0]), idle/walk NE depuis stage0 NE (clipSets[3]).
            def.IdleFrames = ExtractSprites(clipSets[0].Idle, out _);
            def.WalkFrames = ExtractSprites(clipSets[0].Walk, out _);
            def.IdleFramesNE = ExtractSprites(clipSets[3].Idle, out _);
            def.WalkFramesNE = ExtractSprites(clipSets[3].Walk, out _);
            def.IdleFps = 8f;
            def.WalkFps = 12f;

            // Calibration hub reprise de la classe (point de départ — ajustable sur l'asset).
            var classDef = AssetDatabase.LoadAssetAtPath<NymoraClassDefinition>($"{ClassDir}/{skin.Class}.asset");
            if (classDef != null)
            {
                def.HubVisualScale = classDef.HubVisualScale > 0f ? classDef.HubVisualScale : 1f;
                def.HubVisualYOffset = classDef.HubVisualYOffset;
            }

            // Controllers combat via SerializedObject (champs StageXControllerYY).
            var so = new SerializedObject(def);
            for (int i = 0; i < FieldNames.Length; i++)
            {
                var prop = so.FindProperty(FieldNames[i]);
                if (prop != null) prop.objectReferenceValue = controllers[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);

            report.Add(created ? "CosmeticSkinDefinition créée" : "CosmeticSkinDefinition mise à jour");
            report.Add($"hub : idle {def.IdleFrames.Length}/{def.IdleFramesNE.Length} · walk {def.WalkFrames.Length}/{def.WalkFramesNE.Length}");

            // Vignette boutique (1re frame idle SE).
            Sprite first = def.IdleFrames != null && def.IdleFrames.Length > 0 ? def.IdleFrames[0] : null;
            if (ExportVignette(first, skin.VignetteKey, report))
                report.Add($"vignette → Resources/cosmetics/{skin.VignetteKey}.png");

            return true;
        }

        // ====================================================================
        // Vignette boutique : crop de la 1re frame idle SE en PNG. Toggle isReadable
        // le temps de lire les pixels (puis restaure), pour ne pas alourdir le runtime.
        // ====================================================================
        private static bool ExportVignette(Sprite frame, string vignetteKey, List<string> report)
        {
            if (frame == null) { report.Add("⚠ vignette : pas de frame idle SE"); return false; }
            // L'importer Aseprite n'est pas un TextureImporter -> on lit les pixels via copie GPU
            // (ReadbackTexture), donc pas besoin de rendre la texture source readable.
            try
            {
                Texture2D readable = ReadbackTexture(frame.texture);
                var r = frame.textureRect;
                int x = Mathf.Clamp(Mathf.RoundToInt(r.x), 0, readable.width - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt(r.y), 0, readable.height - 1);
                int w = Mathf.Clamp(Mathf.RoundToInt(r.width), 1, readable.width - x);
                int h = Mathf.Clamp(Mathf.RoundToInt(r.height), 1, readable.height - y);

                var crop = new Texture2D(w, h, TextureFormat.RGBA32, false);
                crop.SetPixels(readable.GetPixels(x, y, w, h));
                crop.Apply();
                byte[] png = crop.EncodeToPNG();
                Object.DestroyImmediate(crop);
                Object.DestroyImmediate(readable);

                string assetPath = $"{ResourcesCosmeticsDir}/{vignetteKey}.png";
                File.WriteAllBytes(assetPath, png);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                ConfigureVignetteImport(assetPath);
                return true;
            }
            catch (System.Exception e)
            {
                report.Add($"⚠ vignette échouée ({vignetteKey}) : {e.Message}");
                return false;
            }
        }

        /// <summary>Copie GPU -> Texture2D CPU lisible (marche même si la source n'est pas readable).</summary>
        private static Texture2D ReadbackTexture(Texture src)
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            var prevActive = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        private static void ConfigureVignetteImport(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter ti) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }

        private static int VerifyImportPpu(string[] asepritePaths)
        {
            int drift = 0;
            foreach (var path in asepritePaths)
            {
                float ppu = -1f;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (a is Sprite sp) { ppu = sp.pixelsPerUnit; break; }
                if (ppu > 0f && !Mathf.Approximately(ppu, Ppu)) drift++;
            }
            return drift;
        }

        // ====================================================================
        // State machine (dupliquée de BuildAshenSovereignCombatAnimator / BuildSoulrenderAnimator).
        // ====================================================================
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
                foreach (var a in assets)
                    if (a is AnimationClip clip) { set.Idle = clip; break; }
            return set;
        }

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

        // Extrait les sprites (binding m_Sprite avec le plus de keyframes) d'un clip.
        private static Sprite[] ExtractSprites(AnimationClip clip, out float fps)
        {
            fps = 8f;
            if (clip == null) return new Sprite[0];
            ObjectReferenceKeyframe[] best = null;
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.propertyName != "m_Sprite") continue;
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (keys != null && (best == null || keys.Length > best.Length)) best = keys;
            }
            if (best == null || best.Length == 0) return new Sprite[0];

            var sprites = new List<Sprite>(best.Length);
            foreach (var k in best)
                if (k.value is Sprite s && s != null) sprites.Add(s);

            if (best.Length >= 2)
            {
                float dt = best[1].time - best[0].time;
                if (dt > 0.0001f) fps = 1f / dt;
            }
            else if (clip.frameRate > 0f) fps = clip.frameRate;

            return sprites.ToArray();
        }

        // Câble TOUS les CosmeticSkinDefinition du projet (Ashen + 4 nouveaux) sur HubAvatar.
        private static void WireHubAvatarPrefab(List<string> report)
        {
            var defs = new List<CosmeticSkinDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:CosmeticSkinDefinition"))
            {
                var def = AssetDatabase.LoadAssetAtPath<CosmeticSkinDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) defs.Add(def);
            }
            defs.Sort((a, b) => string.CompareOrdinal(a.CosmeticId, b.CosmeticId));

            var root = PrefabUtility.LoadPrefabContents(HubAvatarPrefabPath);
            if (root == null) { report.Add("⚠ HubAvatar.prefab introuvable"); return; }
            try
            {
                var avatar = root.GetComponentInChildren<HubAvatar>(true);
                if (avatar == null) { report.Add("⚠ HubAvatar component absent du prefab"); return; }

                var so = new SerializedObject(avatar);
                var skinsProp = so.FindProperty("_skinDefinitions");
                if (skinsProp != null)
                {
                    skinsProp.arraySize = defs.Count;
                    for (int i = 0; i < defs.Count; i++)
                        skinsProp.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
                    report.Add($"HubAvatar._skinDefinitions câblé : {defs.Count} skin(s)");
                }
                var settingsProp = so.FindProperty("_backendSettings");
                if (settingsProp != null && settingsProp.objectReferenceValue == null)
                {
                    var settings = AssetDatabase.LoadMainAssetAtPath(BackendSettingsPath);
                    if (settings != null) settingsProp.objectReferenceValue = settings;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, HubAvatarPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
