using System;
using UnityEngine;
using MotionAR.Core;
using MotionAR.Data;

namespace MotionAR.Interaction
{
    /// <summary>
    /// Orchestrates the training flow by connecting <see cref="PinchGrabber"/> release
    /// events with <see cref="SnapZone"/> validation and <see cref="SessionManager"/>
    /// step progression.
    /// <para>
    /// When a grabbed object is released, this controller finds the nearest SnapZone,
    /// attempts to snap, validates the placement against the current lesson step,
    /// and either advances the lesson or records a mistake.
    /// </para>
    /// </summary>
    public class TrainingFlowController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Reference to the PinchGrabber that fires release events.")]
        private PinchGrabber _pinchGrabber;

        [SerializeField]
        [Tooltip("The lesson to start when the scene loads. Leave null to skip auto-start.")]
        private LessonData _lessonData;

        [SerializeField]
        [Tooltip("Maximum distance to search for a SnapZone when an object is released.")]
        private float _snapSearchRadius = 1.5f;

        [SerializeField]
        [Tooltip("Whether to automatically start the lesson on scene load.")]
        private bool _autoStartLesson = true;

        /// <summary>
        /// Fired when a part is correctly placed. Provides the placed GameObject.
        /// </summary>
        public event Action<GameObject> OnCorrectPlacement;

        /// <summary>
        /// Fired when a part is incorrectly placed. Provides the misplaced GameObject.
        /// </summary>
        public event Action<GameObject> OnIncorrectPlacement;

        /// <summary>
        /// Fired when the lesson is fully completed. Provides the final score.
        /// </summary>
        public event Action<float> OnLessonFinished;

        private void OnEnable()
        {
            if (_pinchGrabber != null)
            {
                _pinchGrabber.OnObjectReleased += HandleObjectReleased;
            }

            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnLessonCompleted += HandleLessonCompleted;
            }
        }

        private void OnDisable()
        {
            if (_pinchGrabber != null)
            {
                _pinchGrabber.OnObjectReleased -= HandleObjectReleased;
            }

            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnLessonCompleted -= HandleLessonCompleted;
            }
        }

        private void Start()
        {
            if (_autoStartLesson && _lessonData != null && SessionManager.Instance != null)
            {
                SessionManager.Instance.StartLesson(_lessonData);
                Debug.Log($"[TrainingFlowController] Auto-started lesson: {_lessonData.lessonTitle}");
            }
        }

        /// <summary>
        /// Handles object release from PinchGrabber. Evaluates placement against
        /// lesson requirements and provides appropriate feedback.
        /// </summary>
        /// <param name="releasedObject">The GameObject that was released.</param>
        /// <param name="releasePosition">World position where the object was released.</param>
        private void HandleObjectReleased(GameObject releasedObject, Vector3 releasePosition)
        {
            if (releasedObject == null) return;

            // Don't evaluate if no lesson is active.
            if (SessionManager.Instance == null || !SessionManager.Instance.IsLessonActive)
            {
                return;
            }

            // Find nearest available SnapZone.
            SnapZone nearestZone = SnapZone.FindNearestSnapZone(releasePosition, _snapSearchRadius);

            if (nearestZone == null)
            {
                // Released in empty space — not a mistake, just no snap.
                return;
            }

            // Attempt to snap the object into the zone.
            bool snapped = nearestZone.TrySnap(releasedObject);

            if (!snapped)
            {
                // Wrong part for this zone, or zone already occupied.
                SessionManager.Instance.RecordMistake();
                OnIncorrectPlacement?.Invoke(releasedObject);
                Debug.Log($"[TrainingFlowController] Incorrect placement: " +
                          $"part tried to snap into zone '{nearestZone.SnapZoneID}'");
                return;
            }

            // Part was snapped — now validate against the current lesson step.
            StepData currentStep = SessionManager.Instance.GetCurrentStep();
            if (currentStep == null)
            {
                return;
            }

            IGrabbable grabbable = releasedObject.GetComponent<IGrabbable>();
            if (grabbable == null)
            {
                grabbable = releasedObject.GetComponentInParent<IGrabbable>();
            }

            string partID = grabbable?.PartID ?? string.Empty;

            bool isCorrectPart = string.Equals(
                partID, currentStep.requiredPartID, StringComparison.Ordinal);
            bool isCorrectZone = string.Equals(
                nearestZone.SnapZoneID, currentStep.snapZoneID, StringComparison.Ordinal);

            if (isCorrectPart && isCorrectZone)
            {
                // Correct placement — lock part and advance step.
                GrabbablePart grabbablePart = releasedObject.GetComponent<GrabbablePart>();
                if (grabbablePart != null)
                {
                    grabbablePart.SetGrabbable(false);
                }

                OnCorrectPlacement?.Invoke(releasedObject);
                SessionManager.Instance.AdvanceStep();

                Debug.Log($"[TrainingFlowController] Correct placement: " +
                          $"'{partID}' → '{nearestZone.SnapZoneID}'");
            }
            else
            {
                // Part snapped into a valid zone but not the one required by the current step.
                SessionManager.Instance.RecordMistake();
                OnIncorrectPlacement?.Invoke(releasedObject);

                // Unsnap the zone so the user can try again.
                nearestZone.ResetZone();

                // Restore grabbability.
                GrabbablePart grabbablePart = releasedObject.GetComponent<GrabbablePart>();
                if (grabbablePart != null)
                {
                    grabbablePart.SetGrabbable(true);
                }

                // Detach from snap position so the user can re-grab it.
                Rigidbody rb = releasedObject.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                }

                Debug.Log($"[TrainingFlowController] Wrong step: " +
                          $"expected '{currentStep.requiredPartID}' → '{currentStep.snapZoneID}', " +
                          $"got '{partID}' → '{nearestZone.SnapZoneID}'");
            }
        }

        /// <summary>
        /// Handles lesson completion from SessionManager.
        /// </summary>
        /// <param name="score">Final score (0-100).</param>
        private void HandleLessonCompleted(float score)
        {
            OnLessonFinished?.Invoke(score);
            Debug.Log($"[TrainingFlowController] Lesson finished! Score: {score:F0}");
        }
    }
}
