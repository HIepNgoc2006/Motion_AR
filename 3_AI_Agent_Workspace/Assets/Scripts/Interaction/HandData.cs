using System;
using UnityEngine;

namespace MotionAR.Interaction
{
    /// <summary>
    /// Immutable snapshot of hand tracking data captured at a specific moment.
    /// Contains 21 hand landmarks, timestamp, and tracking status.
    /// </summary>
    public readonly struct HandData
    {
        /// <summary>
        /// Array of 21 hand joint positions following the MediaPipe hand landmark model.
        /// Index 0 = wrist, 4 = thumb tip, 8 = index finger tip, etc.
        /// </summary>
        public readonly Vector3[] Landmarks;

        /// <summary>
        /// The Time.time value when this hand data was captured.
        /// </summary>
        public readonly float Timestamp;

        /// <summary>
        /// Whether the hand is currently being actively tracked.
        /// </summary>
        public readonly bool IsTracking;

        /// <summary>
        /// Creates a new HandData snapshot with a deep copy of the landmarks array.
        /// </summary>
        /// <param name="landmarks">Array of 21 hand joint positions.</param>
        /// <param name="timestamp">Time.time when this data was captured.</param>
        /// <param name="isTracking">Whether the hand is currently tracked.</param>
        public HandData(Vector3[] landmarks, float timestamp, bool isTracking)
        {
            if (landmarks != null)
            {
                Landmarks = new Vector3[landmarks.Length];
                Array.Copy(landmarks, Landmarks, landmarks.Length);
            }
            else
            {
                Landmarks = Array.Empty<Vector3>();
            }

            Timestamp = timestamp;
            IsTracking = isTracking;
        }
    }

    /// <summary>
    /// Enumeration of all recognized gesture types.
    /// Includes both static poses and dynamic swipe gestures.
    /// </summary>
    public enum GestureType
    {
        /// <summary>No gesture detected.</summary>
        None = 0,
        /// <summary>Horizontal swipe to the left.</summary>
        SwipeLeft = 1,
        /// <summary>Horizontal swipe to the right.</summary>
        SwipeRight = 2,
        /// <summary>Vertical swipe upward.</summary>
        SwipeUp = 3,
        /// <summary>Vertical swipe downward.</summary>
        SwipeDown = 4,
        /// <summary>Single tap gesture.</summary>
        Tap = 5,
        /// <summary>Double tap gesture.</summary>
        DoubleTap = 6,
        /// <summary>Closed fist pose.</summary>
        Fist = 7,
        /// <summary>Open palm with fingers spread.</summary>
        OpenPalm = 8,
        /// <summary>Thumbs up pose.</summary>
        ThumbsUp = 9,
        /// <summary>Thumbs down pose.</summary>
        ThumbsDown = 10,
        /// <summary>Peace/victory sign with two fingers.</summary>
        Peace = 11,
        /// <summary>Index finger pointing upward.</summary>
        PointUp = 12,
        /// <summary>Grab/clutch gesture.</summary>
        Grab = 13
    }

    /// <summary>
    /// Represents the current state of a pinch gesture with hysteresis.
    /// </summary>
    public enum PinchState
    {
        /// <summary>Fingers are apart; no pinch active.</summary>
        Released = 0,
        /// <summary>Thumb and index finger are pinched together.</summary>
        Grabbed = 1
    }

    /// <summary>
    /// Interface for hand tracking data providers.
    /// Implementations can wrap MediaPipe, device SDKs, or mock input.
    /// </summary>
    public interface IHandTrackingProvider
    {
        /// <summary>
        /// Begins hand tracking data acquisition.
        /// </summary>
        void StartTracking();

        /// <summary>
        /// Stops hand tracking data acquisition.
        /// </summary>
        void StopTracking();

        /// <summary>
        /// Whether the tracking provider hardware/software is available.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Fired each time a new hand data frame is available.
        /// </summary>
        event Action<HandData> OnHandDataReceived;
    }

    /// <summary>
    /// Interface for components attached to GameObjects that can be grabbed via pinch gesture.
    /// </summary>
    public interface IGrabbable
    {
        /// <summary>
        /// Unique identifier for this grabbable part (e.g., "monitor_screen", "power_cable").
        /// </summary>
        string PartID { get; }

        /// <summary>
        /// Returns true if the object is currently in a state where it can be grabbed.
        /// </summary>
        bool CanGrab();

        /// <summary>
        /// Called when the object is grabbed by the user's pinch gesture.
        /// </summary>
        void OnGrab();

