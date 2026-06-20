using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TMPro;
using System.Collections.Generic;
using MotionAR.Core;

namespace MotionAR.UI
{
    /// <summary>
    /// First-time user onboarding tutorial overlay system.
    /// Displays sequential tutorial steps with highlight masks, arrows,
    /// and instruction text to guide new users through the application.
    /// <para>
    /// Each step can highlight a specific UI element, show directional
    /// arrows, and display explanatory text. Progresses via "Next" button
    /// or tap-to-continue. Persists completion state via PlayerPrefs.
    /// </para>
    /// </summary>
    public class TutorialOverlay : MonoBehaviour
    {
        // ────────────────────── Singleton ──────────────────────

        /// <summary>Global singleton instance.</summary>
        public static TutorialOverlay Instance { get; private set; }

        // ────────────────────── Configuration ──────────────────────

        [Header("Panel")]
        [SerializeField]
        [Tooltip("Root panel that covers the whole screen.")]
        private GameObject _panelRoot;

        [SerializeField]
        [Tooltip("CanvasGroup for global fade.")]
        private CanvasGroup _canvasGroup;

        [Header("Content")]
        [SerializeField]
        [Tooltip("Title text for the current tutorial step.")]
        private TextMeshProUGUI _stepTitle;

        [SerializeField]
        [Tooltip("Description text for the current tutorial step.")]
        private TextMeshProUGUI _stepDescription;

        [SerializeField]
        [Tooltip("Step counter (e.g., '2/5').")]
        private TextMeshProUGUI _stepCounter;

        [Header("Navigation")]
        [SerializeField]
        [Tooltip("Next/Continue button.")]
        private Button _nextButton;

        [SerializeField]
        [Tooltip("Skip All button.")]
        private Button _skipButton;

        [SerializeField]
        [Tooltip("Text on the Next button.")]
        private TextMeshProUGUI _nextButtonText;

        [Header("Arrow")]
        [SerializeField]
        [Tooltip("Arrow indicator that points at the highlighted element.")]
        private RectTransform _arrowIndicator;

        [Header("Animation")]
        [SerializeField]
        [Tooltip("Fade duration for transitions.")]
        private float _fadeDuration = 0.25f;

        [SerializeField]
        [Tooltip("Arrow bob animation amplitude in pixels.")]
        private float _arrowBobAmplitude = 10f;

        [SerializeField]
        [Tooltip("Arrow bob animation speed.")]
        private float _arrowBobSpeed = 3f;

        // ────────────────────── Tutorial Data ──────────────────────

        private readonly List<TutorialStep> _steps = new List<TutorialStep>();
        private int _currentStepIndex;
        private bool _isActive;
        private string _tutorialKey;

        // ────────────────────── Unity Lifecycle ──────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (_panelRoot == null)
            {
                CreateFallbackUI();
            }

            if (_panelRoot != null)
            {
                _panelRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            _nextButton?.onClick.AddListener(NextStep);
            _skipButton?.onClick.AddListener(SkipAll);
        }

        private void OnDisable()
        {
            _nextButton?.onClick.RemoveListener(NextStep);
            _skipButton?.onClick.RemoveListener(SkipAll);
        }

        private void Update()
        {
            // Animate arrow bob
            if (_isActive && _arrowIndicator != null && _arrowIndicator.gameObject.activeSelf)
            {
                Vector2 pos = _arrowIndicator.anchoredPosition;
                float baseY = pos.y;
                float bob = Mathf.Sin(Time.unscaledTime * _arrowBobSpeed) * _arrowBobAmplitude;
                _arrowIndicator.anchoredPosition = new Vector2(pos.x, baseY + bob * Time.unscaledDeltaTime);
            }
        }

        // ────────────────────── Public API ──────────────────────

