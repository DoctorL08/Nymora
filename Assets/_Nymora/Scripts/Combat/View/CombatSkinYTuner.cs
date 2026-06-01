// Outil dev : Éditeur uniquement. Raccourci F10 pour ouvrir/fermer en Play Mode (scène COMBAT).
// Re-régler X / Y / scale (par phase) de l'apparence de combat LOCALE en Play Mode. Cible AUTO :
//   - si un skin cosmétique est équipé -> règle la CosmeticSkinDefinition du skin,
//   - sinon -> règle la classe de BASE (NymoraClassDefinition).
// Chaque cible a ses propres valeurs ; "Sauvegarder" écrit dans l'asset correspondant.
#if UNITY_EDITOR
using Nymora.Core.Data;
using Nymora.Core.Enums;
using Nymora.Core.ScriptableObjects;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Brique 5.12 — Panneau de réglage LIVE (X / Y / scale, par phase) de l'apparence de combat.
    /// Cible automatiquement ce que porte le joueur local : le skin cosmétique équipé
    /// (CosmeticSkinDefinition) OU, à défaut, la classe de base (NymoraClassDefinition). Les deux
    /// types portent les mêmes champs de calibration combat (StageXCombat{X,Y}Offset + CombatVisualScale),
    /// donc un réglage distinct et persistant pour chacun.
    ///
    /// Le sélecteur de phase force le stage affiché (0/1/2) pour recaler une phase sans avoir à
    /// atteindre la ressource correspondante ; "Auto" rend la main au stage piloté par la ressource.
    ///
    /// ⚠️ N'a d'effet visuel que sur les prefabs dont le sprite est sur un child "Visual"
    /// (Colossar / Necram / Ghostra). Les prefabs mono-GO (Soulrender / Nightseer) ignorent l'offset.
    /// ⚠️ Le réglage de BASE exige que les 5 NymoraClassDefinition soient drag sur le CombatantRenderer.
    ///
    /// Outil dev uniquement (Éditeur). Raccourci F10 (ouvre/ferme). Scène de combat uniquement.
    /// </summary>
    public sealed class CombatSkinYTuner : MonoBehaviour
    {
        private const float Range = 1.5f;
        private const string CatalogResourcePath = "Cosmetics/CosmeticSkinCatalog";

        private bool _open;
        private Rect _window = new Rect(360f, 20f, 340f, 580f);
        private static readonly int WindowId = "CombatSkinYTuner".GetHashCode();

        // Phase forcée en preview : -1 = Auto (stage selon ressource), 0/1/2 = phase forcée.
        private int _previewStage = -1;

        private CosmeticSkinCatalog _catalog;
        private bool _catalogLoaded;
        private CombatantRenderer _renderer;

        // Jeu de 7 valeurs de calibration combat, commun au skin et à la classe de base.
        private struct Calib { public float X0, X1, X2, Y0, Y1, Y2, Scale; }

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
            if (!InCombatScene()) return;
            if (Input.GetKeyDown(KeyCode.F10))
            {
                _open = !_open;
                // Fermer l'outil libère la phase forcée (sinon les persos resteraient bloqués
                // sur un stage de preview une fois le panneau caché).
                if (!_open && _previewStage != -1) SetPreviewStage(-1);
            }
        }

        // ====================== Résolution de la cible (auto) ======================

        private CosmeticSkinCatalog Catalog()
        {
            if (!_catalogLoaded) { _catalog = Resources.Load<CosmeticSkinCatalog>(CatalogResourcePath); _catalogLoaded = true; }
            return _catalog;
        }

        /// <summary>Skin cosmétique local équipé (ou null si base).</summary>
        private CosmeticSkinDefinition LocalSkinDef()
        {
            var cat = Catalog();
            if (cat == null) return null;
            string id = CombatCosmeticsContext.LocalSkinId;
            return string.IsNullOrEmpty(id) ? null : cat.Resolve(id);
        }

        private static NymoraClass LocalClass()
        {
            return System.Enum.TryParse(SelectedClassPreferences.Get(), out NymoraClass c) ? c : NymoraClass.None;
        }

        private NymoraClassDefinition LocalClassDef()
        {
            if (_renderer == null) _renderer = Object.FindFirstObjectByType<CombatantRenderer>();
            if (_renderer == null) return null;
            return _renderer.FindClassDef(LocalClass());
        }

        /// <summary>Un CombatantView est-il la cible courante (skin équipé OU classe de base locale) ?</summary>
        private static bool IsTargetView(CombatantView v, CosmeticSkinDefinition skinDef, NymoraClass localClass)
        {
            if (v == null) return false;
            // v.Class est Quantum.NymoraClass, localClass est Core.Enums.NymoraClass : mêmes
            // valeurs, on compare via (byte) (cf convention TimelineView).
            return skinDef != null
                ? v.AppliedSkinId == skinDef.CosmeticId
                : string.IsNullOrEmpty(v.AppliedSkinId) && (byte)v.Class == (byte)localClass;
        }

        /// <summary>Force (0/1/2) ou libère (-1) la phase de preview sur les combattants cibles.</summary>
        private void SetPreviewStage(int stage)
        {
            _previewStage = stage;
            var skinDef = LocalSkinDef();
            var localClass = LocalClass();
            foreach (var v in Object.FindObjectsByType<CombatantView>(FindObjectsSortMode.None))
            {
                if (!IsTargetView(v, skinDef, localClass)) continue;
                v.SetPreviewStageOverride(stage);
            }
        }

        // ====================== UI ======================

        private void OnGUI()
        {
            if (!InCombatScene()) return;
            if (!_open) return;
            _window = GUILayout.Window(WindowId, _window, DrawWindow, "Réglage apparence combat (skin / base)");
        }

        private void DrawWindow(int id)
        {
            var skinDef = LocalSkinDef();
            bool isBase = skinDef == null;
            var localClass = LocalClass();
            NymoraClassDefinition classDef = isBase ? LocalClassDef() : null;

            if (isBase && classDef == null)
            {
                GUILayout.Label("Cible = classe de BASE (aucun skin équipé).");
                GUILayout.Label($"Classe locale : {localClass}.", Rich());
                GUILayout.Label("⚠ NymoraClassDefinition introuvable : drag les 5 class defs\nsur le CombatantRenderer de la scène.", Warn());
                if (GUILayout.Button("Fermer")) _open = false;
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
                return;
            }

            Object targetAsset = isBase ? (Object)classDef : skinDef;
            string title = isBase
                ? $"BASE — {(string.IsNullOrEmpty(classDef.DisplayName) ? classDef.ClassId.ToString() : classDef.DisplayName)}"
                : $"SKIN — {(string.IsNullOrEmpty(skinDef.DisplayName) ? skinDef.CosmeticId : skinDef.DisplayName)} ({skinDef.ClassId})";
            GUILayout.Label($"Cible : <b>{title}</b>", Rich());

            // Lit la calibration courante depuis l'asset cible.
            Calib c = isBase ? ReadClass(classDef) : ReadSkin(skinDef);

            // Applique en live aux combattants cibles + détecte mono-GO.
            bool anyMatch = false, anyMonoGo = false;
            foreach (var v in Object.FindObjectsByType<CombatantView>(FindObjectsSortMode.None))
            {
                if (!IsTargetView(v, skinDef, localClass)) continue;
                anyMatch = true;
                if (!v.SpriteOnChild) anyMonoGo = true;
                v.SetCombatXOffsets(c.X0, c.X1, c.X2);
                v.SetCombatYOffsets(c.Y0, c.Y1, c.Y2);
                v.SetCombatVisualScale(c.Scale);
            }

            // Sélecteur de phase (force le stage affiché).
            GUILayout.Space(4f);
            GUILayout.Label("<b>Phase affichée (preview)</b>", Rich());
            GUILayout.BeginHorizontal();
            if (PhaseToggle("Auto", _previewStage == -1)) SetPreviewStage(-1);
            if (PhaseToggle("Phase 0", _previewStage == 0)) SetPreviewStage(0);
            if (PhaseToggle("Phase 1", _previewStage == 1)) SetPreviewStage(1);
            if (PhaseToggle("Phase 2", _previewStage == 2)) SetPreviewStage(2);
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label("<b>X par phase</b>", Rich());
            c.X0 = SliderRow("Phase 0  X", c.X0);
            c.X1 = SliderRow("Phase 1  X", c.X1);
            c.X2 = SliderRow("Phase 2  X", c.X2);

            GUILayout.Space(4f);
            GUILayout.Label("<b>Y par phase</b>", Rich());
            c.Y0 = SliderRow("Phase 0  Y", c.Y0);
            c.Y1 = SliderRow("Phase 1  Y", c.Y1);
            c.Y2 = SliderRow("Phase 2  Y", c.Y2);

            GUILayout.Space(4f);
            GUILayout.Label("<b>Taille</b>", Rich());
            c.Scale = ScaleSliderRow("Scale", c.Scale);

            // Réécrit la calibration éditée dans l'asset cible.
            if (isBase) WriteClass(classDef, c); else WriteSkin(skinDef, c);

            // Leurres (Ghostra) : X / Y / scale. Portés par le SKIN équipé si présent, sinon par la
            // classe de BASE (NymoraClassDefinition). N'a de sens que pour Ghostra → masqué sinon.
            bool isGhostra = isBase ? classDef.ClassId == NymoraClass.Ghostra
                                    : skinDef.ClassId == NymoraClass.Ghostra;
            if (isGhostra)
            {
                GUILayout.Space(4f);
                GUILayout.Label("<b>Leurres (Ghostra)</b>", Rich());
                if (isBase)
                {
                    classDef.DecoyCombatXOffset = SliderRow("Leurre  X", classDef.DecoyCombatXOffset);
                    classDef.DecoyCombatYOffset = SliderRow("Leurre  Y", classDef.DecoyCombatYOffset);
                    classDef.DecoyCombatScale   = ScaleSliderRow("Leurre  scale", classDef.DecoyCombatScale);
                }
                else
                {
                    skinDef.DecoyCombatXOffset = SliderRow("Leurre  X", skinDef.DecoyCombatXOffset);
                    skinDef.DecoyCombatYOffset = SliderRow("Leurre  Y", skinDef.DecoyCombatYOffset);
                    skinDef.DecoyCombatScale   = ScaleSliderRow("Leurre  scale", skinDef.DecoyCombatScale);
                }
            }

            GUILayout.Space(4f);
            if (!anyMatch)
                GUILayout.Label("⚠ Aucun combattant cible en scène.", Warn());
            else if (anyMonoGo)
                GUILayout.Label("⚠ Prefab mono-GO (sprite sur le root) : offset ignoré.\nDemande la restructure 'Visual child' pour cette classe.", Warn());
            else
                GUILayout.Label("Recale la phase affichée (les autres s'appliqueront en y montant).");

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sauvegarder")) Save(targetAsset, isBase);
            if (GUILayout.Button("Remettre à 0"))
            {
                var z = new Calib { Scale = 1f };
                if (isBase)
                {
                    WriteClass(classDef, z);
                    classDef.DecoyCombatScale = 1f; classDef.DecoyCombatXOffset = 0f; classDef.DecoyCombatYOffset = 0f;
                }
                else
                {
                    WriteSkin(skinDef, z);
                    skinDef.DecoyCombatScale = 1f; skinDef.DecoyCombatXOffset = 0f; skinDef.DecoyCombatYOffset = 0f;
                }
            }
            if (GUILayout.Button("Fermer")) _open = false;
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        // ====================== Read/Write calibration ======================

        private static Calib ReadSkin(CosmeticSkinDefinition d) => new Calib
        {
            X0 = d.Stage0CombatXOffset, X1 = d.Stage1CombatXOffset, X2 = d.Stage2CombatXOffset,
            Y0 = d.Stage0CombatYOffset, Y1 = d.Stage1CombatYOffset, Y2 = d.Stage2CombatYOffset,
            Scale = d.CombatVisualScale,
        };

        private static void WriteSkin(CosmeticSkinDefinition d, Calib c)
        {
            d.Stage0CombatXOffset = c.X0; d.Stage1CombatXOffset = c.X1; d.Stage2CombatXOffset = c.X2;
            d.Stage0CombatYOffset = c.Y0; d.Stage1CombatYOffset = c.Y1; d.Stage2CombatYOffset = c.Y2;
            d.CombatVisualScale = c.Scale;
        }

        private static Calib ReadClass(NymoraClassDefinition d) => new Calib
        {
            X0 = d.Stage0CombatXOffset, X1 = d.Stage1CombatXOffset, X2 = d.Stage2CombatXOffset,
            Y0 = d.Stage0CombatYOffset, Y1 = d.Stage1CombatYOffset, Y2 = d.Stage2CombatYOffset,
            Scale = d.CombatVisualScale,
        };

        private static void WriteClass(NymoraClassDefinition d, Calib c)
        {
            d.Stage0CombatXOffset = c.X0; d.Stage1CombatXOffset = c.X1; d.Stage2CombatXOffset = c.X2;
            d.Stage0CombatYOffset = c.Y0; d.Stage1CombatYOffset = c.Y1; d.Stage2CombatYOffset = c.Y2;
            d.CombatVisualScale = c.Scale;
        }

        // ====================== Helpers UI ======================

        private static bool PhaseToggle(string label, bool active)
        {
            var style = active ? GUI.skin.box : GUI.skin.button;
            bool clicked = GUILayout.Button(label, style, GUILayout.Height(22f));
            return clicked && !active;
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
            Debug.Log($"[CombatSkinYTuner] Sauvé {(isBase ? "classe de base" : "skin")} : {targetAsset.name}");
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
