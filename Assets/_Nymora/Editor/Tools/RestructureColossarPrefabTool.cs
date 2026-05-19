using Nymora.Combat.View;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Restructure le prefab Combatant_Colossar en parent + enfant "Visual" pour pouvoir
    /// decaler le sprite verticalement par stage (pivot Aseprite stage1/2 non standardise
    /// avec stage0 — workaround runtime Y offset, cf incident 18 mai).
    ///
    /// Mirror exact du RestructureNecram/GhostraPrefabTool : SpriteRenderer + Animator
    /// migrent du root vers le child "Visual". CombatantView reste sur le root et garde
    /// ses refs _sprite / _animator vers le child via SerializedObject re-bind.
    ///
    /// DefaultYOffset = 0 car stage0 est OK visuellement (referrence). L'offset pour
    /// stage1/2 se tune via les SerializeField _stage1VisualYOffset/_stage2VisualYOffset
    /// de CombatantView dans l'Inspector.
    /// Idempotent : si le child "Visual" existe deja, no-op.
    /// </summary>
    public static class RestructureColossarPrefabTool
    {
        private const string PrefabPath = "Assets/_Nymora/Prefabs/Combat/Combatants/Combatant_Colossar.prefab";
        private const string VisualChildName = "Visual";
        private const float DefaultYOffset = 0f;

        [MenuItem("Nymora/Setup/Restructure Colossar Prefab (Sprite Y Offset)")]
        public static void Run()
        {
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[RestructureColossar] Prefab introuvable : {PrefabPath}");
                return;
            }

            try
            {
                var existingChild = prefab.transform.Find(VisualChildName);
                if (existingChild != null)
                {
                    Debug.Log($"[RestructureColossar] Child '{VisualChildName}' deja present. Y={existingChild.localPosition.y}. " +
                              $"Ajuste _stage1VisualYOffset / _stage2VisualYOffset dans l'Inspector du component CombatantView.");
                    PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                    return;
                }

                var srRoot = prefab.GetComponent<SpriteRenderer>();
                var animRoot = prefab.GetComponent<Animator>();
                var view = prefab.GetComponent<CombatantView>();

                if (srRoot == null || animRoot == null || view == null)
                {
                    Debug.LogError("[RestructureColossar] Components manquants sur le prefab racine (SpriteRenderer / Animator / CombatantView).");
                    return;
                }

                var visualGO = new GameObject(VisualChildName);
                visualGO.transform.SetParent(prefab.transform, worldPositionStays: false);
                visualGO.transform.localPosition = new Vector3(0f, DefaultYOffset, 0f);
                visualGO.transform.localRotation = Quaternion.identity;
                visualGO.transform.localScale = Vector3.one;

                var srNew = visualGO.AddComponent<SpriteRenderer>();
                EditorUtility.CopySerialized(srRoot, srNew);

                var animNew = visualGO.AddComponent<Animator>();
                EditorUtility.CopySerialized(animRoot, animNew);

                Object.DestroyImmediate(srRoot, allowDestroyingAssets: true);
                Object.DestroyImmediate(animRoot, allowDestroyingAssets: true);

                var so = new SerializedObject(view);
                so.FindProperty("_sprite").objectReferenceValue = srNew;
                so.FindProperty("_animator").objectReferenceValue = animNew;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                Debug.Log($"[RestructureColossar] OK. Child '{VisualChildName}' cree (Y={DefaultYOffset}). " +
                          $"Refs CombatantView._sprite / ._animator re-binds. " +
                          $"Tune les Y offsets via Inspector CombatantView -> _stage1VisualYOffset / _stage2VisualYOffset.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }
    }
}
