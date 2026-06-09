using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using MotionAR.Interaction;

namespace MotionAR.AI
{
    /// <summary>
    /// Parses AI tutor responses to identify part and snap zone references,
    /// then highlights the corresponding 3D objects in the scene.
    /// <para>
    /// The AI response may contain part IDs or snap zone IDs that match objects
    /// in the scene. This responder finds those objects and triggers a temporary
    /// highlight effect to guide the learner.
    /// </para>
    /// </summary>
    public class HighlightResponder : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        [Tooltip("Duration in seconds for the highlight effect.")]
        private float _highlightDuration = 3f;

        [SerializeField]
        [Tooltip("Color used to highlight referenced parts.")]
        private Color _highlightColor = new Color(1f, 0.84f, 0f, 1f); // Gold

        [SerializeField]
        [Tooltip("Color used to highlight referenced snap zones.")]
        private Color _snapZoneHighlightColor = new Color(0.22f, 1f, 0.08f, 1f); // Neon green

        [SerializeField]
        [Tooltip("Pulse speed for the highlight animation.")]
        private float _pulseSpeed = 4f;

        // Track active highlights for cleanup.
        private readonly List<HighlightTarget> _activeHighlights = new List<HighlightTarget>();

        private struct HighlightTarget
        {
            public Renderer Renderer;
            public Color OriginalEmission;
            public Color TargetColor;
            public float EndTime;
        }

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// Processes an AI response text to find and highlight referenced objects.
        /// Call this after receiving a response from the Gemini API.
        /// </summary>
        /// <param name="responseText">The AI's response text.</param>
        public void ProcessAIResponse(string responseText)
        {
            if (string.IsNullOrEmpty(responseText)) return;

            // Clear previous highlights.
            ClearHighlights();

            // Find all GrabbableParts in the scene.
            GrabbablePart[] parts = FindObjectsOfType<GrabbablePart>();
            SnapZone[] snapZones = FindObjectsOfType<SnapZone>();

            string responseLower = responseText.ToLowerInvariant();

            // Check each part to see if its ID is mentioned in the response.
            foreach (GrabbablePart part in parts)
            {
                if (string.IsNullOrEmpty(part.PartID)) continue;

                // Check if the part ID or a natural language variant is mentioned.
                string partIdLower = part.PartID.ToLowerInvariant();
                string naturalName = ConvertIdToNaturalName(partIdLower);

                if (responseLower.Contains(partIdLower) ||
                    responseLower.Contains(naturalName))
                {
                    HighlightObject(part.gameObject, _highlightColor);
                }
            }

            // Check each snap zone.
            foreach (SnapZone zone in snapZones)
            {
                if (string.IsNullOrEmpty(zone.SnapZoneID)) continue;

                string zoneIdLower = zone.SnapZoneID.ToLowerInvariant();
                string naturalName = ConvertIdToNaturalName(zoneIdLower);

                if (responseLower.Contains(zoneIdLower) ||
                    responseLower.Contains(naturalName))
                {
                    HighlightObject(zone.gameObject, _snapZoneHighlightColor);
                }
            }

            // Also look for keywords like "bước tiếp theo", "vị trí" that indicate
            // the current step's target should be highlighted.
            if (responseLower.Contains("bước tiếp") ||
                responseLower.Contains("vị trí") ||
                responseLower.Contains("đặt vào") ||
                responseLower.Contains("lắp vào"))
            {
                HighlightCurrentStepTarget(parts, snapZones);
            }
        }

        /// <summary>
        /// Highlights the target part and snap zone for the current lesson step.
        /// </summary>
        private void HighlightCurrentStepTarget(GrabbablePart[] parts, SnapZone[] zones)
        {
            if (Core.SessionManager.Instance == null ||
                !Core.SessionManager.Instance.IsLessonActive) return;

            Data.StepData currentStep = Core.SessionManager.Instance.GetCurrentStep();
            if (currentStep == null) return;

            // Highlight the required part.
            foreach (GrabbablePart part in parts)
            {
                if (part.PartID == currentStep.requiredPartID && part.CanGrab())
                {
                    HighlightObject(part.gameObject, _highlightColor);
                    break;
                }
            }

            // Highlight the target snap zone.
            foreach (SnapZone zone in zones)
            {
                if (zone.SnapZoneID == currentStep.snapZoneID && !zone.IsOccupied)
                {
                    HighlightObject(zone.gameObject, _snapZoneHighlightColor);
                    break;
                }
            }
        }

        /// <summary>
        /// Applies a temporary emission highlight to a GameObject.
        /// </summary>
        private void HighlightObject(GameObject obj, Color color)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = obj.GetComponentInChildren<Renderer>();
            }

            if (renderer == null) return;

            // Save original emission.
            Color originalEmission = Color.black;
            if (renderer.material.HasProperty(EmissionColorID))
            {
                originalEmission = renderer.material.GetColor(EmissionColorID);
            }

            // Enable emission.
            renderer.material.EnableKeyword("_EMISSION");

            _activeHighlights.Add(new HighlightTarget
            {
                Renderer = renderer,
                OriginalEmission = originalEmission,
                TargetColor = color,
                EndTime = Time.time + _highlightDuration
            });
        }

        private void Update()
        {
            // Animate active highlights with pulse effect.
            for (int i = _activeHighlights.Count - 1; i >= 0; i--)
            {
                HighlightTarget target = _activeHighlights[i];

                if (target.Renderer == null || Time.time > target.EndTime)
                {
                    // Expired — restore original.
                    if (target.Renderer != null)
                    {
                        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                        target.Renderer.GetPropertyBlock(mpb);
                        mpb.SetColor(EmissionColorID, target.OriginalEmission);
                        target.Renderer.SetPropertyBlock(mpb);
                    }

                    _activeHighlights.RemoveAt(i);
                    continue;
                }

                // Pulse animation.
                float pulse = (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f;
                Color emission = target.TargetColor * Mathf.Lerp(0.5f, 2f, pulse);

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                target.Renderer.GetPropertyBlock(block);
                block.SetColor(EmissionColorID, emission);
                target.Renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// Converts snake_case or camelCase IDs to space-separated natural names.
        /// E.g., "oil_filter" → "oil filter", "monitorScreen" → "monitor screen".
        /// </summary>
        private static string ConvertIdToNaturalName(string id)
        {
            // Replace underscores with spaces.
            string result = id.Replace('_', ' ').Replace('-', ' ');

            // Insert spaces before capitals (camelCase).
            result = Regex.Replace(result, "([a-z])([A-Z])", "$1 $2").ToLowerInvariant();

            return result.Trim();
        }

        /// <summary>
        /// Removes all active highlights and restores original emissions.
        /// </summary>
        public void ClearHighlights()
        {
            foreach (HighlightTarget target in _activeHighlights)
            {
                if (target.Renderer != null)
                {
                    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                    target.Renderer.GetPropertyBlock(mpb);
                    mpb.SetColor(EmissionColorID, target.OriginalEmission);
                    target.Renderer.SetPropertyBlock(mpb);
                }
            }

            _activeHighlights.Clear();
        }

        private void OnDisable()
        {
            ClearHighlights();
        }
    }
}
