using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using MotionAR.Interaction;

namespace MotionAR.AI
{
    /// <summary>
    /// Manages microphone input triggered by hand gestures (Tap or Fist).
    /// Records audio from the default microphone when activated, then provides
    /// the audio clip for speech-to-text processing.
    /// <para>
    /// Workflow: Gesture trigger → Mic ON → Record → Release gesture → Mic OFF →
    /// Fire OnRecordingComplete with AudioClip.
    /// </para>
    /// </summary>
    public class VoiceInputManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("GestureDetector for gesture-triggered recording.")]
        private GestureDetector _gestureDetector;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Maximum recording duration in seconds.")]
        private float _maxRecordDuration = 15f;

        [SerializeField]
        [Tooltip("Sample rate for the microphone recording.")]
        private int _sampleRate = 16000;

        [SerializeField]
        [Tooltip("Gesture that triggers voice recording.")]
        private GestureType _triggerGesture = GestureType.Tap;

        [SerializeField]
        [Tooltip("Alternative gesture for push-to-talk (hold to record).")]
        private GestureType _pushToTalkGesture = GestureType.Fist;

        /// <summary>Fired when recording starts.</summary>
        public event Action OnRecordingStarted;

        /// <summary>Fired when recording stops. Provides the raw AudioClip and duration.</summary>
        public event Action<AudioClip, float> OnRecordingComplete;

        /// <summary>Whether the microphone is currently recording.</summary>
        public bool IsRecording => _isRecording;

        private bool _isRecording;
        private string _micDevice;
        private AudioClip _recordingClip;
        private float _recordStartTime;
        private bool _isPushToTalk;
        private CancellationTokenSource _recordCts;

        private void Start()
        {
            // Get default microphone.
            if (Microphone.devices.Length > 0)
            {
                _micDevice = Microphone.devices[0];
                Debug.Log($"[VoiceInput] Microphone found: {_micDevice}");
            }
            else
            {
                Debug.LogWarning("[VoiceInput] No microphone detected. Voice input disabled.");
            }
        }

        private void OnEnable()
        {
            if (_gestureDetector != null)
            {
                _gestureDetector.OnDynamicGestureDetected += HandleGesture;
                _gestureDetector.OnPinchUpdate += HandlePinchForPushToTalk;
            }
        }

        private void OnDisable()
        {
            if (_gestureDetector != null)
            {
                _gestureDetector.OnDynamicGestureDetected -= HandleGesture;
                _gestureDetector.OnPinchUpdate -= HandlePinchForPushToTalk;
            }

            StopRecording();
        }

        /// <summary>
        /// Handles dynamic gestures to start/stop recording.
        /// </summary>
        private void HandleGesture(GestureType gesture, float confidence)
        {
            if (string.IsNullOrEmpty(_micDevice)) return;

            if (gesture == _triggerGesture && !_isRecording)
            {
                // Toggle mode: Tap to start, Tap again to stop.
                StartRecording(false);
            }
            else if (gesture == _triggerGesture && _isRecording && !_isPushToTalk)
            {
                StopRecording();
            }
            else if (gesture == _pushToTalkGesture && !_isRecording)
            {
                // Push-to-talk: Hold Fist to record.
                StartRecording(true);
            }
        }

        /// <summary>
        /// For push-to-talk mode: stops recording when the fist is released.
        /// We detect release by monitoring pinch state change while in push-to-talk.
        /// </summary>
        private void HandlePinchForPushToTalk(PinchState state, float distance)
        {
            // When in push-to-talk and user opens hand (distance increases), stop recording.
            if (_isPushToTalk && _isRecording && distance > 0.15f)
            {
                StopRecording();
            }
        }

        /// <summary>
        /// Starts microphone recording.
        /// </summary>
        /// <param name="pushToTalk">If true, recording stops when gesture is released.</param>
        public void StartRecording(bool pushToTalk = false)
        {
            if (_isRecording || string.IsNullOrEmpty(_micDevice)) return;

            _isRecording = true;
            _isPushToTalk = pushToTalk;
            _recordStartTime = Time.time;

            _recordingClip = Microphone.Start(_micDevice, false,
                Mathf.CeilToInt(_maxRecordDuration), _sampleRate);

            OnRecordingStarted?.Invoke();
            Debug.Log($"[VoiceInput] Recording started (mode: {(pushToTalk ? "Push-to-talk" : "Toggle")})");

            // Auto-stop after max duration.
            _recordCts = new CancellationTokenSource();
            AutoStopAfterTimeout(_recordCts.Token).Forget();
        }

        /// <summary>
        /// Stops microphone recording and fires the completion event.
        /// </summary>
        public void StopRecording()
        {
            if (!_isRecording) return;

            _isRecording = false;
            _isPushToTalk = false;

            CancelAutoStop();

            if (string.IsNullOrEmpty(_micDevice)) return;

            int lastSample = Microphone.GetPosition(_micDevice);
            Microphone.End(_micDevice);

            float duration = Time.time - _recordStartTime;

            if (lastSample <= 0 || _recordingClip == null)
            {
                Debug.LogWarning("[VoiceInput] Recording was empty.");
                return;
            }

            // Trim the AudioClip to actual recorded length.
            AudioClip trimmedClip = TrimAudioClip(_recordingClip, lastSample);

            Debug.Log($"[VoiceInput] Recording stopped. Duration: {duration:F1}s, Samples: {lastSample}");
            OnRecordingComplete?.Invoke(trimmedClip, duration);
        }

        /// <summary>
        /// Trims an AudioClip to the specified number of samples.
        /// </summary>
        private AudioClip TrimAudioClip(AudioClip source, int sampleCount)
        {
            float[] samples = new float[sampleCount * source.channels];
            source.GetData(samples, 0);

            AudioClip trimmed = AudioClip.Create(
                "VoiceRecording",
                sampleCount,
                source.channels,
                source.frequency,
                false
            );
            trimmed.SetData(samples, 0);

            return trimmed;
        }

        /// <summary>
        /// Automatically stops recording after the max duration.
        /// </summary>
        private async UniTaskVoid AutoStopAfterTimeout(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(_maxRecordDuration),
                    ignoreTimeScale: false,
                    PlayerLoopTiming.Update,
                    ct
                );

                if (_isRecording)
                {
                    Debug.Log("[VoiceInput] Max duration reached. Auto-stopping.");
                    StopRecording();
                }
            }
            catch (OperationCanceledException) { }
        }

        private void CancelAutoStop()
        {
            if (_recordCts != null)
            {
                _recordCts.Cancel();
                _recordCts.Dispose();
                _recordCts = null;
            }
        }

        private void OnDestroy()
        {
            StopRecording();
            CancelAutoStop();
        }
    }
}
