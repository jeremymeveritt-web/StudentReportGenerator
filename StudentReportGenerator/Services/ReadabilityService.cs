using System;
using System.Linq;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Flesch reading-ease scoring so a teacher can see at a glance whether a generated report is
    /// written at a level the parents it's going to can actually follow. Pure text analysis — no
    /// AI call involved, so it's instant and free to recompute on every keystroke.
    /// </summary>
    public static class ReadabilityService
    {
        /// <summary>
        /// Standard Flesch Reading Ease formula: higher scores (up to ~100) mean easier text,
        /// lower/negative scores mean denser text. Returns 0 for empty input.
        /// </summary>
        public static double FleschReadingEase(string text)
        {
            var (sentences, words, syllables) = Count(text);
            if (sentences == 0 || words == 0) return 0;
            return 206.835 - 1.015 * ((double)words / sentences) - 84.6 * ((double)syllables / words);
        }

        /// <summary>Returns a short human-readable summary like "Reading level: fairly readable
        /// (Flesch 62)" for display next to the report preview, or an empty string for empty input.</summary>
        public static string DescribeReadingLevel(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            double score = FleschReadingEase(text);
            string band = score switch
            {
                >= 70 => "easy to read",
                >= 50 => "fairly readable",
                >= 30 => "quite dense",
                _ => "very dense",
            };
            return $"Reading level: {band} (Flesch {Math.Round(score)})";
        }

        /// <summary>Rough sentence/word/syllable counts good enough for a Flesch estimate — not a
        /// full NLP tokenizer, just simple punctuation/whitespace splitting.</summary>
        private static (int Sentences, int Words, int Syllables) Count(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return (0, 0, 0);

            int sentences = text.Count(c => c is '.' or '!' or '?');
            if (sentences == 0) sentences = 1;

            var words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(w => new string(w.Where(char.IsLetter).ToArray()))
                            .Where(w => w.Length > 0)
                            .ToList();

            int syllables = words.Sum(CountSyllables);
            return (sentences, words.Count, syllables);
        }

        /// <summary>Approximates syllable count by counting vowel-group transitions, with a common
        /// heuristic adjustment for silent trailing "e" (e.g. "like" ≈ 1 syllable, not 2).</summary>
        private static int CountSyllables(string word)
        {
            word = word.ToLowerInvariant();
            const string vowels = "aeiouy";
            int count = 0;
            bool previousWasVowel = false;

            foreach (char c in word)
            {
                bool isVowel = vowels.Contains(c);
                if (isVowel && !previousWasVowel) count++;
                previousWasVowel = isVowel;
            }

            if (word.EndsWith("e") && count > 1) count--;
            return Math.Max(1, count);
        }
    }
}
