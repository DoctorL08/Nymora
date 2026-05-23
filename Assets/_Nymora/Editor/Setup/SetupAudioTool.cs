using System;
using System.Collections.Generic;
using Nymora.Core.Audio;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique A1 — Setup du système audio. Crée (ou met à jour) la banque de sons
    /// Resources/Audio/MainSoundBank.asset avec un slot par SoundId, prêt à recevoir les
    /// clips. Idempotent : ré-exécutable sans écraser les clips/volumes déjà réglés (ajoute
    /// uniquement les SoundId manquants).
    ///
    /// Menus :
    ///   Nymora > Setup > Setup Audio System    (crée/maj la banque)
    ///   Nymora > Audio > Play Test Beep         (Play Mode : valide la chaîne à l'oreille)
    /// </summary>
    public static class SetupAudioTool
    {
        private const string ResourcesDir = "Assets/_Nymora/Resources";
        private const string AudioDir = "Assets/_Nymora/Resources/Audio";
        private const string BankAssetPath = "Assets/_Nymora/Resources/Audio/MainSoundBank.asset";

        [MenuItem("Nymora/Setup/Setup Audio System")]
        public static void SetupAudio()
        {
            EnsureFolder(ResourcesDir);
            EnsureFolder(AudioDir);

            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankAssetPath);
            bool created = false;
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, BankAssetPath);
                created = true;
            }

            // Index des entrées existantes pour préserver clips/volumes déjà réglés.
            var existing = new Dictionary<SoundId, SoundBank.Entry>();
            if (bank.Entries != null)
            {
                foreach (var e in bank.Entries)
                    if (e != null && e.Id != SoundId.None) existing[e.Id] = e;
            }

            var merged = new List<SoundBank.Entry>();
            int added = 0;
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
            {
                if (id == SoundId.None) continue;
                if (existing.TryGetValue(id, out var keep))
                {
                    merged.Add(keep);
                }
                else
                {
                    merged.Add(new SoundBank.Entry
                    {
                        Id = id,
                        Bus = DefaultBusFor(id),
                        Clips = Array.Empty<AudioClip>(),
                        Volume = 1f,
                        PitchRange = DefaultPitchFor(id),
                    });
                    added++;
                }
            }

            bank.EditorSetEntries(merged.ToArray());
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = bank;
            EditorGUIUtility.PingObject(bank);
            Debug.Log($"[Audio] SoundBank {(created ? "créée" : "mise à jour")} à {BankAssetPath} " +
                      $"— {merged.Count} entrées ({added} ajoutées). Glisse tes clips dans les slots.");
        }

        [MenuItem("Nymora/Audio/Play Test Beep")]
        public static void PlayTestBeep()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Audio] Entre en Play Mode d'abord — le manager audio ne vit qu'au runtime.");
                return;
            }
            if (NymoraAudioManager.Instance == null)
            {
                Debug.LogError("[Audio] NymoraAudioManager.Instance est null (le bootstrap runtime n'a pas tourné ?).");
                return;
            }
            NymoraAudioManager.Instance.PlayTestBeep();
        }

        private const string DebugBeepMenu = "Nymora/Audio/Toggle Debug Beep (missing clips)";

        [MenuItem(DebugBeepMenu)]
        public static void ToggleDebugBeep()
        {
            NymoraAudioManager.DebugBeepOnMissing = !NymoraAudioManager.DebugBeepOnMissing;
            EditorPrefs.SetBool("nymora.audio.debugBeep", NymoraAudioManager.DebugBeepOnMissing);
            Menu.SetChecked(DebugBeepMenu, NymoraAudioManager.DebugBeepOnMissing);
            Debug.Log($"[Audio] Debug Beep on missing clips : {(NymoraAudioManager.DebugBeepOnMissing ? "ON" : "OFF")}");
        }

        [MenuItem(DebugBeepMenu, true)]
        public static bool ToggleDebugBeepValidate()
        {
            Menu.SetChecked(DebugBeepMenu, EditorPrefs.GetBool("nymora.audio.debugBeep", false));
            return true;
        }

        /// <summary>Bus par défaut déduit de la plage numérique du SoundId (voir SoundId.cs).</summary>
        private static AudioBus DefaultBusFor(SoundId id)
        {
            int v = (int)id;
            if (v >= 500 && v < 600) return AudioBus.Music;
            if (v >= 600 && v < 700) return AudioBus.Ambience;
            if (v >= 100 && v < 300) return AudioBus.Ui; // UI + notifications
            return AudioBus.Sfx;                          // 300-499 combat
        }

        /// <summary>Légère variation de hauteur par défaut sur les sons répétitifs (anti-lassitude).</summary>
        private static Vector2 DefaultPitchFor(SoundId id)
        {
            switch (id)
            {
                case SoundId.Footstep:
                case SoundId.Impact:
                case SoundId.Damage:
                case SoundId.UiClick:
                    return new Vector2(0.95f, 1.05f);
                default:
                    return new Vector2(1f, 1f);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string leaf = path.Substring(slash + 1);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
