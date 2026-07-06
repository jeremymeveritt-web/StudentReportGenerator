using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    // Local-first take on a "centrally approved library": a Head of Department exports the
    // approved tone frameworks and curriculum topics once, and every teacher imports the
    // same file, so a subject or year group writes in a consistent voice.
    public static class FrameworkShareService
    {
        public class SharedLibrary
        {
            public List<ReportFramework> Frameworks { get; set; } = new();
            public List<string> CurriculumTopics { get; set; } = new();
        }

        public static void Export(string filePath, List<ReportFramework> frameworks, List<string> topics)
        {
            var library = new SharedLibrary { Frameworks = frameworks, CurriculumTopics = topics };
            File.WriteAllText(filePath, JsonSerializer.Serialize(library, new JsonSerializerOptions { WriteIndented = true }));
        }

        // Merges into the existing lists (case-insensitive on names) rather than replacing,
        // so a teacher's personal additions survive an import.
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