        /// <summary>
        /// Called when the object is released from the user's pinch gesture.
        /// </summary>
        void OnRelease();
    }

    /// <summary>
    /// Mock hand tracking provider that simulates hand input using the mouse.
    /// Left mouse button simulates pinch; mouse position drives the index finger tip.
    /// Useful for editor testing without actual hand tracking hardware.
    /// </summary>
    public class MockHandTrackingProvider : MonoBehaviour, IHandTrackingProvider
    {
        /// <summary>
        /// Whether the mock tracking provider is available. Always returns true in editor.
        /// </summary>
        public bool IsAvailable => true;

        /// <summary>
        /// Fired each frame with simulated hand data derived from mouse input.
        /// </summary>
        public event Action<HandData> OnHandDataReceived;

        private bool _isTracking;
        private readonly Vector3[] _landmarks = new Vector3[21];

        // Offsets from wrist for a reasonable resting hand shape (right hand, palm facing camera).
        // These define relative positions for each of the 21 landmarks.
        private static readonly Vector3[] _restOffsets = new Vector3[]
        {
            new Vector3(0.000f,  0.000f, 0.000f),  // 0  Wrist
            new Vector3(0.030f,  0.030f, 0.000f),  // 1  Thumb CMC
            new Vector3(0.055f,  0.060f, 0.000f),  // 2  Thumb MCP
            new Vector3(0.070f,  0.085f, 0.000f),  // 3  Thumb IP
            new Vector3(0.080f,  0.100f, 0.000f),  // 4  Thumb Tip
            new Vector3(0.020f,  0.080f, 0.000f),  // 5  Index MCP
            new Vector3(0.020f,  0.115f, 0.000f),  // 6  Index PIP
            new Vector3(0.020f,  0.140f, 0.000f),  // 7  Index DIP
            new Vector3(0.020f,  0.160f, 0.000f),  // 8  Index Tip
            new Vector3(0.000f,  0.085f, 0.000f),  // 9  Middle MCP
            new Vector3(0.000f,  0.120f, 0.000f),  // 10 Middle PIP
            new Vector3(0.000f,  0.148f, 0.000f),  // 11 Middle DIP
            new Vector3(0.000f,  0.168f, 0.000f),  // 12 Middle Tip
            new Vector3(-0.020f, 0.080f, 0.000f),  // 13 Ring MCP
            new Vector3(-0.020f, 0.112f, 0.000f),  // 14 Ring PIP
            new Vector3(-0.020f, 0.136f, 0.000f),  // 15 Ring DIP
            new Vector3(-0.020f, 0.152f, 0.000f),  // 16 Ring Tip
            new Vector3(-0.040f, 0.072f, 0.000f),  // 17 Pinky MCP
            new Vector3(-0.040f, 0.098f, 0.000f),  // 18 Pinky PIP
            new Vector3(-0.040f, 0.116f, 0.000f),  // 19 Pinky DIP
            new Vector3(-0.040f, 0.130f, 0.000f),  // 20 Pinky Tip
        };

        /// <summary>
        /// Begins mock hand tracking. Hand data will be emitted every frame.
        /// </summary>
        public void StartTracking()
        {
            _isTracking = true;
        }

        /// <summary>
        /// Stops mock hand tracking. No further hand data events will fire.
        /// </summary>
        public void StopTracking()
        {
            _isTracking = false;
        }

        private void Update()
        {
            if (!_isTracking)
            {
                return;
            }

            Vector3 mouseScreenPos = Input.mousePosition;
            // Normalize mouse position to a small coordinate space for landmark simulation.
            float normX = (mouseScreenPos.x / Screen.width - 0.5f) * 0.4f;
            float normY = (mouseScreenPos.y / Screen.height - 0.5f) * 0.4f;

            Vector3 wristWorldPos = new Vector3(normX, normY, 0f);

            bool isPinching = Input.GetMouseButton(0);

            for (int i = 0; i < 21; i++)
            {
                _landmarks[i] = wristWorldPos + _restOffsets[i];
            }

            if (isPinching)
            {
                // When pinching, move thumb tip (4) very close to index tip (8).
                Vector3 indexTip = _landmarks[8];
                _landmarks[4] = indexTip + new Vector3(0.005f, -0.005f, 0f);
            }

            HandData data = new HandData(_landmarks, Time.time, true);
            OnHandDataReceived?.Invoke(data);
        }
    }
}
