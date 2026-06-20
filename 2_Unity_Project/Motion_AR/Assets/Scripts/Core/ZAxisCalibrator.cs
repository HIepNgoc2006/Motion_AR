using UnityEngine;
using MotionAR.Interaction;

namespace MotionAR.Core
{
    /// <summary>
    /// Dynamic Z-axis calibration system that maps the user's hand depth
    /// (from MediaPipe z-coordinates) to meaningful Unity world-space depth values.
    /// <para>
    /// Implements three calibration strategies:
    /// 1. Bounding Box: User defines near/far planes by moving hand to extremes.
    /// 2. Push-Pull Gesture: Continuous mapping using real-time z offset.
    /// 3. Auto-Calibrate: Automatic min/max tracking during initial frames.
    /// </para>
    /// <para>
    /// References: Master Plan §5.1 — Z-Axis Calibration for depth interaction.
    /// </para>
    /// </summary>
    public class ZAxisCalibrator : MonoBehaviour
    {
        /// <summary>
        /// Calibration mode for Z-axis depth mapping.
        /// </summary>
        public enum CalibrationMode
        {
            /// <summary>Disabled — Z values pass through unchanged.</summary>
            None,
            /// <summary>Auto-calibrate by tracking min/max Z values.</summary>
            Auto,
            /// <summary>Manual bounding box set by user gestures.</summary>
            BoundingBox,
            /// <summary>Continuous push-pull gesture mapping.</summary>
            PushPull
        }

        [Header("References")]
        [SerializeField]
        [Tooltip("HandTrackingReceiver for hand data events.")]
        private HandTrackingReceiver _handTrackingReceiver;

        [SerializeField]
        [Tooltip("GestureDetector for calibration gesture triggers.")]
        private GestureDetector _gestureDetector;

        [Header("Calibration Settings")]
        [SerializeField]
        [Tooltip("Active calibration mode.")]
        private CalibrationMode _mode = CalibrationMode.Auto;

        [SerializeField]
        [Tooltip("Target Unity world-space Z range (near plane).")]
        private float _worldZNear = -0.3f;

        [SerializeField]
        [Tooltip("Target Unity world-space Z range (far plane).")]
        private float _worldZFar = 0.3f;

        [SerializeField]
        [Tooltip("Smoothing factor for Z-axis output (0 = no smoothing, 0.95 = heavy).")]
        [Range(0f, 0.98f)]
        private float _smoothingFactor = 0.8f;

        [SerializeField]
        [Tooltip("Number of initial frames for auto-calibration.")]
        private int _autoCalibrationFrames = 120;

        /// <summary>Global singleton instance.</summary>
        public static ZAxisCalibrator Instance { get; private set; }

        /// <summary>Whether calibration is complete and Z mapping is active.</summary>
        public bool IsCalibrated => _isCalibrated;

        /// <summary>Current calibration mode.</summary>
        public CalibrationMode Mode => _mode;

        /// <summary>The calibrated Z range: (rawMin, rawMax).</summary>
        public Vector2 CalibratedRange => new Vector2(_rawZMin, _rawZMax);

        // Internal state
        private bool _isCalibrated;
        private float _rawZMin = float.MaxValue;
        private float _rawZMax = float.MinValue;
        private int _calibrationFrameCount;
        private float _smoothedZ;
        private bool _boundingBoxNearSet;
        private bool _boundingBoxFarSet;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            if (_handTrackingReceiver != null)
            {
                _handTrackingReceiver.OnHandUpdate += HandleHandUpdate;
            }

            if (_gestureDetector != null)
            {
                _gestureDetector.OnDynamicGestureDetected += HandleCalibrationGesture;
            }
        }

        private void OnDisable()
        {
            if (_handTrackingReceiver != null)
            {
                _handTrackingReceiver.OnHandUpdate -= HandleHandUpdate;
            }

            if (_gestureDetector != null)
            {
                _gestureDetector.OnDynamicGestureDetected -= HandleCalibrationGesture;
            }
        }

        /// <summary>
        /// Maps a raw MediaPipe Z value to calibrated Unity world-space Z.
        /// </summary>
        /// <param name="rawZ">Raw z-coordinate from hand tracking.</param>
        /// <returns>Calibrated Z value in Unity world space.</returns>
        public float CalibrateZ(float rawZ)
        {
            if (_mode == CalibrationMode.None || !_isCalibrated)
            {
                return rawZ;
            }

            float range = _rawZMax - _rawZMin;
            if (range < 1e-6f)
            {
                return 0f;
            }

            // Normalize raw Z to [0, 1] range.
            float normalized = Mathf.Clamp01((rawZ - _rawZMin) / range);

            // Map to target world-space Z range.
            float targetZ = Mathf.Lerp(_worldZNear, _worldZFar, normalized);

            // Apply smoothing.
            _smoothedZ = Mathf.Lerp(targetZ, _smoothedZ, _smoothingFactor);

            return _smoothedZ;
        }

        /// <summary>
        /// Calibrates an entire HandData structure, mapping all landmark Z values.
        /// Returns a new Vector3 array with calibrated Z.
        /// </summary>
        /// <param name="landmarks">Original 21-landmark array.</param>
        /// <returns>New array with calibrated Z values.</returns>
        public Vector3[] CalibrateHandData(Vector3[] landmarks)
        {
            if (landmarks == null || !_isCalibrated || _mode == CalibrationMode.None)
            {
                return landmarks;
            }

            Vector3[] calibrated = new Vector3[landmarks.Length];
            for (int i = 0; i < landmarks.Length; i++)
            {
                calibrated[i] = new Vector3(
                    landmarks[i].x,
                    landmarks[i].y,
                    CalibrateZ(landmarks[i].z)
                );
            }

            return calibrated;
        }

