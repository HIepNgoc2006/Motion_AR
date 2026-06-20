using UnityEngine;

namespace MotionAR.UI
{
    /// <summary>
    /// Generates procedural audio clips at runtime for UI feedback sounds.
    /// Eliminates the need for external audio asset files — all sounds are
    /// synthesized mathematically using sine waves, noise, and envelopes.
    /// <para>
    /// Provides static methods to generate: beep, click, snap, error buzz,
    /// success chime, hover tick, slide whoosh, pop, and notification sounds.
    /// All generated clips are cached to avoid redundant computation.
    /// </para>
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SAMPLE_RATE = 44100;

        // Thread-safe random (UnityEngine.Random is main-thread only).
        private static readonly System.Random _rng = new System.Random(42);

        // ────────────────────── Cached Clips ──────────────────────

        private static AudioClip _beepClip;
        private static AudioClip _clickClip;
        private static AudioClip _snapClip;
        private static AudioClip _errorClip;
        private static AudioClip _successClip;
        private static AudioClip _hoverTickClip;
        private static AudioClip _slideClip;
        private static AudioClip _popClip;
        private static AudioClip _notificationClip;
        private static AudioClip _confirmClip;

        // ────────────────────── Public Accessors ──────────────────────

        /// <summary>Short UI beep (440Hz, 80ms). Used for button activation.</summary>
        public static AudioClip Beep
        {
            get
            {
                if (_beepClip == null) _beepClip = GenerateBeep();
                return _beepClip;
            }
        }

        /// <summary>Crisp click sound (30ms). Used for UI interactions.</summary>
        public static AudioClip Click
        {
            get
            {
                if (_clickClip == null) _clickClip = GenerateClick();
                return _clickClip;
            }
        }

        /// <summary>Satisfying snap sound (150ms). Used for successful part placement.</summary>
        public static AudioClip Snap
        {
            get
            {
                if (_snapClip == null) _snapClip = GenerateSnap();
                return _snapClip;
            }
        }

        /// <summary>Error buzz (200ms). Used for incorrect actions.</summary>
        public static AudioClip Error
        {
            get
            {
                if (_errorClip == null) _errorClip = GenerateError();
                return _errorClip;
            }
        }

        /// <summary>Success chime — ascending two-tone (300ms). Used for correct placement.</summary>
        public static AudioClip Success
        {
            get
            {
                if (_successClip == null) _successClip = GenerateSuccess();
                return _successClip;
            }
        }

        /// <summary>Light hover tick (20ms). Used for hover feedback.</summary>
        public static AudioClip HoverTick
        {
            get
            {
                if (_hoverTickClip == null) _hoverTickClip = GenerateHoverTick();
                return _hoverTickClip;
            }
        }

        /// <summary>Slide whoosh (200ms). Used for panel slide animations.</summary>
        public static AudioClip Slide
        {
            get
            {
                if (_slideClip == null) _slideClip = GenerateSlide();
                return _slideClip;
            }
        }

        /// <summary>Pop sound (50ms). Used for score pop-in animations.</summary>
        public static AudioClip Pop
        {
            get
            {
                if (_popClip == null) _popClip = GeneratePop();
                return _popClip;
            }
        }

        /// <summary>Notification chime (400ms). Used for AI responses and alerts.</summary>
        public static AudioClip Notification
        {
            get
            {
                if (_notificationClip == null) _notificationClip = GenerateNotification();
                return _notificationClip;
            }
        }

        /// <summary>Confirm sound — descending tone (150ms). Used for confirmations.</summary>
        public static AudioClip Confirm
        {
            get
            {
                if (_confirmClip == null) _confirmClip = GenerateConfirm();
                return _confirmClip;
            }
        }

        // ────────────────────── Generators ──────────────────────

        /// <summary>
        /// Generates a clean 440Hz sine beep with exponential decay.
        /// </summary>
        private static AudioClip GenerateBeep()
        {
            float duration = 0.08f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float envelope = Mathf.Exp(-t * 30f); // Fast decay
                data[i] = Mathf.Sin(2f * Mathf.PI * 440f * t) * envelope * 0.5f;
            }

