using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TMPro;
using System;

namespace MotionAR.UI
{
    /// <summary>
    /// Reusable modal confirmation dialog with callback support.
    /// Used for destructive actions like "Exit training?", "Reset lesson?", etc.
    /// <para>
    /// Presents a message with two buttons (confirm and cancel) and fires
    /// a callback with the user's choice. Supports async/await via UniTask.
    /// Blocks raycasts to underlying UI while visible.
    /// </para>
    /// </summary>
    public class ConfirmDialog : MonoBehaviour
    {
        // ────────────────────── Singleton ──────────────────────

        /// <summary>Global singleton instance.</summary>
        public static ConfirmDialog Instance { get; private set; }

        // ────────────────────── References ──────────────────────

        [Header("Panel")]
        [SerializeField]
        [Tooltip("Root panel GameObject.")]
        private GameObject _panelRoot;

        [SerializeField]
        [Tooltip("CanvasGroup for blocking and fading.")]
        private CanvasGroup _canvasGroup;

        [Header("Content")]
        [SerializeField]
        [Tooltip("Title text.")]
        private TextMeshProUGUI _titleText;

        [SerializeField]
        [Tooltip("Message/body text.")]
        private TextMeshProUGUI _messageText;

        [Header("Buttons")]
        [SerializeField]
        [Tooltip("Confirm (positive) button.")]
        private Button _confirmButton;

        [SerializeField]
        [Tooltip("Cancel (negative) button.")]
        private Button _cancelButton;

        [SerializeField]
        [Tooltip("Confirm button label.")]
        private TextMeshProUGUI _confirmButtonText;

        [SerializeField]
        [Tooltip("Cancel button label.")]
        private TextMeshProUGUI _cancelButtonText;

        [Header("Animation")]
        [SerializeField]
        [Tooltip("Fade duration in seconds.")]
        private float _fadeDuration = 0.15f;

        // ────────────────────── State ──────────────────────

        private Action<bool> _currentCallback;
        private UniTaskCompletionSource<bool> _currentTcs;

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
            _confirmButton?.onClick.AddListener(OnConfirmClicked);
            _cancelButton?.onClick.AddListener(OnCancelClicked);
        }

        private void OnDisable()
        {
            _confirmButton?.onClick.RemoveListener(OnConfirmClicked);
            _cancelButton?.onClick.RemoveListener(OnCancelClicked);
        }

        // ────────────────────── Public API — Callback ──────────────────────

        /// <summary>
        /// Shows the confirm dialog with a callback.
        /// </summary>
        /// <param name="title">Dialog title.</param>
        /// <param name="message">Dialog message body.</param>
        /// <param name="confirmLabel">Text for the confirm button.</param>
        /// <param name="cancelLabel">Text for the cancel button.</param>
        /// <param name="callback">Called with true (confirm) or false (cancel).</param>
        public void Show(
            string title,
            string message,
            string confirmLabel = "Đồng ý",
            string cancelLabel = "Hủy",
            Action<bool> callback = null)
        {
            _currentCallback = callback;

            if (_titleText != null) _titleText.text = title;
            if (_messageText != null) _messageText.text = message;
            if (_confirmButtonText != null) _confirmButtonText.text = confirmLabel;
            if (_cancelButtonText != null) _cancelButtonText.text = cancelLabel;

            ShowPanelAsync().Forget();
        }

        // ────────────────────── Public API — Async ──────────────────────

        /// <summary>
        /// Shows the confirm dialog and awaits the user's response.
        /// Returns true if confirmed, false if cancelled.
        /// </summary>
        /// <param name="title">Dialog title.</param>
        /// <param name="message">Dialog message body.</param>
        /// <param name="confirmLabel">Text for the confirm button.</param>
        /// <param name="cancelLabel">Text for the cancel button.</param>
        /// <returns>True if the user confirmed, false otherwise.</returns>
        public async UniTask<bool> ShowAsync(
            string title,
            string message,
            string confirmLabel = "Đồng ý",
            string cancelLabel = "Hủy")
        {
            // Cancel any pending dialog.
            _currentTcs?.TrySetResult(false);

            _currentTcs = new UniTaskCompletionSource<bool>();

            if (_titleText != null) _titleText.text = title;
            if (_messageText != null) _messageText.text = message;
            if (_confirmButtonText != null) _confirmButtonText.text = confirmLabel;
            if (_cancelButtonText != null) _cancelButtonText.text = cancelLabel;

            ShowPanelAsync().Forget();

            return await _currentTcs.Task;
        }

