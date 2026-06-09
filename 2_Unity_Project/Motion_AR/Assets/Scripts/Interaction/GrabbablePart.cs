using UnityEngine;

namespace MotionAR.Interaction
{
    /// <summary>
    /// Concrete implementation of <see cref="IGrabbable"/> for 3D training parts.
    /// Attach this to any GameObject that should be grabbable via pinch gesture.
    /// <para>
    /// Provides visual feedback by changing the part's color when grabbed,
    /// and supports locking the part after it has been correctly snapped.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class GrabbablePart : MonoBehaviour, IGrabbable
    {
        [SerializeField]
        [Tooltip("Unique identifier matching the requiredPartID in StepData.")]
        private string _partID;

        [SerializeField]
        [Tooltip("Whether this part can currently be grabbed.")]
        private bool _canGrab = true;

        [SerializeField]
        [Tooltip("Color applied to the part when it is being held.")]
        private Color _grabColor = new Color(0f, 1f, 1f, 1f); // Cyan

        private Renderer _renderer;
        private Color _originalColor;
        private bool _isGrabbed;

        /// <summary>
        /// Unique identifier for this grabbable part. Matches
        /// <see cref="Data.StepData.requiredPartID"/> for lesson validation.
        /// </summary>
        public string PartID => _partID;

        /// <summary>
        /// Whether this part is currently being held by the user.
        /// </summary>
        public bool IsGrabbed => _isGrabbed;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer == null)
            {
                _renderer = GetComponentInChildren<Renderer>();
            }

            if (_renderer != null)
            {
                _originalColor = _renderer.material.color;
            }
        }

        /// <summary>
        /// Returns true if the part is in a state where it can be picked up.
        /// Parts that have been snapped into place return false.
        /// </summary>
        public bool CanGrab() => _canGrab && !_isGrabbed;

        /// <summary>
        /// Called by <see cref="PinchGrabber"/> when the user pinches this part.
        /// Changes the material color to provide visual grab feedback.
        /// </summary>
        public void OnGrab()
        {
            _isGrabbed = true;

            if (_renderer != null)
            {
                _renderer.material.color = _grabColor;
            }
        }

        /// <summary>
        /// Called by <see cref="PinchGrabber"/> when the user releases this part.
        /// Restores the original material color.
        /// </summary>
        public void OnRelease()
        {
            _isGrabbed = false;

            if (_renderer != null)
            {
                _renderer.material.color = _originalColor;
            }
        }

        /// <summary>
        /// Locks or unlocks this part for grabbing.
        /// Call with <c>false</c> after the part has been correctly snapped
        /// to prevent it from being moved again.
        /// </summary>
        /// <param name="canGrab">Whether the part should be grabbable.</param>
        public void SetGrabbable(bool canGrab)
        {
            _canGrab = canGrab;
        }

        /// <summary>
        /// Sets a new PartID at runtime. Useful for dynamically spawned parts.
        /// </summary>
        /// <param name="partID">The new unique identifier.</param>
        public void SetPartID(string partID)
        {
            _partID = partID;
        }
    }
}
