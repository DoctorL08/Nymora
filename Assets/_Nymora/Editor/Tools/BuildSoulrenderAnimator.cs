using System.IO;
using Nymora.Combat.View;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Editor tool : automatise le setup des animations Soulrender (2.12).
    ///
    /// Inputs (livres par le designer) :
    ///   Assets/_Nymora/Art/Sprites/Soulrender/Base/sources/SR_animation_stage{0,1,2}_{NE,SE}.aseprite
    ///   - Pre-requis : ces .aseprite doivent etre configures (PPU 128, pivot custom
    ///     compatible iso) avec frame tags Aseprite (idle/walk/attack/cast/hurt/death).
    ///
    /// Outputs (genere automatiquement) :
    ///   Assets/_Nymora/Animations/Soulrender/SoulrenderStage{0,1,2}_{NE,SE}.controller
    ///     - 6 AnimatorController total (1 par stage x 1 par direction iso).
    ///     - Default state = clip 'idle' du .aseprite correspondant.
    ///   Update du prefab Combatant_Soulrender :
    ///     - Ajout Animator component (si absent)
    ///     - Bind CombatantView : _animator + 6 fields _stage{0,1,2}Controller{NE,SE}
    ///
    /// Les directions NW et SW ne sont PAS generees : elles sont obtenues runtime
    /// par flipX sur les controllers NE/SE (cf CombatantView.SetStageAndFacing).
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

        [MenuItem("Nymora/Setup/Build Soulrender Animator")]
        public static void Run()
        {
            int n = AsepritePaths.Length;

            // 1. Pre-flight : verifier que les .aseprite existent et trouver les clips idle.
            AnimationClip[] idleClips = new AnimationClip[n];
            for (int i = 0; i < n; i++)
            {
                if (!File.Exists(AsepritePaths[i]))
                {
                    Debug.LogError($"[BuildSoulrenderAnimator] Fichier introuvable : {AsepritePaths[i]}");
                    return;
                }
                idleClips[i] = FindIdleClip(AsepritePaths[i]);
                if (idleClips[i] == null)
                {
                    Debug.LogWarning($"[BuildSoulrenderAnimator] Aucune AnimationClip dans {AsepritePaths[i]}. " +
                        "Verifie dans l'Inspector AsepriteImporter que les frames sont taguees (idle/walk/etc.) " +
                        "et que 'Generate Animation Clips' est coche.");
                }
                else
                {
                    Debug.Log($"[BuildSoulrenderAnimator] {Path.GetFileNameWithoutExtension(AsepritePaths[i])} idle clip : {idleClips[i].name}");
                }
            }

            // 2. Cree le dossier Animations/Soulrender si absent.
            EnsureFolderRecursive(AnimFolder);

            // 3. Cree les 6 AnimatorController.
            RuntimeAnimatorController[] controllers = new RuntimeAnimatorController[n];
            for (int i = 0; i < n; i++)
            {
                if (File.Exists(CtrlPaths[i]))
                {
                    AssetDatabase.DeleteAsset(CtrlPaths[i]); // overwrite
                }
                var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPaths[i]);
                if (idleClips[i] != null)
                {
                    // AddMotion cree un state avec le clip ET le set comme default state.
                    ctrl.AddMotion(idleClips[i]);
                    Debug.Log($"[BuildSoulrenderAnimator] Cree {CtrlPaths[i]} (idle : {idleClips[i].name})");
                }
                else
                {
                    Debug.LogWarning($"[BuildSoulrenderAnimator] Cree {CtrlPaths[i]} VIDE (pas d'idle clip). Configure le .aseprite et relance.");
                }
                controllers[i] = ctrl;
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

            // Selection finale pour confort visuel.
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CtrlPaths[0]);
            Debug.Log("[BuildSoulrenderAnimator] DONE. Test en jeu : l'idle doit s'animer dans les 4 directions iso.");
        }

        /// <summary>
        /// Cherche l'AnimationClip "idle" dans les sub-assets d'un .aseprite. Fallback :
        /// premiere clip trouvee (cas ou le designer n'a pas tagge mais a quand meme genere
        /// un clip global).
        /// </summary>
        private static AnimationClip FindIdleClip(string asepritePath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(asepritePath);
            AnimationClip firstClip = null;
            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip)
                {
                    if (firstClip == null) firstClip = clip;
                    if (clip.name.ToLowerInvariant().Contains("idle")) return clip;
                }
            }
            return firstClip;
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
