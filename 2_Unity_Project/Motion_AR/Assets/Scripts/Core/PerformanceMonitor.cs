using UnityEngine;
using TMPro;

namespace MotionAR.Core
{
    /// <summary>
    /// Real-time performance monitor displaying FPS, frame time, and memory usage.
    /// <para>
    /// Shows a compact overlay in the corner of the screen for development/profiling.
    /// Can be toggled on/off via <see cref="SetVisible"/>. Designed for Phase 5 profiling.
    /// </para>
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField]
        [Tooltip("TextMeshProUGUI component for displaying stats.")]
        private TextMeshProUGUI _statsText;

        [SerializeField]
        [Tooltip("Background panel for the stats display.")]
        private CanvasGroup _canvasGroup;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("How often to update the display (seconds).")]
        private float _updateInterval = 0.5f;

        [SerializeField]
        [Tooltip("Show the monitor on startup.")]
        private bool _showOnStart = true;

        [SerializeField]
        [Tooltip("Target FPS for color-coding (green above, yellow near, red below).")]
        private int _targetFPS = 30;

        // ────────────────────── Private State ──────────────────────

        private float _timer;
        private int _frameCount;
        private float _fpsSum;
        private float _minFPS = float.MaxValue;
        private float _maxFPS = 0f;
        private float _lastFPS;
        private bool _isVisible;

        // ────────────────────── Colors ──────────────────────

        private static readonly Color COLOR_GOOD = new Color(0.22f, 1f, 0.08f);     // Neon green
        private static readonly Color COLOR_WARNING = new Color(1f, 0.75f, 0f);       // Amber
        private static readonly Color COLOR_BAD = new Color(1f, 0.03f, 0.23f);        // Neon red

        // ────────────────────── Unity Lifecycle ──────────────────────

        private void Start()
        {
            _isVisible = _showOnStart;
            UpdateVisibility();

            // Create fallback UI if no TextMeshPro is assigned.
            if (_statsText == null)
            {
                CreateFallbackUI();
            }
        }

        private void Update()
        {
            if (!_isVisible) return;

            _frameCount++;
            float currentFPS = 1f / Time.unscaledDeltaTime;
            _fpsSum += currentFPS;

            if (currentFPS < _minFPS) _minFPS = currentFPS;
            if (currentFPS > _maxFPS) _maxFPS = currentFPS;

            _timer += Time.unscaledDeltaTime;
            if (_timer >= _updateInterval)
            {
                _lastFPS = _fpsSum / _frameCount;
                UpdateDisplay();

                _timer = 0f;
                _frameCount = 0;
                _fpsSum = 0f;
                _minFPS = float.MaxValue;
                _maxFPS = 0f;
            }
        }

        // ────────────────────── Public API ──────────────────────

        /// <summary>
        /// Shows or hides the performance monitor.
        /// </summary>
        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            UpdateVisibility();
        }

        /// <summary>
        /// Toggles the performance monitor visibility.
        /// </summary>
        public void Toggle()
        {
            SetVisible(!_isVisible);
        }

        /// <summary>
        /// Returns the current averaged FPS value.
        /// </summary>
        public float CurrentFPS => _lastFPS;

        // ────────────────────── Private ──────────────────────

        private void UpdateDisplay()
        {
            if (_statsText == null) return;

            float frameTimeMs = 1000f / Mathf.Max(_lastFPS, 0.001f);
            long totalMemory = System.GC.GetTotalMemory(false);
            float memoryMB = totalMemory / (1024f * 1024f);

            // Color-code based on FPS target.
            Color fpsColor;
            if (_lastFPS >= _targetFPS)
                fpsColor = COLOR_GOOD;
            else if (_lastFPS >= _targetFPS * 0.7f)
                fpsColor = COLOR_WARNING;
            else
                fpsColor = COLOR_BAD;

            string hexColor = ColorUtility.ToHtmlStringRGB(fpsColor);

            _statsText.text = string.Format(
                "<color=#{0}>{1:F0} FPS</color>  |  {2:F1}ms  |  {3:F0}MB\n" +
                "<size=70%>Min: {4:F0}  Max: {5:F0}  Target: {6}</size>",
                hexColor,
                _lastFPS,
                frameTimeMs,
                memoryMB,
                _minFPS == float.MaxValue ? 0f : _minFPS,
                _maxFPS,
                _targetFPS
            );
        }

        private void UpdateVisibility()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = _isVisible ? 1f : 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
            else if (_statsText != null)
            {
                _statsText.gameObject.SetActive(_isVisible);
            }
        }

        /// <summary>
        /// Creates a minimal UI fallback if no TextMeshPro component is assigned.
        /// </summary>
        private void CreateFallbackUI()
        {
            // Try to find or create a Canvas.
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }
            if (canvas == null) return;

            // Create stats text.
            GameObject textGO = new GameObject("PerformanceStats_Text");
            textGO.transform.SetParent(canvas.transform, false);

            _statsText = textGO.AddComponent<TextMeshProUGUI>();
            _statsText.fontSize = 14;
            _statsText.color = COLOR_GOOD;
            _statsText.alignment = TextAlignmentOptions.TopRight;
            _statsText.raycastTarget = false;

            RectTransform rt = _statsText.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-10f, -10f);
            rt.sizeDelta = new Vector2(300f, 50f);

            // Create background.
            GameObject bgGO = new GameObject("PerformanceStats_BG");
            bgGO.transform.SetParent(textGO.transform, false);
            bgGO.transform.SetAsFirstSibling();

            UnityEngine.UI.Image bg = bgGO.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            bg.raycastTarget = false;

            RectTransform bgRt = bg.rectTransform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = new Vector2(20f, 10f);
            bgRt.anchoredPosition = Vector2.zero;

            // Add CanvasGroup for toggling.
            _canvasGroup = textGO.AddComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            UpdateVisibility();
        }
    }
}
