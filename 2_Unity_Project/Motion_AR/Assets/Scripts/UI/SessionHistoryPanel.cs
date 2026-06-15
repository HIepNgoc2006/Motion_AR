using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using MotionAR.Database;

namespace MotionAR.UI
{
    /// <summary>
    /// Displays the user's training session history in a scrollable list.
    /// Fetches data from <see cref="SQLiteConnector.GetSessionHistoryAsync"/>.
    /// <para>
    /// Each row shows: Lesson ID, Score, Grade, Duration, Mistakes, Date.
    /// </para>
    /// </summary>
    public class SessionHistoryPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        [Tooltip("Root panel GameObject.")]
        private GameObject _historyPanel;

        [SerializeField]
        [Tooltip("Content parent inside a ScrollRect for session rows.")]
        private Transform _contentParent;

        [SerializeField]
        [Tooltip("Prefab for a single session row. Must have TextMeshProUGUI children.")]
        private GameObject _sessionRowPrefab;

        [SerializeField]
        [Tooltip("Text shown when no sessions are found.")]
        private TextMeshProUGUI _emptyText;

        [SerializeField]
        [Tooltip("Title text for the panel.")]
        private TextMeshProUGUI _titleText;

        [SerializeField]
        [Tooltip("Button to close/hide the panel.")]
        private Button _closeButton;

        [SerializeField]
        [Tooltip("Button to refresh the history.")]
        private Button _refreshButton;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Maximum number of sessions to display.")]
        private int _maxDisplayCount = 50;

        /// <summary>
        /// The user ID currently being viewed.
        /// </summary>
        private string _currentUserId;

        private void Start()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Hide);
            }

            if (_refreshButton != null)
            {
                _refreshButton.onClick.AddListener(() => RefreshAsync().Forget());
            }
        }

        private void OnDestroy()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
        }

        /// <summary>
        /// Shows the history panel and loads data for the specified user.
        /// </summary>
        /// <param name="userId">The user ID to fetch history for.</param>
        public void Show(string userId)
        {
            _currentUserId = userId;
            if (_historyPanel != null) _historyPanel.SetActive(true);
            RefreshAsync().Forget();
        }

        /// <summary>
        /// Hides the history panel.
        /// </summary>
        public void Hide()
        {
            if (_historyPanel != null) _historyPanel.SetActive(false);
        }

        /// <summary>
        /// Refreshes the session list from the database.
        /// </summary>
        private async UniTaskVoid RefreshAsync()
        {
            if (string.IsNullOrEmpty(_currentUserId)) return;

            ClearRows();
            SetEmptyText("⏳ Đang tải...");

            try
            {
                List<TrainingSessionRecord> sessions =
                    await SQLiteConnector.Instance.GetSessionHistoryAsync(
                        _currentUserId, destroyCancellationToken);

                if (sessions == null || sessions.Count == 0)
                {
                    SetEmptyText("📭 Chưa có lịch sử phiên học nào.");
                    return;
                }

                SetEmptyText("");

                int displayCount = Mathf.Min(sessions.Count, _maxDisplayCount);
                for (int i = 0; i < displayCount; i++)
                {
                    CreateSessionRow(sessions[i], i + 1);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogError($"[SessionHistory] Failed to load: {ex.Message}");
                SetEmptyText("❌ Lỗi tải dữ liệu.");
            }
        }

        /// <summary>
        /// Creates a single session row in the list.
        /// </summary>
        private void CreateSessionRow(TrainingSessionRecord session, int rank)
        {
            if (_contentParent == null) return;

            string grade = GetGrade(session.Score);
            string duration = FormatDuration(session.TotalTimeSeconds);
            string date = session.CompletedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

            if (_sessionRowPrefab != null)
            {
                GameObject row = Instantiate(_sessionRowPrefab, _contentParent);
                TextMeshProUGUI[] texts = row.GetComponentsInChildren<TextMeshProUGUI>();

                // Expected order: Rank, Lesson, Score, Grade, Duration, Mistakes, Date
                if (texts.Length >= 7)
                {
                    texts[0].text = rank.ToString();
                    texts[1].text = session.LessonId;
                    texts[2].text = $"{session.Score:F0}";
                    texts[3].text = grade;
                    texts[4].text = duration;
                    texts[5].text = session.MistakeCount.ToString();
                    texts[6].text = date;
                }
                else if (texts.Length >= 1)
                {
                    // Fallback: single text component.
                    texts[0].text =
                        $"#{rank}  {session.LessonId}  |  {grade} ({session.Score:F0})  " +
                        $"|  {duration}  |  {session.MistakeCount} lỗi  |  {date}";
                }
            }
            else
            {
                // No prefab — create simple text row.
                CreateFallbackRow(
                    $"#{rank}  {session.LessonId}  |  {grade} ({session.Score:F0})  " +
                    $"|  {duration}  |  {session.MistakeCount} lỗi  |  {date}",
                    session.Score
                );
            }
        }

        /// <summary>
        /// Creates a simple TextMeshProUGUI row when no prefab is assigned.
        /// </summary>
        private void CreateFallbackRow(string text, float score)
        {
            GameObject rowGO = new GameObject("SessionRow");
            rowGO.transform.SetParent(_contentParent, false);

            var rt = rowGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 36);

            var tmp = rowGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 14;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            // Color based on score.
            if (score >= 90f)
                tmp.color = new Color(0.22f, 1f, 0.08f); // Neon green
            else if (score >= 70f)
                tmp.color = new Color(0f, 1f, 1f); // Cyan
            else if (score >= 50f)
                tmp.color = new Color(1f, 0.84f, 0f); // Gold
            else
                tmp.color = new Color(1f, 0.3f, 0.3f); // Red-ish

            var fitter = rowGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>
        /// Returns a letter grade based on score.
        /// </summary>
        private static string GetGrade(float score)
        {
            if (score >= 95f) return "S";
            if (score >= 85f) return "A";
            if (score >= 70f) return "B";
            if (score >= 50f) return "C";
            return "F";
        }

        /// <summary>
        /// Formats seconds into a mm:ss string.
        /// </summary>
        private static string FormatDuration(float totalSeconds)
        {
            int minutes = Mathf.FloorToInt(totalSeconds / 60f);
            int seconds = Mathf.FloorToInt(totalSeconds % 60f);
            return $"{minutes:D2}:{seconds:D2}";
        }

        private void ClearRows()
        {
            if (_contentParent == null) return;
            for (int i = _contentParent.childCount - 1; i >= 0; i--)
            {
                Destroy(_contentParent.GetChild(i).gameObject);
            }
        }

        private void SetEmptyText(string text)
        {
            if (_emptyText != null)
            {
                _emptyText.text = text;
                _emptyText.gameObject.SetActive(!string.IsNullOrEmpty(text));
            }
        }
    }
}
