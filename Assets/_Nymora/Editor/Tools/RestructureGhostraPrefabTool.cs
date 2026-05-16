using Nymora.Combat.View;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// 3.6.g — Restructure le prefab Combatant_Ghostra en parent + enfant "Visual" pour
    /// pouvoir agrandir le sprite et le decaler verticalement vis-a-vis de la grille iso
    /// sans toucher au transform racine qui sert d'ancre logique sur la tile.
    ///
    /// Mirror de <see cref="RestructureNecramPrefabTool"/>. Difference :
    ///   - DefaultYOffset = -0.15f (Lorenzo : "baisser un peu en Y, perso pas centre")
    ///   - DefaultScale   = 1.15f  (Lorenzo : "augmenter legerement la taille")
    ///
    /// Avant : GO racine avec Transform + SpriteRenderer + Animator + CombatantView.
    /// Apres : GO racine avec Transform + CombatantView (refs re-binds) ; GO enfant
    /// "Visual" avec Transform (LocalPosition.y reglable + Scale reglable) +
    /// SpriteRenderer + Animator.
    ///
    /// Idempotent : si le child "Visual" existe deja, on log Y et Scale courants et on
    /// ne touche pas (Lorenzo ajuste dans l'Inspector pour fine-tune).
    /// Specifique Ghostra — relancer le tool si tu veux re-restructurer.
    /// </summary>
    public static class RestructureGhostraPrefabTool
    {
        private const string PrefabPath = "Assets/_Nymora/Prefabs/Combat/Combatants/Combatant_Ghostra.prefab";
        private const string VisualChildName = "Visual";
        // Valeurs finales calibrees par Lorenzo en Play Mode (16 mai 2026).
        private const float DefaultYOffset = -0.22f;
        private const float DefaultScale = 1.16f;

        [MenuItem("Nymora/Setup/Restructure Ghostra Prefab (Sprite Scale + Y Offset)")]
        public static void Run()
        {
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[RestructureGhostra] Prefab introuvable : {PrefabPath}");
                return;
            }

            try
            {
                var existingChild = prefab.transform.Find(VisualChildName);
                if (existingChild != null)
                {
                    // Idempotent : on UPDATE Y + Scale aux defaults courants (Lorenzo peut iterer
                    // en modifiant DefaultYOffset / DefaultScale puis relancer le tool).
                    float oldY = existingChild.localPosition.y;
                    Vector3 oldScale = existingChild.localScale;
                    existingChild.localPosition = new Vector3(existingChild.localPosition.x, DefaultYOffset, existingChild.localPosition.z);
                    existingChild.localScale = new Vector3(DefaultScale, DefaultScale, 1f);
                    PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                    Debug.Log($"[RestructureGhostra] Child '{VisualChildName}' deja present — UPDATE applique. " +
                              $"Y: {oldY} -> {DefaultYOffset}, Scale: {oldScale.x} -> {DefaultScale}. " +
                              $"Fine-tune dans l'Inspector si besoin.");
                    return;
                }

                var srRoot = prefab.GetComponent<SpriteRenderer>();
                var animRoot = prefab.GetComponent<Animator>();
                var view = prefab.GetComponent<CombatantView>();

                if (srRoot == null || animRoot == null || view == null)
                {
                    Debug.LogError("[RestructureGhostra] Components manquants sur le prefab racine " +
                                   "(SpriteRenderer / Animator / CombatantView). Lance 'Build Ghostra Animator' avant.");
                    return;
                }

                var visualGO = new GameObject(VisualChildName);
                visualGO.transform.SetParent(prefab.transform, worldPositionStays: false);
                visualGO.transform.localPosition = new Vector3(0f, DefaultYOffset, 0f);
                visualGO.transform.localRotation = Quaternion.identity;
                visualGO.transform.localScale = new Vector3(DefaultScale, DefaultScale, 1f);

                var srNew = visualGO.AddComponent<SpriteRenderer>();
                EditorUtility.CopySerialized(srRoot, srNew);

                var animNew = visualGO.AddComponent<Animator>();
                EditorUtility.CopySerialized(animRoot, animNew);

                Object.DestroyImmediate(srRoot, allowDestroyingAssets: true);
                Object.DestroyImmediate(animRoot, allowDestroyingAssets: true);

                var so = new SerializedObject(view);
                var spriteProp = so.FindProperty("_sprite");
                if (spriteProp != null) spriteProp.objectReferenceValue = srNew;
                var animProp = so.FindProperty("_animator");
                if (animProp != null) animProp.objectReferenceValue = animNew;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                Debug.Log($"[RestructureGhostra] OK. Child '{VisualChildName}' cree avec Y={DefaultYOffset} Scale={DefaultScale}. " +
                          $"Refs CombatantView._sprite / ._animator re-binds. " +
                          $"Ajuste LocalPosition.y et LocalScale dans l'Inspector du child '{VisualChildName}' pour fine-tune.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }
    }
}
