// Outil dev : Éditeur uniquement (F10). Re-régler le Y des skins de combat en Play Mode ;
// les valeurs persistent dans la CosmeticSkinDefinition (Stage0/1/2CombatYOffset).
#if UNITY_EDITOR
using Nymora.Core.Data;
using Nymora.Core.ScriptableObjects;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Brique 5.12 — Panneau de réglage LIVE du Y des skins de combat. Pendant à
    /// CombatPetPlacementTuner mais pour les champs StageXCombatYOffset de la CosmeticSkinDefinition
    /// du skin LOCAL équipé. Tu bouges les curseurs → le combattant se recale en direct (sans reset
    /// d'animation), puis "Sauvegarder" écrit les offsets dans l'asset du skin.
    ///
    /// ⚠️ N'a d'effet visuel que sur les prefabs combattants dont le sprite est sur un child "Visual"
    /// (Colossar / Necram / Ghostra). Les prefabs mono-GO (Soulrender / Nightseer) ignorent l'offset
    /// (cf CombatantView.SetStageAndFacing) → le panneau le signale.
    ///
    /// Rappel : équiper un skin remet les offsets de la classe à ceux du skin (0 par défaut) — d'où
    /// le besoin de re-régler par skin. Tune le stage où se trouve actuellement ton perso (les autres
    /// stages s'appliqueront quand il y montera).
    ///
    /// Outil dev uniquement (Éditeur). Touche bascule : F10. Scène de combat uniquement.
    /// </summary>
    public sealed class CombatSkinYTuner : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.F10;
        private const float Range = 1.5f;
        private const string CatalogResourcePath = "Cosmetics/CosmeticSkinCatalog";

        private bool _open;
        private Rect _window = new Rect(360f, 20f, 340f, 440f);
        private static readonly int WindowId = "CombatSkinYTuner".GetHashCode();

        private CosmeticSkinCatalog _catalog;
        private bool _catalogLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("CombatSkinYTuner");
            go.AddComponent<CombatSkinYTuner>();
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

        private CosmeticSkinDefinition LocalSkinDef()
        {
            if (!_catalogLoaded) { _catalog = Resources.Load<CosmeticSkinCatalog>(CatalogResourcePath); _catalogLoaded = true; }
            if (_catalog == null) return null;
            string id = CombatCosmeticsContext.LocalSkinId;
            return string.IsNullOrEmpty(id) ? null : _catalog.Resolve(id);
        }

        private void OnGUI()
        {
            if (!InCombatScene()) return;

            if (!_open)
            {
                GUI.Label(new Rect(20f, Screen.height - 48f, 480f, 22f), "<b>F10</b> : réglage skin/leurres (Y + taille)");
                return;
            }
            _window = GUILayout.Window(WindowId, _window, DrawWindow, "Réglage skin/leurres (combat)");
        }

        private void DrawWindow(int id)
        {
            var def = LocalSkinDef();
            if (def == null)
            {
                GUILayout.Label("Aucun skin local équipé (ou catalogue absent).");
                GUILayout.Label("Équipe un skin, relance Build Cosmetic Skin Catalog.");
                if (GUILayout.Button("Fermer")) _open = false;
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
                return;
            }

            GUILayout.Label($"Skin : <b>{(string.IsNullOrEmpty(def.DisplayName) ? def.CosmeticId : def.DisplayName)}</b> ({def.ClassId})", Rich());

            // Applique aux combattants en scène portant CE skin + détecte le cas mono-GO.
            bool anyMatch = false, anyMonoGo = false;
            foreach (var v in Object.FindObjectsByType<CombatantView>(FindObjectsSortMode.None))
            {
                if (v == null || v.AppliedSkinId != def.CosmeticId) continue;
                anyMatch = true;
                if (!v.SpriteOnChild) anyMonoGo = true;
                v.SetCombatYOffsets(def.Stage0CombatYOffset, def.Stage1CombatYOffset, def.Stage2CombatYOffset);
                v.SetCombatVisualScale(def.CombatVisualScale);
            }

            GUILayout.Space(4f);
            GUILayout.Label("<b>Skin — Y par stage</b>", Rich());
            def.Stage0CombatYOffset = SliderRow("Stage 0  Y", def.Stage0CombatYOffset);
            def.Stage1CombatYOffset = SliderRow("Stage 1  Y", def.Stage1CombatYOffset);
            def.Stage2CombatYOffset = SliderRow("Stage 2  Y", def.Stage2CombatYOffset);

            GUILayout.Space(4f);
            GUILayout.Label("<b>Skin — taille</b>", Rich());
            def.CombatVisualScale = ScaleSliderRow("Skin  scale", def.CombatVisualScale);

            GUILayout.Space(4f);
            GUILayout.Label("<b>Leurres (Ghostra)</b>", Rich());
            // Lus chaque frame par DecoyView → effet live, pas d'appel direct nécessaire ici.
            def.DecoyCombatYOffset = SliderRow("Leurre  Y", def.DecoyCombatYOffset);
            def.DecoyCombatScale = ScaleSliderRow("Leurre  scale", def.DecoyCombatScale);

            GUILayout.Space(4f);
            if (!anyMatch)
                GUILayout.Label("⚠ Aucun combattant avec ce skin en scène.", Warn());
            else if (anyMonoGo)
                GUILayout.Label("⚠ Prefab mono-GO (sprite sur le root) : offset ignoré.\nDemande la restructure 'Visual child' pour cette classe.", Warn());
            else
                GUILayout.Label("Tune le stage courant (les autres s'appliqueront en montant de stage).");

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sauvegarder")) Save(def);
            if (GUILayout.Button("Remettre à 0"))
            {
                def.Stage0CombatYOffset = def.Stage1CombatYOffset = def.Stage2CombatYOffset = 0f;
                def.CombatVisualScale = 1f;
                def.DecoyCombatScale = 1f;
                def.DecoyCombatYOffset = 0f;
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

        // Slider d'échelle (multiplicateur), borné 0.2 → 2.5.
        private static float ScaleSliderRow(string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(78f));
            value = GUILayout.HorizontalSlider(value, 0.2f, 2.5f);
            GUILayout.Label(value.ToString("0.000"), GUILayout.Width(54f));
            GUILayout.EndHorizontal();
            return value;
        }

        private static void Save(CosmeticSkinDefinition def)
        {
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CombatSkinYTuner] Sauvé {def.CosmeticId} : " +
                      $"s0={def.Stage0CombatYOffset:0.000} s1={def.Stage1CombatYOffset:0.000} s2={def.Stage2CombatYOffset:0.000} " +
                      $"skinScale={def.CombatVisualScale:0.000} leurreY={def.DecoyCombatYOffset:0.000} leurreScale={def.DecoyCombatScale:0.000}");
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
