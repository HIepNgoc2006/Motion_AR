using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MotionAR.Interaction
{
    /// <summary>
    /// Receives hand landmark data from a Python MediaPipe server via UDP.
    /// Runs a background receive thread and pushes <see cref="HandData"/> into the
    /// <see cref="HandTrackingReceiver"/> Producer-Consumer queue.
    /// <para>
    /// Implements <see cref="IHandTrackingProvider"/> so it can be swapped with
    /// <see cref="MockHandTrackingProvider"/> without changing downstream code.
    /// </para>
    /// </summary>
    public class MediaPipeUDPProvider : MonoBehaviour, IHandTrackingProvider
    {
        [SerializeField]
        [Tooltip("UDP port to listen on. Must match the Python server's --port argument.")]
        private int _port = 5065;

        [SerializeField]
        [Tooltip("Whether to automatically start listening when the component starts.")]
        private bool _autoStart = true;

        /// <summary>Whether the provider is currently listening for UDP data.</summary>
        public bool IsAvailable => _isRunning;

        /// <summary>Fired each time a valid hand data frame is parsed from UDP.</summary>
        public event Action<HandData> OnHandDataReceived;

        private UdpClient _udpClient;
        private Thread _receiveThread;
        private volatile bool _isRunning;

        // Time.time cannot be called from a background thread.
        // We cache the latest main-thread time and use it for HandData timestamps.
        private volatile float _mainThreadTime;

        // Stats
        private int _packetsReceived;
        private int _packetsTracking;

        /// <summary>Total UDP packets received since start.</summary>
        public int PacketsReceived => _packetsReceived;

        /// <summary>Packets with valid hand tracking data.</summary>
        public int PacketsTracking => _packetsTracking;

        // ────────────────────── IHandTrackingProvider ──────────────────────

        /// <summary>Starts listening for UDP packets on a background thread.</summary>
        public void StartTracking()
        {
            if (_isRunning) return;

            try
            {
                _udpClient = new UdpClient(_port);
                _udpClient.Client.ReceiveTimeout = 1000; // 1s timeout for clean shutdown
            }
            catch (SocketException ex)
            {
                Debug.LogError($"[MediaPipeUDP] Failed to bind port {_port}: {ex.Message}");
                return;
            }

            _isRunning = true;
            _packetsReceived = 0;
            _packetsTracking = 0;

            _receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "MediaPipe-UDP-Receiver"
            };
            _receiveThread.Start();

            Debug.Log($"[MediaPipeUDP] Listening on UDP port {_port}");
        }

        /// <summary>Stops listening and cleans up socket resources.</summary>
        public void StopTracking()
        {
            _isRunning = false;
            _udpClient?.Close();
            _udpClient = null;
        }

        // ────────────────────── Unity Lifecycle ──────────────────────

        private void Start()
        {
            if (_autoStart)
            {
                StartTracking();
            }
        }

        private void Update()
        {
            // Cache main-thread time for the background thread.
            _mainThreadTime = Time.time;
        }

        private void OnDestroy()
        {
            StopTracking();
        }

        // ────────────────────── Background Thread ──────────────────────

        /// <summary>
        /// Continuous receive loop running on a background thread.
        /// Reads UDP packets, parses JSON, and pushes HandData into the queue.
        /// </summary>
        private void ReceiveLoop()
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

            while (_isRunning)
            {
                try
                {
                    byte[] data = _udpClient.Receive(ref remote);
                    string json = Encoding.UTF8.GetString(data);
                    _packetsReceived++;

                    ProcessPacket(json);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    // Receive timeout — loop and check if still running.
                    continue;
                }
                catch (SocketException) when (!_isRunning)
                {
                    // Socket was closed during shutdown — expected.
                    break;
                }
                catch (ObjectDisposedException) when (!_isRunning)
                {
                    // UdpClient disposed during shutdown — expected.
                    break;
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Debug.LogWarning($"[MediaPipeUDP] Receive error: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Parses a JSON UDP packet and converts it to HandData.
        /// Pushes the result into the ConcurrentQueue via HandTrackingReceiver.
        /// </summary>
        /// <param name="json">Raw JSON string from the UDP packet.</param>
        private void ProcessPacket(string json)
        {
            try
            {
                JObject packet = JObject.Parse(json);
                bool tracking = packet.Value<bool>("tracking");

                if (!tracking) return;

                JArray landmarksArray = (JArray)packet["landmarks"];
                if (landmarksArray == null || landmarksArray.Count < 21) return;

                _packetsTracking++;

                Vector3[] positions = new Vector3[21];
                for (int i = 0; i < 21; i++)
                {
                    JObject lm = (JObject)landmarksArray[i];

                    // MediaPipe outputs x, y in [0, 1] normalized coordinates.
                    // Convert to the same coordinate space as MockHandTrackingProvider:
                    // x ∈ [-0.2, 0.2], y ∈ [-0.2, 0.2], z relative and small.
                    float rawX = lm.Value<float>("x");
                    float rawY = lm.Value<float>("y");
                    float rawZ = lm.Value<float>("z");

                    positions[i] = new Vector3(
                        (rawX - 0.5f) * 0.4f,     // Center and scale X
                        (rawY - 0.5f) * -0.4f,    // Center, flip Y (MediaPipe Y is top-down)
                        rawZ * 0.1f                // Scale Z to reasonable range
                    );
                }

                // CRITICAL: Use cached main-thread time (Time.time is not thread-safe).
                HandData handData = new HandData(positions, _mainThreadTime, true);

                // Push into the Producer-Consumer queue.
                HandTrackingReceiver.EnqueueHandData(handData);

                // Also fire the event for any external subscribers.
                OnHandDataReceived?.Invoke(handData);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MediaPipeUDP] Parse error: {ex.Message}");
            }
        }
    }
}
