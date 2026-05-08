using System.IO;
using Nymora.Core.Enums;
using Nymora.Core.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Genere les 5 ScriptableObjects de classe (Soulrender, Nightseer, Colossar, Necram, Ghostra)
    /// remplis avec les valeurs de la Bible V7.1.
    /// Menu : Nymora &gt; Setup &gt; Create All Class Definitions
    ///
    /// Si un asset existe deja, il n'est PAS ecrase (utiliser le menu "Force Recreate" pour reset).
    /// </summary>
    public static class CreateAllClassDefinitionsTool
    {
        private const string TargetFolder = "Assets/_Nymora/ScriptableObjects/Classes";

        [MenuItem("Nymora/Setup/Create All Class Definitions", priority = 10)]
        private static void CreateAll()
        {
            EnsureFolderExists();

            int created = 0;
            int skipped = 0;

            foreach (var def in BuildAllDefinitions())
            {
                string path = $"{TargetFolder}/{def.name}.asset";
                if (File.Exists(path))
                {
                    Debug.Log($"[Nymora.Setup] Skipped (already exists): {path}");
                    skipped++;
                    continue;
                }

                AssetDatabase.CreateAsset(def, path);
                Debug.Log($"[Nymora.Setup] Created: {path}");
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Nymora.Setup] Done. Created: {created} / Skipped: {skipped}");

            EditorUtility.DisplayDialog("Nymora — Class Definitions",
                $"Generation terminee.\n\nCrees : {created}\nSkippes (deja presents) : {skipped}",
                "OK");
        }

        [MenuItem("Nymora/Setup/Force Recreate All Class Definitions", priority = 11)]
        private static void ForceRecreateAll()
        {
            if (!EditorUtility.DisplayDialog("Force Recreate Class Definitions",
                "Va SUPPRIMER les 5 assets existants dans Classes/ et les recreer. Continuer ?",
                "Oui, recreer", "Annuler"))
            {
                return;
            }

            EnsureFolderExists();

            int recreated = 0;
            foreach (var def in BuildAllDefinitions())
            {
                string path = $"{TargetFolder}/{def.name}.asset";
                if (File.Exists(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }
                AssetDatabase.CreateAsset(def, path);
                Debug.Log($"[Nymora.Setup] Recreated: {path}");
                recreated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Nymora — Class Definitions",
                $"Recree {recreated} assets.", "OK");
        }

        private static void EnsureFolderExists()
        {
            if (!AssetDatabase.IsValidFolder(TargetFolder))
            {
                Directory.CreateDirectory(TargetFolder);
                AssetDatabase.Refresh();
            }
        }

        private static NymoraClassDefinition[] BuildAllDefinitions()
        {
            return new[]
            {
                BuildSoulrender(),
                BuildNightseer(),
                BuildColossar(),
                BuildNecram(),
                BuildGhostra()
            };
        }

        // ---------------------------------------------------------------------
        // Soulrender — Berserker melee saignement
        // ---------------------------------------------------------------------
        private static NymoraClassDefinition BuildSoulrender()
        {
            var d = ScriptableObject.CreateInstance<NymoraClassDefinition>();
            d.name = "Soulrender";
            d.ClassId = NymoraClass.Soulrender;
            d.DisplayName = "Soulrender";
            d.AccentColor = HexColor("#B22222");
            d.Tagline = "Berserker melee. Subir = generer. Plus on le frappe, plus il s'arme.";

            d.ResourceKind = ResourceType.Hemoglyphe;
            d.ResourceCap = 5;
            d.ResourceDescription =
                "HEMOGLYPHE — ressource bilaterale : subir des degats ET en infliger en melee genere de la ressource. " +
                "Aura rouge progressive, fissures ecarlates a 5/5.";

            d.PassiveName = "L'Appel du Sang";
            d.PassiveDescription =
                "Pas de paliers fixes. Montee NON-LINEAIRE indexee sur les seuils HP de la cible : " +
                "plus la cible est basse, plus la generation d'Hemoglyphe acceleree.";

            d.SignatureName = "Ame Lacéréee";
            d.SignatureCooldownTurns = 4;
            d.SignatureDescription = "Sort signature debloque a 5 HG. Cooldown 4 tours apres usage.";

            return d;
        }

        // ---------------------------------------------------------------------
        // Nightseer — Piegeur mid-range
        // ---------------------------------------------------------------------
        private static NymoraClassDefinition BuildNightseer()
        {
            var d = ScriptableObject.CreateInstance<NymoraClassDefinition>();
            d.name = "Nightseer";
            d.ClassId = NymoraClass.Nightseer;
            d.DisplayName = "Nightseer";
            d.AccentColor = HexColor("#6A4FB6");
            d.Tagline = "Piegeur mid-range. Pose des marques sur la grille et l'adversaire.";

            d.ResourceKind = ResourceType.Prescience;
            d.ResourceCap = 4;
            d.ResourceDescription =
                "PRESCIENCE — generee par la pose et la consommation de marques (Traque, Voile, Empreinte). Cap 4.";

            d.PassiveName = "L'Oeil qui n'est pas";
            d.PassiveDescription =
                "La puissance du Nightseer depend du nombre de marques actives : sur la grille, sur l'adversaire, " +
                "ou les deux. Pas de palier de jauge classique.";

            d.SignatureName = "Traquenard";
            d.SignatureCooldownTurns = 4;
            d.SignatureDescription = "Sort signature debloque a 4 PR. Cooldown 4 tours apres usage.";

            return d;
        }

        // ---------------------------------------------------------------------
        // Colossar — Tank punisseur
        // ---------------------------------------------------------------------
        private static NymoraClassDefinition BuildColossar()
        {
            var d = ScriptableObject.CreateInstance<NymoraClassDefinition>();
            d.name = "Colossar";
            d.ClassId = NymoraClass.Colossar;
            d.DisplayName = "Colossar";
            d.AccentColor = HexColor("#7A6B5C");
            d.Tagline = "Tank punisseur. Pose des obstacles, encaisse, contre-attaque.";

            d.ResourceKind = ResourceType.Fondation;
            d.ResourceCap = 3;
            d.ResourceDescription =
                "FONDATION — generee par la pose d'obstacles et l'absorption de degats. Cap 3.";

            d.PassiveName = "Densité Inerte";
            d.PassiveDescription =
                "Etat continu lie au nombre d'obstacles poses : reduction de degats progressive (cap -50%). " +
                "Le Colossar bascule en EFFONDREMENT a Fondation max — punition de zone.";

            d.SignatureName = "Effondrement";
            d.SignatureCooldownTurns = 4;
            d.SignatureDescription = "Sort signature debloque a 3 FD. Cooldown 4 tours apres usage.";

            return d;
        }

        // ---------------------------------------------------------------------
        // Necram — Mage poison venin
        // ---------------------------------------------------------------------
        private static NymoraClassDefinition BuildNecram()
        {
            var d = ScriptableObject.CreateInstance<NymoraClassDefinition>();
            d.name = "Necram";
            d.ClassId = NymoraClass.Necram;
            d.DisplayName = "Necram";
            d.AccentColor = HexColor("#5A8B3E");
            d.Tagline = "Mage poison. Densite toxique progressive. Le DoT ignore les reductions.";

            d.ResourceKind = ResourceType.Putrefaction;
            d.ResourceCap = 6;
            d.ResourceDescription =
                "PUTREFACTION — densite toxique. Generee par les ticks venin sur les ennemis et les marques actives. Cap 6.";

            d.PassiveName = "La Floraison";
            d.PassiveDescription =
                "Suit le nombre de marques de venin actives dans le match (toutes cibles confondues). " +
                "Plus de marques = ticks plus puissants, regen Necram, zones toxiques autour de lui.";

            d.SignatureName = "Virus Fatal";
            d.SignatureCooldownTurns = 4;
            d.SignatureDescription = "Sort signature debloque a 6 PT. Detone tous les ticks. Cooldown 4 tours.";

            return d;
        }

        // ---------------------------------------------------------------------
        // Ghostra — Assassin dorsal
        // ---------------------------------------------------------------------
        private static NymoraClassDefinition BuildGhostra()
        {
            var d = ScriptableObject.CreateInstance<NymoraClassDefinition>();
            d.name = "Ghostra";
            d.ClassId = NymoraClass.Ghostra;
            d.DisplayName = "Ghostra";
            d.AccentColor = HexColor("#6F8FA8");
            d.Tagline = "Assassin dorsal. Leurres et desynchronisation. Frappe dans le dos.";

            d.ResourceKind = ResourceType.Remanence;
            d.ResourceCap = 3;
            d.ResourceDescription =
                "REMANENCE — leurres spectraux poses sur la grille (max 3). Modifient les sorts dorsaux selon leur densite.";

            d.PassiveName = "L'Angle Mort";
            d.PassiveDescription =
                "Systeme de DESYNCHRONISATION : les sorts dorsaux de la Ghostra sont modifies selon le nombre " +
                "de leurres actifs. Plus de leurres = sorts plus letaux et imprevisibles.";

            d.SignatureName = "Exécution Spectrale";
            d.SignatureCooldownTurns = 4;
            d.SignatureDescription = "Sort signature debloque a 3 leurres. Cooldown 4 tours apres usage.";

            return d;
        }

        private static Color HexColor(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
        }
    }
}
