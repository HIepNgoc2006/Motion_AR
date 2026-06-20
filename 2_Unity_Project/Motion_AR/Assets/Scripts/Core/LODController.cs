using UnityEngine;
using System.Collections.Generic;

namespace MotionAR.Core
{
    /// <summary>
    /// Manages Level of Detail (LOD) for 3D objects in the training scene.
    /// Automatically adjusts mesh complexity, material quality, and visual effects
    /// based on camera distance to maintain performance targets.
    /// <para>
    /// Attach to any GameObject with a MeshRenderer. Supports 3 LOD levels:
    /// High (full detail), Medium (simplified), Low (minimal).
    /// Falls back to procedural simplification if explicit LOD meshes are not provided.
    /// </para>
    /// </summary>
    public class LODController : MonoBehaviour
    {
        // ────────────────────── Configuration ──────────────────────

        [Header("LOD Distances")]
        [SerializeField]
        [Tooltip("Distance from camera below which High detail is used.")]
        private float _highDetailDistance = 2.0f;

        [SerializeField]
        [Tooltip("Distance from camera below which Medium detail is used.")]
        private float _mediumDetailDistance = 5.0f;

        [SerializeField]
        [Tooltip("Beyond this distance, Low detail is used (or object is culled).")]
        private float _lowDetailDistance = 10.0f;

        [SerializeField]
        [Tooltip("If true, objects beyond lowDetailDistance are completely hidden.")]
        private bool _cullBeyondLow = true;

        [Header("LOD Meshes (Optional)")]
        [SerializeField]
        [Tooltip("High detail mesh. If null, uses the original mesh.")]
        private Mesh _highDetailMesh;

        [SerializeField]
        [Tooltip("Medium detail mesh. If null, disables shadows as fallback.")]
        private Mesh _mediumDetailMesh;

        [SerializeField]
        [Tooltip("Low detail mesh. If null, disables additional effects as fallback.")]
        private Mesh _lowDetailMesh;

        [Header("Performance")]
        [SerializeField]
        [Tooltip("How often to re-evaluate LOD level (in seconds). Lower = more responsive but heavier.")]
        private float _evaluationInterval = 0.25f;

        [SerializeField]
        [Tooltip("Hysteresis margin to prevent LOD flickering at boundaries.")]
        private float _hysteresisMargin = 0.3f;

        // ────────────────────── State ──────────────────────

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _originalMesh;
        private LODLevel _currentLevel = LODLevel.High;
        private float _lastEvaluationTime;
        private Transform _cameraTransform;
        private bool _isVisible = true;

        // ────────────────────── Static Registry ──────────────────────

        private static readonly List<LODController> _allControllers = new List<LODController>();

        /// <summary>
        /// Read-only list of all active LODControllers for batch processing.
        /// </summary>
        public static IReadOnlyList<LODController> AllControllers => _allControllers;

        /// <summary>Current LOD level of this object.</summary>
        public LODLevel CurrentLevel => _currentLevel;

        // ────────────────────── Unity Lifecycle ──────────────────────

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            if (_meshFilter != null)
            {
                _originalMesh = _meshFilter.sharedMesh;
            }

            if (_highDetailMesh == null && _originalMesh != null)
            {
                _highDetailMesh = _originalMesh;
            }
        }

        private void OnEnable()
        {
            if (!_allControllers.Contains(this))
            {
                _allControllers.Add(this);
            }
        }

        private void OnDisable()
        {
            _allControllers.Remove(this);
        }

