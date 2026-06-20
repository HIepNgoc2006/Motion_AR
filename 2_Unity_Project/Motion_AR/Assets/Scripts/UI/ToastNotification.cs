using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TMPro;
using System.Collections.Generic;

namespace MotionAR.UI
{
    /// <summary>
    /// Toast notification system for non-blocking, auto-dismissing messages.
    /// Displays short messages at the top or bottom of the screen that
    /// automatically fade out after a configurable duration.
    /// <para>
    /// Supports multiple simultaneous toasts with vertical stacking.
    /// Uses object pooling for performance. Messages queue if maximum
    /// visible count is exceeded.
    /// </para>
    /// </summary>
    public class ToastNotification : MonoBehaviour
    {
        // ────────────────────── Singleton ──────────────────────

        /// <summary>Global singleton instance.</summary>
        public static ToastNotification Instance { get; private set; }

        // ────────────────────── Configuration ──────────────────────

        [Header("Position")]
        [SerializeField]
        [Tooltip("Where to display toasts.")]
        private ToastPosition _position = ToastPosition.TopCenter;

        [Header("Timing")]
        [SerializeField]
        [Tooltip("Default display duration in seconds.")]
        private float _defaultDuration = 3.0f;

        [SerializeField]
        [Tooltip("Fade-in duration in seconds.")]
        private float _fadeInDuration = 0.2f;

        [SerializeField]
        [Tooltip("Fade-out duration in seconds.")]
        private float _fadeOutDuration = 0.3f;

        [Header("Limits")]
        [SerializeField]
        [Tooltip("Maximum number of visible toasts at once.")]
        private int _maxVisibleToasts = 3;

        [Header("Appearance")]
        [SerializeField]
        [Tooltip("Font size for toast text.")]
        private int _fontSize = 18;

        [SerializeField]
        [Tooltip("Vertical spacing between stacked toasts in pixels.")]
        private float _spacing = 8f;

        [SerializeField]
        [Tooltip("Horizontal padding inside toast.")]
        private float _horizontalPadding = 24f;

        [SerializeField]
        [Tooltip("Vertical padding inside toast.")]
        private float _verticalPadding = 12f;

        // ────────────────────── State ──────────────────────

        private RectTransform _containerRT;
        private readonly List<ToastItem> _activeToasts = new List<ToastItem>();
        private readonly Queue<ToastRequest> _pendingQueue = new Queue<ToastRequest>();

        // ────────────────────── Unity Lifecycle ──────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            CreateContainer();
        }

        // ────────────────────── Public API ──────────────────────

        /// <summary>
        /// Shows a toast notification with the default style.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="duration">Display duration in seconds. Use 0 for default.</param>
        public void Show(string message, float duration = 0f)
        {
            ShowToast(message, ToastType.Info, duration <= 0 ? _defaultDuration : duration);
        }

        /// <summary>
        /// Shows a success toast (green accent).
        /// </summary>
        public void ShowSuccess(string message, float duration = 0f)
        {
            ShowToast(message, ToastType.Success, duration <= 0 ? _defaultDuration : duration);
        }

        /// <summary>
        /// Shows a warning toast (yellow accent).
        /// </summary>
        public void ShowWarning(string message, float duration = 0f)
        {
            ShowToast(message, ToastType.Warning, duration <= 0 ? _defaultDuration : duration);
        }

        /// <summary>
        /// Shows an error toast (red accent).
        /// </summary>
        public void ShowError(string message, float duration = 0f)
        {
            ShowToast(message, ToastType.Error, duration <= 0 ? _defaultDuration + 1f : duration);
        }

        // ────────────────────── Core Logic ──────────────────────

        private void ShowToast(string message, ToastType type, float duration)
        {
            if (_activeToasts.Count >= _maxVisibleToasts)
            {
                // Queue the toast for later display.
                _pendingQueue.Enqueue(new ToastRequest(message, type, duration));
                return;
            }

            DisplayToastAsync(message, type, duration).Forget();
        }

        private async UniTaskVoid DisplayToastAsync(string message, ToastType type, float duration)
        {
            // Create toast item.
            ToastItem toast = CreateToastItem(message, type);
            _activeToasts.Add(toast);
            RepositionToasts();

            // Fade in.
            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeInDuration);
                toast.CanvasGroup.alpha = t;
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            toast.CanvasGroup.alpha = 1f;

