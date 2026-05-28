using StudentReportGenerator.Models;
using System.Net.Http;
using System.Threading.Tasks;

namespace StudentReportGenerator.Services
{
    // The Interface is just the "rule" that all AI engines must follow
    public interface IAiService
    {
        Task<ReportResponse> GenerateReportAsync(ReportRequest request);
       
    }
}