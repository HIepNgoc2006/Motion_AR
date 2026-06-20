using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TMPro;
using MotionAR.Core;

namespace MotionAR.UI
{
    /// <summary>
    /// Pause menu panel displayed during active training sessions.
    /// Provides options to resume, restart, or exit the current lesson.
    /// <para>
    /// Pauses the game by setting <c>Time.timeScale = 0</c> and unpauses on resume.
    /// All UI animations use <c>unscaledDeltaTime</c> to function during pause.
    /// Can be triggered by ESC key or a gesture-mapped button.
    /// </para>
    /// </summary>
    public class PauseMenuPanel : MonoBehaviour
    {
        // ────────────────────── References ──────────────────────

        [Header("Panel Root")]
        [SerializeField]
        [Tooltip("The root panel GameObject that is toggled on/off.")]
        private GameObject _panelRoot;

        [SerializeField]
        [Tooltip("CanvasGroup for fade animation.")]
        private CanvasGroup _canvasGroup;

        [Header("Buttons")]
        [SerializeField]
        [Tooltip("Resume button — closes the menu and continues training.")]
        private Button _resumeButton;

        [SerializeField]
        [Tooltip("Restart button — resets and restarts the current lesson.")]
        private Button _restartButton;

        [SerializeField]
        [Tooltip("Settings button — opens the settings panel.")]
        private Button _settingsButton;

        [SerializeField]
        [Tooltip("Exit button — exits to lesson select after confirmation.")]
        private Button _exitButton;

        [Header("Display")]
        [SerializeField]
        [Tooltip("Title text. Defaults to 'TẠM DỪNG'.")]
        private TextMeshProUGUI _titleText;

        [SerializeField]
        [Tooltip("Elapsed time display during pause.")]
        private TextMeshProUGUI _elapsedTimeText;

        [SerializeField]
        [Tooltip("Current step progress display.")]
        private TextMeshProUGUI _progressText;

        [Header("Animation")]
        [SerializeField]
        [Tooltip("Duration of fade-in/fade-out animation in seconds.")]
        private float _fadeDuration = 0.2f;

        [SerializeField]
        [Tooltip("Scale animation start value (smaller = more dramatic pop-in).")]
        private float _scaleFrom = 0.85f;

        // ────────────────────── State ──────────────────────

        /// <summary>Whether the pause menu is currently visible.</summary>
        public bool IsPaused { get; private set; }

        private bool _isAnimating;

        // ────────────────────── Unity Lifecycle ──────────────────────

        private void Awake()
        {
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(false);
            }

            // Auto-create UI if references are missing.
            if (_panelRoot == null)
            {
                CreateFallbackUI();
            }
        }

        private void OnEnable()
        {
            _resumeButton?.onClick.AddListener(Resume);
            _restartButton?.onClick.AddListener(HandleRestart);
            _settingsButton?.onClick.AddListener(HandleSettings);
            _exitButton?.onClick.AddListener(HandleExit);
        }

        private void OnDisable()
        {
            _resumeButton?.onClick.RemoveListener(Resume);
            _restartButton?.onClick.RemoveListener(HandleRestart);
            _settingsButton?.onClick.RemoveListener(HandleSettings);
            _exitButton?.onClick.RemoveListener(HandleExit);
        }

        private void Update()
        {
            // Toggle pause with Escape key during training.
            if (Input.GetKeyDown(KeyCode.Escape) && !_isAnimating)
            {
                if (AppFlowManager.Instance != null &&
                    AppFlowManager.Instance.CurrentPhase == AppPhase.Training)
                {
                    if (IsPaused)
                    {
                        Resume();
                    }
                    else
                    {
                        Pause();
                    }
                }
            }
        }

        // ────────────────────── Public Methods ──────────────────────

        /// <summary>
        /// Opens the pause menu and pauses the game.
        /// </summary>
        public void Pause()
        {
            if (IsPaused || _isAnimating) return;

            IsPaused = true;
            Time.timeScale = 0f;

            UpdateDisplayInfo();
            ShowPanelAsync().Forget();
        }

        /// <summary>
        /// Closes the pause menu and resumes the game.
        /// </summary>
        public void Resume()
        {
            if (!IsPaused || _isAnimating) return;

            IsPaused = false;
            Time.timeScale = 1f;

            HidePanelAsync().Forget();
        }

        // ────────────────────── Button Handlers ──────────────────────

        private void HandleRestart()
        {
            Resume();

            // Restart current lesson.
            if (SessionManager.Instance != null && SessionManager.Instance.CurrentLesson != null)
            {
                var lesson = SessionManager.Instance.CurrentLesson;
                SessionManager.Instance.ResetSession();
                SessionManager.Instance.StartLesson(lesson);
            }
        }

        private void HandleSettings()
        {
            // Don't unpause — overlay settings on top of pause.
            if (AppFlowManager.Instance != null)
            {
                AppFlowManager.Instance.ShowSettings();
            }
        }

        private void HandleExit()
        {
            Resume();

            if (AppFlowManager.Instance != null)
            {
                if (SessionManager.Instance != null)
                {
                    SessionManager.Instance.ResetSession();
                }
                AppFlowManager.Instance.ShowLessonSelect();
            }
        }

        // ────────────────────── Display Update ──────────────────────

        private void UpdateDisplayInfo()
        {
            if (_elapsedTimeText != null && SessionManager.Instance != null)
            {
                float elapsed = SessionManager.Instance.ElapsedTime;
                int minutes = (int)(elapsed / 60f);
                int seconds = (int)(elapsed % 60f);
                _elapsedTimeText.text = $"Thời gian: {minutes:D2}:{seconds:D2}";
            }

            if (_progressText != null && SessionManager.Instance != null)
            {
                var lesson = SessionManager.Instance.CurrentLesson;
                if (lesson != null)
                {
                    int current = SessionManager.Instance.CurrentStepIndex + 1;
                    int total = lesson.TotalSteps;
                    _progressText.text = $"Tiến độ: {current}/{total} bước";
                }
            }
        }

