// Outil dev : Éditeur uniquement (F8 retiré des builds). Re-régler le placement via Play Mode
// dans l'éditeur ; les valeurs persistent dans PetPlacementConfig.asset.
#if UNITY_EDITOR
using Nymora.Core.ScriptableObjects;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 5.10 (B4 — reliquat placement) — Panneau de réglage LIVE du placement du familier
    /// dans le hub. Tu bouges les curseurs en Play Mode et le familier se déplace/redimensionne
    /// en direct (les valeurs mutent PetPlacementConfig.Instance, relu chaque frame par HubAvatar
    /// /HubPetView). Un bouton sauvegarde dans l'asset Resources pour que ce soit permanent.
    ///
    /// Un offset séparé par direction iso (SE/SW/NE/NW). Marche dans le hub pour changer
    /// d'orientation et régler chaque cas. Le bloc de la direction courante est mis en évidence.
    ///
    /// Outil dev uniquement (compilé seulement en Éditeur / Development Build, jamais en release).
    /// Touche bascule : F8. Auto-instancié au lancement, ne s'affiche que dans le hub (= quand un
    /// HubAvatar.Local existe avec un familier équipé).
    /// </summary>
    public sealed class HubPetPlacementTuner : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.F8;
        private const float OffsetRange = 2f;

        private bool _open;
        private Rect _window = new Rect(20f, 20f, 320f, 470f);
        private static readonly int WindowId = "HubPetPlacementTuner".GetHashCode();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("HubPetPlacementTuner");
            go.AddComponent<HubPetPlacementTuner>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey)) _open = !_open;
        }

        private void OnGUI()
        {
            // Rien à régler hors du hub (pas d'avatar local).
            if (HubAvatar.Local == null) return;

            if (!_open)
            {
                GUI.Label(new Rect(20f, Screen.height - 28f, 360f, 22f),
                          "<b>F8</b> : réglage familier");
                return;
            }
            _window = GUILayout.Window(WindowId, _window, DrawWindow, "Réglage familier (hub)");
        }

        private void DrawWindow(int id)
        {
            var cfg = PetPlacementConfig.Instance;
            int facing = HubAvatar.Local != null ? HubAvatar.Local.CurrentFacingIndex : -1;

            cfg.SeOffset = OffsetBlock("SE (bas-droite)", cfg.SeOffset, facing == 0);
            cfg.NeOffset = OffsetBlock("NE (haut-droite, dos)", cfg.NeOffset, facing == 1);
            cfg.SwOffset = OffsetBlock("SW (bas-gauche)", cfg.SwOffset, facing == 2);
            cfg.NwOffset = OffsetBlock("NW (haut-gauche, dos)", cfg.NwOffset, facing == 3);

            GUILayout.Space(8f);
            GUILayout.Label("Taille (toutes directions)", Bold());
            cfg.SizeFactor = SliderRow("×", cfg.SizeFactor, 0.1f, 2f);

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

        // Un bloc de réglage X/Y pour une direction. `active` = direction courante de l'avatar ->
        // titre surligné pour repérer ce qu'on règle en marchant dans le hub.
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
            var def = new Vector2(0.42f, -0.12f);
            cfg.SeOffset = def;
            cfg.SwOffset = def;
            cfg.NeOffset = def;
            cfg.NwOffset = def;
            cfg.SizeFactor = 0.65f;
        }

        private static GUIStyle Bold(bool active = false)
        {
            var s = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            if (active) s.normal.textColor = new Color(0.45f, 0.85f, 1f); // bleu = direction courante
            return s;
        }

#if UNITY_EDITOR
        private const string AssetDir = "Assets/_Nymora/Resources/config";
        private const string AssetPath = AssetDir + "/PetPlacementConfig.asset";

        // Persiste les valeurs courantes dans l'asset Resources (le crée si besoin) puis rebascule
        // l'instance globale dessus pour que les prochaines lectures viennent de l'asset.
        private static void SaveToAsset(PetPlacementConfig runtime)
        {
            var asset = AssetDatabase.LoadAssetAtPath<PetPlacementConfig>(AssetPath);
            if (asset == null)
            {
                if (!Directory.Exists(AssetDir)) Directory.CreateDirectory(AssetDir);
                asset = ScriptableObject.CreateInstance<PetPlacementConfig>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            asset.CopyValuesFrom(runtime);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            PetPlacementConfig.SetInstance(asset);
            Debug.Log($"[HubPetPlacementTuner] Sauvé dans {AssetPath} : SE={asset.SeOffset} " +
                      $"SW={asset.SwOffset} NE={asset.NeOffset} NW={asset.NwOffset} " +
                      $"taille={asset.SizeFactor:0.00}");
        }
#endif
    }
}
#endif
