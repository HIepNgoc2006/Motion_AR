using UnityEngine;
using MotionAR.Core;
using MotionAR.Interaction;
using MotionAR.AI;

namespace MotionAR.UI
{
    /// <summary>
    /// Scene bootstrapper that validates and connects all required components
    /// at scene startup. Reports any missing references to the console.
    /// <para>
    /// This is a debugging/setup helper — it checks that all the required
    /// cross-references between systems are properly configured in the Inspector.
    /// Place on a root "--- SYSTEMS ---" GameObject in the scene.
    /// </para>
    /// </summary>
    public class SceneBootstrapper : MonoBehaviour
    {
        [Header("Core Systems")]
        [SerializeField] private SessionManager _sessionManager;
        [SerializeField] private GeminiApiConnector _geminiApiConnector;

        [Header("Interaction")]
        [SerializeField] private HandTrackingReceiver _handTrackingReceiver;
        [SerializeField] private GestureDetector _gestureDetector;
        [SerializeField] private PinchGrabber _pinchGrabber;
        [SerializeField] private TrainingFlowController _trainingFlowController;

        [Header("Input")]
        [SerializeField] private MediaPipeUDPProvider _mediaPipeProvider;
        [SerializeField] private WebcamBackground _webcamBackground;

        [Header("UI")]
        [SerializeField] private HUDManager _hudManager;
        [SerializeField] private AudioFeedbackManager _audioFeedbackManager;

        [Header("AI")]
        [SerializeField] private AIChatPanel _aiChatPanel;
        [SerializeField] private VoiceInputManager _voiceInputManager;
        [SerializeField] private HighlightResponder _highlightResponder;

        private void Awake()
        {
            ValidateReferences();
        }

        /// <summary>
        /// Validates all required component references and logs warnings for missing ones.
        /// </summary>
        private void ValidateReferences()
        {
            int warnings = 0;
            int errors = 0;

            Debug.Log("═══════════════════════════════════════════════════════");
            Debug.Log("  🔍 SCENE BOOTSTRAPPER — VALIDATING REFERENCES...");
            Debug.Log("═══════════════════════════════════════════════════════");

            // Critical (will cause null refs at runtime)
            errors += CheckCritical("SessionManager", _sessionManager);
            errors += CheckCritical("HandTrackingReceiver", _handTrackingReceiver);
            errors += CheckCritical("GestureDetector", _gestureDetector);
            errors += CheckCritical("PinchGrabber", _pinchGrabber);
            errors += CheckCritical("TrainingFlowController", _trainingFlowController);

            // Important (features won't work)
            warnings += CheckOptional("GeminiApiConnector", _geminiApiConnector);
            warnings += CheckOptional("HUDManager", _hudManager);
            warnings += CheckOptional("AIChatPanel", _aiChatPanel);
            warnings += CheckOptional("AudioFeedbackManager", _audioFeedbackManager);

            // Optional (nice to have)
            warnings += CheckOptional("MediaPipeUDPProvider", _mediaPipeProvider);
            warnings += CheckOptional("WebcamBackground", _webcamBackground);
            warnings += CheckOptional("VoiceInputManager", _voiceInputManager);
            warnings += CheckOptional("HighlightResponder", _highlightResponder);

            // Validate data
            ValidateStreamingAssets();
            ValidateSceneObjects();

            // Summary
            Debug.Log("═══════════════════════════════════════════════════════");
            if (errors == 0 && warnings == 0)
            {
                Debug.Log("  ✅ ALL REFERENCES OK — Ready to run!");
            }
            else if (errors == 0)
            {
                Debug.LogWarning($"  ⚠ {warnings} optional reference(s) missing.");
            }
            else
            {
                Debug.LogError($"  ❌ {errors} CRITICAL + {warnings} optional missing!");
            }
            Debug.Log("═══════════════════════════════════════════════════════");
        }

        private int CheckCritical<T>(string name, T component) where T : Object
        {
            if (component == null)
            {
                Debug.LogError($"  ❌ CRITICAL: {name} is NOT assigned!");
                return 1;
            }
            Debug.Log($"  ✅ {name}: OK");
            return 0;
        }

        private int CheckOptional<T>(string name, T component) where T : Object
        {
            if (component == null)
            {
                Debug.LogWarning($"  ⚠ Optional: {name} is not assigned.");
                return 1;
            }
            Debug.Log($"  ✅ {name}: OK");
            return 0;
        }

        /// <summary>
        /// Validates that required StreamingAssets files exist.
        /// </summary>
        private void ValidateStreamingAssets()
        {
            string configPath = System.IO.Path.Combine(
                Application.streamingAssetsPath, "config.json");
            string scalerPath = System.IO.Path.Combine(
                Application.streamingAssetsPath, "scaler_params.json");

            if (!System.IO.File.Exists(configPath))
            {
                Debug.LogWarning("  ⚠ StreamingAssets/config.json not found (AI tutor needs API key).");
            }
            else
            {
                Debug.Log("  ✅ StreamingAssets/config.json: OK");
            }

            if (!System.IO.File.Exists(scalerPath))
            {
                Debug.LogWarning("  ⚠ StreamingAssets/scaler_params.json not found (gesture normalization).");
            }
            else
            {
                Debug.Log("  ✅ StreamingAssets/scaler_params.json: OK");
            }
        }

        /// <summary>
        /// Validates that required scene objects exist (SnapZones, GrabbableParts).
        /// </summary>
        private void ValidateSceneObjects()
        {
            SnapZone[] snapZones = FindObjectsOfType<SnapZone>();
            GrabbablePart[] parts = FindObjectsOfType<GrabbablePart>();

            if (snapZones.Length == 0)
            {
                Debug.LogWarning("  ⚠ No SnapZone components found in scene.");
            }
            else
            {
                Debug.Log($"  ✅ SnapZones: {snapZones.Length} found");
            }

            if (parts.Length == 0)
            {
                Debug.LogWarning("  ⚠ No GrabbablePart components found in scene.");
            }
            else
            {
                Debug.Log($"  ✅ GrabbableParts: {parts.Length} found");
            }
        }
    }
}
