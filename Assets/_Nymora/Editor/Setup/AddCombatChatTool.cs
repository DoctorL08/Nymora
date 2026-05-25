using System.IO;
using Nymora.Hub;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Pose le MÊME chat que le hub dans les scènes combat (IA + Casual + Ranked).
    ///
    /// Le chat est déjà un prefab (Prefabs/Hub/ChatPanel.prefab) instancié dans le hub. Cet outil
    /// l'instancie aussi dans chaque scène combat, sous un Canvas dédié "ChatCanvas", ancré
    /// bas-gauche. À runtime, le HubChatUI se connecte au HubChatClient (DontDestroyOnLoad,
    /// survit hub->combat) et affiche le ChatFeed (static, persistant). Donc en venant du hub,
    /// la conversation continue ; en Play direct dans une scène combat, la fenêtre s'affiche sans
    /// connexion live (HubChatClient.Instance null) — normal.
    ///
    /// La garde clavier (CombatInputController : pas de hotkeys de sorts quand on tape dans le
    /// chat) est livrée à part dans le même chantier.
    ///
    /// Idempotent : si un HubChatUI existe déjà dans la scène, on ne ré-instancie pas.
    ///
    /// Menu : Nymora &gt; Setup &gt; UI Menu &gt; Add Chat to Combat Scenes
    /// </summary>
    public static class AddCombatChatTool
    {
        private const string ChatPrefabPath = "Assets/_Nymora/Prefabs/Hub/ChatPanel.prefab";
        private const string ChatCanvasName = "ChatCanvas";

        private static readonly string[] CombatScenes =
        {
            "Assets/_Nymora/Scenes/30_CombatIA.unity",
            "Assets/_Nymora/Scenes/33_CombatCasual.unity",
            "Assets/_Nymora/Scenes/40_CombatRanked1v1.unity",
        };

        [MenuItem("Nymora/Setup/UI Menu/Add Chat to Combat Scenes", priority = 38)]
        private static void Run()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChatPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Add Combat Chat", $"Prefab chat introuvable : {ChatPrefabPath}", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int added = 0, skipped = 0;
            foreach (var path in CombatScenes)
            {
                if (!File.Exists(path)) { Debug.LogWarning($"[AddCombatChat] Scène absente : {path}"); continue; }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                var existing = Object.FindAnyObjectByType<HubChatUI>(FindObjectsInactive.Include);
                GameObject inst;
                if (existing != null)
                {
                    inst = existing.gameObject;
                    skipped++;
                }
                else
                {
                    // Canvas dédié (sous les overlays modaux abandon/coinflip à 30000+, au-dessus du HUD).
                    var canvasGo = GameObject.Find(ChatCanvasName);
                    if (canvasGo == null)
                    {
                        canvasGo = new GameObject(ChatCanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                        SceneManager.MoveGameObjectToScene(canvasGo, scene);
                        var canvas = canvasGo.GetComponent<Canvas>();
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        canvas.sortingOrder = 100;
                        var scaler = canvasGo.GetComponent<CanvasScaler>();
                        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                        scaler.referenceResolution = new Vector2(1920f, 1080f);
                        scaler.matchWidthOrHeight = 0.5f;
                    }
                    inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    inst.transform.SetParent(canvasGo.transform, worldPositionStays: false);
                    added++;
                }

                // Clé de persistance DÉDIÉE combat : UiResizableWindow persiste pos/taille sous le
                // nom du GameObject. Un nom distinct du chat hub -> position indépendante (sinon le
                // chat combat héritait de la position basse du hub et son bas se retrouvait coupé).
                inst.name = "ChatPanelCombat";

                // Restyle monochrome (idempotent) pour matcher exactement le chat du hub : le restyle
                // du hub vit en overrides sur SON instance, pas dans le prefab -> à ré-appliquer ici.
                HubChatRestyleTool.Restyle();

                // Le placeholder "Tape un message... ou /w <user> <msg>" est long : dans le chat
                // combat (plus étroit) il passait à la ligne et DÉBORDAIT sous le panneau ("petit
                // bout en bas"). On force une seule ligne (clip au lieu de wrap) -> plus de débord.
                var chatUi = inst.GetComponentInChildren<HubChatUI>(true);
                if (chatUi != null)
                {
                    var soChat = new SerializedObject(chatUi);
                    var input = soChat.FindProperty("_inputField")?.objectReferenceValue as TMP_InputField;
                    if (input != null && input.placeholder is TMP_Text ph)
                    {
                        ph.enableWordWrapping = false;
                        ph.overflowMode = TextOverflowModes.Ellipsis;
                        EditorUtility.SetDirty(ph);
                    }
                }

                // Ancrage bas-gauche + TAILLE EXPLICITE. La clé de persistance dédiée n'a pas d'état
                // sauvé -> UiResizableWindow retombe sinon sur la taille par défaut du prefab (minuscule),
                // d'où le panneau réduit à une fine barre sombre. On fixe une taille de fenêtre correcte.
                var rt = inst.transform as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 0f);
                    rt.pivot = new Vector2(0f, 0f);
                    rt.sizeDelta = new Vector2(450f, 310f);
                    rt.anchoredPosition = new Vector2(28f, 72f);
                }

                EnsureEventSystem(scene);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"[AddCombatChat] Terminé : {added} ajouté(s), {skipped} déjà présent(s).");
            EditorUtility.DisplayDialog("Add Combat Chat",
                $"Chat ajouté aux scènes combat.\n\n- Ajoutés : {added}\n- Déjà présents : {skipped}\n\n" +
                "Test : depuis le hub, lance un combat -> le chat suit. Tape dedans : les touches 1-7 " +
                "ne déclenchent plus les sorts pendant la saisie.\n" +
                "⚠️ Scènes modifiées -> rebuild standalone côté designer.",
                "OK");
        }

        private static void EnsureEventSystem(Scene scene)
        {
            if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(es, scene);
        }
    }
}
