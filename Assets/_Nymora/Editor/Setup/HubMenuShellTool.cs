using Nymora.Core.ScriptableObjects;
using Nymora.Hub.Menu;
using Nymora.Network.Backend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// M1 — Pose (ou rafraîchit) la coquille du nouveau menu hub dans la scène ouverte.
    ///
    /// Crée un Canvas "HubMenuCanvas" (Overlay, sortingOrder 200) + le composant HubMenuShell
    /// + assigne l'asset HubMenuTheme. La coquille construit ensuite TOUTE son UI en code au
    /// Play (hamburger ☰, Échap, fond flou, onglets, 4 cartes, monnaies). À lancer sur
    /// 10_CommunityHub puis SAUVER la scène (composant persistant, contrairement au preview M0).
    ///
    /// Idempotent : relançable, ne duplique pas le canvas, ré-assigne le thème.
    /// </summary>
    public static class HubMenuShellTool
    {
        private const string ThemePath = "Assets/_Nymora/ScriptableObjects/Settings/HubMenuTheme.asset";
        private const string CanvasName = "HubMenuCanvas";
        private const string CardArtDir = "Assets/_Nymora/Art/UI/MenuCards";

        // id de carte -> nom de fichier (sans extension) dans CardArtDir
        private static readonly string[][] CardArtMap =
        {
            new[] { "arena", "ui_cadre_arene" },
            new[] { "character", "ui_cadre_personnage" },
            new[] { "battlepass", "ui_cadre_battlepass" },
            new[] { "shop", "ui_cadre_boutique" },
            new[] { "mode_2v2", "ui_cadre_2vs2" },
            new[] { "mode_3v3", "ui_cadre_3vs3" },
            new[] { "mode_training", "ui_image_colossar" },
            new[] { "mode_1v1", "ui_image_soulrender" },
        };

        [MenuItem("Nymora/Setup/UI Menu/Create or Refresh Menu Shell")]
        public static void Run()
        {
            var theme = AssetDatabase.LoadAssetAtPath<HubMenuTheme>(ThemePath);
            if (theme == null)
            {
                Debug.LogError("[HubMenuShell] Thème introuvable à " + ThemePath + " — lance d'abord 'Create Theme + Style Preview'.");
                return;
            }

            var existing = GameObject.Find(CanvasName);
            GameObject canvasGo;
            if (existing != null)
            {
                canvasGo = existing;
            }
            else
            {
                canvasGo = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            }

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // au-dessus des canvas hub existants (boutons, chat, popups)

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var shell = canvasGo.GetComponent<HubMenuShell>();
            if (shell == null) shell = canvasGo.AddComponent<HubMenuShell>();

            // Assigne le thème + le backend (champs privés sérialisés)
            var so = new SerializedObject(shell);
            var themeProp = so.FindProperty("_theme");
            if (themeProp != null) themeProp.objectReferenceValue = theme;

            var settings = FindBackendSettings();
            var settingsProp = so.FindProperty("_backendSettings");
            if (settingsProp != null && settings != null) settingsProp.objectReferenceValue = settings;

            var defs = FindClassDefs();
            var defsProp = so.FindProperty("_classDefinitions");
            if (defsProp != null)
            {
                defsProp.arraySize = defs.Length;
                for (int i = 0; i < defs.Length; i++)
                    defsProp.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
            }

            var catalog = FindFirstAsset<SpellCatalog>();
            var catalogProp = so.FindProperty("_spellCatalog");
            if (catalogProp != null && catalog != null) catalogProp.objectReferenceValue = catalog;

            var skins = FindAllAssets<CosmeticSkinDefinition>();
            var skinsProp = so.FindProperty("_skinDefinitions");
            if (skinsProp != null)
            {
                skinsProp.arraySize = skins.Length;
                for (int i = 0; i < skins.Length; i++)
                    skinsProp.GetArrayElementAtIndex(i).objectReferenceValue = skins[i];
            }

            // Illustrations des cartes (import en Sprite + assignation)
            var artProp = so.FindProperty("_cardArt");
            if (artProp != null)
            {
                var ids = new System.Collections.Generic.List<string>();
                var sprites = new System.Collections.Generic.List<Sprite>();
                foreach (var pair in CardArtMap)
                {
                    var sp = LoadSpriteEnsureImport(CardArtDir + "/" + pair[1] + ".png");
                    if (sp != null) { ids.Add(pair[0]); sprites.Add(sp); }
                }
                artProp.arraySize = ids.Count;
                for (int i = 0; i < ids.Count; i++)
                {
                    var el = artProp.GetArrayElementAtIndex(i);
                    el.FindPropertyRelative("Id").stringValue = ids[i];
                    el.FindPropertyRelative("Sprite").objectReferenceValue = sprites[i];
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            if (catalog == null)
                Debug.LogWarning("[HubMenuShell] SpellCatalog introuvable — le deck builder du menu restera indisponible. Assigne-le à la main sur HubMenuCanvas.");
            if (settings == null)
                Debug.LogWarning("[HubMenuShell] NymoraBackendSettings introuvable — la barre XP de Personnage restera vide. Assigne-le à la main sur HubMenuCanvas.");
            if (defs.Length == 0)
                Debug.LogWarning("[HubMenuShell] Aucun NymoraClassDefinition trouvé — le portrait de classe restera vide.");

            EnsureEventSystem();

            EditorUtility.SetDirty(canvasGo);
            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Selection.activeGameObject = canvasGo;
            Debug.Log("[HubMenuShell] Coquille posée/rafraîchie sur '" + CanvasName + "'. SAUVE la scène, puis teste en Play (Échap ou hamburger ☰).");
        }

        private static NymoraBackendSettings FindBackendSettings()
        {
            var guids = AssetDatabase.FindAssets("t:NymoraBackendSettings");
            if (guids == null || guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<NymoraBackendSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static T FindFirstAsset<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids == null || guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static NymoraClassDefinition[] FindClassDefs() => FindAllAssets<NymoraClassDefinition>();

        private static Sprite LoadSpriteEnsureImport(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return null;
            if (imp.textureType != TextureImporterType.Sprite)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static T[] FindAllAssets<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            var list = new System.Collections.Generic.List<T>();
            if (guids != null)
                foreach (var g in guids)
                {
                    var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g));
                    if (a != null) list.Add(a);
                }
            return list.ToArray();
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
        }
    }
}