        /// <summary>
        /// Defines and starts a tutorial sequence. Skips if already completed.
        /// </summary>
        /// <param name="tutorialKey">Unique key for persistence (e.g., "main_tutorial").</param>
        /// <param name="steps">The tutorial steps to display.</param>
        /// <param name="forceShow">If true, shows even if previously completed.</param>
        public void StartTutorial(string tutorialKey, TutorialStep[] steps, bool forceShow = false)
        {
            _tutorialKey = tutorialKey;

            // Check if already completed
            if (!forceShow && PlayerPrefs.GetInt($"tutorial_done_{tutorialKey}", 0) == 1)
            {
                Debug.Log($"[TutorialOverlay] Tutorial '{tutorialKey}' already completed. Skipping.");
                return;
            }

            _steps.Clear();
            _steps.AddRange(steps);
            _currentStepIndex = 0;
            _isActive = true;

            ShowStepAsync(_currentStepIndex).Forget();
        }

        /// <summary>
        /// Starts the built-in main application tutorial.
        /// Covers: navigation, hand tracking, grab/snap, AI tutor.
        /// </summary>
        public void StartMainTutorial()
        {
            var steps = new TutorialStep[]
            {
                new TutorialStep
                {
                    Title = "Chào mừng!",
                    Description = "Đây là ứng dụng huấn luyện lắp ráp AR bằng cử chỉ tay.\n" +
                                  "Hãy cùng tìm hiểu cách sử dụng nhé!",
                    ArrowDirection = ArrowDirection.None
                },
                new TutorialStep
                {
                    Title = "Chọn bài học",
                    Description = "Từ menu chính, chọn một bài học để bắt đầu thực hành.\n" +
                                  "Mỗi bài gồm nhiều bước lắp ráp theo thứ tự.",
                    ArrowDirection = ArrowDirection.Left
                },
                new TutorialStep
                {
                    Title = "Điều khiển bằng tay",
                    Description = "Di chuyển tay trước webcam để điều khiển con trỏ.\n" +
                                  "Con trỏ sẽ theo dõi vị trí tay của bạn.",
                    ArrowDirection = ArrowDirection.None
                },
                new TutorialStep
                {
                    Title = "Cử chỉ Pinch — Gắp",
                    Description = "Chụm ngón cái và ngón trỏ lại để GẮP một linh kiện.\n" +
                                  "Di chuyển tay để kéo linh kiện đến vị trí mong muốn.",
                    ArrowDirection = ArrowDirection.None
                },
                new TutorialStep
                {
                    Title = "Đặt linh kiện",
                    Description = "Mở ngón tay (thả pinch) để ĐẶT linh kiện.\n" +
                                  "Nếu đặt đúng vị trí, linh kiện sẽ tự động gắn vào.",
                    ArrowDirection = ArrowDirection.Down
                },
                new TutorialStep
                {
                    Title = "Hỏi AI Trợ lý",
                    Description = "Nếu cần hướng dẫn, nhấn giữ nút mic hoặc ra cử chỉ Fist\n" +
                                  "rồi nói câu hỏi bằng tiếng Việt. AI sẽ trả lời và highlight gợi ý.",
                    ArrowDirection = ArrowDirection.Right
                },
                new TutorialStep
                {
                    Title = "Hoàn thành!",
                    Description = "Bạn đã sẵn sàng! Điểm số sẽ được tính dựa trên\n" +
                                  "số lần sai và thời gian hoàn thành.",
                    ArrowDirection = ArrowDirection.None
                }
            };

            StartTutorial("main_tutorial", steps);
        }

        // ────────────────────── Navigation ──────────────────────

        /// <summary>
        /// Advances to the next tutorial step, or finishes if on the last step.
        /// </summary>
        public void NextStep()
        {
            _currentStepIndex++;

            if (_currentStepIndex >= _steps.Count)
            {
                CompleteTutorial();
            }
            else
            {
                ShowStepAsync(_currentStepIndex).Forget();
            }
        }

        /// <summary>
        /// Skips all remaining steps and marks the tutorial as completed.
        /// </summary>
        public void SkipAll()
        {
            CompleteTutorial();
        }

        // ────────────────────── Internal ──────────────────────