        // ────────────────────── Animations ──────────────────────

        private async UniTaskVoid ShowPanelAsync()
        {
            _isAnimating = true;

            if (_panelRoot != null) _panelRoot.SetActive(true);
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;

            RectTransform rt = _panelRoot?.GetComponent<RectTransform>();

            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeDuration);
                // Ease-out cubic
                float easedT = 1f - Mathf.Pow(1f - t, 3f);

                if (_canvasGroup != null) _canvasGroup.alpha = easedT;
                if (rt != null)
                {
                    float scale = Mathf.Lerp(_scaleFrom, 1f, easedT);
                    rt.localScale = new Vector3(scale, scale, 1f);
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            if (rt != null) rt.localScale = Vector3.one;

            _isAnimating = false;
        }

        private async UniTaskVoid HidePanelAsync()
        {
            _isAnimating = true;

            RectTransform rt = _panelRoot?.GetComponent<RectTransform>();

            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeDuration);
                float easedT = 1f - Mathf.Pow(1f - t, 3f);

                if (_canvasGroup != null) _canvasGroup.alpha = 1f - easedT;
                if (rt != null)
                {
                    float scale = Mathf.Lerp(1f, _scaleFrom, easedT);
                    rt.localScale = new Vector3(scale, scale, 1f);
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (_panelRoot != null) _panelRoot.SetActive(false);
            _isAnimating = false;
        }

        // ────────────────────── Fallback UI ──────────────────────

        /// <summary>
        /// Creates a minimal pause menu UI if no prefab references are set.
        /// </summary>
        private void CreateFallbackUI()
        {
            _panelRoot = new GameObject("PauseMenuPanel_Fallback");
            _panelRoot.transform.SetParent(transform, false);

            Canvas canvas = _panelRoot.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                // Can't create UI without a canvas in the hierarchy.
                Debug.LogWarning("[PauseMenuPanel] No Canvas found in parent hierarchy. Fallback UI aborted.");
                return;
            }

            RectTransform rt = _panelRoot.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Semi-transparent background
            Image bg = _panelRoot.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.7f);

            _canvasGroup = _panelRoot.AddComponent<CanvasGroup>();

            // Title
            GameObject titleGO = new GameObject("Title");
            titleGO.transform.SetParent(_panelRoot.transform, false);
            _titleText = titleGO.AddComponent<TextMeshProUGUI>();
            _titleText.text = "TẠM DỪNG";
            _titleText.fontSize = 48;
            _titleText.alignment = TextAlignmentOptions.Center;
            _titleText.color = new Color(0f, 1f, 1f, 1f); // Cyan
            RectTransform titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.3f, 0.7f);
            titleRT.anchorMax = new Vector2(0.7f, 0.85f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            // Elapsed time
            GameObject timeGO = new GameObject("ElapsedTime");
            timeGO.transform.SetParent(_panelRoot.transform, false);
            _elapsedTimeText = timeGO.AddComponent<TextMeshProUGUI>();
            _elapsedTimeText.fontSize = 24;
            _elapsedTimeText.alignment = TextAlignmentOptions.Center;
            _elapsedTimeText.color = Color.white;
            RectTransform timeRT = timeGO.GetComponent<RectTransform>();
            timeRT.anchorMin = new Vector2(0.3f, 0.6f);
            timeRT.anchorMax = new Vector2(0.7f, 0.7f);
            timeRT.offsetMin = Vector2.zero;
            timeRT.offsetMax = Vector2.zero;

            // Progress
            GameObject progressGO = new GameObject("Progress");
            progressGO.transform.SetParent(_panelRoot.transform, false);
            _progressText = progressGO.AddComponent<TextMeshProUGUI>();
            _progressText.fontSize = 20;
            _progressText.alignment = TextAlignmentOptions.Center;
            _progressText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            RectTransform progressRT = progressGO.GetComponent<RectTransform>();
            progressRT.anchorMin = new Vector2(0.3f, 0.53f);
            progressRT.anchorMax = new Vector2(0.7f, 0.6f);
            progressRT.offsetMin = Vector2.zero;
            progressRT.offsetMax = Vector2.zero;

            // Create buttons
            _resumeButton = CreateFallbackButton(_panelRoot.transform, "Tiếp tục", 0.45f);
            _restartButton = CreateFallbackButton(_panelRoot.transform, "Làm lại", 0.37f);
            _exitButton = CreateFallbackButton(_panelRoot.transform, "Thoát", 0.29f);

            _panelRoot.SetActive(false);
        }

        /// <summary>
        /// Creates a simple button for fallback UI.
        /// </summary>
        private Button CreateFallbackButton(Transform parent, string label, float yAnchor)
        {
            GameObject btnGO = new GameObject($"Btn_{label}");
            btnGO.transform.SetParent(parent, false);

            RectTransform btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.35f, yAnchor);
            btnRT.anchorMax = new Vector2(0.65f, yAnchor + 0.06f);
            btnRT.offsetMin = Vector2.zero;
            btnRT.offsetMax = Vector2.zero;

            Image btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0f, 0.8f, 0.8f, 0.3f);

            Button btn = btnGO.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.highlightedColor = new Color(0f, 1f, 1f, 0.5f);
            colors.pressedColor = new Color(0f, 0.6f, 0.6f, 0.7f);
            btn.colors = colors;

            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(btnGO.transform, false);
            TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 22;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            return btn;
        }
    }
}