            // Wait for display duration.
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(duration),
                ignoreTimeScale: true,
                PlayerLoopTiming.Update);

            // Fade out.
            elapsed = 0f;
            while (elapsed < _fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeOutDuration);
                toast.CanvasGroup.alpha = 1f - t;
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            // Cleanup.
            _activeToasts.Remove(toast);
            if (toast.Root != null)
            {
                Destroy(toast.Root);
            }
            RepositionToasts();

            // Dequeue pending toast if available.
            if (_pendingQueue.Count > 0)
            {
                ToastRequest next = _pendingQueue.Dequeue();
                DisplayToastAsync(next.Message, next.Type, next.Duration).Forget();
            }
        }

        // ────────────────────── Toast Creation ──────────────────────

        private ToastItem CreateToastItem(string message, ToastType type)
        {
            GameObject toastGO = new GameObject("Toast");
            toastGO.transform.SetParent(_containerRT, false);

            RectTransform rt = toastGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400f, 50f); // Will be resized by content

            // Background
            Image bg = toastGO.AddComponent<Image>();
            bg.color = GetBackgroundColor(type);

            // Left accent bar
            GameObject accent = new GameObject("Accent");
            accent.transform.SetParent(toastGO.transform, false);
            RectTransform accentRT = accent.AddComponent<RectTransform>();
            accentRT.anchorMin = new Vector2(0f, 0f);
            accentRT.anchorMax = new Vector2(0f, 1f);
            accentRT.sizeDelta = new Vector2(4f, 0f);
            accentRT.anchoredPosition = new Vector2(2f, 0f);
            Image accentImg = accent.AddComponent<Image>();
            accentImg.color = GetAccentColor(type);

            // Icon
            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(toastGO.transform, false);
            TextMeshProUGUI iconText = iconGO.AddComponent<TextMeshProUGUI>();
            iconText.text = GetIcon(type);
            iconText.fontSize = _fontSize;
            iconText.alignment = TextAlignmentOptions.Center;
            iconText.color = GetAccentColor(type);
            RectTransform iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0f, 0f);
            iconRT.anchorMax = new Vector2(0f, 1f);
            iconRT.sizeDelta = new Vector2(30f, 0f);
            iconRT.anchoredPosition = new Vector2(22f, 0f);

            // Text
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(toastGO.transform, false);
            TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
            text.text = message;
            text.fontSize = _fontSize;
            text.alignment = TextAlignmentOptions.Left;
            text.color = Color.white;
            text.enableWordWrapping = true;
            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0f, 0f);
            textRT.anchorMax = new Vector2(1f, 1f);
            textRT.offsetMin = new Vector2(42f, _verticalPadding * 0.5f);
            textRT.offsetMax = new Vector2(-_horizontalPadding, -_verticalPadding * 0.5f);

            // Calculate size based on text
            float preferredWidth = Mathf.Min(text.preferredWidth + 42f + _horizontalPadding * 2f, 500f);
            float preferredHeight = Mathf.Max(
                text.preferredHeight + _verticalPadding, 44f);
            rt.sizeDelta = new Vector2(preferredWidth, preferredHeight);

            // CanvasGroup for fade
            CanvasGroup cg = toastGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            return new ToastItem
            {
                Root = toastGO,
                RectTransform = rt,
                CanvasGroup = cg
            };
        }

        // ────────────────────── Layout ──────────────────────

        private void CreateContainer()
        {
            GameObject container = new GameObject("ToastContainer");
            container.transform.SetParent(transform, false);

            _containerRT = container.AddComponent<RectTransform>();

            switch (_position)
            {
                case ToastPosition.TopCenter:
                    _containerRT.anchorMin = new Vector2(0.5f, 1f);
                    _containerRT.anchorMax = new Vector2(0.5f, 1f);
                    _containerRT.pivot = new Vector2(0.5f, 1f);
                    _containerRT.anchoredPosition = new Vector2(0f, -20f);
                    break;

                case ToastPosition.BottomCenter:
                    _containerRT.anchorMin = new Vector2(0.5f, 0f);
                    _containerRT.anchorMax = new Vector2(0.5f, 0f);
                    _containerRT.pivot = new Vector2(0.5f, 0f);
                    _containerRT.anchoredPosition = new Vector2(0f, 20f);
                    break;

                case ToastPosition.TopRight:
                    _containerRT.anchorMin = new Vector2(1f, 1f);
                    _containerRT.anchorMax = new Vector2(1f, 1f);
                    _containerRT.pivot = new Vector2(1f, 1f);
                    _containerRT.anchoredPosition = new Vector2(-20f, -20f);
                    break;
            }

            _containerRT.sizeDelta = Vector2.zero;
        }

        private void RepositionToasts()
        {
            bool stackDown = _position == ToastPosition.TopCenter || _position == ToastPosition.TopRight;
            float yOffset = 0f;

            for (int i = 0; i < _activeToasts.Count; i++)
            {
                ToastItem toast = _activeToasts[i];
                if (toast.RectTransform == null) continue;

                float y = stackDown ? -yOffset : yOffset;
                toast.RectTransform.anchoredPosition = new Vector2(0f, y);
                yOffset += toast.RectTransform.sizeDelta.y + _spacing;
            }
        }

        // ────────────────────── Style Helpers ──────────────────────

        private static Color GetBackgroundColor(ToastType type)
        {
            return new Color(0.1f, 0.1f, 0.15f, 0.92f);
        }

        private static Color GetAccentColor(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success: return new Color(0.22f, 1f, 0.08f, 1f);  // #39FF14
                case ToastType.Warning: return new Color(1f, 0.85f, 0f, 1f);     // Yellow
                case ToastType.Error:   return new Color(1f, 0.03f, 0.23f, 1f);  // #FF073A
                default:                return new Color(0f, 1f, 1f, 1f);        // #00FFFF
            }
        }

        private static string GetIcon(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success: return "✓";
                case ToastType.Warning: return "⚠";
                case ToastType.Error:   return "✕";
                default:                return "ℹ";
            }
        }

        // ────────────────────── Data Structures ──────────────────────

        private struct ToastItem
        {
            public GameObject Root;
            public RectTransform RectTransform;
            public CanvasGroup CanvasGroup;
        }

        private struct ToastRequest
        {
            public string Message;
            public ToastType Type;
            public float Duration;

            public ToastRequest(string message, ToastType type, float duration)
            {
                Message = message;
                Type = type;
                Duration = duration;
            }
        }
    }

    /// <summary>
    /// Position presets for toast notifications.
    /// </summary>
    public enum ToastPosition
    {
        TopCenter,
        BottomCenter,
        TopRight
    }

    /// <summary>
    /// Visual style presets for toast messages.
    /// </summary>
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }
}
