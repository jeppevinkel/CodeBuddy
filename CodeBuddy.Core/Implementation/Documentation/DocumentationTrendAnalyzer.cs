using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    public class DocumentationTrendAnalyzer
    {
        private readonly IFileOperations _fileOps;
        private const string TrendStoragePath = "docs/trends/documentation_trends.json";
        private const int TrendAnalysisPeriodDays = 30;

        public DocumentationTrendAnalyzer(IFileOperations fileOps)
        {
            _fileOps = fileOps;
        }

        public async Task StoreTrendDataAsync(DocumentationCoverageReport currentReport, string commitId, string branch)
        {
            var trends = await LoadTrendsAsync();
            
            trends.Add(new DocumentationTrend
            {
                Date = DateTime.UtcNow,
                OverallCoverage = currentReport.OverallCoverage,
                TypeCoverage = currentReport.Types.Average(t => t.TypeCoverage),
                MethodCoverage = currentReport.Types.Average(t => t.MethodCoverage),
                PropertyCoverage = currentReport.Types.Average(t => t.PropertyCoverage),
                TotalIssues = currentReport.Issues.Count,
                CommitId = commitId,
                Branch = branch
            });

            // Keep only last 90 days of data
            trends = trends.Where(t => t.Date >= DateTime.UtcNow.AddDays(-90)).ToList();
            
            await _fileOps.WriteFileAsync(TrendStoragePath, 
                System.Text.Json.JsonSerializer.Serialize(trends, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                }));
        }

        public async Task<DocumentationTrendAnalysis> AnalyzeTrendsAsync()
        {
            var trends = await LoadTrendsAsync();
            var analysis = new DocumentationTrendAnalysis { Trends = trends };

            if (trends.Count < 2)
            {
                analysis.Insights.Add("Insufficient trend data available. Need more coverage reports to analyze trends.");
                return analysis;
            }

            var recentTrends = trends
                .Where(t => t.Date >= DateTime.UtcNow.AddDays(-TrendAnalysisPeriodDays))
                .OrderBy(t => t.Date)
                .ToList();

            var oldestInPeriod = recentTrends.First();
            var newest = recentTrends.Last();

            // Calculate changes
            analysis.CoverageChange = newest.OverallCoverage - oldestInPeriod.OverallCoverage;
            analysis.IssuesChange = newest.TotalIssues - oldestInPeriod.TotalIssues;
            analysis.IsImproving = analysis.CoverageChange > 0 && analysis.IssuesChange <= 0;

            // Generate insights
            GenerateInsights(analysis, recentTrends);

            return analysis;
        }

        private void GenerateInsights(DocumentationTrendAnalysis analysis, List<DocumentationTrend> trends)
        {
            // Coverage trend
            if (analysis.CoverageChange > 5)
                analysis.Insights.Add($"Documentation coverage has significantly improved by {analysis.CoverageChange:F1}% over the last {TrendAnalysisPeriodDays} days");
            else if (analysis.CoverageChange < -5)
                analysis.Insights.Add($"Warning: Documentation coverage has declined by {Math.Abs(analysis.CoverageChange):F1}% over the last {TrendAnalysisPeriodDays} days");

            // Issue trend
            if (analysis.IssuesChange > 10)
                analysis.Insights.Add($"Warning: Documentation issues have increased by {analysis.IssuesChange} over the last {TrendAnalysisPeriodDays} days");
            else if (analysis.IssuesChange < -10)
                analysis.Insights.Add($"Documentation quality has improved with {Math.Abs(analysis.IssuesChange)} fewer issues");

            // Consistency analysis
            var coverageStdDev = CalculateStandardDeviation(trends.Select(t => t.OverallCoverage));
            if (coverageStdDev > 5)
                analysis.Insights.Add("Warning: Documentation coverage shows high variability. Consider implementing more consistent documentation practices.");

            // Branch analysis
            var branchCoverage = trends
                .GroupBy(t => t.Branch)
                .Select(g => new { Branch = g.Key, AvgCoverage = g.Average(t => t.OverallCoverage) });

            foreach (var branch in branchCoverage)
            {
                if (branch.AvgCoverage < trends.Average(t => t.OverallCoverage) - 5)
                    analysis.Insights.Add($"Warning: Branch '{branch.Branch}' consistently shows lower documentation coverage");
            }
        }

        private double CalculateStandardDeviation(IEnumerable<double> values)
        {
            var avg = values.Average();
            var sumOfSquaresOfDifferences = values.Select(val => (val - avg) * (val - avg)).Sum();
            return Math.Sqrt(sumOfSquaresOfDifferences / values.Count());
        }

        private async Task<List<DocumentationTrend>> LoadTrendsAsync()
        {
            try
            {
                var content = await _fileOps.ReadFileAsync(TrendStoragePath);
                return System.Text.Json.JsonSerializer.Deserialize<List<DocumentationTrend>>(content) 
                    ?? new List<DocumentationTrend>();
            }
            catch
            {
                return new List<DocumentationTrend>();
            }
        }
    }
}