using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Clone les 2 AnimatorController Soulrender stage0 (NE/SE) vers Nightseer en
    /// rebindant chaque AnimatorState sur le sub-asset AnimationClip equivalent du
    /// .aseprite Nightseer (matching par nom : state "Idle" -> clip contenant "idle").
    ///
    /// Met aussi a jour Combatant_Nightseer.prefab : ajoute un Animator si absent,
    /// bind les 6 controllers (stage0/1/2 NE/SE — stage1/2 = fallback stage0 tant
    /// que le designer n'a pas livre les autres stages), bind le _animator field
    /// du CombatantView.
    ///
    /// Pre-requis :
    ///   1. .aseprite Nightseer importes (sources/NS_animation_stage0_*.aseprite)
    ///   2. Tune Aseprite Character Sprites lance (pivot/PPU OK)
    /// </summary>
    public static class CloneSoulrenderControllersToNightseer
    {
        private const string SoulrenderCtrlNE = "Assets/_Nymora/Animations/Soulrender/SoulrenderStage0_NE.controller";
        private const string SoulrenderCtrlSE = "Assets/_Nymora/Animations/Soulrender/SoulrenderStage0_SE.controller";
        private const string NightseerCtrlNE = "Assets/_Nymora/Animations/Nightseer/NightseerStage0_NE.controller";
        private const string NightseerCtrlSE = "Assets/_Nymora/Animations/Nightseer/NightseerStage0_SE.controller";
        private const string NightseerAseNE = "Assets/_Nymora/Art/Sprites/Nightseer/Base/sources/NS_animation_stage0_NE.aseprite";
        private const string NightseerAseSE = "Assets/_Nymora/Art/Sprites/Nightseer/Base/sources/NS_animation_stage0_SE.aseprite";
        private const string NightseerFolder = "Assets/_Nymora/Animations/Nightseer";
        private const string NightseerPrefabPath = "Assets/_Nymora/Prefabs/Combat/Combatants/Combatant_Nightseer.prefab";

        [MenuItem("Nymora/Setup/Clone Soulrender Controllers to Nightseer")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(NightseerFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Nymora/Animations", "Nightseer");
                Debug.Log($"[Nymora.CloneCtrls] Cree dossier {NightseerFolder}");
            }

            CloneAndRebind(SoulrenderCtrlNE, NightseerCtrlNE, NightseerAseNE);
            CloneAndRebind(SoulrenderCtrlSE, NightseerCtrlSE, NightseerAseSE);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BindControllersToNightseerPrefab();

            Debug.Log("[Nymora.CloneCtrls] Termine. Combatant_Nightseer.prefab pret pour le Play Mode.");
        }

        private static void CloneAndRebind(string srcCtrl, string dstCtrl, string nsAsePath)
        {
            // Step 1 : copy le .controller (skip si deja existant pour ne pas ecraser un re-binding manuel).
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(dstCtrl) == null)
            {
                if (!AssetDatabase.CopyAsset(srcCtrl, dstCtrl))
                {
                    Debug.LogError($"[Nymora.CloneCtrls] CopyAsset echec : {srcCtrl} -> {dstCtrl}");
                    return;
                }
                Debug.Log($"[Nymora.CloneCtrls] Clone : {srcCtrl} -> {dstCtrl}");
            }
            else
            {
                Debug.Log($"[Nymora.CloneCtrls] {dstCtrl} existe deja, on rebind seulement les motions.");
            }

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(dstCtrl);
            if (ctrl == null)
            {
                Debug.LogError($"[Nymora.CloneCtrls] Load AnimatorController echec : {dstCtrl}");
                return;
            }

            // Step 2 : index tous les AnimationClips sub-assets du .aseprite Nightseer.
            var clips = AssetDatabase.LoadAllAssetsAtPath(nsAsePath);
            var clipByLowerName = new Dictionary<string, AnimationClip>();
            foreach (var asset in clips)
            {
                if (asset is AnimationClip clip)
                {
                    string key = clip.name.ToLowerInvariant();
                    if (!clipByLowerName.ContainsKey(key))
                    {
                        clipByLowerName[key] = clip;
                    }
                }
            }
            if (clipByLowerName.Count == 0)
            {
                Debug.LogError($"[Nymora.CloneCtrls] Aucun AnimationClip dans {nsAsePath}. Lance d'abord 'Nymora/Setup/Tune Aseprite Character Sprites' et verifie que le .aseprite a des tags d'animation.");
                return;
            }
            Debug.Log($"[Nymora.CloneCtrls] {clipByLowerName.Count} clips trouves dans {nsAsePath} : [{string.Join(", ", clipByLowerName.Keys)}]");

            // Step 3 : rebind chaque AnimatorState sur le clip dont le nom contient le state name.
            foreach (var layer in ctrl.layers)
            {
                RebindStatesRecursive(layer.stateMachine, clipByLowerName, dstCtrl);
            }

            EditorUtility.SetDirty(ctrl);
        }

        private static void RebindStatesRecursive(AnimatorStateMachine sm, Dictionary<string, AnimationClip> clipByLowerName, string ctrlPath)
        {
            foreach (var childState in sm.states)
            {
                var state = childState.state;
                string stateName = state.name.ToLowerInvariant();
                AnimationClip match = FindMatchingClip(stateName, clipByLowerName);
                if (match != null)
                {
                    state.motion = match;
                    Debug.Log($"[Nymora.CloneCtrls] {ctrlPath} : state '{state.name}' rebind -> '{match.name}'");
                }
                else
                {
                    Debug.LogWarning($"[Nymora.CloneCtrls] {ctrlPath} : aucun clip Nightseer match pour state '{state.name}' (motion Soulrender conservee, drag manuel necessaire).");
                }
            }
            foreach (var childSM in sm.stateMachines)
            {
                RebindStatesRecursive(childSM.stateMachine, clipByLowerName, ctrlPath);
            }
        }

        private static AnimationClip FindMatchingClip(string stateName, Dictionary<string, AnimationClip> clipByLowerName)
        {
            // Match exact d'abord, fallback Contains.
            if (clipByLowerName.TryGetValue(stateName, out var exact)) return exact;
            foreach (var pair in clipByLowerName)
            {
                if (pair.Key.Contains(stateName)) return pair.Value;
            }
            return null;
        }

        // ====================================================================
        // Step 4 : update Combatant_Nightseer.prefab
        // ====================================================================

        private static void BindControllersToNightseerPrefab()
        {
            var ctrlNE = AssetDatabase.LoadAssetAtPath<AnimatorController>(NightseerCtrlNE);
            var ctrlSE = AssetDatabase.LoadAssetAtPath<AnimatorController>(NightseerCtrlSE);
            if (ctrlNE == null || ctrlSE == null)
            {
                Debug.LogError($"[Nymora.CloneCtrls] Controllers Nightseer introuvables apres clone : NE={ctrlNE} SE={ctrlSE}. Skip bind prefab.");
                return;
            }

            var prefabContents = PrefabUtility.LoadPrefabContents(NightseerPrefabPath);
            if (prefabContents == null)
            {
                Debug.LogError($"[Nymora.CloneCtrls] LoadPrefabContents echec : {NightseerPrefabPath}");
                return;
            }

            try
            {
                // Add Animator component si absent (le prefab Nightseer est un stub par defaut).
                var animator = prefabContents.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = prefabContents.AddComponent<Animator>();
                    Debug.Log("[Nymora.CloneCtrls] Animator component ajoute sur Combatant_Nightseer.");
                }
                animator.runtimeAnimatorController = ctrlSE; // default convention SE (cf Soulrender prefab)
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // Extrait le 1er sprite du tag "idle" pour chaque direction. Sert de fallback statique
                // (SpriteRenderer.sprite + Stage Sprites) si l'Animator est desactive ou pas encore play.
                Sprite idleSE = GetIdleFirstSprite(NightseerAseSE);
                Sprite idleNE = GetIdleFirstSprite(NightseerAseNE);
                if (idleSE == null || idleNE == null)
                {
                    Debug.LogWarning($"[Nymora.CloneCtrls] Idle sprite NE/SE introuvable (NE={idleNE} SE={idleSE}). Le placeholder restera affiche en Inspector.");
                }

                // Set le sprite par defaut du SpriteRenderer (= idle SE frame 0).
                var spriteRenderer = prefabContents.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null && idleSE != null)
                {
                    spriteRenderer.sprite = idleSE;
                    Debug.Log($"[Nymora.CloneCtrls] SpriteRenderer.sprite = {idleSE.name}");
                }

                // Find CombatantView component (par nom de type pour eviter la dep stricte sur Nymora.Combat).
                MonoBehaviour view = null;
                foreach (var comp in prefabContents.GetComponents<MonoBehaviour>())
                {
                    if (comp != null && comp.GetType().Name == "CombatantView")
                    {
                        view = comp;
                        break;
                    }
                }
                if (view == null)
                {
                    Debug.LogError($"[Nymora.CloneCtrls] CombatantView introuvable sur {NightseerPrefabPath}");
                    return;
                }

                // Bind via SerializedObject (private fields [SerializeField] OK).
                var serObj = new SerializedObject(view);
                BindObjectField(serObj, "_animator", animator);
                BindObjectField(serObj, "_sprite", spriteRenderer); // au cas ou
                // Controllers (anims animees, prioritaires sur sprites statiques)
                BindObjectField(serObj, "_stage0ControllerSE", ctrlSE);
                BindObjectField(serObj, "_stage1ControllerSE", ctrlSE); // fallback stage0 en attendant designer
                BindObjectField(serObj, "_stage2ControllerSE", ctrlSE);
                BindObjectField(serObj, "_stage0ControllerNE", ctrlNE);
                BindObjectField(serObj, "_stage1ControllerNE", ctrlNE);
                BindObjectField(serObj, "_stage2ControllerNE", ctrlNE);
                // Stages statiques (fallback si Animator absent — bind aussi pour Inspector clean)
                if (idleSE != null)
                {
                    BindObjectField(serObj, "_stage0SpriteSE", idleSE);
                    BindObjectField(serObj, "_stage1SpriteSE", idleSE);
                    BindObjectField(serObj, "_stage2SpriteSE", idleSE);
                }
                if (idleNE != null)
                {
                    BindObjectField(serObj, "_stage0SpriteNE", idleNE);
                    BindObjectField(serObj, "_stage1SpriteNE", idleNE);
                    BindObjectField(serObj, "_stage2SpriteNE", idleNE);
                }
                serObj.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefabContents, NightseerPrefabPath);
                Debug.Log($"[Nymora.CloneCtrls] Prefab {NightseerPrefabPath} mis a jour : Animator + 6 controllers + sprite par defaut + 6 stage sprites.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private static void BindObjectField(SerializedObject serObj, string fieldName, Object value)
        {
            var prop = serObj.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[Nymora.CloneCtrls] Field '{fieldName}' introuvable sur CombatantView (skip).");
                return;
            }
            prop.objectReferenceValue = value;
        }

        /// <summary>
        /// Cherche le 1er sprite du tag "idle" dans un .aseprite (sub-asset Sprite).
        /// Tri par nom pour avoir le frame 0 en premier (convention Aseprite : "idle_0", "idle_1"...).
        /// Fallback : si pas de sprite "idle", retourne le premier sprite trouve.
        /// </summary>
        private static Sprite GetIdleFirstSprite(string asePath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(asePath);
            Sprite firstIdle = null;
            string firstIdleName = null;
            Sprite firstAny = null;
            foreach (var asset in assets)
            {
                if (!(asset is Sprite spr)) continue;
                if (firstAny == null) firstAny = spr;
                if (spr.name.ToLowerInvariant().Contains("idle"))
                {
                    if (firstIdleName == null
                        || string.Compare(spr.name, firstIdleName, System.StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        firstIdle = spr;
                        firstIdleName = spr.name;
                    }
                }
            }
            return firstIdle ?? firstAny;
        }
    }
}
