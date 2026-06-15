using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using MotionAR.Core;
using MotionAR.Data;

namespace MotionAR.UI
{
    /// <summary>
    /// Lesson selection menu that presents a list of available training lessons
    /// for the user to choose from. Supports both click and hover-to-select.
    /// <para>
    /// Lessons are configured as a serialized list of <see cref="LessonData"/>
    /// ScriptableObjects assigned in the Inspector.
    /// </para>
    /// </summary>
    public class LessonSelectPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        [Tooltip("Root panel GameObject.")]
        private GameObject _selectPanel;

        [SerializeField]
        [Tooltip("Content parent inside a ScrollRect for lesson cards.")]
        private Transform _contentParent;

        [SerializeField]
        [Tooltip("Prefab for a lesson card. Should contain TMP texts and a Button.")]
        private GameObject _lessonCardPrefab;

        [SerializeField]
        [Tooltip("Title text for the panel.")]
        private TextMeshProUGUI _titleText;

        [SerializeField]
        [Tooltip("Description text updated when hovering over a lesson.")]
        private TextMeshProUGUI _descriptionText;

        [SerializeField]
        [Tooltip("Button to view session history.")]
        private Button _historyButton;

        [SerializeField]
        [Tooltip("Reference to the SessionHistoryPanel for viewing history.")]
        private SessionHistoryPanel _historyPanel;

        [Header("Available Lessons")]
        [SerializeField]
        [Tooltip("List of lesson ScriptableObjects available for selection.")]
        private List<LessonData> _availableLessons = new List<LessonData>();

        /// <summary>
        /// Fired when a lesson is selected. Provides the chosen LessonData.
        /// </summary>
        public event Action<LessonData> OnLessonSelected;

        private readonly List<GameObject> _spawnedCards = new List<GameObject>();

        private void Start()
        {
            if (_historyButton != null)
            {
                _historyButton.onClick.AddListener(OnHistoryClicked);
            }

            BuildLessonList();
        }

        private void OnDestroy()
        {
            if (_historyButton != null)
            {
                _historyButton.onClick.RemoveListener(OnHistoryClicked);
            }
        }

        /// <summary>
        /// Shows the lesson selection panel.
        /// </summary>
        public void Show()
        {
            if (_selectPanel != null) _selectPanel.SetActive(true);
            BuildLessonList();
        }

        /// <summary>
        /// Hides the lesson selection panel.
        /// </summary>
        public void Hide()
        {
            if (_selectPanel != null) _selectPanel.SetActive(false);
        }

        /// <summary>
        /// Builds the UI cards for each available lesson.
        /// </summary>
        private void BuildLessonList()
        {
            ClearCards();

            if (_availableLessons == null || _availableLessons.Count == 0)
            {
                if (_descriptionText != null)
                {
                    _descriptionText.text = "⚠ Chưa có bài học nào. Tạo LessonData ScriptableObject và gán vào danh sách.";
                }
                return;
            }

            for (int i = 0; i < _availableLessons.Count; i++)
            {
                LessonData lesson = _availableLessons[i];
                if (lesson == null) continue;

                CreateLessonCard(lesson, i);
            }

            // Show description of first lesson by default.
            if (_availableLessons.Count > 0 && _descriptionText != null)
            {
                LessonData first = _availableLessons[0];
                _descriptionText.text = first.description ?? "";
            }
        }

        /// <summary>
        /// Creates a UI card for a single lesson.
        /// </summary>
        private void CreateLessonCard(LessonData lesson, int index)
        {
            if (_contentParent == null) return;

            if (_lessonCardPrefab != null)
            {
                GameObject card = Instantiate(_lessonCardPrefab, _contentParent);
                _spawnedCards.Add(card);

                // Set texts.
                TextMeshProUGUI[] texts = card.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 2)
                {
                    texts[0].text = lesson.lessonTitle;
                    texts[1].text = $"{lesson.TotalSteps} bước";
                }
                else if (texts.Length >= 1)
                {
                    texts[0].text = $"{lesson.lessonTitle} ({lesson.TotalSteps} bước)";
                }

                // Set button click.
                Button btn = card.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    LessonData capturedLesson = lesson;
                    btn.onClick.AddListener(() => SelectLesson(capturedLesson));
                }
            }
            else
            {
                // Fallback: create simple button.
                CreateFallbackCard(lesson);
            }
        }

        /// <summary>
        /// Creates a simple text+button card when no prefab is assigned.
        /// </summary>
        private void CreateFallbackCard(LessonData lesson)
        {
            GameObject cardGO = new GameObject($"LessonCard_{lesson.lessonID}");
            cardGO.transform.SetParent(_contentParent, false);
            _spawnedCards.Add(cardGO);

            var rt = cardGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 60);

            // Background image.
            var bg = cardGO.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.4f);

            // Layout.
            var layout = cardGO.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childAlignment = TextAnchor.MiddleLeft;

            // Title text.
            GameObject titleGO = new GameObject("Title");
            titleGO.transform.SetParent(cardGO.transform, false);
            var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
            titleTMP.text = $"📘 {lesson.lessonTitle}";
            titleTMP.fontSize = 16;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.color = new Color(0f, 1f, 1f); // Cyan

            // Subtitle text.
            GameObject subtitleGO = new GameObject("Subtitle");
            subtitleGO.transform.SetParent(cardGO.transform, false);
            var subtitleTMP = subtitleGO.AddComponent<TextMeshProUGUI>();
            subtitleTMP.text = $"{lesson.TotalSteps} bước  •  {lesson.description ?? ""}";
            subtitleTMP.fontSize = 12;
            subtitleTMP.color = new Color(0.7f, 0.7f, 0.7f);
            subtitleTMP.enableWordWrapping = true;

            // Content size fitter.
            var fitter = cardGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Button behavior.
            var button = cardGO.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0f, 1f, 1f, 0.15f);
            colors.pressedColor = new Color(0f, 1f, 1f, 0.3f);
            button.colors = colors;

            LessonData capturedLesson = lesson;
            button.onClick.AddListener(() => SelectLesson(capturedLesson));
        }

        /// <summary>
        /// Selects a lesson, starts it, and transitions to the training scene.
        /// </summary>
        private void SelectLesson(LessonData lesson)
        {
            if (lesson == null) return;

            Debug.Log($"[LessonSelect] Selected: {lesson.lessonTitle} ({lesson.lessonID})");

            // Start the lesson via SessionManager.
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.StartLesson(lesson);
            }

            // Fire event.
            OnLessonSelected?.Invoke(lesson);

            // Hide this panel.
            Hide();
        }

        /// <summary>
        /// Opens the session history panel.
        /// </summary>
        private void OnHistoryClicked()
        {
            if (_historyPanel != null && SessionManager.Instance != null)
            {
                string userId = SessionManager.Instance.CurrentUserId;
                if (!string.IsNullOrEmpty(userId))
                {
                    _historyPanel.Show(userId);
                }
                else
                {
                    Debug.LogWarning("[LessonSelect] Cannot show history — no user logged in.");
                }
            }
        }

        private void ClearCards()
        {
            foreach (GameObject card in _spawnedCards)
            {
                if (card != null) Destroy(card);
            }
            _spawnedCards.Clear();
        }
    }
}
