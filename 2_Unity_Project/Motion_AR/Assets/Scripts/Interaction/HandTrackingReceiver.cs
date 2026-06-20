using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace MotionAR.Interaction
{
    /// <summary>
    /// Consumer side of the Producer-Consumer threading pattern for hand tracking.
    /// Background threads (producers) enqueue <see cref="HandData"/> via the static
    /// <see cref="EnqueueHandData"/> method. This MonoBehaviour drains the queue on
    /// the main thread each frame, projects the index finger tip into 3D space,
    /// and drives a smoothed cursor visual.
    /// </summary>
    public class HandTrackingReceiver : MonoBehaviour
    {
        /// <summary>
        /// Thread-safe queue shared between producer threads and this consumer.
        /// Producers push hand data from any thread; the consumer drains it in Update().
        /// <para>
        /// <b>Design Decision — Intentionally Static:</b> This queue is static so that
        /// all producer threads (e.g., <c>MediaPipeUDPProvider</c>, <c>MockHandTrackingProvider</c>)
        /// can push data without needing a reference to a specific <see cref="HandTrackingReceiver"/>
        /// instance. If multiple receivers exist (e.g., during scene transitions with
        /// <c>DontDestroyOnLoad</c>), they will share the same queue. This is by-design:
        /// only one consumer should drain the queue at any given time.
        /// </para>
        /// </summary>
        private static readonly ConcurrentQueue<HandData> _handDataQueue = new ConcurrentQueue<HandData>();

        [SerializeField]
        [Tooltip("Transform used as the visual cursor driven by hand tracking.")]
        private Transform cursorVisual;

        [SerializeField]
        [Tooltip("Distance from the camera to the virtual touch plane along the camera's forward axis.")]
        private float _virtualPlaneZ = 5f;

        [SerializeField]
        [Tooltip("Interpolation speed for smoothing cursor movement.")]
        private float _lerpSpeed = 15f;

        [SerializeField]
        [Tooltip("Seconds without data before tracking is considered lost.")]
        private float _trackingTimeoutSeconds = 0.3f;

        [SerializeField]
        [Tooltip("Main camera reference. Falls back to Camera.main if not assigned.")]
        private Camera _mainCamera;

        private Vector3 _smoothedPosition;
        private float _lastReceivedTime;
        private bool _isTrackingActive;
        private HandData _latestHandData;

        /// <summary>
        /// The smoothed 3D world position of the cursor derived from hand tracking.
        /// </summary>
        public Vector3 SmoothedCursorPosition => _smoothedPosition;

        /// <summary>
        /// The most recently processed hand data frame.
        /// </summary>
        public HandData LatestHandData => _latestHandData;

        /// <summary>
        /// Whether hand tracking data is actively being received within the timeout window.
        /// </summary>
        public bool IsTrackingActive => _isTrackingActive;

        /// <summary>
        /// Fired on the main thread each frame when new hand data is processed.
        /// </summary>
        public event Action<HandData> OnHandUpdate;

        /// <summary>
        /// Fired when hand tracking data has not been received for longer than
        /// <see cref="_trackingTimeoutSeconds"/>.
        /// </summary>
        public event Action OnTrackingLost;

        /// <summary>
        /// Thread-safe method for producers on any thread to submit hand data.
        /// Data is queued and will be consumed on the main thread during the next Update().
        /// </summary>
        /// <param name="data">The hand data frame to enqueue.</param>
        public static void EnqueueHandData(HandData data)
        {
            _handDataQueue.Enqueue(data);
        }

        private void Awake()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            _smoothedPosition = Vector3.zero;
            _lastReceivedTime = Time.time;
        }

        private void Update()
        {
            // Drain the queue, keeping only the latest frame.
            HandData? latestFrame = null;

            while (_handDataQueue.TryDequeue(out HandData frame))
            {
                latestFrame = frame;
            }

            if (latestFrame.HasValue)
            {
                ProcessHandData(latestFrame.Value);
            }

            // Fail-safe: detect tracking loss if no data received within timeout.
            if (_isTrackingActive && Time.time - _lastReceivedTime > _trackingTimeoutSeconds)
            {
                _isTrackingActive = false;
                OnTrackingLost?.Invoke();
            }
        }

        /// <summary>
        /// Processes a single hand data frame: projects the index finger tip
        /// to 3D world space, smooths cursor movement, and fires events.
        /// </summary>
        private void ProcessHandData(HandData data)
        {
            _latestHandData = data;
            _lastReceivedTime = Time.time;
            _isTrackingActive = true;

            if (data.Landmarks == null || data.Landmarks.Length <= 8)
            {
                return;
            }

            // Map landmark[8] (index finger tip) to screen coordinates.
            // The landmark positions from the provider are in a normalized/model space.
            // We treat the x,y of landmark[8] as a viewport-like coordinate and project
            // through the camera onto a virtual plane at distance _virtualPlaneZ.
            Vector3 indexTip = data.Landmarks[8];

            // Convert landmark to screen-space pixel coordinates.
            float screenX = (indexTip.x + 0.2f) / 0.4f * Screen.width;
            float screenY = (indexTip.y + 0.2f) / 0.4f * Screen.height;
            Vector3 screenPoint = new Vector3(
                Mathf.Clamp(screenX, 0f, Screen.width),
                Mathf.Clamp(screenY, 0f, Screen.height),
                0f
            );

            Ray ray = _mainCamera.ScreenPointToRay(screenPoint);

            // Intersect ray with a virtual plane perpendicular to the camera forward
            // at distance _virtualPlaneZ from the camera.
            Vector3 planeNormal = _mainCamera.transform.forward;
            Vector3 planePoint = _mainCamera.transform.position + planeNormal * _virtualPlaneZ;

            float denom = Vector3.Dot(planeNormal, ray.direction);
            Vector3 targetPosition = _smoothedPosition;

            if (Mathf.Abs(denom) > 1e-6f)
            {
                float t = Vector3.Dot(planePoint - ray.origin, planeNormal) / denom;
                if (t >= 0f)
                {
                    targetPosition = ray.origin + ray.direction * t;
                }
            }

            // Smooth cursor movement via lerp.
            _smoothedPosition = Vector3.Lerp(_smoothedPosition, targetPosition, Time.deltaTime * _lerpSpeed);

            // Move cursor visual.
            if (cursorVisual != null)
            {
                cursorVisual.position = _smoothedPosition;
            }

            OnHandUpdate?.Invoke(data);
        }

        /// <summary>
        /// Forces release of any tracked object and fires the tracking lost event.
        /// Call this to manually interrupt tracking (e.g., when switching modes).
        /// </summary>
        public void ForceReleaseObject()
        {
            _isTrackingActive = false;
            OnTrackingLost?.Invoke();
        }
    }
}
