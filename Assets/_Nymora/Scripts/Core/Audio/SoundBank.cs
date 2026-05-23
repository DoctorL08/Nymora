using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nymora.Core.Audio
{
    /// <summary>
    /// Les 5 bus de mixage logiques. Chaque bus a un volume 0..1 réglable par le joueur
    /// (persisté en PlayerPrefs par NymoraAudioManager). Master multiplie tous les autres.
    /// </summary>
    public enum AudioBus
    {
        Master = 0,
        Music = 1,
        Sfx = 2,
        Ambience = 3,
        Ui = 4,
    }

    /// <summary>
    /// Mapping SoundId -> clip(s) + réglages, édité dans l'Inspector. Source unique de tous
    /// les sons : aucun AudioClip n'est référencé en dur dans le code (règle "pas de valeur
    /// magique"). Chargé au runtime par NymoraAudioManager depuis Resources/Audio/MainSoundBank.
    ///
    /// Plusieurs clips par entrée = variation aléatoire (anti-répétition). PitchRange ajoute
    /// une variation de hauteur aléatoire. La sélection aléatoire utilise UnityEngine.Random
    /// (autorisé : 100% View, jamais dans la simulation Quantum).
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Audio/Sound Bank", fileName = "MainSoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public SoundId Id;
            [Tooltip("Bus de mixage. Détermine quel slider de volume affecte ce son.")]
            public AudioBus Bus = AudioBus.Sfx;
            [Tooltip("1 clip = son fixe ; plusieurs = un tiré au hasard à chaque lecture (anti-répétition).")]
            public AudioClip[] Clips;
            [Range(0f, 1f)] public float Volume = 1f;
            [Tooltip("Variation de hauteur aléatoire [min,max]. (1,1) = pas de variation.")]
            public Vector2 PitchRange = new Vector2(1f, 1f);
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        public Entry[] Entries => _entries;

        private Dictionary<SoundId, Entry> _lookup;

        private void BuildLookup()
        {
            _lookup = new Dictionary<SoundId, Entry>(_entries.Length);
            foreach (var e in _entries)
            {
                if (e != null && e.Id != SoundId.None) _lookup[e.Id] = e;
            }
        }

        /// <summary>
        /// Résout un SoundId en clip jouable + volume d'entrée + bus + pitch (variation appliquée).
        /// Retourne false si l'entrée n'existe pas ou n'a aucun clip assigné (slot encore vide).
        /// </summary>
        public bool TryResolve(SoundId id, out AudioClip clip, out float volume, out AudioBus bus, out float pitch)
        {
            clip = null; volume = 1f; bus = AudioBus.Sfx; pitch = 1f;
            if (_lookup == null) BuildLookup();
            if (_lookup == null || !_lookup.TryGetValue(id, out var e) || e == null) return false;

            bus = e.Bus;
            volume = e.Volume;
            pitch = Mathf.Approximately(e.PitchRange.x, e.PitchRange.y)
                ? e.PitchRange.x
                : UnityEngine.Random.Range(e.PitchRange.x, e.PitchRange.y);

            if (e.Clips == null || e.Clips.Length == 0) return false;
            clip = e.Clips.Length == 1 ? e.Clips[0] : e.Clips[UnityEngine.Random.Range(0, e.Clips.Length)];
            return clip != null;
        }

        /// <summary>Retourne juste le bus configuré pour un SoundId (fallback Sfx). Sert au bip de test.</summary>
        public AudioBus ResolveBus(SoundId id)
        {
            if (_lookup == null) BuildLookup();
            return (_lookup != null && _lookup.TryGetValue(id, out var e) && e != null) ? e.Bus : AudioBus.Sfx;
        }

#if UNITY_EDITOR
        /// <summary>Éditeur seul : remplace la liste d'entrées (utilisé par SetupAudioTool).</summary>
        public void EditorSetEntries(Entry[] entries) { _entries = entries; _lookup = null; }
#endif
    }
}
