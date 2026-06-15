using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using MotionAR.Core;
using MotionAR.Database;

namespace MotionAR.UI
{
    /// <summary>
    /// Login screen that allows the user to enter their username before
    /// starting a training session. Creates new users automatically via
    /// <see cref="SQLiteConnector.GetOrCreateUserAsync"/>.
    /// <para>
    /// Shows on scene start. After successful login, hides itself and
    /// activates the lesson selection panel.
    /// </para>
    /// </summary>
    public class LoginScreen : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        [Tooltip("Root panel GameObject — toggled on/off for show/hide.")]
        private GameObject _loginPanel;

        [SerializeField]
        [Tooltip("Text input field for the username.")]
        private TMP_InputField _usernameInput;

        [SerializeField]
        [Tooltip("Text input field for the full name (optional).")]
        private TMP_InputField _fullNameInput;

        [SerializeField]
        [Tooltip("Login button — triggers authentication.")]
        private Button _loginButton;

        [SerializeField]
        [Tooltip("Status text for showing errors or loading state.")]
        private TextMeshProUGUI _statusText;

        [SerializeField]
        [Tooltip("Title text displayed at the top of the login screen.")]
        private TextMeshProUGUI _titleText;

        [Header("Navigation")]
        [SerializeField]
        [Tooltip("Panel to activate after successful login (e.g., LessonSelectPanel).")]
        private GameObject _nextPanel;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Show login screen automatically on Start.")]
        private bool _showOnStart = true;

        [SerializeField]
        [Tooltip("Minimum username length required.")]
        private int _minUsernameLength = 2;

        /// <summary>
        /// Fired when a user successfully logs in. Provides the UserRecord.
        /// </summary>
        public event Action<UserRecord> OnLoginSuccess;

        private bool _isProcessing;

        private void Start()
        {
            if (_loginButton != null)
            {
                _loginButton.onClick.AddListener(OnLoginClicked);
            }

            if (_usernameInput != null)
            {
                // Submit on Enter key.
                _usernameInput.onSubmit.AddListener(_ => OnLoginClicked());
            }

            if (_showOnStart)
            {
                Show();
            }

            SetStatus("");
        }

        private void OnDestroy()
        {
            if (_loginButton != null)
            {
                _loginButton.onClick.RemoveListener(OnLoginClicked);
            }
        }

        /// <summary>
        /// Shows the login screen.
        /// </summary>
        public void Show()
        {
            if (_loginPanel != null)
            {
                _loginPanel.SetActive(true);
            }

            if (_usernameInput != null)
            {
                _usernameInput.text = string.Empty;
                _usernameInput.ActivateInputField();
            }

            if (_fullNameInput != null)
            {
                _fullNameInput.text = string.Empty;
            }

            SetStatus("");
        }

        /// <summary>
        /// Hides the login screen.
        /// </summary>
        public void Hide()
        {
            if (_loginPanel != null)
            {
                _loginPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Called when the login button is clicked or Enter is pressed.
        /// </summary>
        private void OnLoginClicked()
        {
            if (_isProcessing) return;

            string username = _usernameInput != null ? _usernameInput.text.Trim() : string.Empty;
            string fullName = _fullNameInput != null ? _fullNameInput.text.Trim() : string.Empty;

            // Validation.
            if (string.IsNullOrEmpty(username) || username.Length < _minUsernameLength)
            {
                SetStatus($"⚠ Tên đăng nhập phải có ít nhất {_minUsernameLength} ký tự.");
                return;
            }

            // Use username as full name if full name is empty.
            if (string.IsNullOrEmpty(fullName))
            {
                fullName = username;
            }

            ProcessLoginAsync(username, fullName).Forget();
        }

        /// <summary>
        /// Performs the async login/registration flow.
        /// </summary>
        private async UniTaskVoid ProcessLoginAsync(string username, string fullName)
        {
            _isProcessing = true;
            SetStatus("⏳ Đang đăng nhập...");
            SetButtonInteractable(false);

            try
            {
                // Ensure database is ready.
                await SQLiteConnector.Instance.InitializeAsync(destroyCancellationToken);

                // Get or create user.
                UserRecord user = await SQLiteConnector.Instance.GetOrCreateUserAsync(
                    username, fullName, destroyCancellationToken);

                if (user == null)
                {
                    SetStatus("❌ Lỗi cơ sở dữ liệu. Vui lòng thử lại.");
                    return;
                }

                // Set session user.
                if (SessionManager.Instance != null)
                {
                    SessionManager.Instance.CurrentUserId = user.UserId;
                }

                Debug.Log($"[LoginScreen] Login success: {user.Username} ({user.UserId})");
                SetStatus($"✅ Chào mừng, {user.FullName}!");

                // Brief delay to show success message.
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f),
                    cancellationToken: destroyCancellationToken);

                // Fire event.
                OnLoginSuccess?.Invoke(user);

                // Transition to next screen.
                Hide();
                if (_nextPanel != null)
                {
                    _nextPanel.SetActive(true);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogError($"[LoginScreen] Login error: {ex.Message}");
                SetStatus("❌ Đã xảy ra lỗi. Vui lòng thử lại.");
            }
            finally
            {
                _isProcessing = false;
                SetButtonInteractable(true);
            }
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }
        }

        private void SetButtonInteractable(bool interactable)
        {
            if (_loginButton != null)
            {
                _loginButton.interactable = interactable;
            }
        }
    }
}
