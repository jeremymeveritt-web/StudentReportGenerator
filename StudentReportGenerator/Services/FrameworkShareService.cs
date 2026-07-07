using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Local-first take on a "centrally approved library": a Head of Department exports the
    /// approved tone frameworks and curriculum topics to a plain JSON file once, and every teacher
    /// in the department imports the same file, so a subject or year group writes in a consistent
    /// voice without needing any shared server infrastructure.
    /// </summary>
    public static class FrameworkShareService
    {
        /// <summary>The on-disk shape of an exported library file.</summary>
        public class SharedLibrary
        {
            public List<ReportFramework> Frameworks { get; set; } = new();
            public List<string> CurriculumTopics { get; set; } = new();
        }

        /// <summary>Writes the given frameworks and topics to <paramref name="filePath"/> as
        /// human-readable JSON, ready to be shared (email, shared drive) with colleagues.</summary>
        public static void Export(string filePath, List<ReportFramework> frameworks, List<string> topics)
        {
            var library = new SharedLibrary { Frameworks = frameworks, CurriculumTopics = topics };
            File.WriteAllText(filePath, JsonSerializer.Serialize(library, new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <summary>
        /// Merges a shared library file into the caller's existing lists, matching on name
        /// case-insensitively, rather than replacing them outright — so a teacher's own personal
        /// frameworks and topics always survive importing a colleague's shared library.
        /// </summary>
        /// <returns>How many new frameworks/topics were actually added (existing names are skipped).</returns>
        public static (int FrameworksAdded, int TopicsAdded) Import(string filePath, List<ReportFramework> frameworks, List<string> topics)
        {
            var library = JsonSerializer.Deserialize<SharedLibrary>(File.ReadAllText(filePath));
            if (library == null) return (0, 0);

            int frameworksAdded = 0, topicsAdded = 0;

            foreach (var incoming in library.Frameworks)
            {
                if (string.IsNullOrWhiteSpace(incoming.Name)) continue;
                if (!frameworks.Exists(f => string.Equals(f.Name, incoming.Name, System.StringComparison.OrdinalIgnoreCase)))
                {
                    frameworks.Add(incoming);
                    frameworksAdded++;
                }
            }

            foreach (var topic in library.CurriculumTopics)
            {
                if (string.IsNullOrWhiteSpace(topic)) continue;
                if (!topics.Exists(t => string.Equals(t, topic, System.StringComparison.OrdinalIgnoreCase)))
                {
                    topics.Add(topic);
                    topicsAdded++;
                }
            }

            return (frameworksAdded, topicsAdded);
        }
    }
}
