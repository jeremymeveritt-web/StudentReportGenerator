using System;
using System.Collections.Generic;
using System.Linq;

namespace StudentReportGenerator.Services
{
    // Lightweight keyword screen over teacher-typed notes. If an observation looks like it
    // may be a safeguarding disclosure, the teacher is reminded to use the school's proper
    // referral route (e.g. CPOMS) — report-writing input is never the right channel for that.
    // Deliberately conservative: it nudges, it never blocks, and it never sends anything anywhere.
    public static class SafeguardingScanService
    {
        private static readonly string[] Keywords =
        {
            "safeguarding", "disclosed", "disclosure", "self-harm", "self harm", "suicide", "suicidal",
            "abuse", "abused", "neglect", "neglected", "bruise", "bruises", "bruising",
            "hurt at home", "unsafe at home", "scared of", "afraid of", "hit by", "hits me", "hits them",
            "grooming", "groomed", "ran away", "run away from home", "not eating", "starving",
            "touched inappropriately", "inappropriate touching", "cpoms", "child protection"
        };

        public static IReadOnlyList<string> Scan(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return Array.Empty<string>();
            return Keywords.Where(k => notes.Contains(k, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}
