using UnityEngine;
using UnityEngine.Video;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.X — Lecture en boucle de la vidéo de fond du hub.
    ///
    /// Architecture : VideoPlayer écrit dans une RenderTexture créée au runtime, la RT est
    /// bindée comme texture du Material du MeshRenderer (Quad). Compatible URP 2D.
    ///
    /// Stratégie de loop : `isLooping = true` (loop forward natural).
    /// Le ping-pong (forward → reverse → forward) était tenté via `vp.frame--` mais
    /// le seek backwards sur H.264 non-baseline glitch trop (re-décodage GOP).
    /// Solution propre = pré-baker un MP4 ping-pong via ffmpeg (cf docs Nymora).
    /// </summary>
    public sealed class HubBackgroundVideo : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private VideoPlayer _videoPlayer;
        [SerializeField] private MeshRenderer _meshRenderer;

        [Header("Config")]
        [SerializeField, Range(0.1f, 2f)] private float _slowMotionSpeed = 0.5f;
        [SerializeField] private int _fallbackWidth = 1920;
        [SerializeField] private int _fallbackHeight = 1080;

        private RenderTexture _rt;

        private void Awake()
        {
            if (_videoPlayer == null) _videoPlayer = GetComponent<VideoPlayer>();
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();

            int w = _videoPlayer != null && _videoPlayer.clip != null ? (int)_videoPlayer.clip.width : _fallbackWidth;
            int h = _videoPlayer != null && _videoPlayer.clip != null ? (int)_videoPlayer.clip.height : _fallbackHeight;
            _rt = new RenderTexture(w, h, 0);
            _rt.Create();

            if (_videoPlayer != null)
            {
                _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                _videoPlayer.targetTexture = _rt;
                _videoPlayer.isLooping = true; // loop natural forward
                _videoPlayer.playbackSpeed = _slowMotionSpeed;
            }

            if (_meshRenderer != null)
            {
                var mat = _meshRenderer.material;
                mat.mainTexture = _rt;
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", _rt);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _rt);
            }
        }

        private void Start()
        {
            if (_videoPlayer == null)
            {
                Debug.LogError("[HubBackgroundVideo] VideoPlayer non assigné");
                return;
            }
            _videoPlayer.prepareCompleted += OnPrepared;
            _videoPlayer.Prepare();
        }

        private void OnPrepared(VideoPlayer vp)
        {
            vp.frame = 0;
            vp.playbackSpeed = _slowMotionSpeed;
            vp.Play();
        }

        private void OnDestroy()
        {
            if (_videoPlayer != null) _videoPlayer.prepareCompleted -= OnPrepared;
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
            }
        }
    }
}
