using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// A personal, growing snippet library of the phrases a teacher reuses across a class
    /// ("has settled well this term", "would benefit from checking work before submitting"),
    /// persisted as plain (non-encrypted) JSON — these are stock phrases the teacher chose to
    /// save, not sensitive student data, so they don't need the DPAPI treatment used elsewhere.
    /// </summary>
    public static class CommentBankService
    {
        private static readonly string FilePath = FileSandboxService.GetSafeFilePath("comment_bank.json");

        /// <summary>Seed phrases shown to every teacher on first use, before they've saved any of their own.</summary>
        private static readonly List<string> StarterPhrases = new()
        {
            "has settled well this term",
            "would benefit from checking work carefully before submitting",
            "consistently meets deadlines and comes to lessons well prepared",
            "is developing more confidence when contributing to class discussion",
            "should continue reading around the subject to consolidate understanding",
        };

        /// <summary>Loads the teacher's saved phrases, or the starter set if none have been saved yet
        /// or the file is missing/corrupt.</summary>
        public static List<string> LoadPhrases()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<string>(StarterPhrases);
                return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FilePath)) ?? new List<string>(StarterPhrases);
            }
            catch
            {
                return new List<string>(StarterPhrases);
            }
        }

        /// <summary>Persists the phrase list, trimming whitespace and removing case-insensitive
        /// duplicates before writing.</summary>
        public static void SavePhrases(IEnumerable<string> phrases)
        {
            var clean = phrases.Select(p => p.Trim())
                               .Where(p => p.Length > 0)
                               .Distinct(StringComparer.OrdinalIgnoreCase)
                               .ToList();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(clean, new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <summary>Filters the phrase list to those containing <paramref name="filter"/>
        /// (case-insensitive), or returns the full list unfiltered when no filter text is given.</summary>
        public static List<string> Suggest(List<string> phrases, string? filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return phrases;
            return phrases.Where(p => p.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}
