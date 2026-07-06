using System.Collections.Generic;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using StudentReportGenerator.Models;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace StudentReportGenerator.Services
{
    public static class WordExportService
    {
        public static void ExportSingle(string filePath, string studentName, string reportText, SchoolBranding? branding = null)
        {
            using (WordprocessingDocument wordDoc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document();
                Body body = mainPart.Document.AppendChild(new Body());

                AddLetterhead(mainPart, body, branding);
                AddParagraph(body, $"Official Report: {studentName}", true, AccentHex(branding));
                AddParagraph(body, reportText, false, null);
            }
        }

        public static void ExportBatch(string filePath, List<SessionRecord> records, SchoolBranding? branding = null)
        {
            using (WordprocessingDocument wordDoc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document();
                Body body = mainPart.Document.AppendChild(new Body());

                for (int i = 0; i < records.Count; i++)
                {
                    AddLetterhead(mainPart, body, branding);
                    AddParagraph(body, $"Official Report: {records[i].StudentName}", true, AccentHex(branding));
                    AddParagraph(body, records[i].GeneratedReport, false, null);

                    // Insert a Page Break after every student (except the very last one)
                    if (i < records.Count - 1)
                    {
                        body.AppendChild(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
                    }
                }
            }
        }

        // School letterhead: logo (when uploaded), school name in the school's accent
        // colour, and a rule underneath, so exports carry the school's identity
        private static void AddLetterhead(MainDocumentPart mainPart, Body body, SchoolBranding? branding)
        {
            if (branding == null) return;

            if (!string.IsNullOrWhiteSpace(branding.LogoPath) && File.Exists(branding.LogoPath))
            {
                try { AddLogo(mainPart, body, branding.LogoPath); } catch { /* corrupt/locked image — skip the logo, keep the export */ }
            }

            if (!string.IsNullOrWhiteSpace(branding.SchoolName) && branding.SchoolName != "Enter School Name")
            {
                var para = body.AppendChild(new Paragraph(new ParagraphProperties(
                    new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 12, Color = AccentHex(branding) ?? "392A4C" }),
                    new SpacingBetweenLines { After = "240" })));
                var run = para.AppendChild(new Run(new Text(branding.SchoolName.ToUpperInvariant())));
                run.PrependChild(new RunProperties(
                    new Bold(),
                    new Color { Val = AccentHex(branding) ?? "392A4C" },
                    new FontSize { Val = "40" }));
            }
        }

        private static string? AccentHex(SchoolBranding? branding)
        {
            if (branding == null || string.IsNullOrWhiteSpace(branding.AccentColorHex)) return null;
            string hex = branding.AccentColorHex.TrimStart('#');
            if (hex.Length == 8) hex = hex.Substring(2); // drop WPF's alpha channel
            return hex.Length == 6 ? hex : null;
        }

        private static void AddLogo(MainDocumentPart mainPart, Body body, string logoPath)
        {
            string ext = Path.GetExtension(logoPath).ToLowerInvariant();
            var imagePart = mainPart.AddImagePart(ext == ".png" ? ImagePartType.Png : ImagePartType.Jpeg);
            using (var stream = File.OpenRead(logoPath)) imagePart.FeedData(stream);
            string relationshipId = mainPart.GetIdOfPart(imagePart);

            // Measure via WPF imaging (no System.Drawing dependency) and scale to 1.2cm tall
            var frame = System.Windows.Media.Imaging.BitmapFrame.Create(
                new System.Uri(logoPath), System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,
                System.Windows.Media.Imaging.BitmapCacheOption.None);
            const long targetHeightEmu = 432000; // 1.2 cm
            long widthEmu = (long)(targetHeightEmu * ((double)frame.PixelWidth / frame.PixelHeight));

            var drawing = new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = widthEmu, Cy = targetHeightEmu },
                    new DW.DocProperties { Id = 1U, Name = "School Logo" },
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = "logo" },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip { Embed = relationshipId },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0L, Y = 0L },
                                        new A.Extents { Cx = widthEmu, Cy = targetHeightEmu }),
                                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });

            body.AppendChild(new Paragraph(new Run(drawing)));
        }

        private static void AddParagraph(Body body, string text, bool isHeader, string? colorHex)
        {
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
            foreach (var line in lines)
            {
                Paragraph para = body.AppendChild(new Paragraph());

                ParagraphProperties paraProps = new ParagraphProperties(
                    new SpacingBetweenLines { After = "160", Line = "276", LineRule = LineSpacingRuleValues.Auto }
                );
                para.PrependChild(paraProps);

                Run run = para.AppendChild(new Run());
                run.AppendChild(new Text(line) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });

                if (isHeader)
                {
                    var runProps = new RunProperties(new Bold(), new FontSize { Val = "32" });
                    if (colorHex != null) runProps.AppendChild(new Color { Val = colorHex });
                    run.PrependChild(runProps);
                }
            }
        }
    }
}
