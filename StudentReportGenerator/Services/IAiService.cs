using System.Threading.Tasks;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    // The Interface is just the "rule" that all AI engines must follow
    public interface IAiService
    {
        Task<ReportResponse> GenerateReportAsync(ReportRequest request);
    }
}