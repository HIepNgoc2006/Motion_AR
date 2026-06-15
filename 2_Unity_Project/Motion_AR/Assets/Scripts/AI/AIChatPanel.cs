using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using MotionAR.Core;
using MotionAR.Interaction;

namespace MotionAR.AI
{
    /// <summary>
    /// AI Chat Panel that displays the conversation between the user and the
    /// Gemini AI tutor. Handles the full pipeline: voice/text input → context build →
    /// Gemini API call → display response → highlight relevant objects.
    /// <para>
    /// Design: Glassmorphism panel sliding from the right side, with chat bubbles
    /// for user questions and AI answers.
    /// </para>
    /// </summary>
    public class AIChatPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _chatPanel;
        [SerializeField] private Transform _chatContentParent;
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private TMP_InputField _textInput;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private CanvasGroup _panelCanvasGroup;

        [Header("Chat Bubble Prefabs")]
        [SerializeField]
        [Tooltip("Prefab for user message bubbles (aligned right).")]
        private GameObject _userBubblePrefab;

        [SerializeField]
        [Tooltip("Prefab for AI response bubbles (aligned left).")]
        private GameObject _aiBubblePrefab;

        [Header("Integration")]
        [SerializeField] private VoiceInputManager _voiceInputManager;
        [SerializeField] private PinchGrabber _pinchGrabber;
        [SerializeField] private HighlightResponder _highlightResponder;

        [Header("Settings")]
        [SerializeField] private int _maxChatHistory = 20;

        /// <summary>Fired when AI provides a response. Carries the response text.</summary>
        public event Action<string> OnAIResponse;

        private readonly List<ChatMessage> _chatHistory = new List<ChatMessage>();
        private CancellationTokenSource _queryCts;
        private bool _isProcessing;

        private struct ChatMessage
        {
            public bool IsUser;
            public string Text;
            public float Timestamp;
        }

        private void OnEnable()
        {
            if (_voiceInputManager != null)
            {
                _voiceInputManager.OnRecordingStarted += HandleRecordingStarted;
                _voiceInputManager.OnRecordingComplete += HandleRecordingComplete;
            }
        }

        private void OnDisable()
        {
            if (_voiceInputManager != null)
            {
                _voiceInputManager.OnRecordingStarted -= HandleRecordingStarted;
                _voiceInputManager.OnRecordingComplete -= HandleRecordingComplete;
            }

            CancelQuery();
        }

        /// <summary>
        /// Submits a text question to the AI tutor.
        /// </summary>
        /// <param name="question">The user's question text.</param>
        public void SubmitQuestion(string question)
        {
            if (string.IsNullOrWhiteSpace(question) || _isProcessing) return;

            CancelQuery();
            _queryCts = new CancellationTokenSource();
            ProcessQuestionAsync(question, _queryCts.Token).Forget();
        }

