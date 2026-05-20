using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;

namespace StudentReportGenerator.Services
{
    public static class PdfExportService
    {
        public static void ExportSingle(string filePath, string studentName, string reportText)
        {
            PdfDocument document = new PdfDocument();
            document.Info.Title = $"Report for {studentName}";

            PdfPage page = document.AddPage();
            XGraphics gfx = XGraphics.FromPdfPage(page);

            XFont titleFont = new XFont("Arial", 18, XFontStyleEx.Bold);
            XFont contentFont = new XFont("Arial", 12, XFontStyleEx.Regular);
            XTextFormatter tf = new XTextFormatter(gfx);

            // Using .Point fixes the "obsolete implicit double conversion" warnings!
            double pageWidth = page.Width.Point;
            double pageHeight = page.Height.Point;

            // Draw the header
            gfx.DrawString($"Official Report: {studentName}", titleFont, XBrushes.Black, new XRect(40, 40, pageWidth - 80, 30), XStringFormats.TopLeft);

            // Draw the wrapped text body
            tf.DrawString(reportText, contentFont, XBrushes.Black, new XRect(40, 80, pageWidth - 80, pageHeight - 120));

            document.Save(filePath);
        }
    }
}