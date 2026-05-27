#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Nymora.Core.ScriptableObjects;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace Nymora.Combat.View
{
    /// <summary>
    /// Brique 5.10 (B5) — Panneau de réglage LIVE du placement du familier EN COMBAT. Pendant à
    /// HubPetPlacementTuner mais pour les champs Combat* de PetPlacementConfig (offset par
    /// direction + taille). Tu bouges les curseurs, le familier se replace en direct (les valeurs
    /// mutent PetPlacementConfig.Instance, relu chaque frame par CombatPetView).
    ///
    /// Outil dev uniquement (Éditeur / Development Build). Touche bascule : F9. Ne s'affiche que
    /// dans une scène de combat. Le bloc de la direction courante du familier LOCAL est surligné.
    /// </summary>
    public sealed class CombatPetPlacementTuner : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.F9;
        private const float OffsetRange = 2f;

        private bool _open;
        private Rect _window = new Rect(20f, 20f, 320f, 470f);
        private static readonly int WindowId = "CombatPetPlacementTuner".GetHashCode();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("CombatPetPlacementTuner");
            go.AddComponent<CombatPetPlacementTuner>();
            DontDestroyOnLoad(go);
        }

        private static bool InCombatScene()
        {
            var name = SceneManager.GetActiveScene().name;
            return !string.IsNullOrEmpty(name) && name.Contains("Combat");
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey)) _open = !_open;
        }

        private void OnGUI()
        {
            if (!InCombatScene()) return;

            if (!_open)
            {
                GUI.Label(new Rect(20f, Screen.height - 28f, 360f, 22f),
                          "<b>F9</b> : réglage familier (combat)");
                return;
            }
            _window = GUILayout.Window(WindowId, _window, DrawWindow, "Réglage familier (combat)");
        }

        private void DrawWindow(int id)
        {
            var cfg = PetPlacementConfig.Instance;
            int facing = CombatPetView.LastLocalFacingIndex; // View IsoFacing : NE=0, SE=1, NW=2, SW=3

            cfg.CombatNeOffset = OffsetBlock("NE (haut-droite)", cfg.CombatNeOffset, facing == 0);
            cfg.CombatSeOffset = OffsetBlock("SE (bas-droite)", cfg.CombatSeOffset, facing == 1);
            cfg.CombatNwOffset = OffsetBlock("NW (haut-gauche)", cfg.CombatNwOffset, facing == 2);
            cfg.CombatSwOffset = OffsetBlock("SW (bas-gauche)", cfg.CombatSwOffset, facing == 3);

            GUILayout.Space(8f);
            GUILayout.Label("Taille (toutes directions)", Bold());
            cfg.CombatSizeFactor = SliderRow("×", cfg.CombatSizeFactor, 0.1f, 2f);

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
#if UNITY_EDITOR
            if (GUILayout.Button("Sauvegarder")) SaveToAsset(cfg);
#else
            GUILayout.Label("(sauvegarde dispo en Éditeur)");
#endif
            if (GUILayout.Button("Défauts")) ResetDefaults(cfg);
            if (GUILayout.Button("Fermer")) _open = false;
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private static Vector2 OffsetBlock(string title, Vector2 value, bool active)
        {
            GUILayout.Space(6f);
            GUILayout.Label(active ? $"▶ {title}" : title, Bold(active));
            value.x = SliderRow("X", value.x, -OffsetRange, OffsetRange);
            value.y = SliderRow("Y", value.y, -OffsetRange, OffsetRange);
            return value;
        }

        private static float SliderRow(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(24f));
            value = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.Label(value.ToString("0.00"), GUILayout.Width(48f));
            GUILayout.EndHorizontal();
            return value;
        }

        private static void ResetDefaults(PetPlacementConfig cfg)
        {
            cfg.CombatNeOffset = new Vector2(0.35f, -0.1f);
            cfg.CombatSeOffset = new Vector2(0.35f, -0.1f);
            cfg.CombatNwOffset = new Vector2(-0.35f, -0.1f);
            cfg.CombatSwOffset = new Vector2(-0.35f, -0.1f);
            cfg.CombatSizeFactor = 0.6f;
        }

        private static GUIStyle Bold(bool active = false)
        {
            var s = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            if (active) s.normal.textColor = new Color(0.45f, 0.85f, 1f);
            return s;
        }

#if UNITY_EDITOR
        private const string AssetDir = "Assets/_Nymora/Resources/config";
        private const string AssetPath = AssetDir + "/PetPlacementConfig.asset";

        private static void SaveToAsset(PetPlacementConfig runtime)
        {
            var asset = AssetDatabase.LoadAssetAtPath<PetPlacementConfig>(AssetPath);
            if (asset == null)
            {
                if (!Directory.Exists(AssetDir)) Directory.CreateDirectory(AssetDir);
                asset = ScriptableObject.CreateInstance<PetPlacementConfig>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            asset.CopyValuesFrom(runtime); // hub + combat -> n'écrase pas les réglages hub
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            PetPlacementConfig.SetInstance(asset);
            Debug.Log($"[CombatPetPlacementTuner] Sauvé dans {AssetPath} : NE={asset.CombatNeOffset} " +
                      $"SE={asset.CombatSeOffset} NW={asset.CombatNwOffset} SW={asset.CombatSwOffset} " +
                      $"taille={asset.CombatSizeFactor:0.00}");
        }
#endif
    }
}
#endif
