using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MotionAR.AI
{
    /// <summary>
    /// Static utility class responsible for loading and caching Gemini API
    /// configuration. Loads the API key from environment variable first,
    /// then falls back to a config.json file in StreamingAssets.
    /// </summary>
    public static class ConfigLoader
    {
        private const string EnvVarName = "GEMINI_API_KEY";
        private const string ConfigFileName = "config.json";
        private const string DefaultModel = "gemini-2.0-flash";
        private const int DefaultTimeoutSeconds = 30;

        private static bool _isLoaded;

        /// <summary>
        /// The Gemini API key used for authentication.
        /// </summary>
        public static string GeminiApiKey { get; private set; }

        /// <summary>
        /// The Gemini model identifier to use (e.g. "gemini-2.0-flash").
        /// </summary>
        public static string GeminiModel { get; private set; }

        /// <summary>
        /// Timeout in seconds for Gemini API requests.
        /// </summary>
        public static int GeminiTimeoutSeconds { get; private set; }

        /// <summary>
        /// Loads configuration from environment variable and/or config.json.
        /// Safe to call multiple times; subsequent calls are no-ops.
        /// Priority: environment variable > config.json > defaults.
        /// </summary>
        public static void LoadConfig()
        {
            if (_isLoaded)
            {
                return;
            }

            // Defaults
            GeminiModel = DefaultModel;
            GeminiTimeoutSeconds = DefaultTimeoutSeconds;
            GeminiApiKey = null;

            // 1. Try environment variable for the API key
            string envKey = Environment.GetEnvironmentVariable(EnvVarName);
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                GeminiApiKey = envKey.Trim();
            }

            // 2. Try config.json in StreamingAssets for all settings
            LoadFromConfigFile();

            // 3. Final validation
            if (string.IsNullOrEmpty(GeminiApiKey))
            {
                Debug.LogError(
                    "[ConfigLoader] Gemini API key not found!\n" +
                    "Set up your key using one of the following methods:\n" +
                    "  1. Set environment variable: GEMINI_API_KEY=<your-key>\n" +
                    $"  2. Create '{ConfigFileName}' in StreamingAssets with:\n" +
                    "     {\"geminiApiKey\": \"<your-key>\"}\n" +
                    "The AI assistant will not function without a valid API key."
                );
            }

            _isLoaded = true;
        }

        /// <summary>
        /// Attempts to read and parse the config.json file from StreamingAssets.
        /// Overwrites model/timeout if present. Only overwrites API key if
        /// it was not already set via environment variable.
        /// </summary>
        private static void LoadFromConfigFile()
        {
            string configPath = Path.Combine(Application.streamingAssetsPath, ConfigFileName);

            if (!File.Exists(configPath))
            {
                Debug.Log($"[ConfigLoader] Config file not found at: {configPath}. Using defaults.");
                return;
            }

            string json;
            try
            {
                json = File.ReadAllText(configPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConfigLoader] Failed to read config file: {ex.Message}");
                return;
            }

            JObject config;
            try
            {
                config = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConfigLoader] Malformed JSON in config file: {ex.Message}");
                return;
            }

            // API key from file (lower priority than env var)
            if (string.IsNullOrEmpty(GeminiApiKey))
            {
                string fileKey = config.Value<string>("geminiApiKey");
                if (!string.IsNullOrWhiteSpace(fileKey))
                {
                    GeminiApiKey = fileKey.Trim();
                }
            }

            // Model
            string model = config.Value<string>("geminiModel");
            if (!string.IsNullOrWhiteSpace(model))
            {
                GeminiModel = model.Trim();
            }

            // Timeout
            int? timeout = config.Value<int?>("geminiTimeoutSeconds");
            if (timeout.HasValue && timeout.Value > 0)
            {
                GeminiTimeoutSeconds = timeout.Value;
            }
        }
    }
}
