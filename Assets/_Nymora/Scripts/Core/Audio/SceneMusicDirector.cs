using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.Core.Audio
{
    /// <summary>
    /// Brique A4 — Pilote la musique de fond + l'ambiance selon la scène active. Politique
    /// musicale centralisée en un seul endroit :
    ///   - scène "*Combat*"        -> MusicCombat   + AmbienceCombat
    ///   - scène "*Hub*"           -> MusicHub       + AmbienceHub
    ///   - autres (Login/MainMenu) -> MusicMenu      + (pas d'ambiance)
    ///
    /// Singleton auto-bootstrappé + DontDestroyOnLoad. Le crossfade musical est géré par
    /// NymoraAudioManager (la musique ne se coupe pas brutalement entre 2 scènes). 100% View.
    ///
    /// La musique de fin de match (Victory/Defeat) est déclenchée ailleurs (MatchEndOverlay) :
    /// elle prime ponctuellement sur MusicCombat, puis le retour au hub repasse sur MusicHub.
    /// </summary>
    public sealed class SceneMusicDirector : MonoBehaviour
    {
        private static SceneMusicDirector _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("SceneMusicDirector");
            go.AddComponent<SceneMusicDirector>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyForScene(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this) _instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyForScene(scene.name);

        private static void ApplyForScene(string sceneName)
        {
            var audio = NymoraAudioManager.Instance;
            if (audio == null || string.IsNullOrEmpty(sceneName)) return; // retry au prochain sceneLoaded

            if (sceneName.Contains("Combat"))
            {
                audio.PlayMusic(SoundId.MusicCombat);
                audio.PlayAmbience(SoundId.AmbienceCombat);
            }
            else if (sceneName.Contains("Hub"))
            {
                audio.PlayMusic(SoundId.MusicHub);
                audio.PlayAmbience(SoundId.AmbienceHub);
            }
            else // Login / MainMenu / divers
            {
                audio.PlayMusic(SoundId.MusicMenu);
                audio.StopAmbience();
            }
        }
    }
}
