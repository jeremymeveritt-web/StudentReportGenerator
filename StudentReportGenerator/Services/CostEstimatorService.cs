using System;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Translates the running token count into an approximate running cost, because nobody making
    /// a school purchasing decision thinks in tokens. Deliberately rough: uses blended per-million-
    /// token rates for the default model tier of each provider rather than exact per-model pricing,
    /// and the output is explicitly labelled as an estimate rather than an invoice-grade figure.
    /// </summary>
    public static class CostEstimatorService
    {
        // Approximate blended (input+output) USD per 1M tokens, mid-2026 list prices.
        // Update these constants if a provider's list pricing changes materially.
        private const double NvidiaRate = 0.00;   // NIM free tier
        private const double GeminiRate = 1.00;   // Gemini 2.5 Flash class
        private const double OpenAiRate = 0.60;   // GPT-4o mini class
        private const double ClaudeRate = 2.00;   // Claude Haiku class

        /// <summary>
        /// Distributes the total estimated token count proportionally across providers by their
        /// share of total reports generated, then applies each provider's rate. This is an
        /// approximation (it assumes similar average report length per provider) rather than exact
        /// per-provider token tracking, which the app does not currently record separately.
        /// </summary>
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
