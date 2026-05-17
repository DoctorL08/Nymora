using Nymora.Core.Enums;
using Nymora.Core.ScriptableObjects;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Editor tool 5.3.f — Populate les 5 NymoraClassDefinition.asset existants avec :
    ///   - Lore long (3-5 phrases Bible V7.1 / pour Class Selector)
    ///   - IdleAnimator (RuntimeAnimatorController Stage0_NE de la classe)
    ///
    /// Menu : Nymora > Setup > Populate Class Definitions (Lore + Animator)
    ///
    /// Re-runnable. NE TOUCHE PAS aux champs deja remplis a la main par Lorenzo si non-vides
    /// pour les Tagline / Passive / Signature descriptions (juste remplis si vides).
    /// Pour Lore + IdleAnimator : overwrite systematique (source-of-truth ici).
    /// </summary>
    public static class PopulateClassDefinitions
    {
        private const string ClassesFolder = "Assets/_Nymora/ScriptableObjects/Classes";
        private const string AnimationsFolder = "Assets/_Nymora/Animations";

        // Mapping Lore (3-5 phrases par classe, ton fantasy / motivation Bible V7.1).
        // Format : "phrase 1. phrase 2. phrase 3."
        private static readonly System.Collections.Generic.Dictionary<NymoraClass, string> _lores = new System.Collections.Generic.Dictionary<NymoraClass, string>
        {
            { NymoraClass.Soulrender,
                "Berserker melee assoiffe de combat. Plus on le frappe, plus il s'arme. " +
                "Sa ressource Hemoglyphe se nourrit de la douleur — la sienne, celle qu'il inflige. " +
                "Au seuil de la mort, il devient une lame qu'aucun bouclier n'arrete. " +
                "Sa signature Ame Laceree consume tout son sang pour un coup definitif."
            },
            { NymoraClass.Nightseer,
                "Tireur d'elite spectral. Voit ce que les autres ne voient pas, marque ce qu'ils ignorent. " +
                "Sa Prescience se charge par lecture des futurs probables — chaque marque est un fil tendu. " +
                "Quand sa cible est Traquee, Voilee ou Empreintee, elle est deja morte avant d'avoir leve son arme. " +
                "Sa signature Traquenard est l'execution finale du contrat invisible."
            },
            { NymoraClass.Colossar,
                "Forge geante qui marche. Pose des piliers, leve des murs, ancre la grille a sa volonte. " +
                "Sa Fondation accumule alors qu'il modifie le terrain — chaque obstacle est une promesse de retour de coup. " +
                "Quand le poids du monde l'entoure, il declenche Effondrement et tout cede en cascade. " +
                "Densite Inerte le rend plus immuable a chaque pas."
            },
            { NymoraClass.Necram,
                "Mort vivant en floraison. Marque, infecte, propage. " +
                "Sa Putrefaction monte alors que ses ennemis macerent dans le venin. " +
                "La Floraison rend chaque marque plus virulente — une mort lente devient un effondrement immediat. " +
                "Sa signature Virus Fatal consume toute sa ressource pour un triplement instantane des ticks venin."
            },
            { NymoraClass.Ghostra,
                "Assassin spectral qui frappe l'angle mort. Sa Remanence est faite de leurres — clones visuels qui ne servent qu'a fragmenter l'attention adverse. " +
                "Plus elle pose de leurres, plus son Angle Mort s'elargit, plus ses coups dorsaux deviennent letaux. " +
                "Frappe Fantome, Saigne-Ame, Danse des Lames : ses combos exigent une lecture parfaite du regard adverse. " +
                "Sa signature Execution Spectrale, dorsale obligatoire, peut finir un match en un seul tour."
            },
        };

        // Mapping Animator : path vers le Stage0_SE controller (facing SUD-EST, face camera).
        private static readonly System.Collections.Generic.Dictionary<NymoraClass, string> _animatorPaths = new System.Collections.Generic.Dictionary<NymoraClass, string>
        {
            { NymoraClass.Soulrender, $"{AnimationsFolder}/Soulrender/SoulrenderStage0_SE.controller" },
            { NymoraClass.Nightseer,  $"{AnimationsFolder}/Nightseer/NightseerStage0_SE.controller" },
            { NymoraClass.Colossar,   $"{AnimationsFolder}/Colossar/ColossarStage0_SE.controller" },
            { NymoraClass.Necram,     $"{AnimationsFolder}/Necram/NecramStage0_SE.controller" },
            { NymoraClass.Ghostra,    $"{AnimationsFolder}/Ghostra/GhostraStage0_SE.controller" },
        };

        // Mapping Animator NE (facing NORD-EST, dos camera). Pour hub avatar quand
        // mouvement vers haut-droite iso. NW/SW = NE/SE mirror flipX.
        private static readonly System.Collections.Generic.Dictionary<NymoraClass, string> _animatorPathsNE = new System.Collections.Generic.Dictionary<NymoraClass, string>
        {
            { NymoraClass.Soulrender, $"{AnimationsFolder}/Soulrender/SoulrenderStage0_NE.controller" },
            { NymoraClass.Nightseer,  $"{AnimationsFolder}/Nightseer/NightseerStage0_NE.controller" },
            { NymoraClass.Colossar,   $"{AnimationsFolder}/Colossar/ColossarStage0_NE.controller" },
            { NymoraClass.Necram,     $"{AnimationsFolder}/Necram/NecramStage0_NE.controller" },
            { NymoraClass.Ghostra,    $"{AnimationsFolder}/Ghostra/GhostraStage0_NE.controller" },
        };

        // Mapping calibration hub Visual (Y offset + Scale) per-classe, porte depuis les
        // prefabs combat IA. Valeurs effectives = root.scale * visual.localScale (Scale)
        // et root.scale.y * visual.localPosition.y (Y).
        //   Soulrender/Nightseer/Colossar : mono-GO en combat (pas de Visual child) -> defauts (1, 0)
        //   Necram  : Visual(Y=-0.2, Scale=1)        -> Hub (1.00, -0.20)
        //   Ghostra : root.Scale=1.15 x Visual(Y=-0.18, Scale=1.05)
        //             -> effectif Scale=1.15*1.05=1.2075, Y=1.15*-0.18=-0.207
        private struct HubCalib { public float Scale; public float Y; public HubCalib(float s, float y) { Scale = s; Y = y; } }
        private static readonly System.Collections.Generic.Dictionary<NymoraClass, HubCalib> _hubCalib = new System.Collections.Generic.Dictionary<NymoraClass, HubCalib>
        {
            { NymoraClass.Soulrender, new HubCalib(1.00f,  0.00f) },
            { NymoraClass.Nightseer,  new HubCalib(1.00f,  0.00f) },
            { NymoraClass.Colossar,   new HubCalib(1.00f,  0.00f) },
            { NymoraClass.Necram,     new HubCalib(1.00f, -0.20f) },
            { NymoraClass.Ghostra,    new HubCalib(1.2075f, -0.207f) },
        };

        [MenuItem("Nymora/Setup/Populate Class Definitions (Lore + Animator)")]
        public static void Run()
        {
            int updated = 0;
            foreach (NymoraClass cls in System.Enum.GetValues(typeof(NymoraClass)))
            {
                if (cls == NymoraClass.None) continue;

                string assetPath = $"{ClassesFolder}/{cls}.asset";
                var def = AssetDatabase.LoadAssetAtPath<NymoraClassDefinition>(assetPath);
                if (def == null)
                {
                    Debug.LogWarning($"[Nymora.PopulateClassDefinitions] {assetPath} introuvable, skip.");
                    continue;
                }

                bool changed = false;

                // Lore : overwrite si mapping fourni
                if (_lores.TryGetValue(cls, out string lore) && def.Lore != lore)
                {
                    def.Lore = lore;
                    changed = true;
                }

                // IdleAnimator SE (facing sud-est) : load + bind + extract frames Idle + Walk
                if (_animatorPaths.TryGetValue(cls, out string animPath))
                {
                    var anim = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(animPath);
                    if (anim != null)
                    {
                        if (def.IdleAnimator != anim) { def.IdleAnimator = anim; changed = true; }
                        var idleFrames = ExtractClipFrames(anim, "Idle", cls);
                        if (idleFrames != null && idleFrames.Length > 0 && !ArraysEqual(def.IdleFrames, idleFrames))
                        {
                            def.IdleFrames = idleFrames;
                            changed = true;
                        }
                        var walkFrames = ExtractClipFrames(anim, "Walk", cls);
                        if (walkFrames != null && walkFrames.Length > 0 && !ArraysEqual(def.WalkFrames, walkFrames))
                        {
                            def.WalkFrames = walkFrames;
                            changed = true;
                        }
                    }
                    else Debug.LogWarning($"[Nymora.PopulateClassDefinitions] Animator SE introuvable a {animPath}");
                }

                // IdleAnimator NE (facing nord-est) : load + bind + extract frames Idle + Walk
                if (_animatorPathsNE.TryGetValue(cls, out string animPathNE))
                {
                    var animNE = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(animPathNE);
                    if (animNE != null)
                    {
                        if (def.IdleAnimatorNE != animNE) { def.IdleAnimatorNE = animNE; changed = true; }
                        var idleFramesNE = ExtractClipFrames(animNE, "Idle", cls);
                        if (idleFramesNE != null && idleFramesNE.Length > 0 && !ArraysEqual(def.IdleFramesNE, idleFramesNE))
                        {
                            def.IdleFramesNE = idleFramesNE;
                            changed = true;
                        }
                        var walkFramesNE = ExtractClipFrames(animNE, "Walk", cls);
                        if (walkFramesNE != null && walkFramesNE.Length > 0 && !ArraysEqual(def.WalkFramesNE, walkFramesNE))
                        {
                            def.WalkFramesNE = walkFramesNE;
                            changed = true;
                        }
                    }
                    else Debug.LogWarning($"[Nymora.PopulateClassDefinitions] Animator NE introuvable a {animPathNE}");
                }

                // Calibration hub Visual (Y offset + Scale) portee depuis combat IA.
                if (_hubCalib.TryGetValue(cls, out HubCalib calib))
                {
                    if (!Mathf.Approximately(def.HubVisualScale, calib.Scale))
                    {
                        def.HubVisualScale = calib.Scale;
                        changed = true;
                    }
                    if (!Mathf.Approximately(def.HubVisualYOffset, calib.Y))
                    {
                        def.HubVisualYOffset = calib.Y;
                        changed = true;
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(def);
                    updated++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Nymora.PopulateClassDefinitions] {updated} ClassDefinition.asset mis a jour (sur 5).");
        }

        /// <summary>
        /// Extrait les Sprites animes du clip nomme <paramref name="clipName"/> (case-insensitive
        /// contains) dans l'AnimatorController. Cherche la curve "m_Sprite" (sprite swap pixel art
        /// standard Unity). Fallback : si aucun clip ne matche le nom, prend animationClips[0]
        /// (preserve compat avec le comportement initial qui retournait "Idle" par defaut).
        /// </summary>
        private static Sprite[] ExtractClipFrames(RuntimeAnimatorController controller, string clipName, NymoraClass cls)
        {
            if (controller == null || controller.animationClips == null || controller.animationClips.Length == 0) return null;
            AnimationClip targetClip = null;
            foreach (var c in controller.animationClips)
            {
                if (c != null && !string.IsNullOrEmpty(c.name) && c.name.IndexOf(clipName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetClip = c;
                    break;
                }
            }
            if (targetClip == null)
            {
                Debug.LogWarning($"[Nymora.PopulateClassDefinitions] Clip '{clipName}' introuvable dans {controller.name} (classe {cls}). Clips disponibles : {string.Join(", ", System.Array.ConvertAll(controller.animationClips, c => c != null ? c.name : "<null>"))}");
                return null;
            }
            var bindings = UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(targetClip);
            foreach (var binding in bindings)
            {
                if (binding.propertyName != "m_Sprite") continue;
                var keyframes = UnityEditor.AnimationUtility.GetObjectReferenceCurve(targetClip, binding);
                if (keyframes == null || keyframes.Length == 0) continue;
                var sprites = new Sprite[keyframes.Length];
                for (int i = 0; i < keyframes.Length; i++) sprites[i] = keyframes[i].value as Sprite;
                return sprites;
            }
            Debug.LogWarning($"[Nymora.PopulateClassDefinitions] Clip '{targetClip.name}' n'a pas de binding m_Sprite (classe {cls}).");
            return null;
        }

        private static bool ArraysEqual(Sprite[] a, Sprite[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
