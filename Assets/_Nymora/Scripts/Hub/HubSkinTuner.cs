// Outil dev : Éditeur uniquement. Raccourci F10 pour ouvrir/fermer en Play Mode (scène HUB).
// Pendant hub du CombatSkinYTuner. Cible AUTO l'apparence de l'avatar hub local :
//   - skin cosmétique équipé -> règle la CosmeticSkinDefinition,
//   - sinon -> règle la classe de BASE (NymoraClassDefinition).
// En hub il n'y a qu'un seul "stage" -> un seul jeu X / Y / scale (pas de phases). Les valeurs
// persistent dans l'asset cible (HubVisualXOffset / HubVisualYOffset / HubVisualScale).
#if UNITY_EDITOR
using Nymora.Core.ScriptableObjects;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace Nymora.Hub
{
    /// <summary>
    /// Tuner dev (Éditeur) de l'apparence en HUB. Symétrique du CombatSkinYTuner mais :
    ///   - cible l'avatar HUB local (HubAvatar.Local),
    ///   - PAS de phases (le hub n'a qu'un stage) → un seul jeu X / Y / scale,
    ///   - cible AUTO : skin équipé (CosmeticSkinDefinition) sinon classe de base (NymoraClassDefinition).
    /// Les deux types portent HubVisual{X,Y}Offset + HubVisualScale → réglage distinct par cible.
    ///
    /// Auto-instancié, actif uniquement dans une scène dont le nom contient "Hub". F10 ouvre/ferme.
    /// N'a d'effet visuel que si le sprite de l'avatar est sur un child "Visual" (prefab restructuré).
    /// </summary>
    public sealed class HubSkinTuner : MonoBehaviour
    {
        private const float Range = 1.5f;

        private bool _open;
        private Rect _window = new Rect(360f, 20f, 340f, 320f);
        private static readonly int WindowId = "HubSkinTuner".GetHashCode();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("HubSkinTuner");
            go.AddComponent<HubSkinTuner>();
            DontDestroyOnLoad(go);
        }

        private static bool InHubScene()
        {
            var name = SceneManager.GetActiveScene().name;
            return !string.IsNullOrEmpty(name) && name.Contains("Hub");
        }

        private void Update()
        {
            if (!InHubScene()) return;
            if (Input.GetKeyDown(KeyCode.F10)) _open = !_open;
        }

        private void OnGUI()
        {
            if (!InHubScene()) return;
            if (!_open) return;
            _window = GUILayout.Window(WindowId, _window, DrawWindow, "Réglage apparence hub (skin / base)");
        }

        private void DrawWindow(int id)
        {
            var local = HubAvatar.Local;
            if (local == null)
            {
                GUILayout.Label("Avatar hub local introuvable.");
                if (GUILayout.Button("Fermer")) _open = false;
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
                return;
            }

            var skinDef = local.CurrentSkinDefinition;       // null = base
            var classDef = local.CurrentClassDefinition;
            bool isBase = skinDef == null;

            if (isBase && classDef == null)
            {
                GUILayout.Label("Classe locale non résolue (visual pas encore appliqué).");
                if (GUILayout.Button("Fermer")) _open = false;
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
                return;
            }

            Object targetAsset = isBase ? (Object)classDef : skinDef;
            string title = isBase
                ? $"BASE — {(string.IsNullOrEmpty(classDef.DisplayName) ? classDef.ClassId.ToString() : classDef.DisplayName)}"
                : $"SKIN — {(string.IsNullOrEmpty(skinDef.DisplayName) ? skinDef.CosmeticId : skinDef.DisplayName)} ({skinDef.ClassId})";
            GUILayout.Label($"Cible : <b>{title}</b>", Rich());
            GUILayout.Label("Hub = stage unique (pas de phases).");

            // Lit les 3 valeurs hub depuis la cible (mêmes noms de champs des 2 côtés).
            float x = isBase ? classDef.HubVisualXOffset : skinDef.HubVisualXOffset;
            float y = isBase ? classDef.HubVisualYOffset : skinDef.HubVisualYOffset;
            float scale = isBase ? classDef.HubVisualScale : skinDef.HubVisualScale;

            // Applique en live sur l'avatar hub local.
            bool monoGo = !local.VisualOnChild;
            local.SetHubVisualCalibration(x, y, scale);

            GUILayout.Space(4f);
            x = SliderRow("X", x);
            y = SliderRow("Y", y);
            scale = ScaleSliderRow("Scale", scale);

            // Réécrit dans la cible.
            if (isBase) { classDef.HubVisualXOffset = x; classDef.HubVisualYOffset = y; classDef.HubVisualScale = scale; }
            else { skinDef.HubVisualXOffset = x; skinDef.HubVisualYOffset = y; skinDef.HubVisualScale = scale; }

            GUILayout.Space(4f);
            if (monoGo)
                GUILayout.Label("⚠ Prefab mono-GO (sprite sur le root) : offset ignoré.\nDemande la restructure 'Visual child' pour le hub.", Warn());

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sauvegarder")) Save(targetAsset, isBase);
            if (GUILayout.Button("Remettre à 0"))
            {
                if (isBase) { classDef.HubVisualXOffset = 0f; classDef.HubVisualYOffset = 0f; classDef.HubVisualScale = 1f; }
                else { skinDef.HubVisualXOffset = 0f; skinDef.HubVisualYOffset = 0f; skinDef.HubVisualScale = 1f; }
            }
            if (GUILayout.Button("Fermer")) _open = false;
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private static float SliderRow(string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(78f));
            value = GUILayout.HorizontalSlider(value, -Range, Range);
            GUILayout.Label(value.ToString("0.000"), GUILayout.Width(54f));
            GUILayout.EndHorizontal();
            return value;
        }

        private static float ScaleSliderRow(string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(78f));
            value = GUILayout.HorizontalSlider(value, 0.2f, 2.5f);
            GUILayout.Label(value.ToString("0.000"), GUILayout.Width(54f));
            GUILayout.EndHorizontal();
            return value;
        }

        private static void Save(Object targetAsset, bool isBase)
        {
            if (targetAsset == null) return;
            EditorUtility.SetDirty(targetAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HubSkinTuner] Sauvé {(isBase ? "classe de base" : "skin")} (hub) : {targetAsset.name}");
        }

        private static GUIStyle Rich()
        {
            return new GUIStyle(GUI.skin.label) { richText = true };
        }

        private static GUIStyle Warn()
        {
            var s = new GUIStyle(GUI.skin.label) { wordWrap = true };
            s.normal.textColor = new Color(1f, 0.78f, 0.35f);
            return s;
        }
    }
}
#endif