        // ────────────────────── Button Handlers ──────────────────────

        private void OnConfirmClicked()
        {
            Resolve(true);
        }

        private void OnCancelClicked()
        {
            Resolve(false);
        }

        private void Resolve(bool result)
        {
            _currentCallback?.Invoke(result);
            _currentCallback = null;

            _currentTcs?.TrySetResult(result);
            _currentTcs = null;

            HidePanelAsync().Forget();
        }

        // ────────────────────── Animation ──────────────────────

        private async UniTaskVoid ShowPanelAsync()
        {
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

        // ────────────────────── Fallback UI ──────────────────────

        private void CreateFallbackUI()
        {
            _panelRoot = new GameObject("ConfirmDialog_Fallback");
            _panelRoot.transform.SetParent(transform, false);

            RectTransform rootRT = _panelRoot.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;

            // Dimmed background
            Image dimBG = _panelRoot.AddComponent<Image>();
            dimBG.color = new Color(0f, 0f, 0f, 0.6f);

            _canvasGroup = _panelRoot.AddComponent<CanvasGroup>();

            // Dialog card
            GameObject card = new GameObject("Card");
            card.transform.SetParent(_panelRoot.transform, false);
            RectTransform cardRT = card.AddComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.25f, 0.3f);
            cardRT.anchorMax = new Vector2(0.75f, 0.7f);
            cardRT.offsetMin = Vector2.zero;
            cardRT.offsetMax = Vector2.zero;

            Image cardBG = card.AddComponent<Image>();
            cardBG.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

            // Title
            GameObject titleGO = new GameObject("Title");
            titleGO.transform.SetParent(card.transform, false);
            _titleText = titleGO.AddComponent<TextMeshProUGUI>();
            _titleText.fontSize = 32;
            _titleText.alignment = TextAlignmentOptions.Center;
            _titleText.color = new Color(0f, 1f, 1f, 1f); // Cyan
            RectTransform titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.05f, 0.7f);
            titleRT.anchorMax = new Vector2(0.95f, 0.95f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            // Message
            GameObject msgGO = new GameObject("Message");
            msgGO.transform.SetParent(card.transform, false);
            _messageText = msgGO.AddComponent<TextMeshProUGUI>();
            _messageText.fontSize = 20;
            _messageText.alignment = TextAlignmentOptions.Center;
            _messageText.color = Color.white;
            RectTransform msgRT = msgGO.GetComponent<RectTransform>();
            msgRT.anchorMin = new Vector2(0.1f, 0.35f);
            msgRT.anchorMax = new Vector2(0.9f, 0.7f);
            msgRT.offsetMin = Vector2.zero;
            msgRT.offsetMax = Vector2.zero;

            // Confirm button
            _confirmButton = CreateDialogButton(card.transform, "Đồng ý",
                new Vector2(0.55f, 0.08f), new Vector2(0.9f, 0.28f),
                new Color(0f, 0.8f, 0.4f, 0.8f));
            _confirmButtonText = _confirmButton.GetComponentInChildren<TextMeshProUGUI>();

            // Cancel button
            _cancelButton = CreateDialogButton(card.transform, "Hủy",
                new Vector2(0.1f, 0.08f), new Vector2(0.45f, 0.28f),
                new Color(0.5f, 0.5f, 0.5f, 0.6f));
            _cancelButtonText = _cancelButton.GetComponentInChildren<TextMeshProUGUI>();

            _panelRoot.SetActive(false);
        }

        private Button CreateDialogButton(Transform parent, string label,
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

            Button btn = btnGO.AddComponent<Button>();

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

            return btn;
        }
    }
}
