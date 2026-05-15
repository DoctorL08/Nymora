// NYMORA PATCH (Brique 4.1) : ScriptedImporter desactive — conflit avec QuantumEditorConfigImporter.
// Quantum 3 et Fusion 2 declarent tous les deux un importer pour .editorconfig ; Unity rejette les deux.
// On garde celui de Quantum (installe en premier, Phase 1.2). A re-appliquer apres chaque update Fusion SDK.
#if FUSION_EDITORCONFIG_IMPORTER_ENABLED
namespace Fusion.Editor {
  using System.IO;
  using UnityEditor.AssetImporters;
  using UnityEngine;

  [ScriptedImporter(0, "editorconfig")]
  class FusionEditorConfigImporter : ScriptedImporter {
    public override void OnImportAsset(AssetImportContext ctx) {
      var path      = ctx.assetPath;
      var contents  = File.ReadAllText(path);

      // create internal text asset for convenience
      var mainAsset = new TextAsset(contents);
      ctx.AddObjectToAsset("main", mainAsset);
      ctx.SetMainObject(mainAsset);

      // write the actual editorconfig for editors to consume
      var editorConfigPath = Path.Combine(Path.GetDirectoryName(path), ".editorconfig");
      File.WriteAllText(editorConfigPath, contents);
    }
  }
}
#endif
