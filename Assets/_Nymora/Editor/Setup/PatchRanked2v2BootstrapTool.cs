using Nymora.Combat.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.EditorTools
{
    /// <summary>
    /// Brique 5.7 (sous-brique B) — Ajoute le bootstrap RÉSEAU 2v2 (CombatBootstrapRanked2v2) à la
    /// scène 41_CombatRanked2v2, à côté du hot-seat (CombatBootstrap2v2), en CLONANT ses références
    /// (RuntimeConfig / QuantumMap / CombatMap / SpellCatalog / SessionConfig).
    ///
    /// Les deux bootstraps cohabitent : au runtime, le réseau s'active si un match appairé est en
    /// attente (Match2v2Bridge), sinon le hot-seat prend la main (Play direct = test local).
    ///
    /// Menu : Nymora > Setup > Patch Ranked 2v2 Bootstrap. À lancer la scène 41 OUVERTE.
    /// </summary>
    public static class PatchRanked2v2BootstrapTool
    {
        private const string SceneName = "41_CombatRanked2v2";

        [MenuItem("Nymora/Setup/Patch Ranked 2v2 Bootstrap")]
        public static void Patch()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != SceneName)
            {
                EditorUtility.DisplayDialog("Patch Ranked 2v2 Bootstrap",
                    $"Ouvre d'abord la scène '{SceneName}' (elle doit être la scène ACTIVE).", "OK");
                return;
            }

            var hotSeat = Object.FindFirstObjectByType<CombatBootstrap2v2>(FindObjectsInactive.Include);
            if (hotSeat == null)
            {
                EditorUtility.DisplayDialog("Patch Ranked 2v2 Bootstrap",
                    "CombatBootstrap2v2 (hot-seat) introuvable dans la scène — impossible de cloner les références.", "OK");
                return;
            }

            var net = hotSeat.GetComponent<CombatBootstrapRanked2v2>();
            if (net == null) net = hotSeat.gameObject.AddComponent<CombatBootstrapRanked2v2>();

            // Clone des références depuis le hot-seat.
            net.RuntimeConfig = hotSeat.RuntimeConfig;
            net.SessionConfig = hotSeat.SessionConfig;
            net.TeamQuantumMap = hotSeat.TeamQuantumMap;
            net.CombatMap = hotSeat.CombatMap;
            net.SpellCatalog = hotSeat.SpellCatalog;
            // PhotonServerSettings : laissé null -> résolu au runtime via TryGetGlobal.
            net.FixedRegion = "eu";
            net.AppVersion = "0.1.0";

            EditorUtility.SetDirty(net);
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[PatchRanked2v2Bootstrap] CombatBootstrapRanked2v2 ajouté/mis à jour sur '" +
                      hotSeat.gameObject.name + "' (refs clonées du hot-seat). Sauvegarde la scène (Ctrl+S).");
            EditorUtility.DisplayDialog("Patch Ranked 2v2 Bootstrap",
                "OK — CombatBootstrapRanked2v2 ajouté (refs clonées du hot-seat).\nSauvegarde la scène (Ctrl+S).", "OK");
        }
    }
}
