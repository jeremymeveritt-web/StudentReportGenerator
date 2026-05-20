using System.Collections.Generic;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    public static class WordExportService
    {
        public static void ExportSingle(string filePath, string studentName, string reportText)
        {
            using (WordprocessingDocument wordDoc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document();
                Body body = mainPart.Document.AppendChild(new Body());

                AddParagraph(body, $"Official Report: {studentName}", true);
                AddParagraph(body, reportText, false);
            }
        }

        public static void ExportBatch(string filePath, List<SessionRecord> records)
        {
            using (WordprocessingDocument wordDoc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document();
                Body body = mainPart.Document.AppendChild(new Body());

                for (int i = 0; i < records.Count; i++)
                {
                    AddParagraph(body, $"Official Report: {records[i].StudentName}", true);
                    AddParagraph(body, records[i].GeneratedReport, false);

                    // Insert a Page Break after every student (except the very last one)
                    if (i < records.Count - 1)
                    {
                        body.AppendChild(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
                    }
                }
            }
        }

        private static void AddParagraph(Body body, string text, bool isHeader)
        {
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
            foreach (var line in lines)
            {
                Paragraph para = body.AppendChild(new Paragraph());
                Run run = para.AppendChild(new Run());
                run.AppendChild(new Text(line));

                if (isHeader)
                {
                    // Applies styling (Bold, Size 32 half-points = 16pt font)
                    RunProperties runProps = new RunProperties(new Bold(), new FontSize() { Val = "32" });
                    run.PrependChild(runProps);
                }
            }
        }
    }
}