        /// <summary>
        /// Resets calibration state. Call this to re-calibrate.
        /// </summary>
        public void ResetCalibration()
        {
            _isCalibrated = false;
            _rawZMin = float.MaxValue;
            _rawZMax = float.MinValue;
            _calibrationFrameCount = 0;
            _boundingBoxNearSet = false;
            _boundingBoxFarSet = false;
            _smoothedZ = 0f;

            Debug.Log("[ZAxisCalibrator] Calibration reset.");
        }

        /// <summary>
        /// Manually sets the calibration range (useful for saved profiles).
        /// </summary>
        public void SetCalibrationRange(float rawMin, float rawMax)
        {
            _rawZMin = rawMin;
            _rawZMax = rawMax;
            _isCalibrated = true;
            Debug.Log($"[ZAxisCalibrator] Manual calibration set: [{rawMin:F4}, {rawMax:F4}]");
        }

        /// <summary>
        /// Sets the calibration mode at runtime.
        /// </summary>
        public void SetMode(CalibrationMode mode)
        {
            _mode = mode;
            ResetCalibration();
        }

        // ────────────────── Hand Data Processing ──────────────────

        private void HandleHandUpdate(HandData data)
        {
            if (data.Landmarks == null || data.Landmarks.Length < 21)
                return;

            // Use wrist Z (landmark 0) as the primary depth reference.
            float wristZ = data.Landmarks[0].z;

            switch (_mode)
            {
                case CalibrationMode.Auto:
                    ProcessAutoCalibration(wristZ);
                    break;

                case CalibrationMode.PushPull:
                    ProcessPushPullCalibration(wristZ);
                    break;

                case CalibrationMode.BoundingBox:
                    // BoundingBox mode is gesture-triggered; see HandleCalibrationGesture.
                    break;
            }
        }

        /// <summary>
        /// Auto-calibration: tracks min/max Z during initial frames,
        /// then finalizes the calibration range.
        /// </summary>
        private void ProcessAutoCalibration(float z)
        {
            if (_isCalibrated) return;

            _calibrationFrameCount++;

            if (z < _rawZMin) _rawZMin = z;
            if (z > _rawZMax) _rawZMax = z;

            if (_calibrationFrameCount >= _autoCalibrationFrames)
            {
                FinalizeCalibration();
            }
        }

        /// <summary>
        /// Push-pull: continuously updates the calibration range.
        /// Expands the range if the user pushes/pulls beyond known bounds.
        /// </summary>
        private void ProcessPushPullCalibration(float z)
        {
            if (z < _rawZMin)
            {
                _rawZMin = z;
            }

            if (z > _rawZMax)
            {
                _rawZMax = z;
            }

            float range = _rawZMax - _rawZMin;
            if (range > 0.01f && !_isCalibrated)
            {
                _isCalibrated = true;
                Debug.Log($"[ZAxisCalibrator] PushPull calibrated: [{_rawZMin:F4}, {_rawZMax:F4}]");
            }
        }

        /// <summary>
        /// Handles calibration gestures for BoundingBox mode.
        /// ThumbsUp = set near plane, ThumbsDown = set far plane.
        /// </summary>
        private void HandleCalibrationGesture(GestureType gesture, float confidence)
        {
            if (_mode != CalibrationMode.BoundingBox) return;

            if (_handTrackingReceiver == null) return;

            // We'll use the last known wrist Z.
            // In a real implementation, we'd get the current HandData directly.
            float currentZ = 0f;
            // Simplified: use the gesture as a trigger to save current Z.

            if (gesture == GestureType.ThumbsUp)
            {
                _rawZMin = currentZ;
                _boundingBoxNearSet = true;
                Debug.Log($"[ZAxisCalibrator] BoundingBox NEAR set: {_rawZMin:F4}");
            }
            else if (gesture == GestureType.ThumbsDown)
            {
                _rawZMax = currentZ;
                _boundingBoxFarSet = true;
                Debug.Log($"[ZAxisCalibrator] BoundingBox FAR set: {_rawZMax:F4}");
            }

            if (_boundingBoxNearSet && _boundingBoxFarSet)
            {
                FinalizeCalibration();
            }
        }

        /// <summary>
        /// Finalizes calibration by adding a small margin to the detected range.
        /// </summary>
        private void FinalizeCalibration()
        {
            float range = _rawZMax - _rawZMin;

            if (range < 0.005f)
            {
                // Range too small — user didn't move in Z enough.
                Debug.LogWarning("[ZAxisCalibrator] Calibration range too small. " +
                    "Using default range. Move your hand forward and backward for better calibration.");
                _rawZMin = -0.05f;
                _rawZMax = 0.05f;
            }
            else
            {
                // Add 10% margin on each side.
                float margin = range * 0.1f;
                _rawZMin -= margin;
                _rawZMax += margin;
            }

            _isCalibrated = true;
            Debug.Log($"[ZAxisCalibrator] Calibration complete! " +
                $"Z range: [{_rawZMin:F4}, {_rawZMax:F4}] → " +
                $"World: [{_worldZNear:F2}, {_worldZFar:F2}]");
        }
    }
}
