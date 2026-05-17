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
    /// POLISH-5c — outil unique de normalisation et bind des assets visuels de combat.
    ///
    /// Scan systematique des 4 sous-dossiers Marks/Marques/Tiles/VFX de chaque classe
    /// (Soulrender, Nightseer, Colossar, Necram, Ghostra) et :
    ///   1. NORMALIZE : unifie les Import Settings de chaque PNG (Sprite/Single/Point/
    ///      Uncompressed/Pivot center) avec PPU calcule dynamiquement depuis la resolution
    ///      du fichier : PPU = max(width, height) / targetUnits. Resultat : chaque sprite
    ///      mesure exactement targetUnits en monde Unity, peu importe sa resolution source
    ///      (128, 192, 256...). Plus de tile geante a cote d'une tile minuscule.
    ///
    ///   2. APPLY TO LIBRARIES : detecte les series {nom}-export[1..N].png, les groupe,
    ///      auto-match le prefixe vers un OU PLUSIEURS fields des 3 SO (Mark/Terrain/VFX
    ///      SpriteLibrary) via PrefixMap. Multi-binding supporte : un meme sprite peut
    ///      remplir plusieurs fields (ex : plaie_ouverte bind a la fois _antiHealShieldFrames
    ///      Soulrender ET _plaieOuverteFrames Ghostra car ils partagent le visuel).
    ///
    /// SUPERSEDE PopulateCombatSpriteLibraries.cs. L'ancien populator est conserve pour
    /// compat mais ne plus le lancer apres ce normalizer (son helper EnsureSpriteImporter
    /// ecrase le PPU dynamique avec 128 ou 192 hardcode).
    ///
    /// Hypothese (17 mai) : tous les .gif ont ete supprimes, le designer livre uniquement
    /// du .png en frames separees {nom}-export[1..N].png.
    ///
    /// Menu : Nymora &gt; Validation &gt; Normalize Combat Assets
    /// </summary>
    public class CombatAssetsNormalizer : EditorWindow
    {
        // ==================================================================
        // Chemins
        // ==================================================================
        private const string ArtRoot = "Assets/_Nymora/Art/Sprites";
        private const string LibrariesFolder = "Assets/_Nymora/ScriptableObjects/Combat";
        private const string MarkLibPath = LibrariesFolder + "/MarkSpriteLibrary.asset";
        private const string TerrainLibPath = LibrariesFolder + "/TerrainSpriteLibrary.asset";
        private const string VfxLibPath = LibrariesFolder + "/VFXSpriteLibrary.asset";

        private static readonly string[] Classes = { "Soulrender", "Nightseer", "Colossar", "Necram", "Ghostra" };

        // Dossiers categorise par type d'asset (le designer alterne Marks/Marques en FR/EN
        // selon la classe — on inclut les 2 orthographes).
        private static readonly string[] MarkFolders = { "Marks", "Marques" };
        private static readonly string[] TerrainFolders = { "Tiles", "Terrains" };
        private static readonly string[] VfxFolders = { "VFX" };

        // ==================================================================
        // Mapping convention de nommage : prefix -> liste de bindings (1+)
        // ==================================================================
        // Synchronise avec MarkSpriteLibrary.cs / TerrainSpriteLibrary.cs / VFXSpriteLibrary.cs.
        // Ajouter une entree ici quand le designer livre un nouvel asset.
        //
        // Matching = startsWith (insensitive). Si plusieurs prefixes matchent (ex : "marque_traque"
        // et "marque_traque_oeil_pulsant" matchent tous les deux "marque_traque_oeil_pulsant_4frame"),
        // SEUL LE PLUS LONG est retenu (long-prefix wins). Permet d'avoir des aliases courts en fallback.
        //
        // Multi-binding : si plusieurs Binding sont declares dans la liste d'un prefix, le sprite
        // est pousse dans tous les fields. Ex : plaie_ouverte -> Soulrender AntiHealShield + Ghostra
        // PlaieOuverte (memes 4 frames partagees entre les 2 classes par design Bible V7.1).
        private enum LibKind { Mark, Terrain, Vfx }

        private struct Binding
        {
            public LibKind Library;
            public string FieldName;
            public string DisplayName;
        }

        private static readonly List<(string Prefix, List<Binding> Binds)> PrefixMap =
            new List<(string, List<Binding>)>
        {
            // === MARKS ===
            // Soulrender : plaie_ouverte est SHARED avec Ghostra (memes 4 frames, 2 fields)
            ("plaie_ouverte", new List<Binding>
            {
                new Binding { Library = LibKind.Mark, FieldName = "_antiHealShieldFrames", DisplayName = "Soulrender AntiHealShield" },
                new Binding { Library = LibKind.Mark, FieldName = "_plaieOuverteFrames",   DisplayName = "Ghostra PlaieOuverte (shared)" },
            }),
            ("marque_de_carnage", new List<Binding>
            {
                new Binding { Library = LibKind.Mark, FieldName = "_markedByCarnageFrames", DisplayName = "Soulrender MarkedByCarnage" },
            }),
            // Nightseer
            ("marque_traque_oeil_pulsant", new List<Binding>
            {
                new Binding { Library = LibKind.Mark, FieldName = "_traqueFrames", DisplayName = "Nightseer Traque" },
            }),
            ("marque_traque", new List<Binding>
            {
                new Binding { Library = LibKind.Mark, FieldName = "_traqueFrames", DisplayName = "Nightseer Traque (alias)" },
            }),
            ("marque_empreinte", new List<Binding>
            {
                new Binding { Library = LibKind.Mark, FieldName = "_empreinteFrames", DisplayName = "Nightseer Empreinte" },
            }),
            ("marque_voile_brume", new List<Binding>
            {
                new Binding { Library = LibKind.Mark, FieldName = "_untargetableFrames", DisplayName = "Nightseer Untargetable (Voile)" },
            }),
            // Necram
            ("marque_venin_putrefaction", new List<Binding>
            {
                new Binding { Library = LibKind.Mark, FieldName = "_veninStacksFrames", DisplayName = "Necram Venin Stacks" },
            }),
            ("marque_venin", new List<Binding>
            {
                new Binding { Library = LibKind.Mark, FieldName = "_veninStacksFrames", DisplayName = "Necram Venin Stacks (alias)" },
            }),
            // Ghostra
            ("marque_ciblage_ghostra", new List<Binding>
            {
                new Binding { Library = LibKind.Mark, FieldName = "_marqueDeLOmbreFrames", DisplayName = "Ghostra MarqueDeLOmbre" },
            }),
            ("marque_de_lombre", new List<Binding>
            {
                new Binding { Library = LibKind.Mark, FieldName = "_marqueDeLOmbreFrames", DisplayName = "Ghostra MarqueDeLOmbre (alias)" },
            }),

            // === TERRAINS ===
            ("sang_coagule", new List<Binding>
            {
                new Binding { Library = LibKind.Terrain, FieldName = "_sangCoaguleFrames", DisplayName = "Soulrender SangCoagule" },
            }),
            ("tiles_vapeur_carmin", new List<Binding>
            {
                new Binding { Library = LibKind.Terrain, FieldName = "_vapeurCarminFrames", DisplayName = "Soulrender VapeurCarmin" },
            }),
            ("vapeur_carmin", new List<Binding>
            {
                new Binding { Library = LibKind.Terrain, FieldName = "_vapeurCarminFrames", DisplayName = "Soulrender VapeurCarmin (alias)" },
            }),
            ("tiles_zone_putride", new List<Binding>
            {
                new Binding { Library = LibKind.Terrain, FieldName = "_brumeToxiqueFrames", DisplayName = "Necram BrumeToxique" },
            }),
            ("zone_putride", new List<Binding>
            {
                new Binding { Library = LibKind.Terrain, FieldName = "_brumeToxiqueFrames", DisplayName = "Necram BrumeToxique (alias)" },
            }),
            ("brume_toxique", new List<Binding>
            {
                new Binding { Library = LibKind.Terrain, FieldName = "_brumeToxiqueFrames", DisplayName = "Necram BrumeToxique (alias)" },
            }),

            // === VFX ===
            ("VFX_ame_laceree", new List<Binding>
            {
                new Binding { Library = LibKind.Vfx, FieldName = "_ameLaceeFrames", DisplayName = "Soulrender Ame Laceree" },
            }),
            ("VFX_strates_qui_sempilent", new List<Binding>
            {
                new Binding { Library = LibKind.Vfx, FieldName = "_effondrementFrames", DisplayName = "Colossar Effondrement" },
            }),
            ("VFX_effondrement", new List<Binding>
            {
                new Binding { Library = LibKind.Vfx, FieldName = "_effondrementFrames", DisplayName = "Colossar Effondrement (alias)" },
            }),
            ("VFX_virus_fatal", new List<Binding>
            {
                new Binding { Library = LibKind.Vfx, FieldName = "_virusFatalFrames", DisplayName = "Necram Virus Fatal" },
            }),
            ("VFX_sort_signature_necram", new List<Binding>
            {
                new Binding { Library = LibKind.Vfx, FieldName = "_virusFatalFrames", DisplayName = "Necram Virus Fatal (alias)" },
            }),
            ("VFX_sort_signature", new List<Binding>
            {
                new Binding { Library = LibKind.Vfx, FieldName = "_virusFatalFrames", DisplayName = "Necram Virus Fatal (legacy)" },
            }),
            ("VFX_execution_spectrale", new List<Binding>
            {
                new Binding { Library = LibKind.Vfx, FieldName = "_executionSpectraleFrames", DisplayName = "Ghostra Execution Spectrale" },
            }),
            ("GHOSTRA_sort_signature_vfx", new List<Binding>
            {
                new Binding { Library = LibKind.Vfx, FieldName = "_executionSpectraleFrames", DisplayName = "Ghostra Execution Spectrale (alias)" },
            }),
        };

        // ==================================================================
        // State window
        // ==================================================================
        private float _targetUnits = 1f;
        private Vector2 _scroll;
        private List<Series> _series = new List<Series>();
        private bool _scanned;
        private string _lastLog = "";

        [MenuItem("Nymora/Validation/Normalize Combat Assets")]
        public static void Open()
        {
            var win = GetWindow<CombatAssetsNormalizer>(true, "Combat Assets Normalizer", true);
            win.minSize = new Vector2(720, 560);
            win.Show();
        }

        // ==================================================================
        // GUI
        // ==================================================================
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Combat Assets Normalizer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Scan Art/Sprites/{5 classes}/{Marks|Marques,Tiles,Terrains,VFX}/, " +
                "uniformise les Import Settings (PPU dynamique = max(W,H)/targetUnits) et bind les frames " +
                "dans les 3 SO (Mark/Terrain/VFX SpriteLibrary).\n\n" +
                "Multi-binding supporte (ex : plaie_ouverte -> Soulrender + Ghostra).\n" +
                "Workflow recommande : Do All (= Normalize + Scan + Apply enchainés).",
                MessageType.Info);

            _targetUnits = EditorGUILayout.Slider(
                new GUIContent(
                    "Target Size (units)",
                    "Taille cible du sprite en unites Unity. 1.0 = sprite remplit pile la largeur d'une tile."),
                _targetUnits, 0.5f, 2f);

            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("1. Normalize", GUILayout.Height(34)))
                {
                    NormalizeAll();
                }
                if (GUILayout.Button("2. Scan & Bind", GUILayout.Height(34)))
                {
                    ScanAndBind();
                }
                using (new EditorGUI.DisabledScope(!_scanned || _series.Count == 0))
                {
                    if (GUILayout.Button("3. Apply to Libraries", GUILayout.Height(34)))
                    {
                        ApplyToLibraries();
                    }
                }
                if (GUILayout.Button("Do All", GUILayout.Height(34)))
                {
                    NormalizeAll();
                    ScanAndBind();
                    ApplyToLibraries();
                }
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Dump Libraries State (check actual content)", GUILayout.Height(24)))
            {
                DumpLibrariesState();
            }
            EditorGUILayout.Space(2);
            var nukeStyle = new GUIStyle(GUI.skin.button);
            nukeStyle.normal.textColor = new Color(1f, 0.5f, 0.3f);
            if (GUILayout.Button(
                "Nuke Broken Metas (force regen for spriteMode=Multiple .meta)",
                nukeStyle, GUILayout.Height(24)))
            {
                NukeBrokenMetas();
            }

            EditorGUILayout.Space(10);
            DrawSeriesSection("Marks", LibKind.Mark);
            DrawSeriesSection("Terrains", LibKind.Terrain);
            DrawSeriesSection("VFX", LibKind.Vfx);
            DrawUnmappedSection();

            EditorGUILayout.Space(6);
            if (!string.IsNullOrEmpty(_lastLog))
            {
                EditorGUILayout.LabelField("Last run", EditorStyles.boldLabel);
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(100), GUILayout.MaxHeight(200));
                EditorGUILayout.SelectableLabel(_lastLog, EditorStyles.textArea, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSeriesSection(string title, LibKind kind)
        {
            // Une serie est dans la section si AU MOINS un de ses bindings est de cette kind.
            var subset = _series.Where(s => s.Mapped && s.Bindings.Any(b => b.Library == kind)).ToList();
            EditorGUILayout.LabelField($"{title} ({subset.Count})", EditorStyles.boldLabel);
            if (!_scanned)
            {
                EditorGUILayout.LabelField("  (clique 'Scan & Bind' pour analyser)", EditorStyles.miniLabel);
                return;
            }
            if (subset.Count == 0)
            {
                EditorGUILayout.LabelField("  (rien)", EditorStyles.miniLabel);
                return;
            }
            foreach (var s in subset)
            {
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    s.Apply = EditorGUILayout.Toggle(s.Apply, GUILayout.Width(20));
                    EditorGUILayout.LabelField(
                        new GUIContent($"{s.Prefix}  ({s.FramePaths.Count}f)",
                            string.Join("\n", s.FramePaths)),
                        GUILayout.MinWidth(280));
                    // Liste tous les bindings de cette kind (peut etre plusieurs pour multi-binding).
                    var displays = s.Bindings.Where(b => b.Library == kind)
                        .Select(b => $"{b.FieldName} ({b.DisplayName})");
                    EditorGUILayout.LabelField("-> " + string.Join(" + ", displays), EditorStyles.miniLabel);
                }
            }
        }

        private void DrawUnmappedSection()
        {
            var unmapped = _series.Where(s => !s.Mapped).ToList();
            if (unmapped.Count == 0) return;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Unmapped ({unmapped.Count}) — pas de binding connu", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Ces series ont ete normalisees mais ne sont pas bindees a un SO. " +
                "Ajoute leur prefix dans PrefixMap[] (en haut de CombatAssetsNormalizer.cs) pour les binder.",
                MessageType.Warning);
            foreach (var s in unmapped)
            {
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    EditorGUILayout.LabelField(
                        new GUIContent($"{s.Prefix}  ({s.FramePaths.Count}f)",
                            string.Join("\n", s.FramePaths)),
                        GUILayout.MinWidth(320));
                    EditorGUILayout.LabelField($"   (folder: {s.SourceFolder})", EditorStyles.miniLabel);
                }
            }
        }

        // ==================================================================
        // Action 1 : Normalize
        // ==================================================================
        private void NormalizeAll()
        {
            var paths = FindAllPngs();
            if (paths.Count == 0)
            {
                _lastLog = $"[Normalize] Aucun PNG trouve sous {ArtRoot}/<classe>/{{Marks|Marques,Tiles,VFX}}/.";
                Debug.LogWarning(_lastLog);
                return;
            }

            var log = new System.Text.StringBuilder();
            log.AppendLine($"[Normalize] Scan {paths.Count} PNG, targetUnits={_targetUnits}");
            int touched = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var path in paths)
                {
                    if (NormalizeOne(path, _targetUnits, log)) touched++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            log.AppendLine($"[Normalize] OK : {touched}/{paths.Count} fichier(s) modifie(s).");
            _lastLog = log.ToString();
            Debug.Log(_lastLog);
        }

        private static bool NormalizeOne(string assetPath, float targetUnits, System.Text.StringBuilder log)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                log.AppendLine($"  skip (importer null) : {assetPath}");
                return false;
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
            {
                log.AppendLine($"  skip (texture null) : {assetPath}");
                return false;
            }
            int w = tex.width, h = tex.height;
            float targetPPU = Mathf.Max(w, h) / Mathf.Max(0.01f, targetUnits);

            // IMPORTANT : utiliser les setters DIRECTEMENT sur TextureImporter pour
            // spriteImportMode/spritePixelsPerUnit, pas via TextureImporterSettings.
            // Unity ignore certains champs de TextureImporterSettings lors de
            // SetTextureSettings — notamment spriteMode (= spriteImportMode), ce qui laisse
            // ces .meta avec spriteMode=2 (Multiple) heritage des anciens .gif slicés et
            // empeche LoadAssetAtPath&lt;Sprite&gt; de renvoyer le sprite principal.
            // Pour spriteAlignment + spritePivot, on doit passer par TextureImporterSettings
            // car pas de setter direct sur importer.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

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
            if (settings.spriteAlignment != (int)SpriteAlignment.Center)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                changed = true;
            }
            else if (settings.spritePivot != new Vector2(0.5f, 0.5f))
            {
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                changed = true;
            }
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, targetPPU))
            {
                importer.spritePixelsPerUnit = targetPPU;
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
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (!changed)
            {
                log.AppendLine($"  ok : {Path.GetFileName(assetPath)} [{w}x{h}, PPU={targetPPU:0.#}] (already normalized)");
                return false;
            }

            // Applique les settings (alignment/pivot) ET sauve l'importer (mode/PPU/etc).
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            // CRITIQUE : SaveAndReimport est SUPPOSE synchrone mais le Sprite n'est pas
            // toujours immediatement visible via LoadAssetAtPath<Sprite>() apres un changement
            // de textureType (Default -> Sprite). On force une 2eme reimport synchrone
            // pour garantir que ApplyToLibraries (qui suit) charge bien le Sprite.
            // Sans ca, marque_de_carnage et plaie_ouverte (premiers binds apres normalize)
            // echouaient avec "no loadable sprite".
            AssetDatabase.ImportAsset(assetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            log.AppendLine($"  fixed : {Path.GetFileName(assetPath)} [{w}x{h}] -> PPU={targetPPU:0.#}");
            return true;
        }

        // ==================================================================
        // Action 2 : Scan & Bind
        // ==================================================================
        private void ScanAndBind()
        {
            _series.Clear();

            // Groupe les PNG par (folder, prefix). Inclut le folder dans la clef pour eviter
            // qu'un prefix homonyme entre 2 classes (ex : VFX_sort_signature) ne fusionne.
            var groups = new Dictionary<string, GroupAccum>();

            foreach (var path in FindAllPngs())
            {
                string filename = Path.GetFileNameWithoutExtension(path);
                string folder = Path.GetDirectoryName(path).Replace('\\', '/');
                string prefix = ExtractSeriesPrefix(filename);
                int idx = ParseFrameIndex(filename);
                string key = folder + "|" + prefix;

                if (!groups.TryGetValue(key, out var g))
                {
                    g = new GroupAccum { Folder = folder, Prefix = prefix, Frames = new List<(string, int)>() };
                    groups[key] = g;
                }
                g.Frames.Add((path, idx));
            }

            foreach (var g in groups.Values)
            {
                var sorted = g.Frames.OrderBy(t => t.Item2).ToList();
                var framesUsed = sorted.Where(t => t.Item2 > 0).ToList();
                if (framesUsed.Count == 0) framesUsed = sorted;

                var s = new Series
                {
                    Prefix = g.Prefix,
                    SourceFolder = g.Folder,
                    FramePaths = framesUsed.Select(t => t.Item1).ToList(),
                    Apply = true,
                    Bindings = new List<Binding>(),
                };

                if (TryAutoMatch(g.Prefix, out var binds))
                {
                    s.Mapped = true;
                    s.Bindings = binds;
                }
                _series.Add(s);
            }

            _series = _series.OrderBy(s => !s.Mapped)
                             .ThenBy(s => s.Mapped ? s.Bindings[0].Library : LibKind.Mark)
                             .ThenBy(s => s.Prefix)
                             .ToList();
            _scanned = true;

            var log = new System.Text.StringBuilder();
            int mappedCount = _series.Count(s => s.Mapped);
            log.AppendLine($"[Scan] {_series.Count} serie(s) detectee(s), {mappedCount} bindee(s) automatiquement.");
            foreach (var s in _series.Where(x => x.Mapped))
            {
                string targets = string.Join(" + ", s.Bindings.Select(b => b.FieldName));
                log.AppendLine($"  ok  : {s.Prefix} ({s.FramePaths.Count}f) -> {targets}");
            }
            foreach (var s in _series.Where(x => !x.Mapped))
            {
                log.AppendLine($"  ??? : {s.Prefix} ({s.FramePaths.Count}f) @ {s.SourceFolder}");
            }
            _lastLog = log.ToString();
            Debug.Log(_lastLog);
            Repaint();
        }

        // ==================================================================
        // Action 3 : Apply
        // ==================================================================
        private void ApplyToLibraries()
        {
            var markLib = AssetDatabase.LoadAssetAtPath<MarkSpriteLibrary>(MarkLibPath);
            var terrainLib = AssetDatabase.LoadAssetAtPath<TerrainSpriteLibrary>(TerrainLibPath);
            var vfxLib = AssetDatabase.LoadAssetAtPath<VFXSpriteLibrary>(VfxLibPath);

            if (markLib == null) { Debug.LogError($"[Apply] MarkSpriteLibrary introuvable a {MarkLibPath}"); return; }
            if (terrainLib == null) { Debug.LogError($"[Apply] TerrainSpriteLibrary introuvable a {TerrainLibPath}"); return; }
            if (vfxLib == null) { Debug.LogError($"[Apply] VFXSpriteLibrary introuvable a {VfxLibPath}"); return; }

            var log = new System.Text.StringBuilder();
            log.AppendLine("[Apply] Push series vers les 3 SO :");
            int boundFields = 0, skipped = 0, failed = 0;

            foreach (var s in _series)
            {
                if (!s.Mapped) { skipped++; continue; }
                if (!s.Apply) { log.AppendLine($"  skip (unchecked) : {s.Prefix}"); skipped++; continue; }

                var sprites = s.FramePaths
                    .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p))
                    .Where(sp => sp != null)
                    .ToArray();
                if (sprites.Length == 0)
                {
                    log.AppendLine($"  fail (no loadable sprite — lance 'Normalize' avant) : {s.Prefix}");
                    failed++;
                    continue;
                }

                foreach (var bind in s.Bindings)
                {
                    ScriptableObject target = bind.Library switch
                    {
                        LibKind.Mark => markLib,
                        LibKind.Terrain => terrainLib,
                        LibKind.Vfx => vfxLib,
                        _ => null,
                    };
                    if (target == null) { failed++; continue; }

                    if (!SetField(target, bind.FieldName, sprites))
                    {
                        log.AppendLine($"  fail (field {bind.FieldName} introuvable sur {target.GetType().Name}) : {s.Prefix}");
                        failed++;
                        continue;
                    }
                    boundFields++;
                    log.AppendLine($"  ok : {s.Prefix} -> {target.GetType().Name}.{bind.FieldName} ({sprites.Length}f) [{bind.DisplayName}]");
                }
            }

            EditorUtility.SetDirty(markLib);
            EditorUtility.SetDirty(terrainLib);
            EditorUtility.SetDirty(vfxLib);
            AssetDatabase.SaveAssets();

            log.AppendLine($"[Apply] OK : boundFields={boundFields}, skipped={skipped}, failed={failed}");
            _lastLog = log.ToString();
            Debug.Log(_lastLog);
        }

        // ==================================================================
        // Arme nucleaire : supprime les .meta foireux (spriteMode: Multiple
        // legacy) pour forcer Unity a regenerer from scratch via le postprocessor.
        // Utile UNIQUEMENT si NormalizeAll echoue a passer le mode de Multiple
        // -> Single (cas observe sur d'anciens .meta heritage de spritesheets slicees).
        // ==================================================================
        private void NukeBrokenMetas()
        {
            var paths = FindAllPngs();
            var log = new System.Text.StringBuilder();
            log.AppendLine("[NukeMetas] Scan des .meta pour spriteMode: 2 (Multiple legacy) :");
            int nuked = 0;
            foreach (var path in paths)
            {
                string metaPath = path + ".meta";
                if (!System.IO.File.Exists(metaPath)) continue;
                string content;
                try { content = System.IO.File.ReadAllText(metaPath); }
                catch { continue; }
                if (!content.Contains("spriteMode: 2")) continue;

                try
                {
                    System.IO.File.Delete(metaPath);
                    log.AppendLine($"  nuked : {Path.GetFileName(path)} (was spriteMode=Multiple)");
                    nuked++;
                }
                catch (System.Exception ex)
                {
                    log.AppendLine($"  fail : {Path.GetFileName(path)} ({ex.Message})");
                }
            }
            if (nuked > 0)
            {
                // Force Unity a regenerer les .meta via le postprocessor (qui ne force
                // plus Multiple depuis POLISH-5c) et un import synchrone.
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            log.AppendLine($"[NukeMetas] OK : {nuked} .meta supprime(s). Relance 'Do All' pour normaliser et binder.");
            _lastLog = log.ToString();
            Debug.Log(_lastLog);
        }

        // ==================================================================
        // Diag : Dump l'etat actuel des 3 SO (sans rien modifier)
        // ==================================================================
        private void DumpLibrariesState()
        {
            var markLib = AssetDatabase.LoadAssetAtPath<MarkSpriteLibrary>(MarkLibPath);
            var terrainLib = AssetDatabase.LoadAssetAtPath<TerrainSpriteLibrary>(TerrainLibPath);
            var vfxLib = AssetDatabase.LoadAssetAtPath<VFXSpriteLibrary>(VfxLibPath);

            var log = new System.Text.StringBuilder();
            log.AppendLine("[Dump] Etat actuel des 3 SO :");
            DumpOne(log, "MarkSpriteLibrary", markLib);
            DumpOne(log, "TerrainSpriteLibrary", terrainLib);
            DumpOne(log, "VFXSpriteLibrary", vfxLib);
            _lastLog = log.ToString();
            Debug.Log(_lastLog);
        }

        private static void DumpOne(System.Text.StringBuilder log, string label, ScriptableObject so)
        {
            if (so == null)
            {
                log.AppendLine($"  {label} : NULL (asset non charge)");
                return;
            }
            log.AppendLine($"  {label} ({so.name}) :");
            var fields = so.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(f => f.FieldType == typeof(Sprite[]));
            foreach (var f in fields)
            {
                var arr = f.GetValue(so) as Sprite[];
                if (arr == null) { log.AppendLine($"    {f.Name} = NULL"); continue; }
                int nullCount = arr.Count(sp => sp == null);
                string firstName = arr.Length > 0 && arr[0] != null ? arr[0].name : "<null>";
                string status = nullCount > 0 ? $"  /!\\ {nullCount} sprite(s) null (asset fantome !)" : "";
                log.AppendLine($"    {f.Name} = {arr.Length} frame(s), first='{firstName}'{status}");
            }
        }

        // ==================================================================
        // Helpers
        // ==================================================================
        private static List<string> FindAllPngs()
        {
            var result = new List<string>();
            foreach (var cls in Classes)
            {
                foreach (var sub in MarkFolders.Concat(TerrainFolders).Concat(VfxFolders))
                {
                    string folder = $"{ArtRoot}/{cls}/{sub}";
                    if (!AssetDatabase.IsValidFolder(folder)) continue;
                    string[] guids = AssetDatabase.FindAssets("t:texture2D", new[] { folder });
                    foreach (var guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) continue;
                        result.Add(path);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Extrait le prefixe d'une serie (= identifiant logique de la marque/tile/vfx).
        /// Strip toujours les digits finaux car ils sont TOUJOURS un index de frame
        /// (conventions designer rencontrees) :
        ///   "sang_coagule_4frame-export2"       -> "sang_coagule_4frame"     (idx=2)
        ///   "VFX_ame_laceree_8frame-export5"    -> "VFX_ame_laceree_8frame"  (idx=5)
        ///   "marque_de_carnage_4frame"          -> "marque_de_carnage_4frame" (mono, idx=0)
        ///   "marque_ciblage_ghostra_4frame1"    -> "marque_ciblage_ghostra_4frame" (idx=1)
        ///   "GHOSTRA_sort_signature_vfx_8frame3" -> "GHOSTRA_sort_signature_vfx_8frame" (idx=3)
        ///   "GHOSTRA_tiles_case_voilee_128px"   -> "GHOSTRA_tiles_case_voilee_128px" (mono, finit par 'x')
        /// </summary>
        private static string ExtractSeriesPrefix(string filename)
        {
            // Cas 1 : suffixe "-export[N]"
            int exportIdx = filename.LastIndexOf("-export", System.StringComparison.OrdinalIgnoreCase);
            if (exportIdx > 0)
            {
                string after = filename.Substring(exportIdx + "-export".Length);
                if (int.TryParse(after, out _)) return filename.Substring(0, exportIdx);
            }
            // Cas 2 : digits finaux = index de frame (convention Ghostra "_4frame1", legacy).
            int i = filename.Length - 1;
            while (i >= 0 && char.IsDigit(filename[i])) i--;
            if (i == filename.Length - 1) return filename; // pas de digits finaux -> mono
            return filename.Substring(0, i + 1);
        }

        private static int ParseFrameIndex(string filename)
        {
            int exportIdx = filename.LastIndexOf("-export", System.StringComparison.OrdinalIgnoreCase);
            if (exportIdx > 0)
            {
                string after = filename.Substring(exportIdx + "-export".Length);
                if (int.TryParse(after, out int v)) return v;
            }
            int i = filename.Length - 1;
            while (i >= 0 && char.IsDigit(filename[i])) i--;
            if (i == filename.Length - 1) return 0;
            return int.TryParse(filename.Substring(i + 1), out int idx) ? idx : 0;
        }

        /// <summary>
        /// Cherche le prefix LE PLUS LONG qui matche (insensitive startsWith). Si trouve,
        /// retourne TOUS les bindings declares pour ce prefix (multi-binding possible).
        /// </summary>
        private static bool TryAutoMatch(string prefix, out List<Binding> binds)
        {
            string bestPrefix = null;
            List<Binding> bestBinds = null;
            int bestLen = -1;
            foreach (var (p, b) in PrefixMap)
            {
                if (prefix.StartsWith(p, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (p.Length > bestLen)
                    {
                        bestPrefix = p;
                        bestBinds = b;
                        bestLen = p.Length;
                    }
                }
            }
            if (bestBinds != null)
            {
                binds = bestBinds;
                return true;
            }
            binds = null;
            return false;
        }

        private static bool SetField(ScriptableObject target, string fieldName, Sprite[] value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) return false;
            field.SetValue(target, value);
            return true;
        }

        // ==================================================================
        // Modeles internes
        // ==================================================================
        private class GroupAccum
        {
            public string Folder;
            public string Prefix;
            public List<(string, int)> Frames;
        }

        private class Series
        {
            public string Prefix;
            public string SourceFolder;
            public List<string> FramePaths;
            public bool Mapped;
            public List<Binding> Bindings;
            public bool Apply;
        }
    }
}
