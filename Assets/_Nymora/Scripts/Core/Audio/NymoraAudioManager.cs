using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.Core.Audio
{
    /// <summary>
    /// Point d'entrée unique de tout l'audio du jeu. Singleton auto-bootstrappé avant la
    /// 1re scène (RuntimeInitializeOnLoadMethod) + DontDestroyOnLoad → survit aux changements
    /// de scène (login -> hub -> combat -> hub) sans coupure musicale.
    ///
    /// API publique :
    ///   PlaySfx(SoundId)              one-shot SFX/UI (voix poolées, peut se superposer)
    ///   PlayMusic(SoundId, fade)      musique en boucle avec crossfade
    ///   PlayAmbience(SoundId)         ambiance en boucle
    ///   StopMusic(fade) / StopAmbience()
    ///   SetBusVolume / GetBusVolume   réglages joueur (persistés PlayerPrefs)
    ///
    /// Volume effectif = volume_entrée(SoundBank) × volume_bus × volume_Master.
    /// Jeu 2D → tous les sons sont 2D (spatialBlend = 0), pas de spatialisation.
    ///
    /// 100% View. Aucune dépendance simulation, aucun float interdit (Quantum ne voit rien).
    /// </summary>
    public sealed class NymoraAudioManager : MonoBehaviour
    {
        public static NymoraAudioManager Instance { get; private set; }

        /// <summary>
        /// Mode debug (A2) : quand un SoundId n'a PAS de clip assigné dans la banque, joue un
        /// petit bip de tonalité distincte par event au lieu du silence. Permet de valider à
        /// l'oreille que les events combat/UI se câblent bien AVANT d'avoir les vrais sons (A6).
        /// Togglé via « Nymora > Audio > Toggle Debug Beep (missing clips) ». OFF par défaut.
        /// </summary>
        public static bool DebugBeepOnMissing;

        /// <summary>
        /// Facteur de volume des sons UI "doux" (UiBack, PurchaseSuccess, RewardClaim) : on les
        /// veut feutrés, pas agressifs. À passer en 2e arg de PlaySfx. Le hover a son propre
        /// facteur (encore plus bas) côté UiSfxHook.
        /// </summary>
        public const float SoftUiVolume = 0.1f;

        private const int SfxVoiceCount = 8;
        private const string BankResourcePath = "Audio/MainSoundBank";
        private const string PrefKeyPrefix = "nymora.audio.vol.";

        private SoundBank _bank;
        private AudioSource[] _sfxVoices;
        private int _nextVoice;
        private AudioSource _musicSource;
        private AudioSource _ambienceSource;

        private readonly Dictionary<AudioBus, float> _busVolume = new Dictionary<AudioBus, float>(5);

        // Mémorise ce qui joue en continu pour réappliquer le volume quand un slider bouge.
        private SoundId _currentMusic = SoundId.None;
        private float _currentMusicEntryVolume = 1f;
        private SoundId _currentAmbience = SoundId.None;
        private float _currentAmbienceEntryVolume = 1f;

        private Coroutine _musicFade;
        private AudioClip _beepClip;
        private readonly Dictionary<int, AudioClip> _beepCache = new Dictionary<int, AudioClip>();

        // AudioListener de secours : certaines scènes (ex. 00_Login) n'en ont pas sur leur
        // caméra → sans listener, AUCUN son ne sort. On en pose un sur le manager et on
        // l'active uniquement quand la scène courante n'en a aucun (sinon double-listener
        // = warning Unity).
        private AudioListener _fallbackListener;

        // Volumes par défaut (si aucune préférence sauvegardée). Musique/ambiance plus bas
        // pour ne pas noyer les SFX.
        private static readonly Dictionary<AudioBus, float> DefaultVolumes = new Dictionary<AudioBus, float>
        {
            { AudioBus.Master, 0.9f },
            { AudioBus.Music, 0.55f },
            { AudioBus.Sfx, 1f },
            { AudioBus.Ambience, 0.25f },
            { AudioBus.Ui, 0.6f },
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("NymoraAudioManager");
            go.AddComponent<NymoraAudioManager>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

#if UNITY_EDITOR
            // Restaure le toggle debug (persisté par SetupAudioTool) — sinon le static repart
            // à false au domain reload de l'entrée en Play.
            DebugBeepOnMissing = UnityEditor.EditorPrefs.GetBool("nymora.audio.debugBeep", false);
#endif

            LoadBusVolumes();

            _bank = Resources.Load<SoundBank>(BankResourcePath);
            if (_bank == null)
            {
                Debug.LogWarning($"[Audio] SoundBank introuvable à Resources/{BankResourcePath}. " +
                                 "Lance « Nymora > Setup > Setup Audio System » pour la créer.");
            }

            _sfxVoices = new AudioSource[SfxVoiceCount];
            for (int i = 0; i < SfxVoiceCount; i++) _sfxVoices[i] = CreateSource($"SfxVoice{i}", loop: false);
            _musicSource = CreateSource("Music", loop: true);
            _ambienceSource = CreateSource("Ambience", loop: true);

            // Listener de secours (désactivé par défaut, activé si la scène n'en a pas).
            _fallbackListener = gameObject.AddComponent<AudioListener>();
            _fallbackListener.enabled = false;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureAudioListener();

            Debug.Log("[Audio] NymoraAudioManager initialisé (bus : " +
                      $"Master {_busVolume[AudioBus.Master]:0.00}, Music {_busVolume[AudioBus.Music]:0.00}, " +
                      $"Sfx {_busVolume[AudioBus.Sfx]:0.00}, Amb {_busVolume[AudioBus.Ambience]:0.00}, " +
                      $"Ui {_busVolume[AudioBus.Ui]:0.00}).");
        }

        private AudioSource CreateSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f; // 2D
            return src;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureAudioListener();

        /// <summary>
        /// Active le listener de secours uniquement si la scène courante n'a aucun autre
        /// AudioListener actif (sinon Unity warne "no audio listeners" OU "more than one").
        /// </summary>
        private void EnsureAudioListener()
        {
            if (_fallbackListener == null) return;
            var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            bool sceneHasOne = false;
            foreach (var l in listeners)
            {
                if (l == null || l == _fallbackListener) continue;
                if (l.isActiveAndEnabled) { sceneHasOne = true; break; }
            }
            _fallbackListener.enabled = !sceneHasOne;
        }

        // ---------------------------------------------------------------- Volumes / bus

        private void LoadBusVolumes()
        {
            foreach (var kv in DefaultVolumes)
            {
                float v = PlayerPrefs.GetFloat(PrefKeyPrefix + kv.Key, kv.Value);
                _busVolume[kv.Key] = Mathf.Clamp01(v);
            }
        }

        public float GetBusVolume(AudioBus bus) => _busVolume.TryGetValue(bus, out var v) ? v : 1f;

        public void SetBusVolume(AudioBus bus, float value01)
        {
            value01 = Mathf.Clamp01(value01);
            _busVolume[bus] = value01;
            PlayerPrefs.SetFloat(PrefKeyPrefix + bus, value01);
            RefreshContinuousVolumes();
        }

        /// <summary>Volume final d'un bus en tenant compte du Master.</summary>
        private float EffectiveBusVolume(AudioBus bus)
        {
            float master = _busVolume.TryGetValue(AudioBus.Master, out var m) ? m : 1f;
            if (bus == AudioBus.Master) return master;
            float b = _busVolume.TryGetValue(bus, out var v) ? v : 1f;
            return master * b;
        }

        private void RefreshContinuousVolumes()
        {
            if (_musicSource != null && _currentMusic != SoundId.None)
                _musicSource.volume = _currentMusicEntryVolume * EffectiveBusVolume(AudioBus.Music);
            if (_ambienceSource != null && _currentAmbience != SoundId.None)
                _ambienceSource.volume = _currentAmbienceEntryVolume * EffectiveBusVolume(AudioBus.Ambience);
        }

        // ---------------------------------------------------------------- SFX / UI

        public void PlaySfx(SoundId id) => PlaySfx(id, 1f);

        /// <summary>
        /// Joue un SFX avec un facteur de volume additionnel (0..1+) en plus du volume d'entrée
        /// et du bus. Sert aux sons devant rester très discrets (ex: survol UI) sans toucher le
        /// volume du bus entier (qui porte aussi les clics).
        /// </summary>
        public void PlaySfx(SoundId id, float volumeScale)
        {
            if (id == SoundId.None) return;
            if (_bank != null && _bank.TryResolve(id, out var clip, out var entryVol, out var bus, out var pitch))
            {
                PlayOnVoice(clip, entryVol * Mathf.Max(0f, volumeScale) * EffectiveBusVolume(bus), pitch);
                return;
            }
            // Clip manquant : silence en prod, bip de debug si activé (A2).
            if (DebugBeepOnMissing) PlayMissingClipBeep(id);
        }

        private void PlayMissingClipBeep(SoundId id)
        {
            AudioBus bus = _bank != null ? _bank.ResolveBus(id) : AudioBus.Sfx;
            int freq = 320 + ((int)id % 18) * 32; // tonalité distincte par event
            if (!_beepCache.TryGetValue(freq, out var clip) || clip == null)
            {
                clip = GenerateBeep(freq, 0.08f);
                _beepCache[freq] = clip;
            }
            PlaySfxClip(clip, bus, 0.3f, 1f);
        }

        /// <summary>Joue un clip arbitraire sur une voix SFX poolée (sert au bip de test + cas spéciaux).</summary>
        public void PlaySfxClip(AudioClip clip, AudioBus bus = AudioBus.Sfx, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            PlayOnVoice(clip, Mathf.Clamp01(volume) * EffectiveBusVolume(bus), pitch);
        }

        private void PlayOnVoice(AudioClip clip, float volume, float pitch)
        {
            if (_sfxVoices == null) return;
            var voice = _sfxVoices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _sfxVoices.Length;
            voice.pitch = pitch;
            voice.volume = volume;
            voice.PlayOneShot(clip, 1f);
        }

        // ---------------------------------------------------------------- Musique

        public void PlayMusic(SoundId id, float fadeSeconds = 0.6f)
        {
            if (id == _currentMusic) return;
            if (_bank != null && _bank.TryResolve(id, out var clip, out var entryVol, out _, out _))
            {
                _currentMusic = id;
                _currentMusicEntryVolume = entryVol;
                float target = entryVol * EffectiveBusVolume(AudioBus.Music);
                if (_musicFade != null) StopCoroutine(_musicFade);
                _musicFade = StartCoroutine(CrossfadeMusic(clip, target, Mathf.Max(0f, fadeSeconds)));
                return;
            }
            // Clip manquant : on COUPE la musique courante (on ne garde pas la scène précédente).
            // On mémorise l'id cible pour ne pas re-déclencher chaque frame.
            Debug.Log($"[Audio] PlayMusic({id}) — clip absent : musique coupée (audible après A6).");
            _currentMusic = id;
            if (_musicFade != null) StopCoroutine(_musicFade);
            _musicFade = StartCoroutine(CrossfadeMusic(null, 0f, Mathf.Max(0f, fadeSeconds)));
            if (DebugBeepOnMissing) PlayMissingClipBeep(id);
        }

        public void StopMusic(float fadeSeconds = 0.6f)
        {
            _currentMusic = SoundId.None;
            if (_musicFade != null) StopCoroutine(_musicFade);
            _musicFade = StartCoroutine(CrossfadeMusic(null, 0f, Mathf.Max(0f, fadeSeconds)));
        }

        private IEnumerator CrossfadeMusic(AudioClip next, float targetVolume, float fade)
        {
            // Fade out l'actuel
            float startVol = _musicSource.volume;
            float t = 0f;
            while (t < fade && _musicSource.isPlaying)
            {
                t += Time.unscaledDeltaTime;
                _musicSource.volume = Mathf.Lerp(startVol, 0f, fade > 0f ? t / fade : 1f);
                yield return null;
            }
            _musicSource.Stop();

            if (next == null) { _musicSource.volume = 0f; yield break; }

            // Fade in le nouveau
            _musicSource.clip = next;
            _musicSource.volume = 0f;
            _musicSource.Play();
            t = 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                _musicSource.volume = Mathf.Lerp(0f, targetVolume, fade > 0f ? t / fade : 1f);
                yield return null;
            }
            _musicSource.volume = targetVolume;
        }

        // ---------------------------------------------------------------- Ambiance

        public void PlayAmbience(SoundId id)
        {
            if (id == _currentAmbience) return;
            if (_bank != null && _bank.TryResolve(id, out var clip, out var entryVol, out _, out _))
            {
                _currentAmbience = id;
                _currentAmbienceEntryVolume = entryVol;
                _ambienceSource.clip = clip;
                _ambienceSource.volume = entryVol * EffectiveBusVolume(AudioBus.Ambience);
                _ambienceSource.Play();
                return;
            }
            // Clip manquant : on coupe l'ambiance courante (pas de carry-over entre scènes).
            Debug.Log($"[Audio] PlayAmbience({id}) — clip absent : ambiance coupée (audible après A6).");
            _currentAmbience = id;
            if (_ambienceSource != null) _ambienceSource.Stop();
        }

        public void StopAmbience()
        {
            _currentAmbience = SoundId.None;
            if (_ambienceSource != null) _ambienceSource.Stop();
        }

        // ---------------------------------------------------------------- Bip de test (A1)

        /// <summary>
        /// Joue un bip procédural (sinusoïde) à travers tout le pipeline (voix SFX + volumes
        /// + bus). Permet de valider la chaîne audio AVANT d'avoir le moindre fichier son.
        /// Appelé par « Nymora > Audio > Play Test Beep » en Play Mode.
        /// </summary>
        public void PlayTestBeep()
        {
            if (_beepClip == null) _beepClip = GenerateBeep(660f, 0.18f);
            PlaySfxClip(_beepClip, AudioBus.Sfx, 0.6f, 1f);
            Debug.Log("[Audio] Bip de test joué (bus Sfx).");
        }

        private static AudioClip GenerateBeep(float frequency, float seconds)
        {
            const int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * seconds);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float tt = (float)i / sampleRate;
                // enveloppe simple (attack/decay) pour éviter les clics
                float env = Mathf.Min(1f, tt * 40f) * Mathf.Clamp01((seconds - tt) * 12f);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * tt) * 0.5f * env;
            }
            var clip = AudioClip.Create("TestBeep", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
