using System.Collections.Generic;
using Nymora.Hub;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 5.5.d — Remplace le placeholder "Coming soon" du tab Cosmétiques du HubProfilePanel
    /// par un Header (compteur + classe active) + Container vertical pour spawn runtime des items
    /// (inventaire /shop/inventory, équiper avec garde-fou classe).
    ///
    /// Wire `_cosmeticsHeader` et `_cosmeticsContainer` sur HubProfilePanel.
    /// Calqué sur PatchProfileAchievementsTabTool.
    ///
    /// Menu : Nymora > Setup > Patch Profile Cosmetics Tab
    /// </summary>
    public static class PatchProfileCosmeticsTabTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private static readonly Color HeaderColor = new Color(0.9f, 0.9f, 0.92f, 1f);

        [MenuItem("Nymora/Setup/Patch Profile Cosmetics Tab", priority = 44)]
        private static void Patch()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Patch Cosmetics Tab", "Stoppe Play Mode d'abord.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Cosmetics Tab",
                    $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?",
                    "Ouvrir", "Annuler"))
                {
                    return;
                }
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var panel = Object.FindFirstObjectByType<HubProfilePanel>();
            if (panel == null)
            {
                EditorUtility.DisplayDialog("Patch Cosmetics Tab",
                    "HubProfilePanel introuvable. Lance d'abord Patch Profile Panel.", "OK");
                return;
            }

            var panelSo = new SerializedObject(panel);
            var contentProp = panelSo.FindProperty("_contentCosmetics");
            if (contentProp == null || contentProp.objectReferenceValue == null)
            {
                EditorUtility.DisplayDialog("Patch Cosmetics Tab",
                    "HubProfilePanel._contentCosmetics non assigné. Relance Patch Profile Panel d'abord.", "OK");
                return;
            }
            var content = contentProp.objectReferenceValue as GameObject;
            if (content == null)
            {
                EditorUtility.DisplayDialog("Patch Cosmetics Tab", "_contentCosmetics n'est pas un GameObject.", "OK");
                return;
            }

            var actions = new List<string>();

            CleanupPlaceholder(content, actions);

            // Header
            var headerGo = FindOrCreateChild(content.transform, "Header", out bool _, typeof(TextMeshProUGUI), typeof(LayoutElement));
            var headerTmp = headerGo.GetComponent<TextMeshProUGUI>();
            headerTmp.text = "<b>Cosmétiques</b>";
            headerTmp.fontSize = 22;
            headerTmp.color = HeaderColor;
            headerTmp.alignment = TextAlignmentOptions.MidlineLeft;
            headerTmp.richText = true;
            headerGo.GetComponent<LayoutElement>().preferredHeight = 32f;

            // Container vertical pour spawn runtime
            var containerGo = FindOrCreateChild(content.transform, "Container", out bool createdC, typeof(VerticalLayoutGroup), typeof(LayoutElement));
            if (createdC)
            {
                var vl = containerGo.GetComponent<VerticalLayoutGroup>();
                vl.spacing = 4;
                vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
                vl.childControlWidth = true; vl.childControlHeight = true;
                containerGo.GetComponent<LayoutElement>().flexibleWidth = 1f;
                actions.Add("+ Cosmetics Container");
            }

            TryWire(panelSo, "_cosmeticsHeader", headerTmp, actions, "HubProfilePanel._cosmeticsHeader");
            TryWire(panelSo, "_cosmeticsContainer", containerGo.GetComponent<RectTransform>(), actions, "HubProfilePanel._cosmeticsContainer");

            panelSo.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);

            string summary = actions.Count == 0
                ? "OK déjà à jour."
                : "Patch appliqué :\n\n" + string.Join("\n", actions) + "\n\nN'oublie pas Ctrl+S.";
            Debug.Log($"[Nymora.Setup] Patch cosmetics tab : {actions.Count} action(s)\n{summary}");
            EditorUtility.DisplayDialog("Patch Cosmetics Tab", summary, "OK");
        }

        private static void CleanupPlaceholder(GameObject content, List<string> actions)
        {
            string[] placeholderNames = { "Title", "ComingSoon", "Desc" };
            foreach (var n in placeholderNames)
            {
                var t = content.transform.Find(n);
                if (t != null)
                {
                    Object.DestroyImmediate(t.gameObject);
                    actions.Add($"- Placeholder '{n}' détruit");
                }
            }
        }

        private static GameObject FindOrCreateChild(Transform parent, string childName, out bool created, params System.Type[] components)
        {
            var existing = parent.Find(childName);
            if (existing != null) { created = false; return existing.gameObject; }
            var all = new List<System.Type> { typeof(RectTransform) };
            if (components != null) all.AddRange(components);
            var go = new GameObject(childName, all.ToArray());
            go.transform.SetParent(parent, false);
            created = true;
            return go;
        }

        private static void TryWire(SerializedObject so, string propName, Object value, List<string> actions, string label)
        {
            if (value == null) return;
            var prop = so.FindProperty(propName);
            if (prop == null) return;
            if (prop.objectReferenceValue == value) return;
            if (prop.objectReferenceValue != null) return;
            prop.objectReferenceValue = value;
            actions.Add($"+ {label} wire");
        }
    }
}
