using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nymora.Combat.View.Animation;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// POLISH-5 — Editor Tool : scan tous les dossiers Art/Sprites/&lt;classe&gt;/(Marks|Marques|Tiles|
    /// Terrains|VFX) et remplit automatiquement les 3 SO :
    ///   - MarkSpriteLibrary    (StatusKind/MarkKind -> frames anim)
    ///   - TerrainSpriteLibrary (TerrainKind -> frames anim)
    ///   - VFXSpriteLibrary     (SpellId -> frames anim VFX cast one-shot)
    ///
    /// Convention de nommage assets :
    ///   - Marques : "marque_X_4frame.{png,gif}" ou "marque_X_4frame1.png" + "...2.png" etc (frames separees)
    ///   - Terrains : "tiles_X_4frame.png" ou "X_4frame-export1.png" + "...2.png" etc
    ///   - VFX : "VFX_X_8frame.gif" ou "X_signature_vfx_8frame1.png" + "...2.png" etc
    ///
    /// Mapping precis (filename token -> enum value) defini dans les Dictionary statiques
    /// ci-dessous, identique au pattern de PopulateSpellIconRegistry.cs.
    ///
    /// Pour les nouveaux assets MAJ_TILES (POLISH-3) en frames PNG individuelles
    /// "-export[1-4].png", priorite est donnee a ces frames sur les mono-fichiers anciens
    /// (le tool agrège les 4 frames numerotees en array Sprite[4]).
    ///
    /// Usage : Menu Nymora &gt; Setup &gt; Populate Combat Sprite Libraries
    /// </summary>
    public static class PopulateCombatSpriteLibraries
    {
        // -------------------------------------------------------------
        // Chemins assets
        // -------------------------------------------------------------
        private const string ArtRoot = "Assets/_Nymora/Art/Sprites";
        private const string LibrariesFolder = "Assets/_Nymora/ScriptableObjects/Combat";
        private const string MarkLibPath = LibrariesFolder + "/MarkSpriteLibrary.asset";
        private const string TerrainLibPath = LibrariesFolder + "/TerrainSpriteLibrary.asset";
        private const string VfxLibPath = LibrariesFolder + "/VFXSpriteLibrary.asset";

        // -------------------------------------------------------------
        // Mapping filename token -> field name dans le SO
        // -------------------------------------------------------------

        // MarkSpriteLibrary : nom du field privee (reflection) qu'on remplit. La key est le
        // prefixe filename qu'on cherche dans les dossiers Marks/Marques de chaque classe.
        private static readonly Dictionary<string, string> MarkFilenameToField = new Dictionary<string, string>
        {
            // Soulrender (Marks/)
            { "plaie_ouverte",                  "_antiHealShieldFrames" },  // sprite partage Plaie Ouverte = AntiHealShield Soulrender visuel
            { "marque_de_carnage",              "_markedByCarnageFrames" },
            // Nightseer (Marks/)
            { "marque_traque_oeil_pulsant",     "_traqueFrames" },
            { "marque_empreinte",               "_empreinteFrames" },
            { "marque_voile_brume",             "_untargetableFrames" }, // POLISH-5b : bind sur StatusKind.Untargetable (Voile d'Ombre Nightseer)
            // Necram (Marques/)
            { "marque_venin_putrefaction",      "_veninStacksFrames" },
            // Ghostra (Marques/) — Plaie Ouverte est partagee avec Soulrender (le sprite est dans Soulrender/Marks)
            { "marque_ciblage_ghostra",         "_marqueDeLOmbreFrames" },
            // Colossar (Marques/) - marque_fondation = ressource (HUD ressource bar), pas un overlay
            // Status sur le combatant. Skip (le HUD Resource Panel gere lui-meme l'affichage).
            { "marque_fondation",               null },
        };

        // Pour PlaieOuverte specifiquement : meme sprite Soulrender (Marks/plaie_ouverte_4frame.png)
        // applique aussi au field Ghostra PlaieOuverte. On gere ca apres le mapping principal.

        // TerrainSpriteLibrary : nom du field -> prefixe filename
        private static readonly Dictionary<string, string> TerrainFilenameToField = new Dictionary<string, string>
        {
            { "sang_coagule",        "_sangCoaguleFrames" },
            { "tiles_vapeur_carmin", "_vapeurCarminFrames" },
            { "tiles_zone_putride",  "_brumeToxiqueFrames" },
            // tiles_piege_runique -> TrapView a son propre _trapFrames (pas dans TerrainSpriteLibrary)
        };

        // VFXSpriteLibrary : prefixe filename -> nom du field
        // Convention designer : "VFX_<nom>_Nframe.gif" ou "<CLASSE>_sort_signature_vfx_Nframe[1-N].png"
        private static readonly Dictionary<string, string> VfxFilenameToField = new Dictionary<string, string>
        {
            { "VFX_strates_qui_sempilent",          "_effondrementFrames" }, // Colossar Effondrement
            { "VFX_sort_signature",                 "_virusFatalFrames" },   // Necram (folder VFX/, mono-fichier .gif)
            { "GHOSTRA_sort_signature_vfx",         "_executionSpectraleFrames" }, // Ghostra (frames separees PNG)
            // Soulrender Ame Lacérée / Nightseer Traquenard : VFX non livres par le designer (cf audit POLISH-1).
        };

        // -------------------------------------------------------------
        // Menu entry
        // -------------------------------------------------------------
        [MenuItem("Nymora/Setup/Populate Combat Sprite Libraries")]
        public static void Run()
        {
            var markLib = AssetDatabase.LoadAssetAtPath<MarkSpriteLibrary>(MarkLibPath);
            var terrainLib = AssetDatabase.LoadAssetAtPath<TerrainSpriteLibrary>(TerrainLibPath);
            var vfxLib = AssetDatabase.LoadAssetAtPath<VFXSpriteLibrary>(VfxLibPath);

            if (markLib == null) { Debug.LogError($"[PopulateCombatSpriteLibraries] MarkSpriteLibrary non trouve a {MarkLibPath}"); return; }
            if (terrainLib == null) { Debug.LogError($"[PopulateCombatSpriteLibraries] TerrainSpriteLibrary non trouve a {TerrainLibPath}"); return; }
            if (vfxLib == null) { Debug.LogError($"[PopulateCombatSpriteLibraries] VFXSpriteLibrary non trouve a {VfxLibPath}"); return; }

            int totalAssigned = 0;

            // === MARQUES ===
            // Scan Marks/ + Marques/ de toutes les classes
            string[] markFolders = {
                $"{ArtRoot}/Soulrender/Marks",
                $"{ArtRoot}/Nightseer/Marks",
                $"{ArtRoot}/Colossar/Marques",
                $"{ArtRoot}/Necram/Marques",
                $"{ArtRoot}/Ghostra/Marques",
            };
            foreach (string folder in markFolders)
            {
                if (!Directory.Exists(folder)) continue;
                totalAssigned += AssignFramesFromFolder(folder, MarkFilenameToField, markLib);
            }

            // Cas special : PlaieOuverte (Ghostra StatusKind) partage le sprite Soulrender/Marks/plaie_ouverte
            // Le mapping principal a deja set _antiHealShieldFrames (Soulrender) avec plaie_ouverte. On
            // duplique sur _plaieOuverteFrames (Ghostra) pour que les deux StatusKind referencent le meme art.
            Sprite[] plaieFrames = LoadFramesByPrefix($"{ArtRoot}/Soulrender/Marks", "plaie_ouverte");
            if (plaieFrames != null && plaieFrames.Length > 0)
            {
                SetField(markLib, "_plaieOuverteFrames", plaieFrames);
                totalAssigned++;
                Debug.Log($"[PopulateCombatSpriteLibraries] MarkLib : _plaieOuverteFrames <- {plaieFrames.Length} frame(s) (partage Soulrender/Marks/plaie_ouverte)");
            }

            EditorUtility.SetDirty(markLib);

            // === TERRAINS ===
            string[] terrainFolders = {
                $"{ArtRoot}/Soulrender/Terrains",
                $"{ArtRoot}/Necram/Tiles",
                // Ajouter ici si autres classes posent du terrain (Ghostra/Colossar/Nightseer)
            };
            foreach (string folder in terrainFolders)
            {
                if (!Directory.Exists(folder)) continue;
                totalAssigned += AssignFramesFromFolder(folder, TerrainFilenameToField, terrainLib);
            }
            EditorUtility.SetDirty(terrainLib);

            // === VFX ===
            string[] vfxFolders = {
                $"{ArtRoot}/Colossar/VFX",
                $"{ArtRoot}/Necram/VFX",
                $"{ArtRoot}/Ghostra/VFX",
                // Soulrender/VFX et Nightseer/VFX n'existent pas (assets non livres designer, cf audit POLISH-1)
            };
            foreach (string folder in vfxFolders)
            {
                if (!Directory.Exists(folder)) continue;
                totalAssigned += AssignFramesFromFolder(folder, VfxFilenameToField, vfxLib);
            }
            EditorUtility.SetDirty(vfxLib);

            // POLISH-5 fix : on retire AssetDatabase.Refresh() ici (Lorenzo 17 mai). Le Refresh
            // global declenche un re-scan complet du dossier Assets/ qui notifie tous les
            // AssetModificationProcessor enregistres, dont GridPaintingState (package
            // com.unity.2d.tilemap) qui a un bug d'init noisy mais inoffensif :
            //   "Instance of GridPaintingState couldn't be created. The script class needs..."
            // SaveAssets() suffit pour persister les modifications des 3 SO sur disque. Si
            // de nouveaux assets visuels sont a importer, Lorenzo lance Refresh manuellement
            // une seule fois en amont.
            AssetDatabase.SaveAssets();

            Debug.Log($"[PopulateCombatSpriteLibraries] OK : {totalAssigned} champ(s) assigne(s) au total. Les 3 SO (Mark/Terrain/VFX) ont ete refreshes.");
        }

        // -------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------

        /// <summary>
        /// Scan tous les .png/.gif dans le dossier, agrege les frames numerotees (1,2,3,4) en array,
        /// et bind sur le field correspondant via reflection.
        /// </summary>
        private static int AssignFramesFromFolder(string folder, Dictionary<string, string> filenameToField, ScriptableObject targetSo)
        {
            int assigned = 0;
            // Inventorie les fichiers par prefix (sans suffix numerique de frame).
            // Convention : "X_Nframe.png" (mono) ou "X_Nframe-export[1-N].png" / "X_NframeN.png" (separees)
            var grouped = new Dictionary<string, List<(string path, int frameIdx)>>();
            foreach (string file in Directory.GetFiles(folder))
            {
                if (file.EndsWith(".meta")) continue;
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".png" && ext != ".gif") continue;

                string filename = Path.GetFileNameWithoutExtension(file);
                // Cherche un prefixe matchant.
                string matchedPrefix = null;
                foreach (string prefix in filenameToField.Keys)
                {
                    if (filename.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedPrefix = prefix;
                        break;
                    }
                }
                if (matchedPrefix == null) continue;

                int frameIdx = ParseFrameIndex(filename);
                if (!grouped.ContainsKey(matchedPrefix)) grouped[matchedPrefix] = new List<(string, int)>();
                grouped[matchedPrefix].Add((file, frameIdx));
            }

            foreach (var kv in grouped)
            {
                string prefix = kv.Key;
                string fieldName = filenameToField[prefix];
                if (string.IsNullOrEmpty(fieldName))
                {
                    Debug.Log($"[PopulateCombatSpriteLibraries] Skip : {prefix} (field=null, mapping marque le sprite comme non-binde)");
                    continue;
                }

                // Tri par frameIdx (0 = pas de num suffix = mono frame).
                var sorted = kv.Value.OrderBy(t => t.frameIdx).ToList();

                // Si plusieurs frames numerotees existent (1..N), on prefere les frames separees.
                // Sinon, on garde l'unique frame mono.
                var framesUsed = sorted.Where(t => t.frameIdx > 0).ToList();
                if (framesUsed.Count == 0)
                {
                    framesUsed = sorted; // fallback : prend le mono (frameIdx=0)
                }

                Sprite[] frames = LoadSpritesFromPaths(framesUsed.Select(t => t.path).ToList());
                if (frames == null || frames.Length == 0)
                {
                    Debug.LogWarning($"[PopulateCombatSpriteLibraries] Skip : {prefix} -> aucun Sprite chargeable depuis {framesUsed.Count} fichier(s) (verifier Import Settings : Texture Type=Sprite)");
                    continue;
                }

                if (SetField(targetSo, fieldName, frames))
                {
                    assigned++;
                    Debug.Log($"[PopulateCombatSpriteLibraries] {targetSo.name} : {fieldName} <- {frames.Length} frame(s) depuis '{prefix}' ({folder})");
                }
            }

            return assigned;
        }

        /// <summary>
        /// Charge les Sprite asset depuis une liste de paths. Si le fichier n'est pas
        /// importe en Sprite (Texture Type=Default Texture par defaut), on auto-fix les
        /// Import Settings (Sprite + Point filter + No compression + PixelsPerUnit=128
        /// par defaut Nymora) et on reimporte avant de charger.
        /// </summary>
        private static Sprite[] LoadSpritesFromPaths(List<string> paths)
        {
            var result = new List<Sprite>();
            foreach (string p in paths)
            {
                Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (s == null)
                {
                    // Auto-fix Import Settings : convertir en Sprite si pas encore.
                    if (EnsureSpriteImporter(p))
                    {
                        s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                    }
                }
                if (s != null) result.Add(s);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Force TextureImporter en mode Sprite + settings pixel-art Nymora (Point filter,
        /// No compression). PixelsPerUnit auto-pick :
        ///   - Si filename contient "-export" (frames POLISH-3 designer, plus haute res) : PPU=192
        ///   - Sinon : PPU=128 (default Nymora pour anciens assets mono-frame)
        /// Returns true si le settings a ete modifie et que le reimport est OK, false sinon.
        /// POLISH-5b (17 mai, 2nd pass) :
        ///   - PPU bumped 256 -> 192 (Lorenzo : tiles encore trop grandes a 256, compromis 192)
        ///   - Ajout ForceSynchronousImport apres SaveAndReimport pour que le LoadAssetAtPath
        ///     immediatement suivant retourne bien le Sprite (sans cycle Editor manquant qui
        ///     faisait Skip "aucun Sprite chargeable" malgre l'auto-fix).
        /// </summary>
        private static bool EnsureSpriteImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return false;

            string filename = Path.GetFileNameWithoutExtension(assetPath);
            float targetPPU = filename.Contains("-export") ? 192f : 128f;

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }
            if (importer.spritePixelsPerUnit != targetPPU)
            {
                importer.spritePixelsPerUnit = targetPPU;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                // CRITIQUE : SaveAndReimport est SUPPOSE synchrone mais le Sprite n'est pas
                // toujours immediatement visible via LoadAssetAtPath<Sprite>(). On force une
                // 2eme reimport synchrone explicite pour garantir que l'asset-database voit
                // le Sprite avant le Load suivant.
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Debug.Log($"[PopulateCombatSpriteLibraries] Auto-fix Import Settings : {assetPath} -> Sprite/Point/Uncompressed/PPU={targetPPU}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Variante : charge tous les Sprite assets du dossier matchant un prefixe (utilise pour
        /// le cas special "plaie_ouverte" partage Soulrender/Ghostra).
        /// </summary>
        private static Sprite[] LoadFramesByPrefix(string folder, string prefix)
        {
            if (!Directory.Exists(folder)) return null;
            var matched = new List<(string path, int frameIdx)>();
            foreach (string file in Directory.GetFiles(folder))
            {
                if (file.EndsWith(".meta")) continue;
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".png" && ext != ".gif") continue;
                string filename = Path.GetFileNameWithoutExtension(file);
                if (!filename.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                matched.Add((file, ParseFrameIndex(filename)));
            }
            var sorted = matched.OrderBy(t => t.frameIdx).ToList();
            var framesUsed = sorted.Where(t => t.frameIdx > 0).ToList();
            if (framesUsed.Count == 0) framesUsed = sorted;
            return LoadSpritesFromPaths(framesUsed.Select(t => t.path).ToList());
        }

        /// <summary>
        /// Parse un index de frame depuis un filename. Returns 0 si pas de suffix numerique.
        /// Supporte les conventions : "X_4frame.png" (=0, mono), "X_4frame1.png" (=1),
        /// "X_4frame-export2.png" (=2), "X_8frame3.png" (=3), etc.
        /// </summary>
        private static int ParseFrameIndex(string filename)
        {
            // Chercher le dernier groupe de chiffres a la fin.
            int i = filename.Length - 1;
            while (i >= 0 && char.IsDigit(filename[i])) i--;
            if (i == filename.Length - 1) return 0; // pas de chiffres a la fin
            string digits = filename.Substring(i + 1);
            return int.TryParse(digits, out int v) ? v : 0;
        }

        /// <summary>
        /// Set un field prive via reflection. Retourne true si OK, false sinon.
        /// </summary>
        private static bool SetField(ScriptableObject target, string fieldName, Sprite[] value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                Debug.LogWarning($"[PopulateCombatSpriteLibraries] Field {fieldName} introuvable sur {target.GetType().Name}");
                return false;
            }
            field.SetValue(target, value);
            return true;
        }
    }
}
