using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Accelere l'animation d'ATTAQUE de TOUS les combattants en une passe (15 juin 2026).
    ///
    /// Pourquoi un tool : les clips d'attaque sont des anims par echange de sprites dont les
    /// keyframes sont posees a des temps ABSOLUS (i/fps) -> augmenter le fps du clip ne les
    /// accelere pas. Le levier propre et uniforme est la VITESSE de l'etat "Attack" de l'Animator
    /// (multiplicateur ; comme la transition retour-idle est en exitTime normalise 0.95, le retour
    /// a l'idle se recale tout seul). On FIXE la vitesse (valeur absolue) -> idempotent, re-runnable
    /// sans cumul.
    ///
    /// Couvre TOUTES les classes (Ghostra/Soulrender/Nightseer refonte + Colossar/Necram) ET les
    /// skins cosmetiques (Ashen/ObsidianTitan/PaleRevenant/PlagueApostle/VoidOracle) : tous leurs
    /// controllers ont un etat nomme exactement "Attack".
    ///
    /// View-only : aucun bump CombatRulesVersion.
    /// ⚠ Si tu RE-BUILD un animator (Build *_Refonte / Build *Animator), re-lance ce tool ensuite
    ///   pour les classes non-refonte (les 3 refontes bakent deja le facteur, cf AttackSpeedMultiplier).
    /// Menu : Nymora > Combat > Set Attack Speed.
    /// </summary>
    public static class SetAttackSpeed
    {
        /// <summary>
        /// Multiplicateur de vitesse de l'attaque. 1 = vitesse d'origine ; 1.5 = +50% plus rapide.
        /// Source de verite unique — change ici pour regler la nervosite (puis re-lance le tool).
        /// Garde-le aligne avec AttackSpeedMultiplier des 3 Build *_Refonte.
        /// </summary>
        public const float AttackSpeed = 1.5f;

        private const string AttackStateName = "Attack";
        private const string CombatAnimRoot = "Assets/_Nymora/Animations";

        [MenuItem("Nymora/Combat/Set Attack Speed", priority = 60)]
        public static void Run()
        {
            var guids = AssetDatabase.FindAssets("t:AnimatorController", new[] { CombatAnimRoot });
            var touched = new List<string>();
            int statesSet = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (ctrl == null) continue;

                bool dirty = false;
                foreach (var layer in ctrl.layers)
                {
                    if (layer.stateMachine == null) continue;
                    foreach (var cs in layer.stateMachine.states)
                    {
                        if (cs.state == null || cs.state.name != AttackStateName) continue;
                        if (!Mathf.Approximately(cs.state.speed, AttackSpeed))
                        {
                            cs.state.speed = AttackSpeed;
                            dirty = true;
                        }
                        statesSet++;
                    }
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(ctrl);
                    AssetDatabase.SaveAssetIfDirty(ctrl);
                    touched.Add(System.IO.Path.GetFileNameWithoutExtension(path));
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string msg = $"Vitesse d'attaque = {AttackSpeed:0.##}× sur {statesSet} états \"Attack\".\n" +
                         $"{touched.Count} controllers modifiés" +
                         (touched.Count > 0 ? " :\n - " + string.Join("\n - ", touched.OrderBy(x => x)) : " (déjà à jour).");
            Debug.Log("[SetAttackSpeed] " + msg);
            EditorUtility.DisplayDialog("Set Attack Speed", msg, "OK");
        }
    }
}
