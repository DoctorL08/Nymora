using System;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// Animateur sprite-sheet generique cote View. Cycle un Sprite[] sur un SpriteRenderer
    /// (ou Image UI) a une cadence FPS fixe. Pure Unity, hors simulation Quantum :
    /// utilise Time.deltaTime, donc NE PAS s'en servir pour declencher de la logique
    /// gameplay.
    ///
    /// Usage typique :
    ///   var anim = go.GetComponent&lt;SpriteAnimator&gt;();
    ///   anim.SetFrames(sprites, fps: 8f, loop: true);
    ///
    /// Pour stopper : SetFrames(null) ou desactiver le GameObject.
    /// </summary>
    public class SpriteAnimator : MonoBehaviour
    {
        [Header("Cible (1 des 2 doit etre set)")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private UnityEngine.UI.Image _uiImage;

        [Header("Configuration")]
        [SerializeField] private Sprite[] _frames;
        [SerializeField, Min(0.1f)] private float _framesPerSecond = 8f;
        [SerializeField] private bool _loop = true;
        [SerializeField] private bool _autoStart = true;

        private float _accumulator;
        private int _currentFrame;
        private bool _playing;

        /// <summary>
        /// Fire quand une animation non-loop atteint sa derniere frame. Utilise par
        /// CombatVFXView pour auto-destroy un VFX one-shot apres la fin de l'anim.
        /// </summary>
        public event Action Completed;

        private void Awake()
        {
            // Auto-resolve : utile quand le composant est ajoute par script (TerrainView etc).
            if (_spriteRenderer == null && _uiImage == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
                if (_spriteRenderer == null)
                {
                    _uiImage = GetComponent<UnityEngine.UI.Image>();
                }
            }
        }

        /// <summary>
        /// Reconfigure l'animateur avec un nouveau set de frames et une nouvelle cadence.
        /// Reset le compteur de frame a 0. Si frames == null ou vide : affichage clear.
        /// </summary>
        public void SetFrames(Sprite[] frames, float fps = 8f, bool loop = true)
        {
            _frames = frames;
            _framesPerSecond = Mathf.Max(0.1f, fps);
            _loop = loop;
            _accumulator = 0f;
            _currentFrame = 0;
            _playing = frames != null && frames.Length > 0;
            ApplyCurrentFrame();
        }

        /// <summary>
        /// Stoppe l'animation et clear la frame affichee.
        /// </summary>
        public void Clear()
        {
            _frames = null;
            _playing = false;
            if (_spriteRenderer != null) _spriteRenderer.sprite = null;
            if (_uiImage != null) _uiImage.sprite = null;
        }

        public bool IsPlaying => _playing;

        private void OnEnable()
        {
            if (_autoStart && _frames != null && _frames.Length > 0)
            {
                _playing = true;
                ApplyCurrentFrame();
            }
        }

        private void Update()
        {
            if (!_playing) return;
            if (_frames == null || _frames.Length == 0) return;

            _accumulator += Time.deltaTime;
            float frameDuration = 1f / _framesPerSecond;
            while (_accumulator >= frameDuration)
            {
                _accumulator -= frameDuration;
                _currentFrame++;
                if (_currentFrame >= _frames.Length)
                {
                    if (_loop)
                    {
                        _currentFrame = 0;
                    }
                    else
                    {
                        _currentFrame = _frames.Length - 1;
                        _playing = false;
                        ApplyCurrentFrame();
                        Completed?.Invoke();
                        return;
                    }
                }
            }
            ApplyCurrentFrame();
        }

        private void ApplyCurrentFrame()
        {
            if (_frames == null || _frames.Length == 0)
            {
                if (_spriteRenderer != null) _spriteRenderer.sprite = null;
                if (_uiImage != null) _uiImage.sprite = null;
                return;
            }
            Sprite frame = _frames[_currentFrame];
            if (_spriteRenderer != null) _spriteRenderer.sprite = frame;
            if (_uiImage != null) _uiImage.sprite = frame;
        }
    }
}
