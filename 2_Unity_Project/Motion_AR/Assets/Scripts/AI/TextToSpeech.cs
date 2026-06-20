using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MotionAR.AI
{
    /// <summary>
    /// Text-to-Speech manager that reads AI responses aloud using Unity's
    /// built-in speech synthesis (Windows SAPI via .NET).
    /// <para>
    /// On platforms where System.Speech is unavailable, falls back to a
    /// simple visual-only mode (text is displayed but not spoken).
    /// </para>
    /// <para>
    /// Supports queue-based playback: if multiple texts are submitted while
    /// one is being spoken, they are queued and played sequentially.
    /// </para>
    /// </summary>
    public class TextToSpeech : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        [Tooltip("Volume for speech output (0.0 to 1.0).")]
        [Range(0f, 1f)]
        private float _volume = 0.8f;

        [SerializeField]
        [Tooltip("Speech rate: -10 (slowest) to 10 (fastest). 0 is normal.")]
        [Range(-5, 5)]
        private int _speechRate = 0;

        [SerializeField]
        [Tooltip("Maximum number of characters per speech segment. Longer texts are split.")]
        private int _maxCharsPerSegment = 200;

        [SerializeField]
        [Tooltip("Enable/disable TTS at runtime.")]
        private bool _isTTSEnabled = true;

        /// <summary>Global singleton instance.</summary>
        public static TextToSpeech Instance { get; private set; }

        /// <summary>Whether TTS is currently speaking.</summary>
        public bool IsSpeaking => _isSpeaking;

        /// <summary>Whether TTS is available on this platform.</summary>
        public bool IsAvailable => _isAvailable;

        /// <summary>Fired when speech starts for a text segment.</summary>
        public event Action<string> OnSpeechStarted;

        /// <summary>Fired when speech finishes for a text segment.</summary>
        public event Action OnSpeechFinished;

        private bool _isSpeaking;
        private bool _isAvailable;
        private readonly Queue<string> _speechQueue = new Queue<string>();
        private AudioSource _audioSource;

#if ENABLE_SAPI_TTS
        private System.Speech.Synthesis.SpeechSynthesizer _synthesizer;
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            InitializeSynthesizer();
        }

        private void OnDestroy()
        {
            DisposeSynthesizer();
        }

        /// <summary>
        /// Initializes the platform-specific speech synthesizer.
        /// </summary>
        private void InitializeSynthesizer()
        {
#if ENABLE_SAPI_TTS
            try
            {
                _synthesizer = new System.Speech.Synthesis.SpeechSynthesizer();
                _synthesizer.Volume = Mathf.RoundToInt(_volume * 100f);
                _synthesizer.Rate = _speechRate;

                // Try to find a Vietnamese voice, fall back to default.
                bool foundVietnamese = false;
                foreach (var voice in _synthesizer.GetInstalledVoices())
                {
                    if (voice.VoiceInfo.Culture.Name.StartsWith("vi"))
                    {
                        _synthesizer.SelectVoice(voice.VoiceInfo.Name);
                        foundVietnamese = true;
                        Debug.Log($"[TTS] Vietnamese voice found: {voice.VoiceInfo.Name}");
                        break;
                    }
                }

                if (!foundVietnamese)
                {
                    Debug.LogWarning("[TTS] No Vietnamese voice found. Using default voice.");
                }

                _synthesizer.SpeakCompleted += OnSynthesizerSpeakCompleted;
                _isAvailable = true;
                Debug.Log("[TTS] Speech synthesizer initialized (Windows SAPI).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TTS] Failed to initialize: {ex.Message}. TTS disabled.");
                _isAvailable = false;
            }
#else
            Debug.Log("[TTS] Platform does not support System.Speech. TTS disabled.");
            _isAvailable = false;
#endif
        }

        /// <summary>
        /// Disposes the speech synthesizer and clears the queue.
        /// </summary>
        private void DisposeSynthesizer()
        {
#if ENABLE_SAPI_TTS
            if (_synthesizer != null)
            {
                try
                {
                    _synthesizer.SpeakAsyncCancelAll();
                    _synthesizer.SpeakCompleted -= OnSynthesizerSpeakCompleted;
                    _synthesizer.Dispose();
                }
                catch { }
                _synthesizer = null;
            }
#endif
            _speechQueue.Clear();
            _isSpeaking = false;
        }

        /// <summary>
        /// Speaks the given text. If TTS is unavailable, this is a no-op.
        /// Long texts are split into segments for better responsiveness.
        /// </summary>
        /// <param name="text">The text to speak aloud.</param>
        public void Speak(string text)
        {
            if (!_isTTSEnabled || !_isAvailable || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            // Split long text into segments at sentence boundaries.
            var segments = SplitIntoSegments(text);
            foreach (string segment in segments)
            {
                _speechQueue.Enqueue(segment);
            }

            if (!_isSpeaking)
            {
                ProcessNextInQueue();
            }
        }

        /// <summary>
        /// Stops any current speech and clears the queue.
        /// </summary>
        public void StopSpeaking()
        {
            _speechQueue.Clear();

#if ENABLE_SAPI_TTS
            if (_synthesizer != null && _isSpeaking)
            {
                _synthesizer.SpeakAsyncCancelAll();
            }
#endif

            _isSpeaking = false;
            OnSpeechFinished?.Invoke();
        }

        /// <summary>
        /// Enables or disables TTS at runtime.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _isTTSEnabled = enabled;
            if (!enabled)
            {
                StopSpeaking();
            }
        }

        /// <summary>
        /// Sets the speech volume (0.0 to 1.0).
        /// </summary>
        public void SetVolume(float volume)
        {
            _volume = Mathf.Clamp01(volume);
#if ENABLE_SAPI_TTS
            if (_synthesizer != null)
            {
                _synthesizer.Volume = Mathf.RoundToInt(_volume * 100f);
            }
#endif
        }

        /// <summary>
        /// Sets the speech rate (-10 to 10).
        /// </summary>
        public void SetRate(int rate)
        {
            _speechRate = Mathf.Clamp(rate, -10, 10);
#if ENABLE_SAPI_TTS
            if (_synthesizer != null)
            {
                _synthesizer.Rate = _speechRate;
            }
#endif
        }

        // ────────────────── Queue Processing ──────────────────

        private void ProcessNextInQueue()
        {
            if (_speechQueue.Count == 0)
            {
                _isSpeaking = false;
                OnSpeechFinished?.Invoke();
                return;
            }

            string segment = _speechQueue.Dequeue();
            _isSpeaking = true;
            OnSpeechStarted?.Invoke(segment);

#if ENABLE_SAPI_TTS
            if (_synthesizer != null)
            {
                _synthesizer.SpeakAsync(segment);
            }
#else
            // Fallback: simulate speech duration based on text length.
            SimulateSpeechAsync(segment).Forget();
#endif
        }

