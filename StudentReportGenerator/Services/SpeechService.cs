using System;
using System.Speech.Recognition;
using System.Speech.Synthesis;

namespace StudentReportGenerator.Services
{
    // Windows-native dictation (speech-to-text into the observations box) and read-aloud
    // (proofreading by ear / screen-reader-style playback). Runs entirely on-device via
    // the built-in Windows speech engines — no audio ever leaves the machine.
    public class SpeechService : IDisposable
    {
        private SpeechRecognitionEngine? _recognizer;
        private readonly SpeechSynthesizer _synthesizer = new SpeechSynthesizer();

        public bool IsDictating { get; private set; }
        public event Action<string>? TextRecognized;

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

        public void StopDictation()
        {
            if (_recognizer != null)
            {
                try { _recognizer.RecognizeAsyncCancel(); } catch { }
                _recognizer.Dispose();
                _recognizer = null;
            }
            IsDictating = false;
        }

        public void ReadAloud(string text)
        {
            StopReading();
            if (!string.IsNullOrWhiteSpace(text)) _synthesizer.SpeakAsync(text);
        }

        public void StopReading() => _synthesizer.SpeakAsyncCancelAll();

        public void Dispose()
        {
            StopDictation();
            _synthesizer.Dispose();
        }
    }
}
