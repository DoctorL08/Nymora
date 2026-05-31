using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// POLISH KYAMI — Tâche 7 (spritesheets) + 8 (normal maps), TEST sur Colossar.
    ///
    /// Bascule le pipeline sprite du Colossar (perso de base + skin Obsidian Titan) de
    /// l'ancien import .aseprite (un fichier par stage/direction, frames packées) vers les
    /// nouveaux SPRITESHEETS fournis par Kyami (un PNG en bande horizontale par
    /// anim/stage/direction, cellules 128×128), AVEC leurs NORMAL MAPS jumelles montées en
    /// texture secondaire "_NormalMap" (le matériau combat = Sprite-Lit-Default URP 2D les lit
    /// → relief sous les 2D lights, sans toucher au matériau).
    ///
    /// Ce que le tool fait, pour CHAQUE rig (base + skin) :
    ///   1. Copie les PNG (sheet + sheet_normal) du pack Kyami (Downloads) dans le projet.
    ///   2. Importe les sheets : Multiple, slice grille 128, PPU 96, pivot custom 0.5/0.1,
    ///      Point + No Compression (cohérent calibration combat 27 mai).
    ///   3. Importe les normals : Default, sRGB OFF (linéaire), Point + No Compression, puis
    ///      les monte en secondarySpriteTextures "_NormalMap" sur le sheet de base correspondant.
    ///   4. Génère un AnimationClip par anim/stage/direction (cadence reprise des clips
    ///      .aseprite actuels ; idle/walk en loop), bindé sur le SpriteRenderer (path "").
    ///   5. REPOINTE LES 6 CONTROLLERS EXISTANTS EN PLACE (mêmes .controller, mêmes GUID) :
    ///      seules les motions des states Idle/Walk/Cast/Attack/Hurt/Death changent. Les
    ///      prefabs Combatant_Colossar + la CosmeticSkinDefinition ObsidianTitan référencent
    ///      ces controllers par GUID → ZÉRO recâblage, ZÉRO bump CombatRulesVersion (View-only).
    ///
    /// Hors scope de ce test (passes suivantes) : tile pilier, VFX strates, torches hub,
    /// shaders maps, autres classes.
    ///
    /// Menu : Nymora > Setup > Polish Kyami > Import Colossar Spritesheets + Normals (Test).
    /// </summary>
    public static class ImportColossarSpritesheetsTool
    {
        // --- Source : pack Kyami dans Downloads (hors projet) ---
        private const string SrcCharRoot = "C:/Users/Lorenzo/Downloads/Polish_Kyami/Polish/character";

        // --- Paramètres d'import (calibration combat verrouillée) ---
        private const int Cell = 128;
        private const float Ppu = 96f;
        private static readonly Vector2 Pivot = new Vector2(0.5f, 0.1f);

        // anim -> nom du state Animator correspondant
        private static readonly (string anim, string state, bool loop)[] Anims =
        {
            ("idle",   "Idle",   true),
            ("walk",   "Walk",   true),
            ("attack", "Attack", false),
            ("cast",   "Cast",   false),
            ("hurt",   "Hurt",   false),
            ("death",  "Death",  false),
        };
        private static readonly string[] Dirs = { "SE", "NE" };
        private const int Stages = 3;

        // Cadence de secours si le clip .aseprite actuel est illisible (fps).
        private static readonly Dictionary<string, float> FallbackFps = new Dictionary<string, float>
        {
            { "idle", 8f }, { "walk", 12f }, { "attack", 12f },
            { "cast", 12f }, { "hurt", 12f }, { "death", 10f },
        };

        private class Rig
        {
            public string Label;
            public string SrcSheetDir;     // dossier source des sheets de base
            public string SrcNormalDir;    // dossier source des normals
            public string DstSheetDir;     // dossier projet sheets
            public string DstNormalDir;    // dossier projet normals
            public string DstClipDir;      // dossier projet clips
            public string ControllerDir;   // dossier des .controller existants à repointer
            public string ControllerPrefix; // ex "Colossar" -> ColossarStage0_SE.controller
            public string ClipPrefix;      // préfixe de nommage des clips
        }

        /// <summary>
        /// Une classe = dossier source du pack Kyami + nom du skin associé (dossier source à
        /// l'espace/casse près + nom PascalCase côté projet). Le reste (controllers, clips, sheets)
        /// se déduit par convention <c>&lt;Class&gt;Stage{n}_{DIR}</c>.
        /// </summary>
        private class ClassDef
        {
            public string Class;        // PascalCase projet, ex "Colossar"
            public string SrcFolder;    // dossier source pack (lowercase), ex "colossar"
            public string SkinSrcDir;   // sous-dossier skin dans le pack (casse/espace exacts)
            public string SkinName;     // skin PascalCase projet (= dossier controllers), ex "ObsidianTitan"
            public string SkinCtrlPrefix; // préfixe des .controller du skin si != SkinName (ex Ashen)
        }

        private static readonly ClassDef[] Classes =
        {
            new ClassDef { Class = "Colossar",   SrcFolder = "colossar",   SkinSrcDir = "OBSIDIAN_TITAN",   SkinName = "ObsidianTitan"  },
            // Soulrender : dossier "AshenSovereign" mais .controller préfixés "Ashen".
            new ClassDef { Class = "Soulrender",  SrcFolder = "soulrender", SkinSrcDir = "ASHEN_SOVEREIGN",  SkinName = "AshenSovereign", SkinCtrlPrefix = "Ashen" },
            new ClassDef { Class = "Nightseer",   SrcFolder = "nightseer",  SkinSrcDir = "VOID_ORACLE",      SkinName = "VoidOracle"     },
            new ClassDef { Class = "Necram",      SrcFolder = "necram",     SkinSrcDir = "PLAGUE_ APOSTLE",  SkinName = "PlagueApostle"  },
            new ClassDef { Class = "Ghostra",     SrcFolder = "ghostra",    SkinSrcDir = "PALE_REVENANT",    SkinName = "PaleRevenant"   },
        };

        // Construit les 2 rigs (base + skin) d'une classe.
        private static IEnumerable<Rig> RigsFor(ClassDef c)
        {
            string classSrc = $"{SrcCharRoot}/{c.SrcFolder}";
            yield return new Rig
            {
                Label            = $"{c.Class} (base)",
                SrcSheetDir      = $"{classSrc}/character_spritesheet",
                SrcNormalDir     = $"{classSrc}/character_spritesheet_normal",
                DstSheetDir      = $"Assets/_Nymora/Art/Sprites/{c.Class}/Base/sheets",
                DstNormalDir     = $"Assets/_Nymora/Art/Sprites/{c.Class}/Base/sheets_normal",
                DstClipDir       = $"Assets/_Nymora/Animations/{c.Class}/Clips",
                ControllerDir    = $"Assets/_Nymora/Animations/{c.Class}",
                ControllerPrefix = c.Class,
                ClipPrefix       = c.Class,
            };
            yield return new Rig
            {
                Label            = $"{c.SkinName} (skin)",
                SrcSheetDir      = $"{classSrc}/{c.SkinSrcDir}/character_spritesheet",
                SrcNormalDir     = $"{classSrc}/{c.SkinSrcDir}/character_spritesheet_normal",
                DstSheetDir      = $"Assets/_Nymora/Art/Cosmetics/{c.SkinName}/sheets",
                DstNormalDir     = $"Assets/_Nymora/Art/Cosmetics/{c.SkinName}/sheets_normal",
                DstClipDir       = $"Assets/_Nymora/Animations/Cosmetics/{c.SkinName}/Clips",
                ControllerDir    = $"Assets/_Nymora/Animations/Cosmetics/{c.SkinName}",
                ControllerPrefix = string.IsNullOrEmpty(c.SkinCtrlPrefix) ? c.SkinName : c.SkinCtrlPrefix,
                ClipPrefix       = c.SkinName,
            };
        }

        [MenuItem("Nymora/Setup/Polish Kyami/Import Colossar Spritesheets + Normals (Test)", priority = 60)]
        public static void RunColossar() => RunRigs(
            RigsFor(Classes[0]).ToList(), "Colossar");

        [MenuItem("Nymora/Setup/Polish Kyami/Import ALL Classes Spritesheets + Normals", priority = 61)]
        public static void RunAll() => RunRigs(
            Classes.SelectMany(RigsFor).ToList(), "toutes les classes");

        private static void RunRigs(List<Rig> rigs, string scopeLabel)
        {
            var report = new List<string>();
            int rigsOk = 0;

            // Pas de StartAssetEditing/StopAssetEditing ici : le slice (SaveAndReimport) doit être
            // SYNCHRONE pour que LoadOrderedSprites lise les sprites fraîchement découpés. Un batch
            // différerait les réimports et produirait des clips vides.
            foreach (var rig in rigs)
            {
                report.Add($"── {rig.Label} ──");
                if (ProcessRig(rig, report)) rigsOk++;
                report.Add("");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ImportSpritesheets]\n" + string.Join("\n", report));
            EditorUtility.DisplayDialog("Import Spritesheets + Normals",
                $"{rigsOk}/{rigs.Count} rig(s) traité(s) — {scopeLabel}.\n\n" + string.Join("\n", report) +
                "\n\nView-only : pas de bump version, pas de regen. Lance un combat IA par classe pour valider relief + anims.",
                "OK");
        }

        private static bool ProcessRig(Rig rig, List<string> report)
        {
            if (!Directory.Exists(rig.SrcSheetDir))
            {
                report.Add($"⚠ source introuvable : {rig.SrcSheetDir} — rig ignoré");
                return false;
            }

            EnsureFolder(rig.DstSheetDir);
            EnsureFolder(rig.DstNormalDir);
            EnsureFolder(rig.DstClipDir);

            // Cadence : lit les clips .aseprite actuels AVANT de repointer (sinon on perd la réf).
            var fpsByKey = ReadCurrentCadence(rig);

            // Map state -> clip généré, pour le repointage final.
            var clipsByStateDir = new Dictionary<(int stage, string dir, string state), AnimationClip>();

            int sheets = 0, normals = 0, clips = 0;

            foreach (var dir in Dirs)
            {
                for (int stage = 0; stage < Stages; stage++)
                {
                    foreach (var (anim, state, loop) in Anims)
                    {
                        string fileBase = $"{anim}_stage{stage}_{dir}";
                        string srcSheet = $"{rig.SrcSheetDir}/{fileBase}.png";
                        string srcNormal = $"{rig.SrcNormalDir}/{fileBase}_normal.png";

                        if (!File.Exists(srcSheet))
                        {
                            report.Add($"⚠ manque {fileBase}.png");
                            continue;
                        }

                        string dstSheet = $"{rig.DstSheetDir}/{fileBase}.png";
                        string dstNormal = $"{rig.DstNormalDir}/{fileBase}_normal.png";

                        // 1) Copie dans le projet.
                        File.Copy(srcSheet, dstSheet, true);
                        AssetDatabase.ImportAsset(dstSheet, ImportAssetOptions.ForceUpdate);
                        sheets++;

                        bool hasNormal = File.Exists(srcNormal);
                        if (hasNormal)
                        {
                            File.Copy(srcNormal, dstNormal, true);
                            AssetDatabase.ImportAsset(dstNormal, ImportAssetOptions.ForceUpdate);
                            ConfigureNormalImporter(dstNormal);
                            normals++;
                        }

                        // 2+3) Slice du sheet + montage de la normal en texture secondaire.
                        int frameCount = SliceSheet(dstSheet, fileBase, hasNormal ? dstNormal : null);
                        if (frameCount <= 0)
                        {
                            report.Add($"⚠ slice échoué : {fileBase}");
                            continue;
                        }

                        // 4) Clip à la cadence reprise du clip actuel (fallback par anim).
                        float fps = fpsByKey.TryGetValue((stage, dir, state), out var f) && f > 0f
                            ? f : FallbackFps[anim];
                        var sprites = LoadOrderedSprites(dstSheet);
                        string clipPath = $"{rig.DstClipDir}/{rig.ClipPrefix}_{fileBase}.anim";
                        var clip = BuildClip(sprites, fps, loop, clipPath);
                        if (clip != null)
                        {
                            clipsByStateDir[(stage, dir, state)] = clip;
                            clips++;
                        }
                    }
                }
            }

            report.Add($"sheets {sheets} · normals {normals} · clips {clips}");

            // 5) Repointe les 6 controllers EN PLACE (mêmes GUID).
            int repointed = RepointControllers(rig, clipsByStateDir, report);
            report.Add($"controllers repointés : {repointed}/6");
            return repointed > 0;
        }

        // =====================================================================
        // Import : slice + secondary normal
        // =====================================================================
#pragma warning disable 0618 // TextureImporter.spritesheet est obsolète mais fiable en 2022.3
        private static int SliceSheet(string assetPath, string spriteBaseName, string normalAssetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter ti) return 0;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            // Les dimensions fiables passent par l'objet importé (la taille disque peut différer).
            int width = tex != null ? tex.width : 0;
            int height = tex != null ? tex.height : Cell;
            if (width <= 0) return 0;

            int frames = Mathf.Max(1, width / Cell);

            var metas = new SpriteMetaData[frames];
            for (int i = 0; i < frames; i++)
            {
                metas[i] = new SpriteMetaData
                {
                    name = $"{spriteBaseName}_{i}",
                    // Unity : origine bas-gauche ; bande horizontale -> y=0, hauteur = cellule.
                    rect = new Rect(i * Cell, 0, Cell, Mathf.Min(Cell, height)),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = Pivot,
                };
            }

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritePixelsPerUnit = Ppu;
            ti.spritesheet = metas;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.isReadable = false;
            ti.sRGBTexture = true;

            // Texture secondaire _NormalMap (lue par Sprite-Lit-Default sur les UV de chaque sprite).
            if (!string.IsNullOrEmpty(normalAssetPath))
            {
                var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalAssetPath);
                if (normalTex != null)
                {
                    ti.secondarySpriteTextures = new[]
                    {
                        new SecondarySpriteTexture { name = "_NormalMap", texture = normalTex },
                    };
                }
            }

            ti.SaveAndReimport();
            return frames;
        }
