using System;
using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;

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

        /// <summary>Number of frames in the sliding window for dynamic gesture input.</summary>
        private const int WINDOW_SIZE = 15;

        /// <summary>Number of hand landmarks tracked (MediaPipe convention).</summary>
        private const int LANDMARKS_COUNT = 21;

        /// <summary>Spatial dimensions per landmark (x, y, z).</summary>
        private const int FEATURES_PER_LANDMARK = 3;

        /// <summary>Total features per frame: 21 × 3 = 63.</summary>
        private const int TOTAL_FEATURES = LANDMARKS_COUNT * FEATURES_PER_LANDMARK;

        private PinchState _currentPinchState;
        private readonly List<float[]> _slidingWindow = new List<float[]>(WINDOW_SIZE);
        private Worker _sentisWorker;
        private float _cooldownTimer;
        private bool _isModelLoaded;

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
            // Flatten 21 landmarks × 3 floats = float[63].
            float[] features = new float[TOTAL_FEATURES];
            for (int i = 0; i < LANDMARKS_COUNT; i++)
            {
                int offset = i * FEATURES_PER_LANDMARK;
                features[offset] = data.Landmarks[i].x;
                features[offset + 1] = data.Landmarks[i].y;
                features[offset + 2] = data.Landmarks[i].z;
            }

            // Add to sliding window (FIFO).
            _slidingWindow.Add(features);
            if (_slidingWindow.Count > WINDOW_SIZE)
            {
                _slidingWindow.RemoveAt(0);
            }

            // Only run inference when the buffer is full and cooldown has elapsed.
            if (_slidingWindow.Count < WINDOW_SIZE || _cooldownTimer > 0f)
            {
                return;
            }

            RunInference();
        }

        /// <summary>
        /// Runs ONNX inference on the filled sliding window buffer.
        /// Creates a tensor with shape [1, WINDOW_SIZE, TOTAL_FEATURES], schedules
        /// the model, reads the output, and fires the gesture event if confidence
        /// exceeds the threshold.
        /// </summary>
        private void RunInference()
        {
            // Build flat array from the sliding window: shape [1, 15, 63].
            float[] tensorData = new float[WINDOW_SIZE * TOTAL_FEATURES];
            for (int frame = 0; frame < WINDOW_SIZE; frame++)
            {
                Array.Copy(_slidingWindow[frame], 0, tensorData, frame * TOTAL_FEATURES, TOTAL_FEATURES);
            }

            TensorFloat inputTensor = null;
            Tensor outputTensorRaw = null;

            try
            {
                inputTensor = new TensorFloat(
                    new TensorShape(1, WINDOW_SIZE, TOTAL_FEATURES),
                    tensorData
                );

                _sentisWorker.Schedule(inputTensor);

                outputTensorRaw = _sentisWorker.PeekOutput();
                TensorFloat outputTensor = outputTensorRaw as TensorFloat;

                if (outputTensor == null)
                {
                    return;
                }

                // Download results to CPU for reading.
                outputTensor.CompleteOperationsAndDownload();

                int numClasses = outputTensor.shape.length;
                int argmaxIndex = 0;
                float maxConfidence = float.MinValue;

                for (int i = 0; i < numClasses; i++)
                {
                    float value = outputTensor[i];
                    if (value > maxConfidence)
                    {
                        maxConfidence = value;
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
                // Note: PeekOutput returns a reference owned by the worker.
                // We do NOT dispose outputTensorRaw here as the worker manages its lifecycle.
            }
        }

        /// <summary>
        /// Maps a model output class index to the corresponding <see cref="GestureType"/> enum value.
        /// </summary>
        /// <param name="index">The argmax class index from the model output.</param>
        /// <returns>The corresponding GestureType, or GestureType.None for unknown indices.</returns>
        private static GestureType MapIndexToGestureType(int index)
        {
            // Map model output indices to GestureType enum values.
            // The enum is defined with explicit values 0-13.
            if (index >= 0 && index <= 13)
            {
                return (GestureType)index;
            }

            return GestureType.None;
        }

        /// <summary>
        /// Clears the sliding window buffer when tracking is lost to prevent
        /// stale data from influencing the next classification.
        /// </summary>
        private void HandleTrackingLost()
        {
            _slidingWindow.Clear();
        }
    }
}
