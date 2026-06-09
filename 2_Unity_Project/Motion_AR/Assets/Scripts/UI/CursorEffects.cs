using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace MotionAR.UI
{
    /// <summary>
    /// Holographic cursor visual that follows the hand tracking position.
    /// Features a pulsing ring effect when pinching and a trailing glow.
    /// <para>
    /// Design reference: Master Plan §7.3 — "Khi Bóp (Pinch), linh kiện co nhẹ
    /// (Scale -5%) để tạo cảm giác đã cầm nắm thành công."
    /// </para>
    /// </summary>
    public class CursorEffects : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("The cursor transform driven by HandTrackingReceiver.")]
        private Transform _cursorTransform;

        [SerializeField]
        [Tooltip("Reference to the GestureDetector for pinch state.")]
        private Interaction.GestureDetector _gestureDetector;

        [Header("Ring Visual")]
        [SerializeField]
        [Tooltip("Outer ring sprite renderer (holographic ring).")]
        private SpriteRenderer _ringRenderer;

        [SerializeField]
        [Tooltip("Inner dot sprite renderer.")]
        private SpriteRenderer _dotRenderer;

        [Header("Colors")]
        [SerializeField] private Color _idleColor = new Color(0f, 1f, 1f, 0.6f);     // Cyan semi-transparent
        [SerializeField] private Color _pinchColor = new Color(0f, 1f, 1f, 1f);       // Cyan full
        [SerializeField] private Color _grabColor = new Color(0.22f, 1f, 0.08f, 1f);  // Neon green

        [Header("Animation")]
        [SerializeField] private float _pulseSpeed = 6f;
        [SerializeField] private float _pulseMinScale = 0.8f;
        [SerializeField] private float _pulseMaxScale = 1.2f;
        [SerializeField] private float _transitionSpeed = 10f;

        [Header("Trail")]
        [SerializeField]
        [Tooltip("Optional TrailRenderer for cursor movement trail.")]
        private TrailRenderer _trailRenderer;

        private Vector3 _baseScale;
        private bool _isPinching;
        private bool _isGrabbing;
        private float _targetAlpha;
        private Color _currentColor;

        private void Start()
        {
            if (_cursorTransform != null)
            {
                _baseScale = _cursorTransform.localScale;
            }

            _currentColor = _idleColor;
            _targetAlpha = _idleColor.a;

            ApplyColor(_idleColor);
        }

        private void OnEnable()
        {
            if (_gestureDetector != null)
            {
                _gestureDetector.OnPinchUpdate += HandlePinchUpdate;
            }
        }

        private void OnDisable()
        {
            if (_gestureDetector != null)
            {
                _gestureDetector.OnPinchUpdate -= HandlePinchUpdate;
            }
        }

        private void Update()
        {
            AnimatePulse();
            AnimateColorTransition();
        }

        /// <summary>
        /// Handles pinch state changes from GestureDetector.
        /// </summary>
        private void HandlePinchUpdate(Interaction.PinchState state, float distance)
        {
            _isPinching = (state == Interaction.PinchState.Grabbed);
        }

        /// <summary>
        /// Animates the cursor ring with a breathing pulse effect.
        /// Pulse is faster and more prominent when pinching.
        /// </summary>
        private void AnimatePulse()
        {
            if (_cursorTransform == null) return;

            float speed = _isPinching ? _pulseSpeed * 1.5f : _pulseSpeed;
            float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
            float scale = Mathf.Lerp(_pulseMinScale, _pulseMaxScale, t);

            if (_isPinching)
            {
                // Shrink slightly when pinching for tactile feedback.
                scale *= 0.85f;
            }

            _cursorTransform.localScale = _baseScale * scale;
        }

        /// <summary>
        /// Smoothly transitions cursor color between idle/pinch/grab states.
        /// </summary>
        private void AnimateColorTransition()
        {
            Color targetColor = _isPinching ? _pinchColor : _idleColor;

            _currentColor = Color.Lerp(_currentColor, targetColor, Time.deltaTime * _transitionSpeed);

            ApplyColor(_currentColor);
        }

        /// <summary>
        /// Applies color to all cursor visual elements.
        /// </summary>
        private void ApplyColor(Color color)
        {
            if (_ringRenderer != null) _ringRenderer.color = color;
            if (_dotRenderer != null) _dotRenderer.color = color;

            if (_trailRenderer != null)
            {
                _trailRenderer.startColor = new Color(color.r, color.g, color.b, color.a * 0.5f);
                _trailRenderer.endColor = new Color(color.r, color.g, color.b, 0f);
            }
        }
    }
}