        /// <summary>
        /// Called when the user presses Enter in the text input field.
        /// </summary>
        public void OnTextInputSubmit()
        {
            if (_textInput == null) return;
            string text = _textInput.text.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                SubmitQuestion(text);
                _textInput.text = string.Empty;
            }
        }

        /// <summary>
        /// Shows recording indicator when voice input starts.
        /// </summary>
        private void HandleRecordingStarted()
        {
            SetStatus("🎤 Đang ghi âm...");
        }

        /// <summary>
        /// Processes recorded audio for speech-to-text.
        /// For now, shows a placeholder message since STT requires external API.
        /// </summary>
        private void HandleRecordingComplete(AudioClip clip, float duration)
        {
            if (duration < 0.5f)
            {
                SetStatus("⚠ Ghi âm quá ngắn. Hãy thử lại.");
                return;
            }

            // TODO: Integrate with Google STT or Gemini multimodal audio API.
            // For now, show instruction to use text input.
            SetStatus("✅ Ghi âm xong. Chức năng STT đang phát triển — hãy dùng text input.");

            // When STT is implemented, the flow will be:
            // AudioClip → WAV bytes → STT API → text → SubmitQuestion(text)
            Debug.Log($"[AIChatPanel] Audio recorded: {duration:F1}s, {clip.samples} samples. STT pending.");
        }

        /// <summary>
        /// Full pipeline: add user bubble → build context → call Gemini → add AI bubble → highlight.
        /// </summary>
        private async UniTaskVoid ProcessQuestionAsync(string question, CancellationToken ct)
        {
            _isProcessing = true;

            // 1. Add user message bubble.
            AddChatBubble(question, true);
            SetStatus("🤔 Đang suy nghĩ...");

            try
            {
                // 2. Build context from current session state.
                string contextJson = BuildCurrentContext();

                // 3. Call Gemini API.
                if (GeminiApiConnector.Instance == null)
                {
                    AddChatBubble("⚠ AI chưa được khởi tạo. Kiểm tra GeminiApiConnector.", false);
                    return;
                }

                string response = await GeminiApiConnector.Instance.AskGeminiAsync(
                    question, contextJson, ct);

                ct.ThrowIfCancellationRequested();

                // 4. Add AI response bubble.
                AddChatBubble(response, false);
                SetStatus("");

                // 5. Fire event for highlight responder.
                OnAIResponse?.Invoke(response);

                // 6. Notify highlight system.
                if (_highlightResponder != null)
                {
                    _highlightResponder.ProcessAIResponse(response);
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("❌ Đã hủy yêu cầu.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIChatPanel] Error: {ex.Message}");
                AddChatBubble("⚠ Đã xảy ra lỗi. Vui lòng thử lại.", false);
                SetStatus("");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// Builds the context JSON from current SessionManager state,
        /// including real-time snap zone statuses and held part information.
        /// </summary>
        private string BuildCurrentContext()
        {
            if (SessionManager.Instance == null || !SessionManager.Instance.IsLessonActive)
            {
                return string.Empty;
            }

            var session = SessionManager.Instance;
            var lesson = session.CurrentLesson;
            var step = session.GetCurrentStep();

            // Build snap zones status from the live registry.
            var snapZones = new List<ContextBuilder.SnapZoneStatus>();
            foreach (MotionAR.Interaction.SnapZone zone in MotionAR.Interaction.SnapZone.AllZones)
            {
                if (zone == null) continue;
                snapZones.Add(new ContextBuilder.SnapZoneStatus
                {
                    id = zone.SnapZoneID,
                    occupied = zone.IsOccupied,
                    partId = zone.OccupiedPartID ?? string.Empty
                });
            }

            // Get held part ID from PinchGrabber.
            string heldPartId = string.Empty;
            if (_pinchGrabber != null && _pinchGrabber.IsHolding)
            {
                heldPartId = _pinchGrabber.GrabbedPartID;
            }

            return ContextBuilder.BuildContextJson(
                lessonId: lesson?.lessonID ?? "",
                currentStep: session.CurrentStepIndex + 1,
                totalSteps: lesson?.TotalSteps ?? 0,
                currentInstruction: step?.instructionText ?? "",
                heldPartId: heldPartId,
                mistakeCount: session.MistakeCount,
                snapZones: snapZones
            );
        }

        /// <summary>
        /// Adds a chat bubble to the panel.
        /// </summary>
        private void AddChatBubble(string text, bool isUser)
        {
            _chatHistory.Add(new ChatMessage
            {
                IsUser = isUser,
                Text = text,
                Timestamp = Time.time
            });

            // Trim history if too long.
            while (_chatHistory.Count > _maxChatHistory)
            {
                _chatHistory.RemoveAt(0);
                if (_chatContentParent != null && _chatContentParent.childCount > 0)
                {
                    Destroy(_chatContentParent.GetChild(0).gameObject);
                }
            }

            // Create UI element.
            if (_chatContentParent != null)
            {
                GameObject prefab = isUser ? _userBubblePrefab : _aiBubblePrefab;

                if (prefab != null)
                {
                    GameObject bubble = Instantiate(prefab, _chatContentParent);
                    TextMeshProUGUI bubbleText = bubble.GetComponentInChildren<TextMeshProUGUI>();
                    if (bubbleText != null)
                    {
                        bubbleText.text = text;
                    }
                }
                else
                {
                    // Fallback: create simple text element.
                    GameObject bubbleGO = new GameObject(isUser ? "UserMsg" : "AIMsg");
                    bubbleGO.transform.SetParent(_chatContentParent, false);
                    var rt = bubbleGO.AddComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(0, 40);
                    var tmp = bubbleGO.AddComponent<TextMeshProUGUI>();
                    tmp.text = (isUser ? "🧑 " : "🤖 ") + text;
                    tmp.fontSize = 14;
                    tmp.color = isUser ? new Color(0.8f, 0.9f, 1f) : new Color(0f, 1f, 1f);
                    tmp.enableWordWrapping = true;

                    // Auto-size height.
                    var fitter = bubbleGO.AddComponent<ContentSizeFitter>();
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }

                // Scroll to bottom.
                ScrollToBottom();
            }
        }

        /// <summary>
        /// Sets the status text (e.g., "Đang suy nghĩ...", "Đang ghi âm...").
        /// </summary>
        private void SetStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.text = status;
            }
        }

        /// <summary>
        /// Scrolls the chat to the bottom after a new message is added.
        /// </summary>
        private void ScrollToBottom()
        {
            if (_scrollRect == null) return;

            // Delay one frame to let layout rebuild.
            ScrollToBottomDelayed().Forget();
        }

        private async UniTaskVoid ScrollToBottomDelayed()
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            if (_scrollRect != null)
            {
                _scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        /// <summary>
        /// Toggles the chat panel visibility.
        /// </summary>
        public void TogglePanel()
        {
            if (_chatPanel != null)
            {
                _chatPanel.SetActive(!_chatPanel.activeSelf);
            }
        }

        /// <summary>
        /// Clears all chat history and UI elements.
        /// </summary>
        public void ClearChat()
        {
            _chatHistory.Clear();
            if (_chatContentParent != null)
            {
                for (int i = _chatContentParent.childCount - 1; i >= 0; i--)
                {
                    Destroy(_chatContentParent.GetChild(i).gameObject);
                }
            }
        }

        private void CancelQuery()
        {
            if (_queryCts != null)
            {
                _queryCts.Cancel();
                _queryCts.Dispose();
                _queryCts = null;
            }
        }
    }
}
