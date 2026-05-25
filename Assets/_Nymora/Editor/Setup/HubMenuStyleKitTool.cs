using Nymora.Hub.Menu;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// M0 — Outil de setup du kit de style du nouveau menu hub.
    ///
    /// "Create Theme + Style Preview" :
    ///   1. Crée (ou charge) l'asset HubMenuTheme + assigne les fonts Ari W9500 SDF.
    ///   2. Génère un canvas "MenuStylePreview" dans la scène ouverte avec un exemplaire
    ///      de chaque widget (barre d'onglets + 4 cartes + 2 boutons + hint) pour valider
    ///      le rendu à l'œil AVANT de construire les écrans M1..M8.
    ///
    /// "Remove Style Preview" : supprime le canvas de preview (rien d'autre).
    ///
    /// Idempotent : relançable sans dupliquer le thème ni le preview.
    /// </summary>
    public static class HubMenuStyleKitTool
    {
        private const string ThemeDir = "Assets/_Nymora/ScriptableObjects/Settings";
        private const string ThemePath = ThemeDir + "/HubMenuTheme.asset";
        private const string FontPath = "Assets/_Nymora/Art/Fonts/Ari W9500 SDF.asset";
        private const string FontBoldPath = "Assets/_Nymora/Art/Fonts/Ari W9500 Bold SDF.asset";
        private const string PreviewName = "MenuStylePreview";

        [MenuItem("Nymora/Setup/UI Menu/Create Theme + Style Preview")]
        public static void Run()
        {
            var theme = CreateOrLoadTheme();
            BuildPreview(theme);
            Debug.Log("[HubMenuStyleKit] Thème + preview prêts. Regarde le canvas 'MenuStylePreview' dans la Game view (ScreenSpace-Overlay).");
        }

        [MenuItem("Nymora/Setup/UI Menu/Remove Style Preview")]
        public static void RemovePreview()
        {
            var existing = GameObject.Find(PreviewName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
                Debug.Log("[HubMenuStyleKit] Preview supprimé.");
            }
        }

        private static HubMenuTheme CreateOrLoadTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<HubMenuTheme>(ThemePath);
            if (theme == null)
            {
                if (!AssetDatabase.IsValidFolder(ThemeDir))
                    System.IO.Directory.CreateDirectory(ThemeDir);
                theme = ScriptableObject.CreateInstance<HubMenuTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
                Debug.Log("[HubMenuStyleKit] Asset thème créé : " + ThemePath);
            }

            if (theme.Font == null) theme.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (theme.FontBold == null) theme.FontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontBoldPath);
            if (theme.Font == null) Debug.LogWarning("[HubMenuStyleKit] Ari W9500 SDF introuvable à " + FontPath + " — assigne-la à la main sur l'asset thème.");
            if (theme.FontBold == null) Debug.LogWarning("[HubMenuStyleKit] Ari W9500 Bold SDF introuvable à " + FontBoldPath + " — fallback sur Font.");

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            return theme;
        }

        private static void BuildPreview(HubMenuTheme theme)
        {
            RemovePreview();
            var f = new HubMenuUIFactory(theme);

            var canvasGo = new GameObject(PreviewName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Backdrop assombri plein écran
            var backdrop = f.MakeImage("Backdrop", canvasGo.transform, theme.Backdrop, rounded: false);
            HubMenuUIFactory.Stretch(backdrop.rectTransform);

            // Barre d'onglets (haut, centrée) — icône + label empilés
            var bar = f.MakeRect("TabBar", canvasGo.transform);
            bar.anchorMin = new Vector2(0.5f, 1f); bar.anchorMax = new Vector2(0.5f, 1f); bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -22f);
            bar.sizeDelta = new Vector2(960f, theme.TabHeight);
            var barLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            barLayout.spacing = theme.TabSpacing;
            barLayout.childAlignment = TextAnchor.MiddleCenter;
            barLayout.childControlWidth = true; barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = false; barLayout.childForceExpandHeight = false;
            string[] tabs = { "Social", "Progression", "Paramètres", "Report bug", "Déconnexion" };
            for (int i = 0; i < tabs.Length; i++)
            {
                f.MakeTabButton(bar, tabs[i], out var lbl, out var ico);
                f.SetTabActive(lbl, ico, i == 0); // "Social" actif en démo
            }

            // Trait de séparation pleine largeur sous la barre (comme la réf)
            var divider = f.MakeDivider(canvasGo.transform);
            var drt = divider.rectTransform;
            drt.anchorMin = new Vector2(0f, 1f); drt.anchorMax = new Vector2(1f, 1f); drt.pivot = new Vector2(0.5f, 1f);
            drt.sizeDelta = new Vector2(-120f, 1.5f);
            drt.anchoredPosition = new Vector2(0f, -(22f + theme.TabHeight + 6f));

            // Rangée de 4 cartes (centre)
            var row = f.MakeRect("CardsRow", canvasGo.transform);
            row.anchorMin = new Vector2(0.5f, 0.5f); row.anchorMax = new Vector2(0.5f, 0.5f); row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = Vector2.zero;
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = theme.CardSpacing;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = true; rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false; rowLayout.childForceExpandHeight = false;
            var fit = row.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            f.MakeCard(row, "Arène", "Affronte d'autres joueurs", out _);
            f.MakeCard(row, "Personnage", "Classe, deck, cosmétiques", out _);
            f.MakeCard(row, "Battle Pass", "Progresse et débloque", out _);
            f.MakeCard(row, "Boutique", "Skins et cosmétiques", out _);

            // Boutons démo (bas)
            var btnRow = f.MakeRect("ButtonsRow", canvasGo.transform);
            btnRow.anchorMin = new Vector2(0.5f, 0f); btnRow.anchorMax = new Vector2(0.5f, 0f); btnRow.pivot = new Vector2(0.5f, 0f);
            btnRow.anchoredPosition = new Vector2(0f, 72f);
            btnRow.sizeDelta = new Vector2(440f, 42f);
            var bl = btnRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            bl.spacing = 16f; bl.childAlignment = TextAnchor.MiddleCenter;
            bl.childControlWidth = true; bl.childControlHeight = true;
            bl.childForceExpandWidth = true; bl.childForceExpandHeight = true;
            f.MakeButton(btnRow, "Bouton principal", true, out _);
            f.MakeButton(btnRow, "Bouton secondaire", false, out _);

            // Hint bas
            var hint = f.MakeText("Hint", canvasGo.transform, "Échap   Fermer", theme.FontSizeSmall, theme.TextMuted, theme.Font, TextAlignmentOptions.Center);
            var hrt = hint.rectTransform;
            hrt.anchorMin = new Vector2(0.5f, 0f); hrt.anchorMax = new Vector2(0.5f, 0f); hrt.pivot = new Vector2(0.5f, 0f);
            hrt.anchoredPosition = new Vector2(0f, 24f);
            hrt.sizeDelta = new Vector2(400f, theme.HintBarHeight);

            EnsureEventSystem();
            Selection.activeGameObject = canvasGo;
            EditorUtility.SetDirty(canvasGo);
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
