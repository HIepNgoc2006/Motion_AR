using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace MotionAR.AI
{
    /// <summary>
    /// Singleton MonoBehaviour that manages asynchronous communication with
    /// the Google Gemini API. Persists across scene loads and guarantees
    /// that no exceptions are thrown to callers — all errors are returned
    /// as user-friendly Vietnamese strings.
    /// </summary>
    public class GeminiApiConnector : MonoBehaviour
    {
        private const string ApiBaseUrl =
            "https://generativelanguage.googleapis.com/v1beta/models/";

        private const string SystemPrompt =
            "Bạn là trợ giảng kỹ thuật trong hệ thống đào tạo AR. " +
            "Nhiệm vụ của bạn là hướng dẫn người học lắp ráp linh kiện " +
            "dựa trên ngữ cảnh 3D hiện tại. Trả lời ngắn gọn, rõ ràng bằng tiếng Việt.";

        private const string TimeoutMessage =
            "⚠ Kết nối AI thất bại. Vui lòng thử lại sau.";

        private const string GenericErrorMessage =
            "⚠ Đã xảy ra lỗi khi kết nối AI. Vui lòng thử lại sau.";

        /// <summary>
        /// Global singleton instance. Automatically created if accessed before
        /// being placed in a scene.
        /// </summary>
        public static GeminiApiConnector Instance { get; private set; }

        /// <summary>
        /// Unity Awake — establishes the singleton and loads configuration.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ConfigLoader.LoadConfig();
        }

        /// <summary>
        /// Sends a user question along with the current 3D session context
        /// to the Gemini API and returns the model's text response.
        /// This method never throws; all failure modes return a user-friendly
        /// error string instead.
        /// </summary>
        /// <param name="userQuestion">The question posed by the trainee.</param>
        /// <param name="contextJson">
        /// JSON string built by <see cref="ContextBuilder.BuildContextJson"/>
        /// describing the current session state.
        /// </param>
        /// <param name="ct">Cancellation token for cooperative cancellation.</param>
        /// <returns>
        /// The Gemini model's text answer, or a Vietnamese error message on failure.
        /// </returns>
        public async UniTask<string> AskGeminiAsync(
            string userQuestion,
            string contextJson,
            CancellationToken ct)
        {
            try
            {
                // 1. Validate API key
                string apiKey = ConfigLoader.GeminiApiKey;
                if (string.IsNullOrEmpty(apiKey))
                {
                    Debug.LogError("[GeminiApiConnector] API key is not configured.");
                    return "⚠ Chưa cấu hình API key. Vui lòng liên hệ quản trị viên.";
                }

                // 2. Build request URL
                string model = ConfigLoader.GeminiModel;
                string url = $"{ApiBaseUrl}{model}:generateContent?key={apiKey}";

                // 3. Build request body
                string requestBody = BuildRequestBody(userQuestion, contextJson);

                // 4. Send request with timeout
                int timeoutSeconds = ConfigLoader.GeminiTimeoutSeconds;
                string responseText = await SendRequestAsync(url, requestBody, timeoutSeconds, ct);

                // 5. Parse response
                return ParseResponse(responseText);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[GeminiApiConnector] Request was cancelled.");
                return TimeoutMessage;
            }
            catch (TimeoutException)
            {
                Debug.LogWarning("[GeminiApiConnector] Request timed out.");
                return TimeoutMessage;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GeminiApiConnector] Unexpected error: {ex.Message}\n{ex.StackTrace}");
                return GenericErrorMessage;
            }
        }

        /// <summary>
        /// Constructs the full Gemini API request body JSON including the
        /// system instruction and user message with embedded context.
        /// </summary>
        private static string BuildRequestBody(string userQuestion, string contextJson)
        {
            // User content combines the context and question
            string userContent = string.IsNullOrEmpty(contextJson)
                ? userQuestion
                : $"[Ngữ cảnh 3D hiện tại]\n{contextJson}\n\n[Câu hỏi]\n{userQuestion}";

            var body = new JObject
            {
                ["system_instruction"] = new JObject
                {
                    ["parts"] = new JArray
                    {
                        new JObject { ["text"] = SystemPrompt }
                    }
                },
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JArray
                        {
                            new JObject { ["text"] = userContent }
                        }
                    }
                }
            };

            return body.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>
        /// Sends an HTTP POST to the Gemini API endpoint using UnityWebRequest
        /// wrapped in UniTask with a configurable timeout.
        /// </summary>
        private static async UniTask<string> SendRequestAsync(
            string url,
            string jsonBody,
            int timeoutSeconds,
            CancellationToken ct)
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = timeoutSeconds;

            using var timeoutCts = new CancellationTokenSource();
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                throw new TimeoutException($"Gemini API request timed out after {timeoutSeconds}s.");
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorBody = request.downloadHandler?.text ?? "No response body";
                Debug.LogError(
                    $"[GeminiApiConnector] HTTP {request.responseCode}: {request.error}\n" +
                    $"Response: {errorBody}"
                );
                throw new Exception($"HTTP {request.responseCode}: {request.error}");
            }

            return request.downloadHandler.text;
        }

        /// <summary>
        /// Extracts the generated text from the Gemini API JSON response.
        /// Expected path: candidates[0].content.parts[0].text
        /// </summary>
        private static string ParseResponse(string responseJson)
        {
            JObject response;
            try
            {
                response = JObject.Parse(responseJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GeminiApiConnector] Failed to parse response JSON: {ex.Message}");
                return GenericErrorMessage;
            }

            JToken textToken = response.SelectToken("candidates[0].content.parts[0].text");
            if (textToken == null)
            {
                // Check for blocked content or other issues
                JToken blockReason = response.SelectToken("candidates[0].finishReason");
                if (blockReason != null && blockReason.ToString() != "STOP")
                {
                    Debug.LogWarning(
                        $"[GeminiApiConnector] Response blocked. Reason: {blockReason}"
                    );
                    return "⚠ Nội dung bị chặn bởi bộ lọc an toàn. Vui lòng thử câu hỏi khác.";
                }

                // Check for prompt feedback errors
                JToken promptFeedback = response.SelectToken("promptFeedback.blockReason");
                if (promptFeedback != null)
                {
                    Debug.LogWarning(
                        $"[GeminiApiConnector] Prompt blocked. Reason: {promptFeedback}"
                    );
                    return "⚠ Câu hỏi bị chặn bởi bộ lọc an toàn. Vui lòng thử câu hỏi khác.";
                }

                Debug.LogError(
                    $"[GeminiApiConnector] Unexpected response structure:\n{responseJson}"
                );
                return GenericErrorMessage;
            }

            string text = textToken.ToString().Trim();
            return string.IsNullOrEmpty(text) ? GenericErrorMessage : text;
        }
    }
}
