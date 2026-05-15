using Nymora.Combat.View;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Restructure le prefab Combatant_Necram en parent + enfant "Visual" pour pouvoir
    /// decaler le sprite verticalement (vis-a-vis de la grille iso) sans toucher au
    /// transform racine qui sert d'ancre logique pour le positionnement sur la tile.
    ///
    /// Avant : GO racine avec Transform + SpriteRenderer + Animator + CombatantView.
    /// Apres : GO racine avec Transform + CombatantView (refs re-binds) ; GO enfant
    /// "Visual" avec Transform (LocalPosition.y reglable) + SpriteRenderer + Animator.
    ///
    /// Idempotent : si le child "Visual" existe deja, on ajuste juste le Y offset.
    /// Specifique Necram — les autres classes restent mono-GO.
    /// </summary>
    public static class RestructureNecramPrefabTool
    {
        private const string PrefabPath = "Assets/_Nymora/Prefabs/Combat/Combatants/Combatant_Necram.prefab";
        private const string VisualChildName = "Visual";
        private const float DefaultYOffset = -0.2f;

        [MenuItem("Nymora/Setup/Restructure Necram Prefab (Sprite Y Offset)")]
        public static void Run()
        {
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[RestructureNecram] Prefab introuvable : {PrefabPath}");
                return;
            }

            try
            {
                var existingChild = prefab.transform.Find(VisualChildName);
                if (existingChild != null)
                {
                    Debug.Log($"[RestructureNecram] Child '{VisualChildName}' deja present. Y={existingChild.localPosition.y}. Ajuste dans l'Inspector si besoin.");
                    PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                    return;
                }

                var srRoot = prefab.GetComponent<SpriteRenderer>();
                var animRoot = prefab.GetComponent<Animator>();
                var view = prefab.GetComponent<CombatantView>();

                if (srRoot == null || animRoot == null || view == null)
                {
                    Debug.LogError("[RestructureNecram] Components manquants sur le prefab racine (SpriteRenderer / Animator / CombatantView).");
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
                Debug.Log($"[RestructureNecram] OK. Child '{VisualChildName}' cree avec Y={DefaultYOffset}. " +
                          $"Refs CombatantView._sprite / ._animator re-binds. " +
                          $"Ajuste LocalPosition.y dans l'Inspector du child '{VisualChildName}' pour fine-tune.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }
    }
}
