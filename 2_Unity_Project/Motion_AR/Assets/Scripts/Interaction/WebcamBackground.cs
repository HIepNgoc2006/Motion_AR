using UnityEngine;
using UnityEngine.UI;

namespace MotionAR.Interaction
{
    /// <summary>
    /// Displays the local webcam feed as the background layer of the AR scene.
    /// Mirrors the image horizontally to create a "magic mirror" effect where
    /// the user's hand movements appear natural on screen.
    /// <para>
    /// Attach this to a Canvas with a <see cref="RawImage"/> that fills the screen.
    /// The webcam feed is rendered behind all other UI and 3D content.
    /// </para>
    /// </summary>
    public class WebcamBackground : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("RawImage component that fills the screen to display the webcam feed.")]
        private RawImage _backgroundImage;

        [SerializeField]
        [Tooltip("Mirror the webcam horizontally to create a 'magic mirror' effect.")]
        private bool _mirrorHorizontally = true;

        [SerializeField]
        [Tooltip("Preferred webcam resolution width.")]
        private int _preferredWidth = 1280;

        [SerializeField]
        [Tooltip("Preferred webcam resolution height.")]
        private int _preferredHeight = 720;

        [SerializeField]
        [Tooltip("Preferred frames per second for the webcam.")]
        private int _preferredFPS = 30;

        [SerializeField]
        [Tooltip("Camera device index. Use 0 for default webcam.")]
        private int _cameraIndex = 0;

        private WebCamTexture _webcamTexture;

        /// <summary>Whether the webcam is currently active and streaming.</summary>
        public bool IsStreaming => _webcamTexture != null && _webcamTexture.isPlaying;

        /// <summary>The active WebCamTexture, or null if not streaming.</summary>
        public WebCamTexture ActiveTexture => _webcamTexture;

        private void Start()
        {
            InitializeWebcam();
        }

        /// <summary>
        /// Initializes the webcam with the preferred settings and starts streaming.
        /// Provides a fallback warning if no webcam devices are detected.
        /// </summary>
        private void InitializeWebcam()
        {
            WebCamDevice[] devices = WebCamTexture.devices;

            if (devices.Length == 0)
            {
                Debug.LogWarning("[WebcamBG] No webcam detected. Background will be empty.");
                return;
            }

            int deviceIndex = Mathf.Clamp(_cameraIndex, 0, devices.Length - 1);
            string deviceName = devices[deviceIndex].name;

            _webcamTexture = new WebCamTexture(
                deviceName, _preferredWidth, _preferredHeight, _preferredFPS);
            _webcamTexture.Play();

            if (_backgroundImage != null)
            {
                _backgroundImage.texture = _webcamTexture;

                // Mirror horizontally to match the "magic mirror" UX.
                if (_mirrorHorizontally)
                {
                    _backgroundImage.rectTransform.localScale = new Vector3(-1, 1, 1);
                }
            }

            Debug.Log($"[WebcamBG] Started: {deviceName} " +
                      $"({_webcamTexture.width}x{_webcamTexture.height} @ {_preferredFPS}fps)");
        }

        /// <summary>
        /// Stops the webcam feed to release the camera device.
        /// </summary>
        public void StopWebcam()
        {
            if (_webcamTexture != null && _webcamTexture.isPlaying)
            {
                _webcamTexture.Stop();
            }
        }

        private void OnDestroy()
        {
            StopWebcam();
        }
    }
}