        private async UniTaskVoid ShowStepAsync(int index)
        {
            if (index < 0 || index >= _steps.Count) return;

            TutorialStep step = _steps[index];

            // Fade out current content
            if (_panelRoot.activeSelf && _canvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < _fadeDuration * 0.5f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / (_fadeDuration * 0.5f));
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }

            // Update content
            if (_stepTitle != null) _stepTitle.text = step.Title;
            if (_stepDescription != null) _stepDescription.text = step.Description;
            if (_stepCounter != null) _stepCounter.text = $"{index + 1}/{_steps.Count}";

            // Update Next button text
            if (_nextButtonText != null)
            {
                _nextButtonText.text = index == _steps.Count - 1 ? "Hoàn tất" : "Tiếp";
            }

            // Position arrow
            if (_arrowIndicator != null)
            {
                bool showArrow = step.ArrowDirection != ArrowDirection.None;
                _arrowIndicator.gameObject.SetActive(showArrow);

                if (showArrow)
                {
                    PositionArrow(step.ArrowDirection);
                }
            }

            // Show and fade in
            if (_panelRoot != null) _panelRoot.SetActive(true);

            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = true;
                float elapsed = 0f;
                while (elapsed < _fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _canvasGroup.alpha = Mathf.Clamp01(elapsed / _fadeDuration);
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
                _canvasGroup.alpha = 1f;
            }
        }

        private void CompleteTutorial()
        {
            _isActive = false;

            if (!string.IsNullOrEmpty(_tutorialKey))
            {
                PlayerPrefs.SetInt($"tutorial_done_{_tutorialKey}", 1);
                PlayerPrefs.Save();
            }

            HidePanelAsync().Forget();
            Debug.Log($"[TutorialOverlay] Tutorial '{_tutorialKey}' completed.");
        }

        private async UniTaskVoid HidePanelAsync()
        {
            if (_canvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < _fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / _fadeDuration);
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }

            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private void PositionArrow(ArrowDirection direction)
        {
            if (_arrowIndicator == null) return;

            // Rotate arrow to point in the specified direction
            float rotation = direction switch
            {
                ArrowDirection.Up => 0f,
                ArrowDirection.Down => 180f,
                ArrowDirection.Left => 90f,
                ArrowDirection.Right => -90f,
                _ => 0f
            };

            _arrowIndicator.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        // ────────────────────── Fallback UI ──────────────────────

        private void CreateFallbackUI()
        {
            _panelRoot = new GameObject("TutorialOverlay_Fallback");
            _panelRoot.transform.SetParent(transform, false);

            RectTransform rootRT = _panelRoot.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;

            // Dimmed background
            Image bg = _panelRoot.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);

            _canvasGroup = _panelRoot.AddComponent<CanvasGroup>();

            // Content card
            GameObject card = new GameObject("Card");
            card.transform.SetParent(_panelRoot.transform, false);
            RectTransform cardRT = card.AddComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.15f, 0.25f);
            cardRT.anchorMax = new Vector2(0.85f, 0.75f);
            cardRT.offsetMin = Vector2.zero;
            cardRT.offsetMax = Vector2.zero;

            Image cardBG = card.AddComponent<Image>();
            cardBG.color = new Color(0.05f, 0.05f, 0.1f, 0.95f);

            // Step counter
            GameObject counterGO = new GameObject("Counter");
            counterGO.transform.SetParent(card.transform, false);
            _stepCounter = counterGO.AddComponent<TextMeshProUGUI>();
            _stepCounter.fontSize = 16;
            _stepCounter.alignment = TextAlignmentOptions.TopRight;
            _stepCounter.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            RectTransform counterRT = counterGO.GetComponent<RectTransform>();
            counterRT.anchorMin = new Vector2(0.7f, 0.85f);
            counterRT.anchorMax = new Vector2(0.95f, 0.98f);
            counterRT.offsetMin = Vector2.zero;
            counterRT.offsetMax = Vector2.zero;