            return CreateClip("ProceduralBeep", data);
        }

        /// <summary>
        /// Generates a crisp click using a short noise burst with very fast decay.
        /// </summary>
        private static AudioClip GenerateClick()
        {
            float duration = 0.03f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float envelope = Mathf.Exp(-t * 150f);
                // Mix of high-freq sine and noise for click character
                float sine = Mathf.Sin(2f * Mathf.PI * 2000f * t);
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                data[i] = (sine * 0.7f + noise * 0.3f) * envelope * 0.4f;
            }

            return CreateClip("ProceduralClick", data);
        }

        /// <summary>
        /// Generates a satisfying snap sound: a tone drop with overtones.
        /// </summary>
        private static AudioClip GenerateSnap()
        {
            float duration = 0.15f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float envelope = Mathf.Exp(-t * 20f);
                // Frequency sweep from 800Hz down to 200Hz
                float freq = Mathf.Lerp(800f, 200f, t / duration);
                float fundamental = Mathf.Sin(2f * Mathf.PI * freq * t);
                float harmonic = Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.3f;
                float click = (float)(_rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 200f) * 0.5f;
                data[i] = (fundamental + harmonic + click) * envelope * 0.4f;
            }

            return CreateClip("ProceduralSnap", data);
        }

        /// <summary>
        /// Generates an error buzz: dissonant low frequency with harmonics.
        /// </summary>
        private static AudioClip GenerateError()
        {
            float duration = 0.2f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                // Fade in slightly then decay
                float fadeIn = Mathf.Clamp01(t * 50f);
                float fadeOut = Mathf.Exp(-Mathf.Max(0f, t - 0.05f) * 15f);
                float envelope = fadeIn * fadeOut;
                // Dissonant tones — 150Hz and 157Hz create a beating effect
                float tone1 = Mathf.Sin(2f * Mathf.PI * 150f * t);
                float tone2 = Mathf.Sin(2f * Mathf.PI * 157f * t);
                float tone3 = Mathf.Sin(2f * Mathf.PI * 300f * t) * 0.3f;
                data[i] = (tone1 + tone2 + tone3) * envelope * 0.25f;
            }

            return CreateClip("ProceduralError", data);
        }

        /// <summary>
        /// Generates a success chime: ascending two-tone (C5 → E5).
        /// </summary>
        private static AudioClip GenerateSuccess()
        {
            float duration = 0.3f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float envelope = Mathf.Exp(-t * 8f);
                // First note: C5 (523Hz) for first half
                // Second note: E5 (659Hz) for second half
                float crossfade = Mathf.Clamp01((t - 0.12f) * 15f);
                float freq = Mathf.Lerp(523f, 659f, crossfade);
                float fundamental = Mathf.Sin(2f * Mathf.PI * freq * t);
                float harmonic = Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.2f;
                data[i] = (fundamental + harmonic) * envelope * 0.4f;
            }

            return CreateClip("ProceduralSuccess", data);
        }

        /// <summary>
        /// Generates a subtle hover tick — very short high-frequency ping.
        /// </summary>
        private static AudioClip GenerateHoverTick()
        {
            float duration = 0.02f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float envelope = Mathf.Exp(-t * 200f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 3000f * t) * envelope * 0.2f;
            }

            return CreateClip("ProceduralHoverTick", data);
        }

        /// <summary>
        /// Generates a slide whoosh using filtered noise with frequency sweep.
        /// </summary>
        private static AudioClip GenerateSlide()
        {
            float duration = 0.2f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];

            float prevSample = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float envelope = Mathf.Sin(Mathf.PI * t / duration); // Bell curve
                // Simple low-pass filtered noise
                float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                float filterCoeff = Mathf.Lerp(0.1f, 0.5f, t / duration);
                float filtered = prevSample * (1f - filterCoeff) + noise * filterCoeff;
                prevSample = filtered;
                data[i] = filtered * envelope * 0.3f;
            }

            return CreateClip("ProceduralSlide", data);
        }

        /// <summary>
        /// Generates a pop sound — sine burst with fast attack and quick decay.
        /// </summary>
        private static AudioClip GeneratePop()
        {
            float duration = 0.05f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float envelope = Mathf.Exp(-t * 80f);
                // Descending pitch for pop character
                float freq = Mathf.Lerp(1200f, 400f, t / duration);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.5f;
            }

            return CreateClip("ProceduralPop", data);
        }

        /// <summary>
        /// Generates a pleasant notification chime: three ascending notes.
        /// </summary>
        private static AudioClip GenerateNotification()
        {
            float duration = 0.4f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];

            // Three notes: G5, B5, D6
            float[] noteFreqs = { 784f, 988f, 1175f };
            float noteDuration = duration / 3f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                int noteIndex = Mathf.Min((int)(t / noteDuration), 2);
                float noteT = t - noteIndex * noteDuration;

                float envelope = Mathf.Exp(-noteT * 12f);
                float fundamental = Mathf.Sin(2f * Mathf.PI * noteFreqs[noteIndex] * t);
                float harmonic = Mathf.Sin(2f * Mathf.PI * noteFreqs[noteIndex] * 2f * t) * 0.15f;
                data[i] = (fundamental + harmonic) * envelope * 0.35f;
            }

            return CreateClip("ProceduralNotification", data);
        }

        /// <summary>
        /// Generates a confirmation sound: descending two-tone (E5 → C5).
        /// </summary>
        private static AudioClip GenerateConfirm()
        {
            float duration = 0.15f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float envelope = Mathf.Exp(-t * 12f);
                float crossfade = Mathf.Clamp01((t - 0.06f) * 20f);
                float freq = Mathf.Lerp(659f, 523f, crossfade);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.4f;
            }

            return CreateClip("ProceduralConfirm", data);
        }

        // ────────────────────── Utility ──────────────────────

        /// <summary>
        /// Creates an AudioClip from raw PCM sample data.
        /// </summary>
        private static AudioClip CreateClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Plays a procedural audio clip at a world position using PlayClipAtPoint.
        /// </summary>
        /// <param name="clip">The audio clip to play.</param>
        /// <param name="position">World position for spatial audio.</param>
        /// <param name="volume">Playback volume (0-1).</param>
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1.0f)
        {
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, position, volume);
            }
        }

        /// <summary>
        /// Plays a procedural audio clip using a specific AudioSource (2D).
        /// </summary>
        /// <param name="source">The AudioSource to use.</param>
        /// <param name="clip">The audio clip to play.</param>
        public static void PlayOneShot(AudioSource source, AudioClip clip)
        {
            if (source != null && clip != null)
            {
                source.PlayOneShot(clip);
            }
        }
    }
}
