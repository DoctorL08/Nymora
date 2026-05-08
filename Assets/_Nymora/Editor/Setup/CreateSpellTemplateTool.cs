using System.IO;
using Nymora.Core.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Genere un asset SpellDefinition vide (_Template_Spell.asset) qui sert de modele
    /// visuel/structurel pour les 75 sorts a venir.
    /// Menu : Nymora &gt; Setup &gt; Create Spell Template
    /// </summary>
    public static class CreateSpellTemplateTool
    {
        private const string TargetFolder = "Assets/_Nymora/ScriptableObjects/Spells";
        private const string TemplateName = "_Template_Spell";

        [MenuItem("Nymora/Setup/Create Spell Template", priority = 20)]
        private static void CreateTemplate()
        {
            EnsureFolderExists();

            string path = $"{TargetFolder}/{TemplateName}.asset";

            if (File.Exists(path))
            {
                if (!EditorUtility.DisplayDialog("Spell Template",
                    $"{TemplateName}.asset existe deja. L'ecraser ?",
                    "Oui, ecraser", "Annuler"))
                {
                    return;
                }
                AssetDatabase.DeleteAsset(path);
            }

            var spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.name = TemplateName;
            spell.SpellId = "_template";
            spell.DisplayName = "Template Spell (do not use in match)";
            spell.Description = "Template vide. Duppliquer (Ctrl+D) puis remplir pour creer un sort reel.";

            AssetDatabase.CreateAsset(spell, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = spell;
            EditorGUIUtility.PingObject(spell);

            Debug.Log($"[Nymora.Setup] Spell template cree : {path}");
        }

        private static void EnsureFolderExists()
        {
            if (!AssetDatabase.IsValidFolder(TargetFolder))
            {
                Directory.CreateDirectory(TargetFolder);
                AssetDatabase.Refresh();
            }
        }
    }
}
