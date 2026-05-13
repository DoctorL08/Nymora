using System.Collections.Generic;
using System.IO;
using Nymora.Combat.View;
using Quantum;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Editor tool : scan le dossier d'icones Soulrender et remplit le SpellIconRegistry
    /// automatiquement en mappant les noms de fichiers aux SpellId Quantum.
    ///
    /// Convention de naming (doc designer pixel art §8.2) :
    ///   icon_<spell_snake_case>.png    -> SpellId.Soulrender<SpellCamelCase>
    ///   icon_passif_hemoglyphe.png     -> PassifSoulrenderIcon (champ separe)
    ///
    /// Usage : Menu Nymora > Setup > Populate Spell Icon Registry
    ///
    /// Si le registry n'existe pas a l'emplacement standard, il est cree automatiquement.
    /// </summary>
    public static class PopulateSpellIconRegistry
    {
        private const string IconsFolder = "Assets/_Nymora/Art/Sprites/Soulrender/soulrender_icons";
        private const string AvatarsFolder = "Assets/_Nymora/Art/UI/Icons/Soulrender";
        private const string RegistryFolder = "Assets/_Nymora/ScriptableObjects/Spells";
        private const string RegistryAssetPath = RegistryFolder + "/SpellIconRegistry.asset";
        // 2.13.e : convention filename portraits = <ClassPrefix>_avatar_<size>px.png.
        // Soulrender -> SR_avatar_*. Phase 3 : NS_avatar_*, CL_avatar_*, NE_avatar_*, GH_avatar_*.
        private const string AvatarSoulrenderPrefix = "SR_avatar";

        // Mapping fichier -> SpellId. Les noms de fichiers viennent du designer.
        private static readonly Dictionary<string, SpellId> FileToSpellId = new Dictionary<string, SpellId>
        {
            // Offensifs
            { "icon_tranche_ame",          SpellId.SoulrenderTrancheAme },
            { "icon_ouvre_plaie",          SpellId.SoulrenderOuvrePlaie },
            { "icon_charge_brutale",       SpellId.SoulrenderChargeBrutale },
            { "icon_detonation_sanglante", SpellId.SoulrenderDetonationSanglante },
            { "icon_curee",                SpellId.SoulrenderCuree },
            // Tactiques
            { "icon_pacte_de_sang",        SpellId.SoulrenderPacteDeSang },
            { "icon_marque_de_carnage",    SpellId.SoulrenderMarqueDeCarnage },
            { "icon_empoignade",           SpellId.SoulrenderEmpoignade },
            { "icon_rugissement",          SpellId.SoulrenderRugissement },
            { "icon_rage_insatiable",      SpellId.SoulrenderRageInsatiable },
            // Survie
            { "icon_riposte_carmin",       SpellId.SoulrenderRiposteCarmin },
            { "icon_cauterisation",        SpellId.SoulrenderCauterisation },
            { "icon_peau_de_fer",          SpellId.SoulrenderPeauDeFer },
            { "icon_seve_vive",            SpellId.SoulrenderSeveVive },
            { "icon_dernier_souffle",      SpellId.SoulrenderDernierSouffle },
            // Signature
            { "icon_ame_laceree",          SpellId.SoulrenderAmeLaceree },
            // Passif (note : pas un SpellId, mappe sur le champ PassifSoulrender du SO)
            // "icon_passif_hemoglyphe" est gere a part.
        };

        [MenuItem("Nymora/Setup/Populate Spell Icon Registry")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(IconsFolder))
            {
                Debug.LogError($"[PopulateSpellIconRegistry] Dossier introuvable : {IconsFolder}");
                return;
            }

            var registry = LoadOrCreateRegistry();
            if (registry == null) return;

            var entries = new List<SpellIconRegistry.Entry>();
            Sprite passifSoulrender = null;
            Sprite avatarSoulrender = null;

            // Scan les icones.
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { IconsFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;

                if (fileName == "icon_passif_hemoglyphe")
                {
                    passifSoulrender = sprite;
                    Debug.Log($"[PopulateSpellIconRegistry] Passif Hemoglyphe -> {path}");
                    continue;
                }

                if (FileToSpellId.TryGetValue(fileName, out SpellId spellId))
                {
                    entries.Add(new SpellIconRegistry.Entry { Spell = spellId, Icon = sprite });
                    Debug.Log($"[PopulateSpellIconRegistry] {spellId} -> {path}");
                }
                else
                {
                    Debug.LogWarning($"[PopulateSpellIconRegistry] Aucun mapping pour {fileName} ({path}). Ajouter dans FileToSpellId.");
                }
            }

            // 2.13.e — Scan le dossier des avatars (portraits HUD).
            if (AssetDatabase.IsValidFolder(AvatarsFolder))
            {
                string[] avatarGuids = AssetDatabase.FindAssets("t:Sprite", new[] { AvatarsFolder });
                foreach (string guid in avatarGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    if (!fileName.StartsWith(AvatarSoulrenderPrefix)) continue;
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null) continue;
                    avatarSoulrender = sprite;
                    Debug.Log($"[PopulateSpellIconRegistry] Avatar Soulrender -> {path}");
                    break;
                }
            }

            // Verifie qu'on a bien 16 sorts mappes (15 + signature) + le passif.
            int expected = FileToSpellId.Count;
            if (entries.Count != expected)
            {
                Debug.LogWarning($"[PopulateSpellIconRegistry] {entries.Count}/{expected} sorts mappes. Verifie les noms de fichiers.");
            }
            if (passifSoulrender == null)
            {
                Debug.LogWarning($"[PopulateSpellIconRegistry] icon_passif_hemoglyphe.png introuvable.");
            }
            if (avatarSoulrender == null)
            {
                Debug.LogWarning($"[PopulateSpellIconRegistry] {AvatarSoulrenderPrefix}*.png introuvable dans {AvatarsFolder}.");
            }

            // Apply et save.
            Undo.RecordObject(registry, "Populate Spell Icon Registry");
            registry.EditorSetEntries(entries.ToArray(), passifSoulrender, avatarSoulrender);
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PopulateSpellIconRegistry] Done. {entries.Count} sorts + passif = {(passifSoulrender != null ? "OK" : "MISSING")} + avatar = {(avatarSoulrender != null ? "OK" : "MISSING")} ecrit dans {RegistryAssetPath}");
            EditorGUIUtility.PingObject(registry);
            Selection.activeObject = registry;
        }

        private static SpellIconRegistry LoadOrCreateRegistry()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SpellIconRegistry>(RegistryAssetPath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(RegistryFolder))
            {
                CreateFolderRecursive(RegistryFolder);
            }

            var created = ScriptableObject.CreateInstance<SpellIconRegistry>();
            AssetDatabase.CreateAsset(created, RegistryAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PopulateSpellIconRegistry] Cree {RegistryAssetPath}");
            return created;
        }

        private static void CreateFolderRecursive(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                CreateFolderRecursive(parent);
            }
            string name = Path.GetFileName(folder);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
