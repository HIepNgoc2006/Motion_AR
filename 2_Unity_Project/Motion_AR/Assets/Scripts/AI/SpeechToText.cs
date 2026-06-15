using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace MotionAR.AI
{
    /// <summary>
    /// Speech-to-Text service using Gemini 2.0 Flash multimodal audio input.
    /// Converts Unity AudioClip to WAV bytes, base64-encodes it, and sends
    /// to the Gemini API with a transcription prompt.
    /// <para>
    /// Pipeline: AudioClip → WAV bytes → Base64 → Gemini multimodal → Text
    /// </para>
    /// </summary>
    public class SpeechToText : MonoBehaviour
    {
        private const string ApiBaseUrl =
            "https://generativelanguage.googleapis.com/v1beta/models/";

        private const string TranscriptionPrompt =
            "Hãy chuyển đoạn audio này thành văn bản tiếng Việt. " +
            "Chỉ trả lời nội dung được nói, không thêm giải thích. " +
            "Nếu không nghe rõ, trả lời: [không rõ].";

        /// <summary>Global singleton instance.</summary>
        public static SpeechToText Instance { get; private set; }

        /// <summary>Fired when transcription completes. Provides the text result.</summary>
        public event Action<string> OnTranscriptionComplete;

        /// <summary>Fired when transcription fails. Provides error message.</summary>
        public event Action<string> OnTranscriptionFailed;

        /// <summary>Whether a transcription request is in progress.</summary>
        public bool IsProcessing => _isProcessing;

        private bool _isProcessing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        /// <summary>
        /// Transcribes an AudioClip to text using Gemini multimodal API.
        /// </summary>
        /// <param name="clip">The audio clip to transcribe.</param>
        /// <param name="ct">Cancellation token for cooperative cancellation.</param>
        /// <returns>The transcribed text, or an error message on failure.</returns>
        public async UniTask<string> TranscribeAsync(AudioClip clip, CancellationToken ct)
        {
            if (clip == null)
            {
                string error = "AudioClip is null.";
                OnTranscriptionFailed?.Invoke(error);
                return string.Empty;
            }

            if (_isProcessing)
            {
                Debug.LogWarning("[SpeechToText] Already processing a transcription request.");
                return string.Empty;
            }

            _isProcessing = true;

            try
            {
                // 1. Validate API key
                ConfigLoader.LoadConfig();
                string apiKey = ConfigLoader.GeminiApiKey;
                if (string.IsNullOrEmpty(apiKey))
                {
                    string error = "API key chưa được cấu hình.";
                    OnTranscriptionFailed?.Invoke(error);
                    return string.Empty;
                }

                // 2. Convert AudioClip to WAV bytes
                byte[] wavBytes = AudioClipToWav(clip);
                if (wavBytes == null || wavBytes.Length == 0)
                {
                    string error = "Không thể chuyển đổi audio.";
                    OnTranscriptionFailed?.Invoke(error);
                    return string.Empty;
                }

                // 3. Base64 encode
                string audioBase64 = Convert.ToBase64String(wavBytes);
                Debug.Log($"[SpeechToText] Audio size: {wavBytes.Length} bytes, Base64: {audioBase64.Length} chars");

                // 4. Build Gemini multimodal request
                string model = ConfigLoader.GeminiModel;
                string url = $"{ApiBaseUrl}{model}:generateContent?key={apiKey}";
                string requestBody = BuildMultimodalRequest(audioBase64);

                // 5. Send request
                int timeout = ConfigLoader.GeminiTimeoutSeconds;
                string responseJson = await SendRequestAsync(url, requestBody, timeout, ct);

                // 6. Parse response
                string transcription = ParseTranscriptionResponse(responseJson);

                if (!string.IsNullOrEmpty(transcription))
                {
                    Debug.Log($"[SpeechToText] Transcription: \"{transcription}\"");
                    OnTranscriptionComplete?.Invoke(transcription);
                }
                else
                {
                    OnTranscriptionFailed?.Invoke("Không thể nhận dạng giọng nói.");
                }

                return transcription;
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[SpeechToText] Transcription cancelled.");
                OnTranscriptionFailed?.Invoke("Đã hủy.");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SpeechToText] Error: {ex.Message}");
                OnTranscriptionFailed?.Invoke($"Lỗi: {ex.Message}");
                return string.Empty;
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// Builds a Gemini multimodal request with inline audio data.
        /// </summary>
        private static string BuildMultimodalRequest(string audioBase64)
        {
            var body = new JObject
            {
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JArray
                        {
                            new JObject
                            {
                                ["inline_data"] = new JObject
                                {
                                    ["mime_type"] = "audio/wav",
                                    ["data"] = audioBase64
                                }
                            },
                            new JObject
                            {
                                ["text"] = TranscriptionPrompt
                            }
                        }
                    }
                }
            };

            return body.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>
        /// Sends an HTTP POST to the Gemini API.
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

            var timeoutCts = new CancellationTokenSource();
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                throw new TimeoutException($"STT request timed out after {timeoutSeconds}s.");
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorBody = request.downloadHandler?.text ?? "No response";
                Debug.LogError($"[SpeechToText] HTTP {request.responseCode}: {request.error}\n{errorBody}");
                throw new Exception($"HTTP {request.responseCode}: {request.error}");
            }

            return request.downloadHandler.text;
        }

        /// <summary>
        /// Parses the Gemini API response to extract the transcription text.
        /// </summary>
        private static string ParseTranscriptionResponse(string responseJson)
        {
            try
            {
                JObject response = JObject.Parse(responseJson);
                JToken textToken = response.SelectToken("candidates[0].content.parts[0].text");

                if (textToken != null)
                {
                    string text = textToken.ToString().Trim();
                    // Clean up any "[không rõ]" responses
                    if (text.Contains("[không rõ]") || text.Contains("[unclear]"))
                    {
                        return string.Empty;
                    }
                    return text;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SpeechToText] Parse error: {ex.Message}");
                return string.Empty;
            }
        }

        // ────────────────── WAV Conversion ──────────────────

        /// <summary>
        /// Converts a Unity AudioClip to WAV byte array (16-bit PCM).
        /// </summary>
        /// <param name="clip">The AudioClip to convert.</param>
        /// <returns>WAV file bytes, or null on failure.</returns>
        private static byte[] AudioClipToWav(AudioClip clip)
        {
            try
            {
                float[] samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);

                int sampleCount = samples.Length;
                int sampleRate = clip.frequency;
                int channels = clip.channels;
                int bitsPerSample = 16;
                int byteRate = sampleRate * channels * bitsPerSample / 8;
                int blockAlign = channels * bitsPerSample / 8;
                int dataSize = sampleCount * blockAlign;

                using var stream = new MemoryStream();
                using var writer = new BinaryWriter(stream);

                // RIFF header
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize); // File size - 8
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                // fmt sub-chunk
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);              // Sub-chunk size
                writer.Write((short)1);        // Audio format (PCM)
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write((short)blockAlign);
                writer.Write((short)bitsPerSample);

                // data sub-chunk
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);

                // Convert float samples [-1.0, 1.0] to 16-bit PCM
                for (int i = 0; i < sampleCount; i++)
                {
                    float sample = Mathf.Clamp(samples[i], -1f, 1f);
                    short pcmSample = (short)(sample * 32767f);
                    writer.Write(pcmSample);
                }

                writer.Flush();
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SpeechToText] WAV conversion error: {ex.Message}");
                return null;
            }
        }
    }
}
