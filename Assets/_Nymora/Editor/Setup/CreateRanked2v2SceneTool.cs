using System;
using System.IO;
using Quantum;        // AssetGuid.NewGuid()
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 5.5c (2v2/3v3) — Crée la scène 41_CombatRanked2v2 + sa QuantumMap DÉDIÉE.
    ///
    /// Base = 30_CombatIA.unity (déjà en mode Local single-client, comme le hot-seat 2v2). La scène
    /// clonée garde grille / caméra / HUD / VFX ; on y branchera CombatBootstrap2v2 (manip guidée).
    ///
    /// ISOLATION (demande Lorenzo) : on crée une QuantumMap_2v2.asset DÉDIÉE (clone de QuantumMap_IA
    /// repointée vers la scène 41 + nouveau Guid Quantum unique) pour ne JAMAIS interférer avec la
    /// ranked 1v1 ni la scène IA. Le bootstrap 2v2 force runtimeConfig.Map = cette QuantumMap.
    ///
    /// Ce que le tool fait AUTOMATIQUEMENT (fiable) :
    ///   1. Backup 41_CombatRanked2v2.unity si déjà présent.
    ///   2. Clone 30_CombatIA.unity -> 41_CombatRanked2v2.unity.
    ///   3. Clone QuantumMap_IA.asset -> QuantumMap_2v2.asset, repointée vers la scène 41 + Guid unique.
    ///   4. Ajoute la scène aux Build Settings.
    ///
    /// Ce qui reste à câbler À LA MAIN (quelques clics, à vérifier à l'œil — cf dialog final) :
    ///   - Sur le GO du bootstrap : remplacer CombatBootstrapIA par CombatBootstrap2v2.
    ///   - QuantumMapData de la scène -> pointer QuantumMap_2v2.
    ///   - Renseigner les refs du bootstrap (RuntimeConfig, TeamQuantumMap, CombatMap, SpellCatalog).
    ///
    /// Menu : Nymora > Setup > Create 2v2 Combat Scene.
    /// </summary>
    public static class CreateRanked2v2SceneTool
    {
        private const string SRC_SCENE = "Assets/_Nymora/Scenes/30_CombatIA.unity";
        private const string DST_SCENE = "Assets/_Nymora/Scenes/41_CombatRanked2v2.unity";
        private const string SRC_QMAP = "Assets/QuantumUser/Resources/QuantumMap_IA.asset";
        private const string DST_QMAP = "Assets/QuantumUser/Resources/QuantumMap_2v2.asset";
        private const string SCENE_NAME = "41_CombatRanked2v2";

        [MenuItem("Nymora/Setup/Create 2v2 Combat Scene", priority = 22)]
        private static void Run()
        {
            if (!File.Exists(SRC_SCENE)) { Debug.LogError($"[Nymora.5.5c] Scène source introuvable : {SRC_SCENE}"); return; }
            if (!File.Exists(SRC_QMAP)) { Debug.LogError($"[Nymora.5.5c] QuantumMap_IA introuvable : {SRC_QMAP}"); return; }

            bool confirm = EditorUtility.DisplayDialog(
                "Créer la scène 2v2",
                "Cette opération :\n" +
                "  1. Backup 41_CombatRanked2v2.unity si présent\n" +
                "  2. Clone 30_CombatIA.unity -> 41_CombatRanked2v2.unity\n" +
                "  3. Crée QuantumMap_2v2.asset DÉDIÉE (repointée scène 41 + Guid unique)\n" +
                "  4. Ajoute la scène aux Build Settings\n\n" +
                "Le câblage final (bootstrap 2v2 + refs) reste manuel (guidé après).\n\nContinuer ?",
                "Créer", "Annuler");
            if (!confirm) return;

            // 1. Backup scène existante
            if (File.Exists(DST_SCENE))
            {
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backup = $"Assets/_Nymora/Scenes/41_CombatRanked2v2_BACKUP_{ts}.unity";
                string err = AssetDatabase.MoveAsset(DST_SCENE, backup);
                if (!string.IsNullOrEmpty(err)) { Debug.LogError($"[Nymora.5.5c] Backup scène échec : {err}"); return; }
                Debug.Log($"[Nymora.5.5c] Backup scène -> {backup}");
            }

            // 2. Clone scène
            if (!AssetDatabase.CopyAsset(SRC_SCENE, DST_SCENE))
            { Debug.LogError($"[Nymora.5.5c] CopyAsset scène échec : {SRC_SCENE} -> {DST_SCENE}"); return; }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Nymora.5.5c] Scène clonée : {DST_SCENE}");

            string sceneGuid = AssetDatabase.AssetPathToGUID(DST_SCENE);

            // 3. QuantumMap dédiée
            if (File.Exists(DST_QMAP))
            {
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                AssetDatabase.MoveAsset(DST_QMAP, $"Assets/QuantumUser/Resources/QuantumMap_2v2_BACKUP_{ts}.asset");
            }
            if (!AssetDatabase.CopyAsset(SRC_QMAP, DST_QMAP))
            { Debug.LogError($"[Nymora.5.5c] CopyAsset QuantumMap échec : {SRC_QMAP} -> {DST_QMAP}"); return; }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RepointQuantumMap(DST_QMAP, sceneGuid);

            // 4. Build settings
            EnsureInBuild();

            EditorUtility.DisplayDialog(
                "Scène 2v2 créée — câblage final",
                "FAIT automatiquement :\n" +
                "  • 41_CombatRanked2v2.unity (clone de 30_CombatIA)\n" +
                "  • QuantumMap_2v2.asset dédiée (repointée scène 41 + Guid unique)\n" +
                "  • Scène ajoutée au build\n\n" +
                "À FAIRE À LA MAIN (ouvre 41_CombatRanked2v2.unity) :\n" +
                "  1. GO du bootstrap (ex 'CombatBootstrapIA') : Remove Component CombatBootstrapIA,\n" +
                "     Add Component CombatBootstrap2v2.\n" +
                "  2. QuantumMapData de la scène : champ Asset -> QuantumMap_2v2.\n" +
                "  3. Sur CombatBootstrap2v2, renseigne :\n" +
                "       RuntimeConfig = RuntimeConfigCombatIA.asset (ou un clone),\n" +
                "       TeamQuantumMap = QuantumMap_2v2,\n" +
                "       CombatMap = CombatMap_2v2,\n" +
                "       SpellCatalog = SpellCatalog.asset.\n" +
                "  4. Sauvegarde la scène, Play -> ça doit afficher 4 combattants sur ta map.\n\n" +
                "(L'input hot-seat = brique 5.5b ; pour l'instant on valide juste le RENDU.)",
                "OK");
        }

        /// <summary>
        /// Régénère UNIQUEMENT QuantumMap_2v2 (sans retoucher la scène 41 déjà clonée), avec un guid
        /// Quantum VALIDE. Supprime au passage les assets cassés du 1er run (l'ancienne map au guid
        /// invalide + ses backups .asset, qui spamment "GUID uses reserved bits" à chaque import).
        /// </summary>
        [MenuItem("Nymora/Setup/Regenerate QuantumMap_2v2 (fix guid)", priority = 23)]
        private static void RegenerateQuantumMap()
        {
            if (!File.Exists(DST_SCENE)) { Debug.LogError($"[Nymora.5.5c] Scène 41 absente : {DST_SCENE}. Lance d'abord 'Create 2v2 Combat Scene'."); return; }
            if (!File.Exists(SRC_QMAP)) { Debug.LogError($"[Nymora.5.5c] QuantumMap_IA introuvable : {SRC_QMAP}"); return; }

            // 1. Supprime l'asset cible + tous les backups cassés (regenerables, on ne les garde pas).
            if (File.Exists(DST_QMAP)) AssetDatabase.DeleteAsset(DST_QMAP);
            foreach (var guid in AssetDatabase.FindAssets("QuantumMap_2v2_BACKUP"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(p)) AssetDatabase.DeleteAsset(p);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 2. Clone IA -> QuantumMap_2v2 puis repointe (Scene/ScenePath/SceneGuid + guid valide).
            if (!AssetDatabase.CopyAsset(SRC_QMAP, DST_QMAP))
            { Debug.LogError($"[Nymora.5.5c] CopyAsset QuantumMap échec : {SRC_QMAP} -> {DST_QMAP}"); return; }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string sceneGuid = AssetDatabase.AssetPathToGUID(DST_SCENE);
            RepointQuantumMap(DST_QMAP, sceneGuid);

            EditorUtility.DisplayDialog(
                "QuantumMap_2v2 régénérée",
                "QuantumMap_2v2.asset recréée avec un guid Quantum VALIDE, repointée vers la scène 41.\n" +
                "Les anciens assets cassés (+ backups) ont été supprimés -> plus d'erreur d'import.\n\n" +
                "Vérifie qu'il n'y a plus de 'GUID uses reserved bits' dans la console, puis reprends\n" +
                "le câblage de la scène (QuantumMapData -> QuantumMap_2v2, bootstrap, refs).",
                "OK");
            Debug.Log("[Nymora.5.5c] QuantumMap_2v2 régénérée (guid valide).");
        }

        /// <summary>Repointe la QuantumMap clonée vers la scène 41 + lui donne un Identifier Path/Guid uniques.</summary>
        private static void RepointQuantumMap(string qmapPath, string sceneGuid)
        {
            var mapObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(qmapPath);
            if (mapObj == null) { Debug.LogError($"[Nymora.5.5c] QuantumMap clonée introuvable : {qmapPath}"); return; }

            var so = new SerializedObject(mapObj);
            SetString(so, "m_Name", "QuantumMap_2v2");
            SetString(so, "Scene", SCENE_NAME);
            SetString(so, "ScenePath", DST_SCENE);
            SetString(so, "SceneGuid", sceneGuid);
            SetString(so, "Identifier.Path", "QuantumUser/Resources/QuantumMap_2v2");
            // Guid Quantum unique (long non nul) pour ne pas collisionner avec QuantumMap_IA.
            var guidProp = so.FindProperty("Identifier.Guid.Value");
            if (guidProp != null) guidProp.longValue = NewQuantumGuid();
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(mapObj);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Nymora.5.5c] QuantumMap_2v2 repointée vers {SCENE_NAME} (sceneGuid={sceneGuid}).");
        }

        private static void SetString(SerializedObject so, string path, string value)
        {
            var p = so.FindProperty(path);
            if (p != null) p.stringValue = value;
            else Debug.LogWarning($"[Nymora.5.5c] Propriété '{path}' introuvable sur la QuantumMap (à vérifier à la main).");
        }

        private static long NewQuantumGuid()
        {
            // ⚠️ Le guid Quantum N'EST PAS un long arbitraire : certains bits sont RÉSERVÉS
            //   (QuantumUnityDBImporter rejette `(Value & AssetGuid.ReservedBits) != 0`).
            //   AssetGuid.NewGuid() génère un guid VALIDE (sans bits réservés) -> on l'utilise.
            return AssetGuid.NewGuid().Value;
        }

        private static void EnsureInBuild()
        {
            try
            {
                var t = typeof(EnsureNymoraScenesInBuildTool);
                var m = t.GetMethod("Run", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (m != null) m.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Nymora.5.5c] Auto-ensure-in-build skipped : {ex.Message}. Lance Nymora > Setup > Ensure Nymora Scenes In Build.");
            }
        }
    }
}
