using System;
using System.Speech.Recognition;
using System.Speech.Synthesis;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Windows-native dictation (speech-to-text into the Teacher Observations box) and read-aloud
    /// (proofreading by ear before sending a report). Both features run entirely on-device via the
    /// built-in Windows speech engines (<see cref="System.Speech"/>) — no audio is ever sent to a
    /// server or third party. Owned by <c>MainViewModel</c> for the lifetime of the app; call
    /// <see cref="Dispose"/> on shutdown to release the microphone and audio device cleanly.
    /// </summary>
    public class SpeechService : IDisposable
    {
        private SpeechRecognitionEngine? _recognizer;
        private readonly SpeechSynthesizer _synthesizer = new SpeechSynthesizer();

        public bool IsDictating { get; private set; }

        /// <summary>Raised on the recognizer's background thread each time a phrase is recognized;
        /// subscribers must marshal back to the UI thread themselves before touching bound properties.</summary>
        public event Action<string>? TextRecognized;

        /// <summary>Starts listening on the default microphone using free-form dictation (no fixed
        /// grammar/vocabulary). Safe to call repeatedly — a no-op if already dictating.</summary>
        /// <returns><c>false</c> if no microphone or speech recognition engine is available on this
        /// machine, so the caller can show a friendly message instead of crashing.</returns>
        public bool StartDictation()
        {
            if (IsDictating) return true;
            try
            {
                _recognizer = new SpeechRecognitionEngine();
                _recognizer.LoadGrammar(new DictationGrammar());
                _recognizer.SetInputToDefaultAudioDevice();
                _recognizer.SpeechRecognized += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Result?.Text))
                        TextRecognized?.Invoke(e.Result.Text);
                };
                _recognizer.RecognizeAsync(RecognizeMode.Multiple);
                IsDictating = true;
                return true;
            }
            catch
            {
                _recognizer?.Dispose();
                _recognizer = null;
                return false; // no microphone / speech engine unavailable
            }
        }

        /// <summary>Stops listening and releases the recognition engine and microphone handle. Safe
        /// to call even if dictation was never started.</summary>
        public void StopDictation()
        {
            if (_recognizer != null)
            {
                try { _recognizer.RecognizeAsyncCancel(); } catch { /* engine already stopped/disposed */ }
                _recognizer.Dispose();
                _recognizer = null;
            }
            IsDictating = false;
        }

        /// <summary>Reads the given text aloud asynchronously, cancelling any speech already in progress first.</summary>
        public void ReadAloud(string text)
        {
            StopReading();
            if (!string.IsNullOrWhiteSpace(text)) _synthesizer.SpeakAsync(text);
        }

        /// <summary>Immediately silences any in-progress or queued read-aloud speech.</summary>
        public void StopReading() => _synthesizer.SpeakAsyncCancelAll();

        public void Dispose()
        {
            StopDictation();
            _synthesizer.Dispose();
        }
    }
}
