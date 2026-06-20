using System;
using System.Collections.Generic;
using System.IO;
using Unity.Sentis;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MotionAR.Interaction
{
    /// <summary>
    /// Hybrid gesture recognition system combining instant pinch detection (Task 1)
    /// with ONNX-based dynamic gesture classification via Unity Sentis (Task 2).
    /// <para>
    /// Task 1 runs every frame with zero latency, computing thumb-index Euclidean
    /// distance and applying hysteresis for clean state transitions.
    /// </para>
    /// <para>
    /// Task 2 maintains a sliding window of 15 frames, each flattened to 63 floats
    /// (21 landmarks × 3), and runs the ONNX model to classify dynamic gestures
    /// when the buffer is full and cooldown has elapsed.
    /// </para>
    /// </summary>
    public class GestureDetector : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Reference to the hand tracking receiver for hand data events.")]
        private HandTrackingReceiver _handTrackingReceiver;

        [SerializeField]
        [Tooltip("ONNX gesture classification model. Leave null to disable dynamic gestures.")]
        private ModelAsset _gestureModel;

        [SerializeField]
        [Tooltip("Thumb-index distance below which a pinch starts (hysteresis lower bound).")]
        private float _pinchStartThreshold = 0.04f;

        [SerializeField]
        [Tooltip("Thumb-index distance above which a pinch ends (hysteresis upper bound).")]
        private float _pinchEndThreshold = 0.06f;

        [SerializeField]
        [Tooltip("Minimum confidence for a dynamic gesture classification to be accepted.")]
        private float _confidenceThreshold = 0.8f;

        [SerializeField]
        [Tooltip("Seconds of cooldown after a dynamic gesture is detected before the next can fire.")]
        private float _cooldownDuration = 1.0f;

        [SerializeField]
        [Tooltip("Run ONNX inference every N frames (1 = every frame, 2 = skip 1, etc.). Higher = better performance.")]
        [Range(1, 5)]
        private int _inferenceEveryNFrames = 2;

        /// <summary>Number of frames in the sliding window for dynamic gesture input.</summary>
        private const int WINDOW_SIZE = 15;

        /// <summary>Number of hand landmarks tracked (MediaPipe convention).</summary>
        private const int LANDMARKS_COUNT = 21;

        /// <summary>Spatial dimensions per landmark (x, y, z).</summary>
        private const int FEATURES_PER_LANDMARK = 3;

        /// <summary>Total features per frame: 21 × 3 = 63.</summary>
        private const int TOTAL_FEATURES = LANDMARKS_COUNT * FEATURES_PER_LANDMARK;

        private PinchState _currentPinchState;
        private readonly Queue<float[]> _slidingWindow = new Queue<float[]>(WINDOW_SIZE);
        private Worker _sentisWorker;
        private float _cooldownTimer;
        private bool _isModelLoaded;
        private int _frameCounter;

        // Pre-allocated buffer to avoid per-frame GC allocations.
        private readonly float[] _featuresBuffer = new float[TOTAL_FEATURES];

        // ── StandardScaler normalization params (exported from Python) ──
        private float[] _scalerMean;
        private float[] _scalerScale;
        private bool _scalerLoaded;

        /// <summary>
        /// Fired every frame with the current pinch state and the raw thumb-index distance.
        /// </summary>
        public event Action<PinchState, float> OnPinchUpdate;

        /// <summary>
        /// Fired when a dynamic gesture is classified with confidence above the threshold.
        /// Provides the detected <see cref="GestureType"/> and its confidence score.
        /// </summary>
        public event Action<GestureType, float> OnDynamicGestureDetected;

        private void Start()
        {
            _currentPinchState = PinchState.Released;
            _cooldownTimer = 0f;
            _isModelLoaded = false;

            LoadScalerParams();

            if (_gestureModel != null)
            {
                LoadModel();
            }
            else
            {
                Debug.LogWarning("[GestureDetector] ONNX model not assigned. Dynamic gesture recognition disabled.");
            }
        }

        /// <summary>
        /// Loads the ONNX model and creates a Sentis Worker.
        /// Attempts GPU compute backend first, falls back to CPU on failure.
        /// </summary>
        private void LoadModel()
        {
            try
            {
                Model runtimeModel = ModelLoader.Load(_gestureModel);

                try
                {
                    _sentisWorker = new Worker(runtimeModel, BackendType.GPUCompute);
                }
                catch (Exception gpuEx)
                {
                    Debug.LogWarning($"[GestureDetector] GPU backend failed ({gpuEx.Message}). Falling back to CPU.");
                    _sentisWorker = new Worker(runtimeModel, BackendType.CPU);
                }

                _isModelLoaded = true;
                Debug.Log("[GestureDetector] ONNX model loaded successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GestureDetector] Failed to load ONNX model: {ex.Message}");
                _isModelLoaded = false;
            }
        }

        private void OnEnable()
        {
            if (_handTrackingReceiver != null)
            {
                _handTrackingReceiver.OnHandUpdate += HandleHandUpdate;
                _handTrackingReceiver.OnTrackingLost += HandleTrackingLost;
            }
        }

        private void OnDisable()
        {
            if (_handTrackingReceiver != null)
            {
                _handTrackingReceiver.OnHandUpdate -= HandleHandUpdate;
                _handTrackingReceiver.OnTrackingLost -= HandleTrackingLost;
            }
        }

        private void OnDestroy()
        {
            if (_sentisWorker != null)
            {
                _sentisWorker.Dispose();
                _sentisWorker = null;
            }
        }

        private void Update()
        {
            // Tick down cooldown timer.
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
            }
        }

        /// <summary>
        /// Processes each hand data frame for both pinch detection and dynamic gesture
        /// classification.
        /// </summary>
        private void HandleHandUpdate(HandData data)
        {
            if (data.Landmarks == null || data.Landmarks.Length < LANDMARKS_COUNT)
            {
                return;
            }

            // ===== TASK 1: PINCH (INSTANT, 0ms) =====
            ProcessPinch(data);

            // ===== TASK 2: DYNAMIC GESTURE via ONNX (SLIDING WINDOW) =====
            if (_isModelLoaded)
            {
                ProcessDynamicGesture(data);
            }
        }

        /// <summary>
        /// Extracts the Euclidean distance between thumb tip (landmark 4) and index tip
        /// (landmark 8), applies hysteresis to determine pinch state transitions, and
        /// fires <see cref="OnPinchUpdate"/> every frame.
        /// </summary>
        private void ProcessPinch(HandData data)
        {
            float distance = Vector3.Distance(data.Landmarks[4], data.Landmarks[8]);

            switch (_currentPinchState)
            {
                case PinchState.Released:
                    if (distance < _pinchStartThreshold)
                    {
                        _currentPinchState = PinchState.Grabbed;
                    }
                    break;

                case PinchState.Grabbed:
                    if (distance > _pinchEndThreshold)
                    {
                        _currentPinchState = PinchState.Released;
                    }
                    break;
            }

            OnPinchUpdate?.Invoke(_currentPinchState, distance);
        }

        /// <summary>
        /// Buffers the current frame's landmarks into the sliding window and, when the
        /// buffer is full and cooldown has elapsed, runs ONNX inference to classify
        /// the dynamic gesture.
        /// </summary>
        private void ProcessDynamicGesture(HandData data)
        {
            // Flatten 21 landmarks × 3 floats = float[63] into reusable buffer.
            for (int i = 0; i < LANDMARKS_COUNT; i++)
            {
                int offset = i * FEATURES_PER_LANDMARK;
                _featuresBuffer[offset] = data.Landmarks[i].x;
                _featuresBuffer[offset + 1] = data.Landmarks[i].y;
                _featuresBuffer[offset + 2] = data.Landmarks[i].z;
            }

            // Apply StandardScaler normalization: (x - mean) / scale.
            // This must match the preprocessing used during model training.
            NormalizeFeatures(_featuresBuffer);

            // Copy to a new array for the window (buffer is reused each frame).
            float[] frameCopy = new float[TOTAL_FEATURES];
            System.Array.Copy(_featuresBuffer, frameCopy, TOTAL_FEATURES);

            // Add to sliding window (FIFO queue — O(1) enqueue/dequeue).
            _slidingWindow.Enqueue(frameCopy);
            while (_slidingWindow.Count > WINDOW_SIZE)
            {
                _slidingWindow.Dequeue();
            }

            // Only run inference when the buffer is full and cooldown has elapsed.
            if (_slidingWindow.Count < WINDOW_SIZE || _cooldownTimer > 0f)
            {
                return;
            }

            // Frame-skip optimization: only run ONNX every N frames.
            _frameCounter++;
            if (_frameCounter % _inferenceEveryNFrames != 0)
            {
                return;
            }

            RunInference();
        }

        /// <summary>
        /// Runs ONNX inference on the filled sliding window buffer.
        /// Creates a tensor with shape [1, WINDOW_SIZE, TOTAL_FEATURES], schedules
        /// the model, reads the output, applies Softmax to convert raw logits into
        /// probabilities, and fires the gesture event if confidence exceeds the threshold.
        /// </summary>
        private void RunInference()
        {
            // Build flat array from the sliding window: shape [1, 15, 63].
            float[] tensorData = new float[WINDOW_SIZE * TOTAL_FEATURES];
            int frameIdx = 0;
            foreach (float[] frameData in _slidingWindow)
            {
                System.Array.Copy(frameData, 0, tensorData, frameIdx * TOTAL_FEATURES, TOTAL_FEATURES);
                frameIdx++;
            }

            Tensor<float> inputTensor = null;
            Tensor<float> cpuOutputTensor = null;

            try
            {
                inputTensor = new Tensor<float>(
                    new TensorShape(1, WINDOW_SIZE, TOTAL_FEATURES),
                    tensorData
                );

                _sentisWorker.Schedule(inputTensor);

                Tensor outputRef = _sentisWorker.PeekOutput();
                Tensor<float> outputTensorRef = outputRef as Tensor<float>;

                if (outputTensorRef == null)
                {
                    return;
                }

                // Download results to CPU for reading (Sentis 2.1+ API).
                cpuOutputTensor = outputTensorRef.ReadbackAndClone();

                int numClasses = cpuOutputTensor.shape.length;

                // ── Apply Softmax to convert raw logits → probabilities ──
                // The Transformer model outputs raw logits (not probabilities).
                // Without Softmax, comparing against a 0.8 threshold is meaningless.
                float maxLogit = float.MinValue;
                for (int i = 0; i < numClasses; i++)
                {
                    float val = cpuOutputTensor[i];
                    if (val > maxLogit) maxLogit = val;
                }

                float sumExp = 0f;
                float[] probabilities = new float[numClasses];
                for (int i = 0; i < numClasses; i++)
                {
                    // Subtract maxLogit for numerical stability (prevents overflow).
                    probabilities[i] = Mathf.Exp(cpuOutputTensor[i] - maxLogit);
                    sumExp += probabilities[i];
                }

                int argmaxIndex = 0;
                float maxConfidence = 0f;
                for (int i = 0; i < numClasses; i++)
                {
                    probabilities[i] /= sumExp;
                    if (probabilities[i] > maxConfidence)
                    {
                        maxConfidence = probabilities[i];
                        argmaxIndex = i;
                    }
                }

                if (maxConfidence > _confidenceThreshold)
                {
                    GestureType detectedGesture = MapIndexToGestureType(argmaxIndex);

                    if (detectedGesture != GestureType.None)
                    {
                        OnDynamicGestureDetected?.Invoke(detectedGesture, maxConfidence);
                        _cooldownTimer = _cooldownDuration;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GestureDetector] Inference error: {ex.Message}");
            }
            finally
            {
                inputTensor?.Dispose();
                // cpuOutputTensor is a clone we own — must dispose it.
                cpuOutputTensor?.Dispose();
            }
        }

        /// <summary>
        /// Maps a model output class index to the corresponding <see cref="GestureType"/> enum value.
        /// </summary>
        /// <param name="index">The argmax class index from the model output.</param>
        /// <returns>The corresponding GestureType, or GestureType.None for unknown indices.</returns>
        private static GestureType MapIndexToGestureType(int index)
        {
            // Python's LabelEncoder sorts class labels alphabetically.
            // Model output indices (alphabetical) → Unity GestureType enum values.
            return index switch
            {
                0  => GestureType.OpenPalm,    // expand   → OpenPalm
                1  => GestureType.Grab,        // grab     → Grab
                2  => GestureType.None,        // pinch    → handled by Task 1 (instant pinch detection)
                3  => GestureType.None,        // rotation_ccw → no Unity mapping yet
                4  => GestureType.None,        // rotation_cw  → no Unity mapping yet
                5  => GestureType.None,        // shake    → no Unity mapping yet
                6  => GestureType.SwipeDown,   // swipe_down  → SwipeDown
                7  => GestureType.SwipeLeft,   // swipe_left  → SwipeLeft
                8  => GestureType.None,        // swipe_plus  → no Unity mapping yet
                9  => GestureType.SwipeRight,  // swipe_right → SwipeRight
                10 => GestureType.SwipeUp,     // swipe_up    → SwipeUp
                11 => GestureType.Peace,       // swipe_v     → Peace
                12 => GestureType.None,        // swipe_x     → no Unity mapping yet
                13 => GestureType.Tap,         // tap         → Tap
                _  => GestureType.None
            };
        }

        /// <summary>
        /// Clears the sliding window buffer when tracking is lost to prevent
        /// stale data from influencing the next classification.
        /// </summary>
        private void HandleTrackingLost()
        {
            _slidingWindow.Clear();
        }

        // ────────────────────── StandardScaler Loading ──────────────────────

        /// <summary>
        /// Loads StandardScaler parameters (mean and scale arrays) from
        /// StreamingAssets/scaler_params.json for feature normalization.
        /// If the file is missing, inference will run without normalization.
        /// </summary>
        private void LoadScalerParams()
        {
            _scalerLoaded = false;
            string path = Path.Combine(Application.streamingAssetsPath, "scaler_params.json");

            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    "[GestureDetector] scaler_params.json not found in StreamingAssets. " +
                    "Inference will run WITHOUT normalization (accuracy may suffer).");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                JObject jObj = JObject.Parse(json);
                _scalerMean = jObj["mean"].ToObject<float[]>();
                _scalerScale = jObj["scale"].ToObject<float[]>();

                if (_scalerMean.Length == TOTAL_FEATURES &&
                    _scalerScale.Length == TOTAL_FEATURES)
                {
                    _scalerLoaded = true;
                    Debug.Log($"[GestureDetector] Scaler params loaded ({TOTAL_FEATURES} features).");
                }
                else
                {
                    Debug.LogError(
                        $"[GestureDetector] Scaler dimension mismatch: " +
                        $"mean={_scalerMean.Length}, scale={_scalerScale.Length}, " +
                        $"expected={TOTAL_FEATURES}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GestureDetector] Failed to load scaler params: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies StandardScaler normalization: (x - mean) / scale for each feature.
        /// No-op if scaler params were not loaded successfully.
        /// </summary>
        /// <param name="features">The float[63] feature array to normalize in-place.</param>
        private void NormalizeFeatures(float[] features)
        {
            if (!_scalerLoaded) return;

            for (int i = 0; i < features.Length && i < TOTAL_FEATURES; i++)
            {
                // Guard against division by zero (degenerate features).
                if (_scalerScale[i] > 1e-8f)
                {
                    features[i] = (features[i] - _scalerMean[i]) / _scalerScale[i];
                }
            }
        }
    }
}
