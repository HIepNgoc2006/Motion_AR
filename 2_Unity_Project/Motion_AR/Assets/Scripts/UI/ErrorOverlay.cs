using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace MotionAR.UI
{
    /// <summary>
    /// Full-screen red flash overlay that triggers on incorrect part placement.
    /// Provides immediate visual feedback that the learner made a mistake.
    /// <para>
    /// Design reference: Master Plan §7.5 — Error flash with Neon Red (#FF073A).
    /// </para>
    /// </summary>
    public class ErrorOverlay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("Full-screen Image component for the error flash overlay.")]
        private Image _overlayImage;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Color of the error flash.")]
        private Color _errorColor = new Color(1f, 0.027f, 0.227f, 0.4f); // #FF073A at 40% opacity

        [SerializeField]
        [Tooltip("Duration of the flash in seconds.")]
        private float _flashDuration = 0.5f;

        [SerializeField]
        [Tooltip("How fast the flash fades in.")]
        private float _fadeInDuration = 0.08f;

        private CancellationTokenSource _flashCts;

        private void Awake()
        {
            // Start fully transparent.
            if (_overlayImage != null)
            {
                Color c = _overlayImage.color;
                c.a = 0f;
                _overlayImage.color = c;
                _overlayImage.raycastTarget = false; // Don't block clicks.
            }
        }

        private void OnEnable()
        {
            if (MotionAR.Core.SessionManager.Instance != null)
            {
                MotionAR.Core.SessionManager.Instance.OnMistakeRecorded += TriggerFlash;
            }
        }

        private void OnDisable()
        {
            if (MotionAR.Core.SessionManager.Instance != null)
            {
                MotionAR.Core.SessionManager.Instance.OnMistakeRecorded -= TriggerFlash;
            }

            CancelFlash();
        }

        /// <summary>
        /// Triggers the error flash. Can be called manually or via SessionManager event.
        /// </summary>
        public void TriggerFlash()
        {
            CancelFlash();
            _flashCts = new CancellationTokenSource();
            FlashAsync(_flashCts.Token).Forget();
        }

        /// <summary>
        /// Performs the flash animation: quick fade-in, then gradual fade-out.
        /// </summary>
        private async UniTaskVoid FlashAsync(CancellationToken ct)
        {
            if (_overlayImage == null) return;

            // Quick fade in.
            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                float t = elapsed / _fadeInDuration;
                Color c = _errorColor;
                c.a = Mathf.Lerp(0f, _errorColor.a, t);
                _overlayImage.color = c;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            _overlayImage.color = _errorColor;

            // Gradual fade out.
            float fadeOutDuration = _flashDuration - _fadeInDuration;
            elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutDuration;
                Color c = _errorColor;
                c.a = Mathf.Lerp(_errorColor.a, 0f, t);
                _overlayImage.color = c;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            Color final = _overlayImage.color;
            final.a = 0f;
            _overlayImage.color = final;
        }

        private void CancelFlash()
        {
            if (_flashCts != null)
            {
                _flashCts.Cancel();
                _flashCts.Dispose();
                _flashCts = null;
            }
        }
    }
}