            // Title
            GameObject titleGO = new GameObject("Title");
            titleGO.transform.SetParent(card.transform, false);
            _stepTitle = titleGO.AddComponent<TextMeshProUGUI>();
            _stepTitle.fontSize = 36;
            _stepTitle.alignment = TextAlignmentOptions.Center;
            _stepTitle.color = new Color(0f, 1f, 1f, 1f); // Cyan
            _stepTitle.fontStyle = FontStyles.Bold;
            RectTransform titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.05f, 0.65f);
            titleRT.anchorMax = new Vector2(0.95f, 0.9f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            // Description
            GameObject descGO = new GameObject("Description");
            descGO.transform.SetParent(card.transform, false);
            _stepDescription = descGO.AddComponent<TextMeshProUGUI>();
            _stepDescription.fontSize = 20;
            _stepDescription.alignment = TextAlignmentOptions.Center;
            _stepDescription.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            _stepDescription.enableWordWrapping = true;
            RectTransform descRT = descGO.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0.08f, 0.3f);
            descRT.anchorMax = new Vector2(0.92f, 0.65f);
            descRT.offsetMin = Vector2.zero;
            descRT.offsetMax = Vector2.zero;

            // Arrow indicator
            GameObject arrowGO = new GameObject("Arrow");
            arrowGO.transform.SetParent(card.transform, false);
            TextMeshProUGUI arrowText = arrowGO.AddComponent<TextMeshProUGUI>();
            arrowText.text = "▲";
            arrowText.fontSize = 40;
            arrowText.alignment = TextAlignmentOptions.Center;
            arrowText.color = new Color(0f, 1f, 1f, 0.7f);
            _arrowIndicator = arrowGO.GetComponent<RectTransform>();
            _arrowIndicator.anchorMin = new Vector2(0.45f, 0.88f);
            _arrowIndicator.anchorMax = new Vector2(0.55f, 0.98f);
            _arrowIndicator.offsetMin = Vector2.zero;
            _arrowIndicator.offsetMax = Vector2.zero;
            arrowGO.SetActive(false);

            // Next button
            _nextButton = CreateTutorialButton(card.transform, "Tiếp",
                new Vector2(0.55f, 0.06f), new Vector2(0.9f, 0.22f),
                new Color(0f, 0.8f, 0.8f, 0.8f));
            _nextButtonText = _nextButton.GetComponentInChildren<TextMeshProUGUI>();

            // Skip button
            _skipButton = CreateTutorialButton(card.transform, "Bỏ qua",
                new Vector2(0.1f, 0.06f), new Vector2(0.45f, 0.22f),
                new Color(0.3f, 0.3f, 0.3f, 0.6f));

            _panelRoot.SetActive(false);
        }

        private Button CreateTutorialButton(Transform parent, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
        {
            GameObject btnGO = new GameObject($"Btn_{label}");
            btnGO.transform.SetParent(parent, false);

            RectTransform btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = anchorMin;
            btnRT.anchorMax = anchorMax;
            btnRT.offsetMin = Vector2.zero;
            btnRT.offsetMax = Vector2.zero;

            Image btnImg = btnGO.AddComponent<Image>();
            btnImg.color = bgColor;
            btnGO.AddComponent<Button>();

            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(btnGO.transform, false);
            TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 20;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            return btnGO.GetComponent<Button>();
        }
    }

    // ────────────────────── Data Structures ──────────────────────

    /// <summary>
    /// Defines a single step in a tutorial sequence.
    /// </summary>
    [System.Serializable]
    public struct TutorialStep
    {
        /// <summary>Title of the tutorial step.</summary>
        public string Title;

        /// <summary>Description/instruction text.</summary>
        public string Description;

        /// <summary>Direction the arrow should point.</summary>
        public ArrowDirection ArrowDirection;

        /// <summary>Optional: RectTransform of the UI element to highlight.</summary>
        public RectTransform HighlightTarget;
    }

    /// <summary>
    /// Direction for the tutorial arrow indicator.
    /// </summary>
    public enum ArrowDirection
    {
        None,
        Up,
        Down,
        Left,
        Right
    }
}
