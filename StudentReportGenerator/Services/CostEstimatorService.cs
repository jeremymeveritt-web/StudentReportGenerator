using System;

namespace StudentReportGenerator.Services
{
    // Translates token counts into an approximate running cost, because nobody making a
    // school purchasing decision thinks in tokens. Deliberately rough: blended per-million-token
    // rates for the default model tier of each provider, clearly labelled as an estimate.
    public static class CostEstimatorService
    {
        // Approximate blended (input+output) USD per 1M tokens, mid-2026 list prices
        private const double NvidiaRate = 0.00;   // NIM free tier
        private const double GeminiRate = 1.00;   // Gemini 2.5 Flash class
        private const double OpenAiRate = 0.60;   // GPT-4o mini class
        private const double ClaudeRate = 2.00;   // Claude Haiku class

        public static string EstimateCostSummary(long totalTokens, int nvidiaReports, int geminiReports, int openAiReports, int claudeReports)
        {
            int totalReports = nvidiaReports + geminiReports + openAiReports + claudeReports;
            if (totalReports == 0 || totalTokens == 0) return "No usage yet — estimated running cost: $0.00";

            double tokensPerReport = (double)totalTokens / totalReports;
            double cost =
                Estimate(tokensPerReport * nvidiaReports, NvidiaRate) +
                Estimate(tokensPerReport * geminiReports, GeminiRate) +
                Estimate(tokensPerReport * openAiReports, OpenAiRate) +
                Estimate(tokensPerReport * claudeReports, ClaudeRate);

            return $"Estimated AI running cost to date: ~${cost:0.00} " +
                   $"(NVIDIA NIM reports are free; other providers estimated at typical list prices)";
        }

        private static double Estimate(double tokens, double ratePerMillion) => tokens / 1_000_000.0 * ratePerMillion;
    }
}
