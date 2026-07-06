using System;
using System.Linq;

namespace StudentReportGenerator.Services
{
    // Flesch reading-ease scoring so a teacher can see at a glance whether a report is
    // written at a level the parents it's going to can actually follow.
    public static class ReadabilityService
    {
        public static double FleschReadingEase(string text)
        {
            var (sentences, words, syllables) = Count(text);
            if (sentences == 0 || words == 0) return 0;
            return 206.835 - 1.015 * ((double)words / sentences) - 84.6 * ((double)syllables / words);
        }

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
