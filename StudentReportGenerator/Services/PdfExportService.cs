using System;
using System.IO;
using System.Collections.Generic;
using PdfSharp.Pdf;
using PdfSharp.Drawing;

namespace StudentReportGenerator.Services
{
    public static class PdfExportService
    {
        public static void ExportSingle(string filePath, string studentName, string reportText)
        {
            PdfDocument document = new PdfDocument();
            document.Info.Title = $"Report for {studentName}";

            XFont titleFont = new XFont("Arial", 16, XFontStyleEx.Bold);
            XFont bodyFont = new XFont("Arial", 11, XFontStyleEx.Regular);

            double margin = 50;
            double leading = 4; // Extra space between lines
            double lineSpacing = bodyFont.Height + leading;

            // Add the initial page
            PdfPage page = document.AddPage();
            XGraphics gfx = XGraphics.FromPdfPage(page);

            double pageWidth = page.Width.Point;
            double pageHeight = page.Height.Point;
            double maxRenderWidth = pageWidth - (margin * 2);
            double bottomLimit = pageHeight - margin;

            // Render Title Header
            gfx.DrawString($"Official Academic Report: {studentName}", titleFont, XBrushes.Black, new XRect(margin, margin, maxRenderWidth, 30), XStringFormats.TopLeft);

            // Set up tracking position for the text rendering cursor
            double currentY = margin + 45;

            // Split report by manual paragraph line returns
            string[] paragraphs = reportText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (string para in paragraphs)
            {
                // Handle empty spaces cleanly as a line paragraph jump
                if (string.IsNullOrWhiteSpace(para))
                {
                    currentY += lineSpacing;
                    continue;
                }

                // Split paragraph into words to compute wrapping bounds manually
                string[] words = para.Split(' ');
                string currentLine = "";

                for (int i = 0; i < words.Length; i++)
                {
                    string testLine = string.IsNullOrEmpty(currentLine) ? words[i] : currentLine + " " + words[i];
                    XSize size = gfx.MeasureString(testLine, bodyFont);

                    if (size.Width > maxRenderWidth)
                    {
                        // Current line is full! Commit rendering layout
                        if (currentY + lineSpacing > bottomLimit)
                        {
                            // Page overflow detected. Open fresh canvas context
                            page = document.AddPage();
                            gfx = XGraphics.FromPdfPage(page);
                            currentY = margin; // Reset cursor back up
                        }

                        gfx.DrawString(currentLine, bodyFont, XBrushes.Black, margin, currentY);
                        currentY += lineSpacing;
                        currentLine = words[i];
                    }
                    else
                    {
                        currentLine = testLine;
                    }
                }

                // Flush out remaining trailing wrapped string data
                if (!string.IsNullOrEmpty(currentLine))
                {
                    if (currentY + lineSpacing > bottomLimit)
                    {
                        page = document.AddPage();
                        gfx = XGraphics.FromPdfPage(page);
                        currentY = margin;
                    }
                    gfx.DrawString(currentLine, bodyFont, XBrushes.Black, margin, currentY);
                    currentY += lineSpacing;
                }
            }

            document.Save(filePath);
        }
    }
}