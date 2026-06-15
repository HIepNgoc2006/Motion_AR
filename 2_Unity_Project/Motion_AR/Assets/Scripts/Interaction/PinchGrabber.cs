using System;
using UnityEngine;

namespace MotionAR.Interaction
{
    /// <summary>
    /// Handles grab and release of <see cref="IGrabbable"/> objects using pinch gesture
    /// state from <see cref="GestureDetector"/>. Objects within grab radius are picked up
    /// when a pinch starts and released when the pinch ends.
    /// <para>
    /// This component subscribes to <see cref="GestureDetector.OnPinchUpdate"/> instead
    /// of computing pinch distance independently, ensuring a single source of truth
    /// for pinch state across the entire system.
    /// </para>
    /// </summary>
    public class PinchGrabber : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Reference to the hand tracking receiver that provides cursor position.")]
        private HandTrackingReceiver _handTrackingReceiver;

        [SerializeField]
        [Tooltip("Reference to the gesture detector that provides authoritative pinch state.")]
        private GestureDetector _gestureDetector;

        [SerializeField]
        [Tooltip("Radius around the cursor position to search for grabbable objects.")]
        private float _grabRadius = 0.5f;

        [SerializeField]
        [Tooltip("Percentage to reduce scale when an object is grabbed (0.05 = 5%).")]
        private float _scaleReduction = 0.05f;

        [SerializeField]
        [Tooltip("Layer mask for objects that can be grabbed.")]
        private LayerMask _grabbableLayer;

        private PinchState _currentPinchState = PinchState.Released;
        private GameObject _grabbedObject;
        private Vector3 _originalScale;
        private IGrabbable _grabbedComponent;

        // ────────────────────── Public Read-Only State ──────────────────────

        /// <summary>
        /// The GameObject currently being held, or null if nothing is grabbed.
        /// </summary>
        public GameObject GrabbedObject => _grabbedObject;

        /// <summary>
        /// The PartID of the currently held part, or empty string if nothing is grabbed.
        /// </summary>
        public string GrabbedPartID => _grabbedComponent?.PartID ?? string.Empty;

        /// <summary>
        /// Whether the user is currently holding an object.
        /// </summary>
        public bool IsHolding => _grabbedObject != null;

        // ────────────────────── Events ──────────────────────

        /// <summary>
        /// Fired when an object is successfully grabbed. Provides the grabbed GameObject.
        /// </summary>
        public event Action<GameObject> OnObjectGrabbed;

        /// <summary>
        /// Fired when an object is released. Provides the released GameObject and the
        /// world position where it was released (for SnapZone evaluation).
        /// </summary>
        public event Action<GameObject, Vector3> OnObjectReleased;

        private void OnEnable()
        {
            if (_gestureDetector != null)
            {
                _gestureDetector.OnPinchUpdate += HandlePinchUpdate;
            }

            if (_handTrackingReceiver != null)
            {
                _handTrackingReceiver.OnTrackingLost += HandleTrackingLost;
            }
        }

        private void OnDisable()
        {
            if (_gestureDetector != null)
            {
                _gestureDetector.OnPinchUpdate -= HandlePinchUpdate;
            }

            if (_handTrackingReceiver != null)
            {
                _handTrackingReceiver.OnTrackingLost -= HandleTrackingLost;
            }
        }

        /// <summary>
        /// Receives authoritative pinch state from GestureDetector.
        /// Reacts to state transitions to grab/release objects.
        /// </summary>
        /// <param name="state">Current pinch state (Released or Grabbed).</param>
        /// <param name="distance">Raw thumb-index Euclidean distance.</param>
        private void HandlePinchUpdate(PinchState state, float distance)
        {
            if (state == PinchState.Grabbed && _currentPinchState == PinchState.Released)
            {
                _currentPinchState = PinchState.Grabbed;
                TransitionToGrabbed();
            }
            else if (state == PinchState.Released && _currentPinchState == PinchState.Grabbed)
            {
                _currentPinchState = PinchState.Released;
                TransitionToReleased();
            }
        }

        /// <summary>
        /// Transitions from Released to Grabbed state. Searches for the nearest
        /// IGrabbable object within grab radius and attaches it to the cursor.
        /// </summary>
        private void TransitionToGrabbed()
        {
            Vector3 cursorPos = _handTrackingReceiver.SmoothedCursorPosition;
            Collider[] hits = Physics.OverlapSphere(cursorPos, _grabRadius, _grabbableLayer);

            if (hits.Length == 0)
            {
                return;
            }

            // Find the nearest collider that has an IGrabbable component and can be grabbed.
            float nearestDist = float.MaxValue;
            IGrabbable nearestGrabbable = null;
            GameObject nearestObject = null;

            for (int i = 0; i < hits.Length; i++)
            {
                IGrabbable grabbable = hits[i].GetComponent<IGrabbable>();
                if (grabbable == null)
                {
                    grabbable = hits[i].GetComponentInParent<IGrabbable>();
                }

                if (grabbable != null && grabbable.CanGrab())
                {
                    float dist = Vector3.Distance(cursorPos, hits[i].transform.position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestGrabbable = grabbable;
                        nearestObject = hits[i].gameObject;
                    }
                }
            }

            if (nearestGrabbable == null || nearestObject == null)
            {
                return;
            }

            _grabbedComponent = nearestGrabbable;
            _grabbedObject = nearestObject;
            _originalScale = _grabbedObject.transform.localScale;

            // Notify the grabbable component.
            _grabbedComponent.OnGrab();

            // Parent the object to the cursor visual for tracking.
            Transform cursorTransform = _handTrackingReceiver.transform;
            _grabbedObject.transform.SetParent(cursorTransform, true);

            // Scale down slightly to provide visual feedback.
            _grabbedObject.transform.localScale = _originalScale * (1f - _scaleReduction);

            OnObjectGrabbed?.Invoke(_grabbedObject);
        }

        /// <summary>
        /// Transitions from Grabbed to Released state. Detaches the held object,
        /// restores its scale, and fires the release event.
        /// </summary>
        private void TransitionToReleased()
        {
            if (_grabbedObject == null)
            {
                return;
            }

            Vector3 releasePosition = _grabbedObject.transform.position;

            // Detach from cursor.
            _grabbedObject.transform.SetParent(null, true);

            // Restore original scale.
            _grabbedObject.transform.localScale = _originalScale;

            // Notify the grabbable component.
            _grabbedComponent.OnRelease();

            GameObject releasedObject = _grabbedObject;

            // Clear references.
            _grabbedObject = null;
            _grabbedComponent = null;
            _originalScale = Vector3.one;

            OnObjectReleased?.Invoke(releasedObject, releasePosition);
        }

        /// <summary>
        /// Forces immediate release of any held object when tracking is lost.
        /// Prevents objects from being stuck to the cursor indefinitely.
        /// </summary>
        private void HandleTrackingLost()
        {
            if (_currentPinchState == PinchState.Grabbed)
            {
                _currentPinchState = PinchState.Released;
                TransitionToReleased();
            }
        }
    }
}
