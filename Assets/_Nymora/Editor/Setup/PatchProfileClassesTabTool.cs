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
    /// Brique 5.1.f — Remplace le placeholder "Coming soon" de l'onglet Classes du HubProfilePanel
    /// par 5 rows fonctionnelles (Soulrender, Nightseer, Colossar, Necram, Ghostra) avec
    /// level label + XP bar (Image filled) + XP label.
    ///
    /// Wire le array `_classRows` du HubProfilePanel via SerializedObject.
    ///
    /// Idempotent : si les rows existent deja, juste re-wire (utile apres modif du script).
    ///
    /// Menu : Nymora > Setup > Patch Profile Classes Tab
    /// </summary>
    public static class PatchProfileClassesTabTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";

        private struct ClassInfo
        {
            public string Id;
            public string Display;
            public Color BgColor;
        }

        private static readonly ClassInfo[] CLASSES = new ClassInfo[]
        {
            new ClassInfo { Id = "Soulrender",  Display = "Soulrender",  BgColor = new Color(0.55f, 0.18f, 0.20f, 1f) },
            new ClassInfo { Id = "Nightseer",   Display = "Nightseer",   BgColor = new Color(0.30f, 0.20f, 0.45f, 1f) },
            new ClassInfo { Id = "Colossar",    Display = "Colossar",    BgColor = new Color(0.50f, 0.40f, 0.25f, 1f) },
            new ClassInfo { Id = "Necram",      Display = "Necram",      BgColor = new Color(0.25f, 0.45f, 0.30f, 1f) },
            new ClassInfo { Id = "Ghostra",     Display = "Ghostra",     BgColor = new Color(0.20f, 0.40f, 0.55f, 1f) },
        };

        private static readonly Color RowBgColor = new Color(0.16f, 0.18f, 0.22f, 1f);
        private static readonly Color XpBarBgColor = new Color(0.10f, 0.10f, 0.12f, 1f);
        private static readonly Color XpBarFillColor = new Color(0.30f, 0.65f, 0.40f, 1f);
        private static readonly Color SectionLabelColor = new Color(0.85f, 0.85f, 0.9f, 1f);

        [MenuItem("Nymora/Setup/Patch Profile Classes Tab", priority = 38)]
        private static void Patch()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Patch Classes Tab", "Stoppe Play Mode d'abord.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Classes Tab",
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
                EditorUtility.DisplayDialog("Patch Classes Tab",
                    "HubProfilePanel introuvable. Lance d'abord Patch Profile Panel.", "OK");
                return;
            }

            // Trouve l'enfant ContentClasses via le SerializedField _contentClasses
            var panelSo = new SerializedObject(panel);
            var contentClassesProp = panelSo.FindProperty("_contentClasses");
            if (contentClassesProp == null || contentClassesProp.objectReferenceValue == null)
            {
                EditorUtility.DisplayDialog("Patch Classes Tab",
                    "HubProfilePanel._contentClasses non assigné. Relance Patch Profile Panel d'abord.", "OK");
                return;
            }
            var contentClasses = contentClassesProp.objectReferenceValue as GameObject;
            if (contentClasses == null)
            {
                EditorUtility.DisplayDialog("Patch Classes Tab", "_contentClasses n'est pas un GameObject.", "OK");
                return;
            }

            var actions = new List<string>();

            // 1. Nettoyage : detruire les enfants placeholder (Title / ComingSoon / Desc)
            CleanupPlaceholder(contentClasses, actions);

            // 2. (Re)build Title + 5 rows
            EnsureTitle(contentClasses.transform, "Progression par classe");

            // 3. Construire/retrouver 5 rows et collecter les refs
            var rowsRefs = new List<(string id, TextMeshProUGUI levelLabel, TextMeshProUGUI xpLabel, Image xpBarFill)>();
            foreach (var info in CLASSES)
            {
                var row = EnsureClassRow(contentClasses.transform, info, actions);
                rowsRefs.Add(row);
            }

            // 4. Wire _classRows array sur HubProfilePanel
            var rowsProp = panelSo.FindProperty("_classRows");
            if (rowsProp == null)
            {
                EditorUtility.DisplayDialog("Patch Classes Tab",
                    "_classRows introuvable sur HubProfilePanel — re-importe le script.", "OK");
                return;
            }
            rowsProp.arraySize = rowsRefs.Count;
            for (int i = 0; i < rowsRefs.Count; i++)
            {
                var elem = rowsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("classId").stringValue = rowsRefs[i].id;
                elem.FindPropertyRelative("levelLabel").objectReferenceValue = rowsRefs[i].levelLabel;
                elem.FindPropertyRelative("xpLabel").objectReferenceValue = rowsRefs[i].xpLabel;
                elem.FindPropertyRelative("xpBarFill").objectReferenceValue = rowsRefs[i].xpBarFill;
            }
            panelSo.ApplyModifiedPropertiesWithoutUndo();
            actions.Add($"+ Array _classRows wire ({rowsRefs.Count} entries)");

            EditorSceneManager.MarkSceneDirty(scene);

            string summary = "Patch applique :\n\n" + string.Join("\n", actions) + "\n\nN'oublie pas Ctrl+S.";
            Debug.Log($"[Nymora.Setup] Patch profile classes tab : {actions.Count} action(s)\n{summary}");
            EditorUtility.DisplayDialog("Patch Classes Tab", summary, "OK");
        }

        private static void CleanupPlaceholder(GameObject contentClasses, List<string> actions)
        {
            // Le placeholder PatchProfilePanelTool genere : Title, ComingSoon, Desc.
            // On detruit ces 3 GO (re-cree dans la nouvelle hierarchie).
            string[] placeholderNames = new[] { "ComingSoon", "Desc" };
            foreach (var n in placeholderNames)
            {
                var t = contentClasses.transform.Find(n);
                if (t != null)
                {
                    Object.DestroyImmediate(t.gameObject);
                    actions.Add($"- Placeholder '{n}' detruit");
                }
            }
        }

        private static void EnsureTitle(Transform parent, string text)
        {
            var titleGo = FindOrCreateChild(parent, "Title", out bool _, typeof(TextMeshProUGUI), typeof(LayoutElement));
            var tmp = titleGo.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.fontStyle = FontStyles.Bold;
            titleGo.GetComponent<LayoutElement>().preferredHeight = 32f;
        }

        private static (string id, TextMeshProUGUI levelLabel, TextMeshProUGUI xpLabel, Image xpBarFill) EnsureClassRow(Transform parent, ClassInfo info, List<string> actions)
        {
            var row = FindOrCreateChild(parent, $"Row_{info.Id}", out bool created, typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            if (created)
            {
                row.GetComponent<Image>().color = RowBgColor;
                var hl = row.GetComponent<HorizontalLayoutGroup>();
                hl.padding = new RectOffset(12, 12, 6, 6);
                hl.spacing = 12;
                hl.childForceExpandWidth = false;
                hl.childForceExpandHeight = true;
                hl.childControlWidth = true;
                hl.childControlHeight = true;
                hl.childAlignment = TextAnchor.MiddleLeft;
                row.GetComponent<LayoutElement>().preferredHeight = 56f;
                actions.Add($"+ Row_{info.Id}");
            }

            // Couleur classe (rectangle vertical à gauche)
            var colorGo = FindOrCreateChild(row.transform, "ClassColor", out bool _, typeof(Image), typeof(LayoutElement));
            colorGo.GetComponent<Image>().color = info.BgColor;
            colorGo.GetComponent<LayoutElement>().preferredWidth = 8f;

            // Nom classe (left, ~160px)
            var nameGo = FindOrCreateChild(row.transform, "Name", out bool _1, typeof(TextMeshProUGUI), typeof(LayoutElement));
            var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
            nameTmp.text = info.Display;
            nameTmp.fontSize = 20;
            nameTmp.color = Color.white;
            nameTmp.alignment = TextAlignmentOptions.MidlineLeft;
            nameTmp.fontStyle = FontStyles.Bold;
            nameGo.GetComponent<LayoutElement>().preferredWidth = 160f;

            // Level label
            var levelGo = FindOrCreateChild(row.transform, "Level", out bool _2, typeof(TextMeshProUGUI), typeof(LayoutElement));
            var levelTmp = levelGo.GetComponent<TextMeshProUGUI>();
            levelTmp.text = "Niv. 1";
            levelTmp.fontSize = 18;
            levelTmp.color = new Color(0.95f, 0.85f, 0.4f, 1f);
            levelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            levelTmp.fontStyle = FontStyles.Bold;
            levelGo.GetComponent<LayoutElement>().preferredWidth = 80f;

            // XP bar (flex)
            var xpBarBgGo = FindOrCreateChild(row.transform, "XpBarBg", out bool createdBar, typeof(Image), typeof(LayoutElement));
            if (createdBar)
            {
                xpBarBgGo.GetComponent<Image>().color = XpBarBgColor;
                var le = xpBarBgGo.GetComponent<LayoutElement>();
                le.preferredHeight = 16f;
                le.flexibleWidth = 1f;
            }
            // XpBarFill (enfant)
            var xpBarFillGo = FindOrCreateChild(xpBarBgGo.transform, "XpBarFill", out bool createdFill, typeof(Image));
            var xpBarFillImg = xpBarFillGo.GetComponent<Image>();
            xpBarFillImg.color = XpBarFillColor;
            xpBarFillImg.type = Image.Type.Filled;
            xpBarFillImg.fillMethod = Image.FillMethod.Horizontal;
            xpBarFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            xpBarFillImg.fillAmount = 0f;
            if (createdFill)
            {
                var rt = xpBarFillGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }

            // XP label (right)
            var xpLabelGo = FindOrCreateChild(row.transform, "XpLabel", out bool _3, typeof(TextMeshProUGUI), typeof(LayoutElement));
            var xpLabelTmp = xpLabelGo.GetComponent<TextMeshProUGUI>();
            xpLabelTmp.text = "0 / 200 XP";
            xpLabelTmp.fontSize = 14;
            xpLabelTmp.color = new Color(0.8f, 0.8f, 0.85f, 1f);
            xpLabelTmp.alignment = TextAlignmentOptions.MidlineRight;
            xpLabelGo.GetComponent<LayoutElement>().preferredWidth = 130f;

            return (info.Id, levelTmp, xpLabelTmp, xpBarFillImg);
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
    }
}
