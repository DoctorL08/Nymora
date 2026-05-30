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
        private const string IconsNightseerFolder = "Assets/_Nymora/Art/Sprites/Nightseer/Icons";
        private const string IconsColossarFolder = "Assets/_Nymora/Art/Sprites/Colossar/colossar_icons";
        private const string IconsNecramFolder = "Assets/_Nymora/Art/Sprites/Necram/necram_icons";
        private const string IconsGhostraFolder = "Assets/_Nymora/Art/Sprites/Ghostra/Spell_Icon";
        private const string AvatarsFolder = "Assets/_Nymora/Art/UI/Icons/Soulrender";
        private const string AvatarsNightseerFolder = "Assets/_Nymora/Art/Sprites/Nightseer/Avatar";
        private const string AvatarsColossarFolder = "Assets/_Nymora/Art/Sprites/Colossar/Avatar";
        private const string AvatarsNecramFolder = "Assets/_Nymora/Art/Sprites/Necram/Avatar";
        private const string AvatarsGhostraFolder = "Assets/_Nymora/Art/Sprites/Ghostra/Avatar";
        private const string RegistryFolder = "Assets/_Nymora/ScriptableObjects/Spells";
        private const string RegistryAssetPath = RegistryFolder + "/SpellIconRegistry.asset";
        // 2.13.e : convention filename portraits par classe (le designer alterne casing).
        // Soulrender -> SR_avatar_*. Nightseer -> NS_avatar_*. Colossar -> colossar_avatar_*.
        // Necram -> necram_avatar_*. Ghostra -> GHOSTRA_Avatar_*.
        private const string AvatarSoulrenderPrefix = "SR_avatar";
        private const string AvatarNightseerPrefix = "NS_avatar";
        private const string AvatarColossarPrefix = "colossar_avatar";
        private const string AvatarNecramPrefix = "necram_avatar";
        private const string AvatarGhostraPrefix = "GHOSTRA_Avatar";

        // Filename des passifs par classe (matching exact case-insensitive).
        private const string PassifSoulrenderFile = "icon_passif_hemoglyphe";
        private const string PassifNightseerFile = "icon_passif_oeil_qui_nest_pas";
        private const string PassifColossarFile = "icon_passif_densite_inerte";
        private const string PassifNecramFile = "icon_passif_floraison";
        private const string PassifGhostraFile = "icon_passif_angle_mort";

        // Mapping fichier -> SpellId. Les noms de fichiers viennent du designer.
        private static readonly Dictionary<string, SpellId> FileToSpellId = new Dictionary<string, SpellId>
        {
            // === SOULRENDER ===
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

            // === NIGHTSEER ===
            // Offensifs
            { "icon_tir_precis",           SpellId.NightseerTirPrecis },
            { "icon_volee_depines",        SpellId.NightseerVoleeDEpines },
            { "icon_detonation_onirique",  SpellId.NightseerDetonationOnirique },
            { "icon_frappe_de_lombre",     SpellId.NightseerFrappeDeLOmbre },
            { "icon_salve_mortelle",       SpellId.NightseerSalveMortelle },
            // Tactiques
            { "icon_marque_du_chasseur",   SpellId.NightseerMarqueDuChasseur },
            { "icon_filet_de_ronces",      SpellId.NightseerFiletDeRonces },
            { "icon_champ_de_mines",       SpellId.NightseerChampDeMines },
            { "icon_bourrasque",           SpellId.NightseerBourrasque },
            { "icon_souffle_glacial",      SpellId.NightseerSouffleGlacial },
            // Survie
            { "icon_voile_dombre",         SpellId.NightseerVoileDOmbre },
            { "icon_pas_furtif",           SpellId.NightseerPasFurtif },
            { "icon_camouflage_ronces",    SpellId.NightseerCamouflageRonces },
            { "icon_seve_sauvage",         SpellId.NightseerSeveSauvage },
            { "icon_evanescence",          SpellId.NightseerEvanescence },
            // Signature
            { "icon_traquenard",           SpellId.NightseerTraquenard },
            // Passif "icon_passif_oeil_qui_nest_pas" est gere a part.

            // === COLOSSAR === (4.14.f hotfix — completer le dict pour faire afficher les icones en PvP)
            // Offensifs
            { "icon_frappe_lourde",        SpellId.ColossarFrappeLourde },
            { "icon_onde_de_choc",         SpellId.ColossarOndeDeChoc },
            { "icon_marteau_punisseur",    SpellId.ColossarMarteauPunisseur },
            { "icon_choc_sismique",        SpellId.ColossarChocSismique },
            { "icon_represailles",         SpellId.ColossarRepresailles },
            // Tactiques
            { "icon_pilier",               SpellId.ColossarPilier },
            { "icon_mur_de_pierre",        SpellId.ColossarMurDePierre },
            { "icon_ancrage",              SpellId.ColossarAncrage },
            { "icon_provocation",          SpellId.ColossarProvocation },
            { "icon_brisure",              SpellId.ColossarBrisure },
            // Survie
            { "icon_stoicisme",            SpellId.ColossarStoicisme },
            { "icon_garde_protectrice",    SpellId.ColossarGardeProtectrice },
            { "icon_ressac_vital",         SpellId.ColossarRessacVital },
            { "icon_renvoi_du_bouclier",   SpellId.ColossarRenvoiDuBouclier },
            { "icon_soin_lourd",           SpellId.ColossarSoinLourd },
            // Signature
            { "icon_effondrement",         SpellId.ColossarEffondrement },

            // === NECRAM ===
            // Offensifs
            { "icon_crachat_acide",        SpellId.NecramCrachatAcide },
            { "icon_morsure_putride",      SpellId.NecramMorsurePutride },
            { "icon_detonation_virulente", SpellId.NecramDetonationVirulente },
            { "icon_faux_decharnee",       SpellId.NecramFauxDecharnee },
            { "icon_brume_toxique",        SpellId.NecramBrumeToxique },
            // Tactiques
            { "icon_inoculation",          SpellId.NecramInoculation },
            { "icon_marque_sacrificielle", SpellId.NecramMarqueSacrificielle },
            { "icon_symbiose_morbide",     SpellId.NecramSymbioseMorbide },
            { "icon_contagion",            SpellId.NecramContagion },
            { "icon_pas_spectral",         SpellId.NecramPasSpectral },
            // Survie
            { "icon_voile_de_pestilence",  SpellId.NecramVoilePestilence },
            { "icon_carapace_visqueuse",   SpellId.NecramCarapaceVisqueuse },
            { "icon_drain_vital",          SpellId.NecramDrainVital },
            // Bible "Régénération Nécrotique" => asset "icon_regeneration_necrotique" mais enum
            // code = "PulseSanguinVert" (artefact historique, cf memory feedback-dont-invent-spell-names).
            { "icon_regeneration_necrotique", SpellId.NecramPulseSanguinVert },
            { "icon_cocon_putride",        SpellId.NecramCoconPutride },
            // Signature
            { "icon_virus_fatal",          SpellId.NecramVirusFatal },

            // === GHOSTRA ===
            // Offensifs (5 Bible — Volte-Face classe Tactique amendement 16 mai mais asset reste icon_volte_face)
            { "icon_lame_spectrale",       SpellId.GhostraLameSpectrale },
            { "icon_lame_vorace_spectrale",SpellId.GhostraLameVoraceSpectrale },
            { "icon_saigne_ame",           SpellId.GhostraSaigneAme },
            { "icon_frappe_fantome",       SpellId.GhostraFrappeFantome },
            { "icon_danse_des_lames",      SpellId.GhostraDanseDesLames },
            // Tactiques (5)
            { "icon_replique_fantome",     SpellId.GhostraRepliqueFantome },
            { "icon_pas_dans_lombre",      SpellId.GhostraPasDansLOmbre },
            { "icon_permutation",          SpellId.GhostraPermutation }, // ex-icon_volte_face (slot 90 reutilise)
            { "icon_eveil_spectral",       SpellId.GhostraEveilSpectral }, // ex-icon_dague_lancee (slot 93 reutilise)
            { "icon_marque_de_lombre",     SpellId.GhostraMarqueDeLOmbre },
            // Survie (5)
            { "icon_linceul_dombres",      SpellId.GhostraLinceulDOmbres },
            { "icon_voile_spectral",       SpellId.GhostraVoileSpectral },
            { "icon_replique_protectrice", SpellId.GhostraRepliqueProtectrice },
            { "icon_dernier_pas",          SpellId.GhostraDernierPas },
            { "icon_pas_de_lau_dela",      SpellId.GhostraPasDeLAuDela },
            // Signature
            { "icon_execution_spectrale",  SpellId.GhostraExecutionSpectrale },
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
            Sprite passifNightseer = null;
            Sprite passifColossar = null;
            Sprite passifNecram = null;
            Sprite passifGhostra = null;
            Sprite avatarSoulrender = null;
            Sprite avatarNightseer = null;
            Sprite avatarColossar = null;
            Sprite avatarNecram = null;
            Sprite avatarGhostra = null;

            // Scan les icones de toutes les classes (passifs + icones de sort si mappees).
            var scanFolderList = new List<string> { IconsFolder };
            if (AssetDatabase.IsValidFolder(IconsNightseerFolder)) scanFolderList.Add(IconsNightseerFolder);
            if (AssetDatabase.IsValidFolder(IconsColossarFolder)) scanFolderList.Add(IconsColossarFolder);
            if (AssetDatabase.IsValidFolder(IconsNecramFolder)) scanFolderList.Add(IconsNecramFolder);
            if (AssetDatabase.IsValidFolder(IconsGhostraFolder)) scanFolderList.Add(IconsGhostraFolder);
            string[] guids = AssetDatabase.FindAssets("t:Sprite", scanFolderList.ToArray());
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;

                // Passifs : matching exact sur le nom (case-insensitive pour tolerer designer).
                if (fileName.Equals(PassifSoulrenderFile, System.StringComparison.OrdinalIgnoreCase))
                {
                    passifSoulrender = sprite;
                    Debug.Log($"[PopulateSpellIconRegistry] Passif Hemoglyphe (SR) -> {path}");
                    continue;
                }
                if (fileName.Equals(PassifNightseerFile, System.StringComparison.OrdinalIgnoreCase))
                {
                    passifNightseer = sprite;
                    Debug.Log($"[PopulateSpellIconRegistry] Passif L'Oeil qui n'est pas (NS) -> {path}");
                    continue;
                }
                if (fileName.Equals(PassifColossarFile, System.StringComparison.OrdinalIgnoreCase))
                {
                    passifColossar = sprite;
                    Debug.Log($"[PopulateSpellIconRegistry] Passif Densite Inerte (CL) -> {path}");
                    continue;
                }
                if (fileName.Equals(PassifNecramFile, System.StringComparison.OrdinalIgnoreCase))
                {
                    passifNecram = sprite;
                    Debug.Log($"[PopulateSpellIconRegistry] Passif Floraison (NE) -> {path}");
                    continue;
                }
                if (fileName.Equals(PassifGhostraFile, System.StringComparison.OrdinalIgnoreCase))
                {
                    passifGhostra = sprite;
                    Debug.Log($"[PopulateSpellIconRegistry] Passif Angle Mort (GH) -> {path}");
                    continue;
                }

                if (FileToSpellId.TryGetValue(fileName, out SpellId spellId))
                {
                    entries.Add(new SpellIconRegistry.Entry { Spell = spellId, Icon = sprite });
                    Debug.Log($"[PopulateSpellIconRegistry] {spellId} -> {path}");
                }
                // Note : les icones de sorts Colossar/Necram/Ghostra ne sont pas encore dans
                // FileToSpellId (TODO Phase 3 polish). Pas de warning pour eviter le spam tant
                // qu'on n'a pas le mapping complet.
            }

            // 2.13.e + 3.7 polish — Scan les avatars des 5 classes (chacun dans son dossier dedie).
            avatarSoulrender = FindFirstAvatarByPrefix(AvatarsFolder, AvatarSoulrenderPrefix, "Soulrender");
            avatarNightseer  = FindFirstAvatarByPrefix(AvatarsNightseerFolder, AvatarNightseerPrefix, "Nightseer");
            avatarColossar   = FindFirstAvatarByPrefix(AvatarsColossarFolder, AvatarColossarPrefix, "Colossar");
            avatarNecram     = FindFirstAvatarByPrefix(AvatarsNecramFolder, AvatarNecramPrefix, "Necram");
            avatarGhostra    = FindFirstAvatarByPrefix(AvatarsGhostraFolder, AvatarGhostraPrefix, "Ghostra");

            // Verifie qu'on a bien tous les sorts mappes (16 SR + 16 NS) + les passifs.
            int expected = FileToSpellId.Count;
            if (entries.Count != expected)
            {
                Debug.LogWarning($"[PopulateSpellIconRegistry] {entries.Count}/{expected} sorts mappes (SR+NS). Mapping Colossar/Necram/Ghostra TODO Phase 3 polish.");
            }
            WarnIfMissing(passifSoulrender, PassifSoulrenderFile, IconsFolder);
            WarnIfMissing(passifNightseer,  PassifNightseerFile,  IconsNightseerFolder);
            WarnIfMissing(passifColossar,   PassifColossarFile,   IconsColossarFolder);
            WarnIfMissing(passifNecram,     PassifNecramFile,     IconsNecramFolder);
            WarnIfMissing(passifGhostra,    PassifGhostraFile,    IconsGhostraFolder);
            WarnIfMissingPrefix(avatarSoulrender, AvatarSoulrenderPrefix, AvatarsFolder);
            WarnIfMissingPrefix(avatarNightseer,  AvatarNightseerPrefix,  AvatarsNightseerFolder);
            WarnIfMissingPrefix(avatarColossar,   AvatarColossarPrefix,   AvatarsColossarFolder);
            WarnIfMissingPrefix(avatarNecram,     AvatarNecramPrefix,     AvatarsNecramFolder);
            WarnIfMissingPrefix(avatarGhostra,    AvatarGhostraPrefix,    AvatarsGhostraFolder);

            // Apply et save.
            Undo.RecordObject(registry, "Populate Spell Icon Registry");
            registry.EditorSetEntries(
                entries.ToArray(),
                passifSoulrender,
                avatarSoulrender,
                avatarNightseer,
                passifNightseer,
                passifColossar,
                passifNecram,
                passifGhostra,
                avatarColossar,
                avatarNecram,
                avatarGhostra);
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();

            string summary =
                $"[PopulateSpellIconRegistry] Done. {entries.Count} sorts mappes. " +
                $"Passifs : SR={Tag(passifSoulrender)} NS={Tag(passifNightseer)} CL={Tag(passifColossar)} NE={Tag(passifNecram)} GH={Tag(passifGhostra)}. " +
                $"Avatars : SR={Tag(avatarSoulrender)} NS={Tag(avatarNightseer)} CL={Tag(avatarColossar)} NE={Tag(avatarNecram)} GH={Tag(avatarGhostra)}. " +
                $"Ecrit dans {RegistryAssetPath}";
            Debug.Log(summary);
            EditorGUIUtility.PingObject(registry);
            Selection.activeObject = registry;
        }

        private static Sprite FindFirstAvatarByPrefix(string folder, string prefix, string labelForLog)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (!fileName.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) continue;
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;
                Debug.Log($"[PopulateSpellIconRegistry] Avatar {labelForLog} -> {path}");
                return sprite;
            }
            return null;
        }

        private static void WarnIfMissing(Sprite sprite, string expectedFilename, string folder)
        {
            if (sprite != null) return;
            Debug.LogWarning($"[PopulateSpellIconRegistry] {expectedFilename}.png introuvable dans {folder}.");
        }

        private static void WarnIfMissingPrefix(Sprite sprite, string prefix, string folder)
        {
            if (sprite != null) return;
            Debug.LogWarning($"[PopulateSpellIconRegistry] {prefix}*.png introuvable dans {folder}.");
        }

        private static string Tag(Sprite s) => s != null ? "OK" : "MISSING";

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