#if ENABLE_SAPI_TTS
        private void OnSynthesizerSpeakCompleted(object sender,
            System.Speech.Synthesis.SpeakCompletedEventArgs e)
        {
            // SpeakCompleted fires on a background thread.
            // Schedule queue processing on the main thread.
            UniTask.Post(() => ProcessNextInQueue(), PlayerLoopTiming.Update);
        }
#endif

        /// <summary>
        /// Fallback for non-Windows platforms: simulates speech delay.
        /// </summary>
        private async UniTaskVoid SimulateSpeechAsync(string text)
        {
            // Approximate: 150 words per minute, average 5 chars per word.
            float duration = Mathf.Max(1f, text.Length / (150f * 5f / 60f));
            await UniTask.Delay(TimeSpan.FromSeconds(duration));
            ProcessNextInQueue();
        }

        // ────────────────── Text Segmentation ──────────────────

        /// <summary>
        /// Splits text into segments at sentence boundaries (., !, ?, ;)
        /// respecting the max chars per segment limit.
        /// </summary>
        private List<string> SplitIntoSegments(string text)
        {
            var segments = new List<string>();
            if (text.Length <= _maxCharsPerSegment)
            {
                segments.Add(text);
                return segments;
            }

            char[] sentenceEnders = { '.', '!', '?', ';', '。' };
            int start = 0;

            while (start < text.Length)
            {
                int remaining = text.Length - start;
                if (remaining <= _maxCharsPerSegment)
                {
                    segments.Add(text.Substring(start).Trim());
                    break;
                }

                // Find the last sentence boundary within the limit.
                int end = start + _maxCharsPerSegment;
                int lastBoundary = -1;

                for (int i = end; i > start; i--)
                {
                    if (Array.IndexOf(sentenceEnders, text[i]) >= 0)
                    {
                        lastBoundary = i + 1;
                        break;
                    }
                }

                if (lastBoundary <= start)
                {
                    // No sentence boundary found; split at the limit.
                    lastBoundary = end;
                }

                string segment = text.Substring(start, lastBoundary - start).Trim();
                if (!string.IsNullOrEmpty(segment))
                {
                    segments.Add(segment);
                }

                start = lastBoundary;
            }

            return segments;
        }
    }
}
