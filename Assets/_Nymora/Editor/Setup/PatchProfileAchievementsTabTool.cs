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
    /// Brique 5.2.d — Remplace le placeholder "Coming soon" du tab Achievements du HubProfilePanel
    /// par un Header (X/Y succès, points totaux) + Container vertical pour spawn runtime des items.
    ///
    /// Wire `_achievementsHeader`, `_achievementsContainer` et `_chatUIForAchievementToast` sur HubProfilePanel.
    ///
    /// Menu : Nymora > Setup > Patch Profile Achievements Tab
    /// </summary>
    public static class PatchProfileAchievementsTabTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private static readonly Color HeaderColor = new Color(0.9f, 0.9f, 0.92f, 1f);

        [MenuItem("Nymora/Setup/Patch Profile Achievements Tab", priority = 39)]
        private static void Patch()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Patch Achievements Tab", "Stoppe Play Mode d'abord.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Achievements Tab",
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
                EditorUtility.DisplayDialog("Patch Achievements Tab",
                    "HubProfilePanel introuvable. Lance d'abord Patch Profile Panel.", "OK");
                return;
            }

            var panelSo = new SerializedObject(panel);
            var contentAchProp = panelSo.FindProperty("_contentAchievements");
            if (contentAchProp == null || contentAchProp.objectReferenceValue == null)
            {
                EditorUtility.DisplayDialog("Patch Achievements Tab",
                    "HubProfilePanel._contentAchievements non assigné. Relance Patch Profile Panel d'abord.", "OK");
                return;
            }
            var contentAch = contentAchProp.objectReferenceValue as GameObject;
            if (contentAch == null)
            {
                EditorUtility.DisplayDialog("Patch Achievements Tab", "_contentAchievements n'est pas un GameObject.", "OK");
                return;
            }

            var actions = new List<string>();

            CleanupPlaceholder(contentAch, actions);

            // Header
            var headerGo = FindOrCreateChild(contentAch.transform, "Header", out bool _, typeof(TextMeshProUGUI), typeof(LayoutElement));
            var headerTmp = headerGo.GetComponent<TextMeshProUGUI>();
            headerTmp.text = "<b>0/0</b> succès · <color=#f5dc66>0 points</color>";
            headerTmp.fontSize = 22;
            headerTmp.color = HeaderColor;
            headerTmp.alignment = TextAlignmentOptions.MidlineLeft;
            headerTmp.richText = true;
            headerGo.GetComponent<LayoutElement>().preferredHeight = 32f;

            // Container vertical pour spawn runtime
            var containerGo = FindOrCreateChild(contentAch.transform, "Container", out bool createdC, typeof(VerticalLayoutGroup), typeof(LayoutElement));
            if (createdC)
            {
                var vl = containerGo.GetComponent<VerticalLayoutGroup>();
                vl.spacing = 4;
                vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
                vl.childControlWidth = true; vl.childControlHeight = true;
                containerGo.GetComponent<LayoutElement>().flexibleWidth = 1f;
                actions.Add("+ Achievements Container");
            }

            // Wire refs
            TryWire(panelSo, "_achievementsHeader", headerTmp, actions, "HubProfilePanel._achievementsHeader");
            TryWire(panelSo, "_achievementsContainer", containerGo.GetComponent<RectTransform>(), actions, "HubProfilePanel._achievementsContainer");

            // Wire chatUI for toast (find HubChatUI in scene)
            var chatUI = Object.FindFirstObjectByType<HubChatUI>();
            if (chatUI != null)
            {
                TryWire(panelSo, "_chatUIForAchievementToast", chatUI, actions, "HubProfilePanel._chatUIForAchievementToast");
            }

            panelSo.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);

            string summary = actions.Count == 0
                ? "OK déjà à jour."
                : "Patch appliqué :\n\n" + string.Join("\n", actions) + "\n\nN'oublie pas Ctrl+S.";
            Debug.Log($"[Nymora.Setup] Patch achievements tab : {actions.Count} action(s)\n{summary}");
            EditorUtility.DisplayDialog("Patch Achievements Tab", summary, "OK");
        }

        private static void CleanupPlaceholder(GameObject contentAch, List<string> actions)
        {
            string[] placeholderNames = { "Title", "ComingSoon", "Desc" };
            foreach (var n in placeholderNames)
            {
                var t = contentAch.transform.Find(n);
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
