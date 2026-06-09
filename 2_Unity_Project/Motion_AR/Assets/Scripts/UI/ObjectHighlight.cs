using UnityEngine;

namespace MotionAR.UI
{
    /// <summary>
    /// Highlights nearby grabbable objects when the cursor is within range.
    /// Adds an emissive glow outline to indicate the object can be interacted with.
    /// <para>
    /// Design reference: Master Plan §7.4 — "Outline emissive khi tay gần linh kiện."
    /// Uses material property manipulation (emission) compatible with URP.
    /// </para>
    /// </summary>
    public class ObjectHighlight : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the HandTrackingReceiver for cursor position.")]
        private Interaction.HandTrackingReceiver _handTrackingReceiver;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Distance within which objects start glowing.")]
        private float _highlightRadius = 0.8f;

        [SerializeField]
        [Tooltip("Distance at which glow reaches maximum intensity.")]
        private float _maxGlowRadius = 0.3f;

        [SerializeField]
        [Tooltip("Emission color for the highlight glow.")]
        private Color _glowColor = new Color(0f, 1f, 1f, 1f); // Cyan

        [SerializeField]
        [Tooltip("Maximum emission intensity (HDR).")]
        private float _maxEmissionIntensity = 2f;

        [SerializeField]
        [Tooltip("Layer mask for objects that can be highlighted.")]
        private LayerMask _highlightLayer;

        [SerializeField]
        [Tooltip("How quickly the glow transitions.")]
        private float _glowTransitionSpeed = 8f;

        // Cache for tracking which objects are currently highlighted.
        private Renderer _currentHighlightedRenderer;
        private Color _currentEmission;
        private Color _targetEmission;
        private bool _hasOriginalEmission;
        private Color _originalEmission;
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        private void Update()
        {
            if (_handTrackingReceiver == null) return;

            Vector3 cursorPos = _handTrackingReceiver.SmoothedCursorPosition;

            // Find nearest highlightable object.
            Collider[] hits = Physics.OverlapSphere(cursorPos, _highlightRadius, _highlightLayer);

            Renderer nearestRenderer = null;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                // Only highlight objects that have IGrabbable and can be grabbed.
                Interaction.IGrabbable grabbable = hits[i].GetComponent<Interaction.IGrabbable>();
                if (grabbable == null)
                    grabbable = hits[i].GetComponentInParent<Interaction.IGrabbable>();

                if (grabbable == null || !grabbable.CanGrab()) continue;

                float dist = Vector3.Distance(cursorPos, hits[i].transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestRenderer = hits[i].GetComponent<Renderer>();
                    if (nearestRenderer == null)
                        nearestRenderer = hits[i].GetComponentInChildren<Renderer>();
                }
            }

            // Handle highlight transitions.
            if (nearestRenderer != null && nearestRenderer != _currentHighlightedRenderer)
            {
                // New object — clear old, start new.
                ClearHighlight();
                StartHighlight(nearestRenderer);
            }
            else if (nearestRenderer == null && _currentHighlightedRenderer != null)
            {
                // No object in range — fade out.
                ClearHighlight();
            }

            // Update emission intensity based on distance.
            if (_currentHighlightedRenderer != null && nearestRenderer != null)
            {
                float normalizedDist = Mathf.InverseLerp(_highlightRadius, _maxGlowRadius, nearestDist);
                float intensity = Mathf.Lerp(0f, _maxEmissionIntensity, normalizedDist);
                _targetEmission = _glowColor * intensity;
            }

            // Smoothly interpolate emission.
            if (_currentHighlightedRenderer != null)
            {
                _currentEmission = Color.Lerp(_currentEmission, _targetEmission,
                    Time.deltaTime * _glowTransitionSpeed);

                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                _currentHighlightedRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionColorID, _currentEmission);
                _currentHighlightedRenderer.SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// Starts highlighting a new renderer by enabling emission.
        /// </summary>
        private void StartHighlight(Renderer renderer)
        {
            _currentHighlightedRenderer = renderer;
            _currentEmission = Color.black;
            _targetEmission = Color.black;

            // Save original emission if the material has one.
            if (renderer.material.HasProperty(EmissionColorID))
            {
                _originalEmission = renderer.material.GetColor(EmissionColorID);
                _hasOriginalEmission = true;
            }
            else
            {
                _originalEmission = Color.black;
                _hasOriginalEmission = false;
            }

            // Enable emission keyword for URP/Standard shader.
            renderer.material.EnableKeyword("_EMISSION");
        }

        /// <summary>
        /// Clears the current highlight and restores original emission.
        /// </summary>
        private void ClearHighlight()
        {
            if (_currentHighlightedRenderer != null)
            {
                // Restore original emission.
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                _currentHighlightedRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionColorID, _hasOriginalEmission ? _originalEmission : Color.black);
                _currentHighlightedRenderer.SetPropertyBlock(mpb);

                _currentHighlightedRenderer = null;
            }
        }

        private void OnDisable()
        {
            ClearHighlight();
        }
    }
}
