using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Port hub du pattern Restructure{Necram|Ghostra}PrefabTool de la scene combat IA.
    ///
    /// Restructure le prefab HubAvatar en parent + enfant "Visual" pour pouvoir appliquer
    /// un Y offset + Scale per-classe (via NymoraClassDefinition.HubVisualYOffset/Scale)
    /// au moment ou HubAvatar.ApplyClassVisual resolve la classe, sans toucher au transform
    /// racine qui sert d'ancre tile pour HubMovementController.
    ///
    /// Avant : GO racine "HubAvatar" avec Transform + SpriteRenderer + NetworkObject +
    ///         HubAvatar + HubMovementController.
    /// Apres : GO racine "HubAvatar" avec Transform + NetworkObject + HubAvatar +
    ///         HubMovementController ; GO enfant "Visual" avec Transform + SpriteRenderer.
    ///
    /// HubAvatar utilise GetComponentInChildren&lt;SpriteRenderer&gt; donc pas de rebind de
    /// reference necessaire (le code retrouve auto le SR sur le child).
    ///
    /// Idempotent : si le child "Visual" existe deja, on log et on ne touche pas (Lorenzo
    /// peut iterer en modifiant les NymoraClassDefinition.HubVisualScale/YOffset puis en
    /// relancant le populator).
    /// </summary>
    public static class RestructureHubAvatarPrefabTool
    {
        private const string PrefabPath = "Assets/_Nymora/Prefabs/Hub/HubAvatar.prefab";
        private const string VisualChildName = "Visual";

        [MenuItem("Nymora/Setup/Restructure HubAvatar Prefab (Visual child for per-class Y/Scale)")]
        public static void Run()
        {
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[RestructureHubAvatar] Prefab introuvable : {PrefabPath}");
                return;
            }

            try
            {
                var existingChild = prefab.transform.Find(VisualChildName);
                if (existingChild != null)
                {
                    Debug.Log($"[RestructureHubAvatar] Child '{VisualChildName}' deja present sur HubAvatar. " +
                              $"localPos.y={existingChild.localPosition.y} localScale={existingChild.localScale.x}. " +
                              $"Pour repeupler les valeurs per-class, relance le populator.");
                    return;
                }

                var srRoot = prefab.GetComponent<SpriteRenderer>();
                if (srRoot == null)
                {
                    Debug.LogError("[RestructureHubAvatar] SpriteRenderer manquant sur le prefab racine. " +
                                   "Le prefab est peut-etre deja restructure ou casse.");
                    return;
                }

                // Cree le child Visual avec localPos = (0,0,0) et localScale = (1,1,1).
                // Les vraies valeurs (Y offset + Scale per-class) sont appliquees a runtime
                // par HubAvatar.ApplyClassVisual depuis NymoraClassDefinition.
                var visualGO = new GameObject(VisualChildName);
                visualGO.transform.SetParent(prefab.transform, worldPositionStays: false);
                visualGO.transform.localPosition = Vector3.zero;
                visualGO.transform.localRotation = Quaternion.identity;
                visualGO.transform.localScale = Vector3.one;

                // Copie le SpriteRenderer du root vers le child Visual (preserve materials,
                // sprite, sortingOrder, color, flipX). Puis destroy l'original sur root.
                var srNew = visualGO.AddComponent<SpriteRenderer>();
                EditorUtility.CopySerialized(srRoot, srNew);

                Object.DestroyImmediate(srRoot, allowDestroyingAssets: true);

                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                Debug.Log($"[RestructureHubAvatar] OK. Child '{VisualChildName}' cree (localPos=0 localScale=1). " +
                          $"HubAvatar applique runtime les valeurs HubVisualScale + HubVisualYOffset de " +
                          $"NymoraClassDefinition. Lance ensuite 'Populate Class Definitions' pour peupler " +
                          $"les valeurs per-class.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }
    }
}