#pragma warning restore 0618

        private static void ConfigureNormalImporter(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter ti) return;
            // Texture secondaire : pas un Sprite, juste une texture linéaire de relief.
            ti.textureType = TextureImporterType.Default;
            ti.spriteImportMode = SpriteImportMode.None;
            ti.sRGBTexture = false;      // normales = espace linéaire (sinon relief faux)
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }

        // =====================================================================
        // Clips
        // =====================================================================
        private static AnimationClip BuildClip(Sprite[] sprites, float fps, bool loop, string clipPath)
        {
            if (sprites == null || sprites.Length == 0 || fps <= 0f) return null;

            // Le nom interne du clip DOIT == nom de fichier (sinon "Main Object Name '' does not
            // match filename" + le pre-commit hook bloque sur l'erreur console).
            var clip = new AnimationClip { frameRate = fps, name = Path.GetFileNameWithoutExtension(clipPath) };

            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",            // Animator + SpriteRenderer sur le même GO "Visual"
                propertyName = "m_Sprite",
            };

            var keys = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            if (loop)
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null)
            {
                // Conserve le GUID du clip s'il existe déjà (réimport propre).
                EditorUtility.CopySerialized(clip, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        private static Sprite[] LoadOrderedSprites(string sheetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(sheetPath)
                .OfType<Sprite>()
                .OrderBy(s => TrailingInt(s.name))
                .ToArray();
        }

        private static int TrailingInt(string name)
        {
            int i = name.Length - 1;
            while (i >= 0 && char.IsDigit(name[i])) i--;
            string digits = name.Substring(i + 1);
            return int.TryParse(digits, out var v) ? v : 0;
        }

        // =====================================================================
        // Cadence : lit le clip .aseprite actuellement câblé dans chaque controller
        // =====================================================================
        private static Dictionary<(int, string, string), float> ReadCurrentCadence(Rig rig)
        {
            var map = new Dictionary<(int, string, string), float>();
            foreach (var dir in Dirs)
            {
                for (int stage = 0; stage < Stages; stage++)
                {
                    string ctrlPath = $"{rig.ControllerDir}/{rig.ControllerPrefix}Stage{stage}_{dir}.controller";
                    var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
                    if (ctrl == null || ctrl.layers.Length == 0) continue;

                    foreach (var child in ctrl.layers[0].stateMachine.states)
                    {
                        if (child.state == null || child.state.motion is not AnimationClip clip) continue;
                        float fps = EstimateFps(clip);
                        if (fps > 0f) map[(stage, dir, child.state.name)] = fps;
                    }
                }
            }
            return map;
        }

        private static float EstimateFps(AnimationClip clip)
        {
            if (clip == null) return 0f;
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (b.propertyName != "m_Sprite") continue;
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
                if (keys != null && keys.Length >= 2)
                {
                    float dt = keys[1].time - keys[0].time;
                    if (dt > 0.0001f) return 1f / dt;
                }
            }
            return clip.frameRate > 0f ? clip.frameRate : 0f;
        }

        // =====================================================================
        // Repointage controllers EN PLACE (préserve GUID + transitions + idle speed 0.4)
        // =====================================================================
        private static int RepointControllers(Rig rig,
            Dictionary<(int, string, string), AnimationClip> clips, List<string> report)
        {
            int done = 0;
            foreach (var dir in Dirs)
            {
                for (int stage = 0; stage < Stages; stage++)
                {
                    string ctrlPath = $"{rig.ControllerDir}/{rig.ControllerPrefix}Stage{stage}_{dir}.controller";
                    var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
                    if (ctrl == null || ctrl.layers.Length == 0)
                    {
                        report.Add($"⚠ controller absent : {ctrlPath}");
                        continue;
                    }

                    bool changed = false;
                    var sm = ctrl.layers[0].stateMachine;
                    foreach (var child in sm.states)
                    {
                        if (child.state == null) continue;
                        if (clips.TryGetValue((stage, dir, child.state.name), out var clip) && clip != null)
                        {
                            child.state.motion = clip;
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        EditorUtility.SetDirty(ctrl);
                        done++;
                    }
                }
            }
            return done;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
