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
    /// Brique 5.3.f.bis — Patch scene 10_CommunityHub : ajoute le bouton "Arene"
    /// dans le hub + le panel ArenaPanelHost avec 4 boutons modes.
    ///
    /// Menu : Nymora > Setup > Patch Arena Panel.
    /// </summary>
    public static class PatchArenaPanelTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color ContainerColor = new Color(0.12f, 0.13f, 0.16f, 0.98f);
        private static readonly Color HeaderColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        private static readonly Color CloseColor = new Color(0.55f, 0.25f, 0.25f, 1f);
        private static readonly Color ArenaButtonColor = new Color(0.55f, 0.40f, 0.25f, 1f); // orange/brun arena
        private static readonly Color TrainingActiveColor = new Color(0.30f, 0.50f, 0.32f, 1f);
        private static readonly Color RankedDisabledColor = new Color(0.18f, 0.20f, 0.24f, 1f);
        private static readonly Color Ranked1v1ActiveColor = new Color(0.45f, 0.38f, 0.62f, 1f); // violet "classe"

        [MenuItem("Nymora/Setup/Patch Arena Panel", priority = 38)]
        private static void Patch()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Patch Arena", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Arena",
                    $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?", "Ouvrir", "Annuler")) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) { EditorUtility.DisplayDialog("Patch Arena", "Aucun Canvas.", "OK"); return; }

            var actions = new List<string>();
            EnsureArenaButton(canvas, actions);
            EnsureArenaPanel(canvas, actions);

            EditorSceneManager.MarkSceneDirty(scene);
            string summary = actions.Count == 0 ? "OK Scene deja a jour." : "Patch applique :\n\n" + string.Join("\n", actions) + "\n\nN'oublie pas Ctrl+S.";
            Debug.Log($"[Nymora.Setup] Patch arena : {actions.Count} action(s)\n{summary}");
            EditorUtility.DisplayDialog("Patch Arena", summary, "OK");
        }

        // -----------------------------------------------------------------------------
        // Bouton hub Arene (a gauche de Decks, position -780, 20)
        // -----------------------------------------------------------------------------
        private static void EnsureArenaButton(Canvas canvas, List<string> actions)
        {
            var existing = canvas.transform.Find("ArenaButton");
            GameObject go;
            bool isNew = existing == null;
            if (!isNew)
            {
                go = existing.gameObject;
                actions.Add("ArenaButton existant : re-style (position PRESERVEE)");
            }
            else
            {
                go = new GameObject("ArenaButton", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(canvas.transform, false);
                actions.Add("ArenaButton cree");
            }

            // IMPORTANT : on ne touche la geometrie (ancres/position/taille) QU'A LA CREATION.
            // Sinon un re-run du tool ecrase le placement manuel (incident 22 mai : Arene
            // ramenee a -780 = pile sous MyProfile, donc invisible). Position rightmost (-20)
            // pour matcher l'ordre Profil/Amis/Clan/Decks/Arene si on cree from scratch.
            if (isNew)
            {
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-20f, 20f);
                rt.sizeDelta = new Vector2(180f, 56f);
            }

            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.color = ArenaButtonColor;
            if (go.GetComponent<Button>() == null) go.AddComponent<Button>();
            go.GetComponent<Button>().targetGraphic = img;
            if (go.GetComponent<HubArenaButton>() == null) go.AddComponent<HubArenaButton>();

            var oldLabel = go.transform.Find("Label");
            if (oldLabel != null) Object.DestroyImmediate(oldLabel.gameObject);
            var labelGo = NewChild("Label", go.transform);
            StretchToParent(labelGo);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "Arène";
            tmp.fontSize = 26f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
        }

        // -----------------------------------------------------------------------------
        // Panel Arena : 4 boutons modes (Entrainement actif + 3 Ranked grises)
        // -----------------------------------------------------------------------------
        private static void EnsureArenaPanel(Canvas canvas, List<string> actions)
        {
            var existing = canvas.transform.Find("ArenaPanelHost");
            HubArenaPanel panel;
            GameObject hostGo;

            if (existing != null)
            {
                hostGo = existing.gameObject;
                panel = hostGo.GetComponent<HubArenaPanel>() ?? hostGo.AddComponent<HubArenaPanel>();
                for (int i = hostGo.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(hostGo.transform.GetChild(i).gameObject);
                actions.Add("ArenaPanelHost cleanup (rebuild)");
            }
            else
            {
                hostGo = new GameObject("ArenaPanelHost", typeof(RectTransform));
                hostGo.transform.SetParent(canvas.transform, false);
                StretchToParent(hostGo);
                panel = hostGo.AddComponent<HubArenaPanel>();
                actions.Add("ArenaPanelHost cree");
            }

            // PanelRoot toggleable
            var panelRoot = NewChild("PanelRoot", hostGo.transform);
            StretchToParent(panelRoot);

            // Backdrop fullscreen
            var backdrop = NewChild("Backdrop", panelRoot.transform);
            StretchToParent(backdrop);
            backdrop.AddComponent<Image>().color = BackdropColor;

            // Container modal centre 600x500
            var container = NewChild("Container", panelRoot.transform);
            var crt = container.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(600f, 500f);
            container.AddComponent<Image>().color = ContainerColor;

            // Header bar
            var header = NewChild("Header", container.transform);
            var hRt = header.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 1f);
            hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot = new Vector2(0.5f, 1f);
            hRt.sizeDelta = new Vector2(0f, 56f);
            header.AddComponent<Image>().color = HeaderColor;

            var title = NewChild("Title", header.transform);
            StretchToParent(title);
            var titleTmp = title.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "Arène";
            titleTmp.fontSize = 26f;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.fontStyle = FontStyles.Bold;

            // Close button
            var closeGo = MakeButton(header.transform, "CloseButton", "X", CloseColor, 44f, 40f);
            var crtClose = closeGo.GetComponent<RectTransform>();
            crtClose.anchorMin = crtClose.anchorMax = new Vector2(1f, 0.5f);
            crtClose.pivot = new Vector2(1f, 0.5f);
            crtClose.anchoredPosition = new Vector2(-12f, 0f);

            // Boutons modes (VerticalLayoutGroup)
            var modesContainer = NewChild("Modes", container.transform);
            var mRt = modesContainer.GetComponent<RectTransform>();
            mRt.anchorMin = new Vector2(0f, 0f);
            mRt.anchorMax = new Vector2(1f, 1f);
            mRt.offsetMin = new Vector2(40f, 40f);
            mRt.offsetMax = new Vector2(-40f, -76f);
            var vlg = modesContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16f;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            var trainingBtn = MakeModeButton(modesContainer.transform, "TrainingButton", "Entraînement (vs IA)", TrainingActiveColor, enabled: true);
            // 6.1 — Ranked 1v1 ACTIF (ouvre le panneau de recherche classee).
            var r1v1Btn = MakeModeButton(modesContainer.transform, "Ranked1v1Button", "Ranked 1v1", Ranked1v1ActiveColor, enabled: true);
            var r2v2Btn = MakeModeButton(modesContainer.transform, "Ranked2v2Button", "Ranked 2v2  (bientôt)", RankedDisabledColor, enabled: false);
            var r3v3Btn = MakeModeButton(modesContainer.transform, "Ranked3v3Button", "Ranked 3v3  (bientôt)", RankedDisabledColor, enabled: false);

            // Wire panel
            var so = new SerializedObject(panel);
            so.FindProperty("_panelRoot").objectReferenceValue = panelRoot;
            so.FindProperty("_closeButton").objectReferenceValue = closeGo.GetComponent<Button>();
            so.FindProperty("_trainingButton").objectReferenceValue = trainingBtn.GetComponent<Button>();
            so.FindProperty("_ranked1v1Button").objectReferenceValue = r1v1Btn.GetComponent<Button>();
            so.FindProperty("_ranked2v2Button").objectReferenceValue = r2v2Btn.GetComponent<Button>();
            so.FindProperty("_ranked3v3Button").objectReferenceValue = r3v3Btn.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();
            actions.Add("ArenaPanel SerializedFields wires");

            panelRoot.SetActive(false);
        }

        private static GameObject MakeModeButton(Transform parent, string name, string label, Color color, bool enabled)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 64f;
            var img = go.GetComponent<Image>();
            img.color = color;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = enabled;

            var labelGo = NewChild("Label", go.transform);
            StretchToParent(labelGo);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 22f;
            tmp.color = enabled ? Color.white : new Color(0.6f, 0.6f, 0.65f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            return go;
        }

        private static GameObject MakeButton(Transform parent, string name, string label, Color color, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (width > 0 && height > 0) rt.sizeDelta = new Vector2(width, height);
            var img = go.GetComponent<Image>();
            img.color = color;
            go.GetComponent<Button>().targetGraphic = img;
            var labelGo = NewChild("Label", go.transform);
            StretchToParent(labelGo);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 22f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            return go;
        }

        private static GameObject NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchToParent(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