        private void Start()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                _cameraTransform = mainCam.transform;
            }
        }

        private void Update()
        {
            if (_cameraTransform == null || Time.time - _lastEvaluationTime < _evaluationInterval)
            {
                return;
            }

            _lastEvaluationTime = Time.time;
            EvaluateLOD();
        }

        // ────────────────────── LOD Evaluation ──────────────────────

        /// <summary>
        /// Evaluates the current camera distance and updates LOD level accordingly.
        /// Uses hysteresis to prevent flickering at LOD boundaries.
        /// </summary>
        private void EvaluateLOD()
        {
            float distance = Vector3.Distance(
                transform.position, _cameraTransform.position);

            LODLevel targetLevel = CalculateTargetLevel(distance);

            if (targetLevel != _currentLevel)
            {
                ApplyLODLevel(targetLevel);
            }
        }

        /// <summary>
        /// Calculates the target LOD level based on distance with hysteresis.
        /// </summary>
        private LODLevel CalculateTargetLevel(float distance)
        {
            // Apply hysteresis — when transitioning to a higher detail level,
            // require the camera to be closer than the threshold by the margin.
            float highThreshold = _currentLevel == LODLevel.High
                ? _highDetailDistance + _hysteresisMargin
                : _highDetailDistance;

            float mediumThreshold = _currentLevel <= LODLevel.Medium
                ? _mediumDetailDistance + _hysteresisMargin
                : _mediumDetailDistance;

            float lowThreshold = _currentLevel <= LODLevel.Low
                ? _lowDetailDistance + _hysteresisMargin
                : _lowDetailDistance;

            if (distance <= highThreshold)
            {
                return LODLevel.High;
            }
            else if (distance <= mediumThreshold)
            {
                return LODLevel.Medium;
            }
            else if (distance <= lowThreshold)
            {
                return LODLevel.Low;
            }
            else
            {
                return LODLevel.Culled;
            }
        }

        /// <summary>
        /// Applies the specified LOD level — swaps meshes and adjusts rendering settings.
        /// </summary>
        private void ApplyLODLevel(LODLevel level)
        {
            _currentLevel = level;

            switch (level)
            {
                case LODLevel.High:
                    SetVisible(true);
                    SetMesh(_highDetailMesh);
                    SetShadows(UnityEngine.Rendering.ShadowCastingMode.On);
                    SetMotionVectors(true);
                    break;

                case LODLevel.Medium:
                    SetVisible(true);
                    SetMesh(_mediumDetailMesh ?? _highDetailMesh);
                    SetShadows(UnityEngine.Rendering.ShadowCastingMode.Off);
                    SetMotionVectors(false);
                    break;

                case LODLevel.Low:
                    SetVisible(true);
                    SetMesh(_lowDetailMesh ?? _mediumDetailMesh ?? _highDetailMesh);
                    SetShadows(UnityEngine.Rendering.ShadowCastingMode.Off);
                    SetMotionVectors(false);
                    break;

                case LODLevel.Culled:
                    if (_cullBeyondLow)
                    {
                        SetVisible(false);
                    }
                    else
                    {
                        SetMesh(_lowDetailMesh ?? _highDetailMesh);
                        SetShadows(UnityEngine.Rendering.ShadowCastingMode.Off);
                        SetMotionVectors(false);
                    }
                    break;
            }
        }

        // ────────────────────── Helpers ──────────────────────

        private void SetMesh(Mesh mesh)
        {
            if (_meshFilter != null && mesh != null)
            {
                _meshFilter.sharedMesh = mesh;
            }
        }

        private void SetVisible(bool visible)
        {
            if (_meshRenderer != null && _isVisible != visible)
            {
                _meshRenderer.enabled = visible;
                _isVisible = visible;
            }
        }

        private void SetShadows(UnityEngine.Rendering.ShadowCastingMode mode)
        {
            if (_meshRenderer != null)
            {
                _meshRenderer.shadowCastingMode = mode;
            }
        }

        private void SetMotionVectors(bool enable)
        {
            if (_meshRenderer != null)
            {
                _meshRenderer.motionVectorGenerationMode = enable
                    ? MotionVectorGenerationMode.Object
                    : MotionVectorGenerationMode.Camera;
            }
        }

        /// <summary>
        /// Forces a specific LOD level override. Useful for testing or cutscenes.
        /// </summary>
        /// <param name="level">The LOD level to force.</param>
        public void ForceLevel(LODLevel level)
        {
            ApplyLODLevel(level);
            // Prevent automatic evaluation from overriding for a short period.
            _lastEvaluationTime = Time.time + 1.0f;
        }

        /// <summary>
        /// Restores automatic LOD evaluation after a ForceLevel call.
        /// </summary>
        public void RestoreAutomatic()
        {
            _lastEvaluationTime = 0f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw LOD distance spheres in editor.
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, _highDetailDistance);

            Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, _mediumDetailDistance);

            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, _lowDetailDistance);
        }
#endif
    }

    /// <summary>
    /// Level of detail stages for 3D objects.
    /// </summary>
    public enum LODLevel
    {
        /// <summary>Full detail — all effects, shadows, high-poly mesh.</summary>
        High = 0,
        /// <summary>Reduced detail — no shadows, medium-poly mesh.</summary>
        Medium = 1,
        /// <summary>Minimal detail — low-poly mesh, reduced effects.</summary>
        Low = 2,
        /// <summary>Completely hidden — renderer disabled.</summary>
        Culled = 3
    }
}
