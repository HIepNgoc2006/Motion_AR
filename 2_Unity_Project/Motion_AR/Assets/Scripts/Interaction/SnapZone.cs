using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MotionAR.Interaction
{
    /// <summary>
    /// Magnetic snap point where a specific part can be placed and locked.
    /// Validates part identity, provides audio and visual feedback for correct
    /// and incorrect placements, and supports nearest-zone lookup via a
    /// self-registering static registry.
    /// </summary>
    public class SnapZone : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Unique identifier for this snap zone.")]
        private string _snapZoneID;

        [SerializeField]
        [Tooltip("The PartID of the part that should be placed here.")]
        private string _expectedPartID;

        [SerializeField]
        [Tooltip("Maximum distance from the snap point for a snap to trigger.")]
        private float _snapRadius = 0.3f;

        [SerializeField]
        [Tooltip("Transform representing the exact position and rotation for snapped parts.")]
        private Transform _snapPoint;

        [SerializeField]
        [Tooltip("Audio clip played on a successful snap.")]
        private AudioClip _snapSuccessSound;

        [SerializeField]
        [Tooltip("Audio clip played when the wrong part is placed.")]
        private AudioClip _snapErrorSound;

        [SerializeField]
        [Tooltip("Material applied briefly to the zone on an incorrect placement.")]
        private Material _errorMaterial;

        [SerializeField]
        [Tooltip("Duration in seconds for the red error flash.")]
        private float _errorFlashDuration = 1.0f;

        private static readonly List<SnapZone> _allZones = new List<SnapZone>();

        private CancellationTokenSource _flashCts;

        /// <summary>
        /// Unique identifier for this snap zone.
        /// </summary>
        public string SnapZoneID => _snapZoneID;

        /// <summary>
        /// The PartID of the part expected by this snap zone.
        /// </summary>
        public string ExpectedPartID => _expectedPartID;

        /// <summary>
        /// Whether a part is currently snapped into this zone.
        /// </summary>
        public bool IsOccupied { get; private set; }

        /// <summary>
        /// The PartID of the part currently occupying this zone, or null if empty.
        /// </summary>
        public string OccupiedPartID { get; private set; }

        private void OnEnable()
        {
            if (!_allZones.Contains(this))
            {
                _allZones.Add(this);
            }
        }

        private void OnDisable()
        {
            _allZones.Remove(this);
            CancelFlash();
        }

        private void OnDestroy()
        {
            CancelFlash();
        }

        /// <summary>
        /// Attempts to snap a part into this zone. Returns true if the snap was successful.
        /// Provides audio and visual feedback for both correct and incorrect placements.
        /// </summary>
        /// <param name="part">The GameObject being placed.</param>
        /// <returns>True if the part was correctly snapped; false otherwise.</returns>
        public bool TrySnap(GameObject part)
        {
            if (part == null || IsOccupied)
            {
                return false;
            }

            Transform snapTarget = _snapPoint != null ? _snapPoint : transform;
            float distance = Vector3.Distance(part.transform.position, snapTarget.position);

            // Too far away — no feedback, just reject.
            if (distance > _snapRadius)
            {
                return false;
            }

            // Part is within range. Validate the part identity.
            IGrabbable grabbable = part.GetComponent<IGrabbable>();
            if (grabbable == null)
            {
                grabbable = part.GetComponentInParent<IGrabbable>();
            }

            string partID = grabbable?.PartID ?? string.Empty;

            if (string.Equals(partID, _expectedPartID, StringComparison.Ordinal))
            {
                // Correct part — snap into place.
                PerformSnap(part, partID);
                return true;
            }
            else
            {
                // Wrong part — flash error and play error sound.
                PerformErrorFeedback(part);
                return false;
            }
        }

        /// <summary>
        /// Snaps a part into the exact position and rotation of the snap point,
        /// freezes its rigidbody, plays success audio, and marks the zone as occupied.
        /// </summary>
        private void PerformSnap(GameObject part, string partID)
        {
            Transform snapTarget = _snapPoint != null ? _snapPoint : transform;

            part.transform.position = snapTarget.position;
            part.transform.rotation = snapTarget.rotation;

            Rigidbody rb = part.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            if (_snapSuccessSound != null)
            {
                AudioSource.PlayClipAtPoint(_snapSuccessSound, snapTarget.position);
            }

            IsOccupied = true;
            OccupiedPartID = partID;
        }

        /// <summary>
        /// Provides visual and audio feedback for an incorrect part placement.
        /// Flashes the part's material to the error material and plays the error sound.
        /// </summary>
        private void PerformErrorFeedback(GameObject part)
        {
            if (_snapErrorSound != null)
            {
                Transform snapTarget = _snapPoint != null ? _snapPoint : transform;
                AudioSource.PlayClipAtPoint(_snapErrorSound, snapTarget.position);
            }

            if (_errorMaterial != null)
            {
                CancelFlash();
                _flashCts = new CancellationTokenSource();
                FlashErrorMaterialAsync(part, _flashCts.Token).Forget();
            }
        }

        /// <summary>
        /// Asynchronously flashes the error material on a part for the configured duration,
        /// then restores the original material. Uses UniTask instead of coroutines.
        /// </summary>
        private async UniTaskVoid FlashErrorMaterialAsync(GameObject part, CancellationToken cancellationToken)
        {
            if (part == null)
            {
                return;
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = part.GetComponentInChildren<Renderer>();
            }

            if (renderer == null)
            {
                return;
            }

            Material originalMaterial = renderer.material;
            renderer.material = _errorMaterial;

            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(_errorFlashDuration),
                    ignoreTimeScale: false,
                    PlayerLoopTiming.Update,
                    cancellationToken
                );
            }
            catch (OperationCanceledException)
            {
                // Flash was cancelled — still restore the original material if possible.
            }

            if (renderer != null)
            {
                renderer.material = originalMaterial;
            }
        }

        /// <summary>
        /// Cancels any active error flash animation.
        /// </summary>
        private void CancelFlash()
        {
            if (_flashCts != null)
            {
                _flashCts.Cancel();
                _flashCts.Dispose();
                _flashCts = null;
            }
        }

        /// <summary>
        /// Resets this snap zone to its unoccupied state.
        /// Does not move or modify any currently snapped part.
        /// </summary>
        public void ResetZone()
        {
            IsOccupied = false;
            OccupiedPartID = null;
        }

        /// <summary>
        /// Finds the nearest non-occupied SnapZone to the given world position
        /// within the specified maximum range.
        /// </summary>
        /// <param name="position">World position to search from.</param>
        /// <param name="maxRange">Maximum search distance.</param>
        /// <returns>The nearest available SnapZone, or null if none found.</returns>
        public static SnapZone FindNearestSnapZone(Vector3 position, float maxRange)
        {
            SnapZone nearest = null;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < _allZones.Count; i++)
            {
                SnapZone zone = _allZones[i];
                if (zone == null || zone.IsOccupied)
                {
                    continue;
                }

                Transform snapTarget = zone._snapPoint != null ? zone._snapPoint : zone.transform;
                float dist = Vector3.Distance(position, snapTarget.position);

                if (dist <= maxRange && dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = zone;
                }
            }

            return nearest;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform snapTarget = _snapPoint != null ? _snapPoint : transform;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(snapTarget.position, _snapRadius);
        }
#endif
    }
}
