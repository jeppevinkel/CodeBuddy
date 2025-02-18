using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.TestCoverage;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public class CoverageTrendAnalyzer : ICoverageTrendAnalyzer
{
    private readonly ICoverageHistoryProvider _historyProvider;
    private readonly ICommitInfoProvider _commitProvider;

    public CoverageTrendAnalyzer(
        ICoverageHistoryProvider historyProvider,
        ICommitInfoProvider commitProvider)
    {
        _historyProvider = historyProvider;
        _commitProvider = commitProvider;
    }

    public async Task<List<CoverageTrendPoint>> AnalyzeTrendsAsync(ValidationContext context)
    {
        // Get historical coverage data
        var historicalData = await _historyProvider.GetHistoricalCoverageAsync(context);
        
        // Get commit information for each data point
        var commits = await _commitProvider.GetCommitInfoAsync(
            historicalData.Select(d => d.Timestamp).ToList());

        // Create trend points combining coverage and commit data
        var trendPoints = new List<CoverageTrendPoint>();

        foreach (var dataPoint in historicalData)
        {
            var commit = commits.FirstOrDefault(c => c.Timestamp == dataPoint.Timestamp);
            if (commit != null)
            {
                trendPoints.Add(new CoverageTrendPoint
                {
                    Timestamp = dataPoint.Timestamp,
                    OverallCoverage = dataPoint.OverallCoverage,
                    BranchCoverage = dataPoint.BranchCoverage,
                    StatementCoverage = dataPoint.StatementCoverage,
                    CommitId = commit.CommitId
                });
            }
        }

        // Sort by timestamp
        return trendPoints.OrderBy(p => p.Timestamp).ToList();
    }

    private Dictionary<string, double> CalculateCoverageTrends(List<CoverageTrendPoint> points)
    {
        if (points.Count < 2)
            return new Dictionary<string, double>();

        var firstPoint = points.First();
        var lastPoint = points.Last();
        var daysDiff = (lastPoint.Timestamp - firstPoint.Timestamp).TotalDays;

        return new Dictionary<string, double>
        {
            ["Overall"] = (lastPoint.OverallCoverage - firstPoint.OverallCoverage) / daysDiff,
            ["Branch"] = (lastPoint.BranchCoverage - firstPoint.BranchCoverage) / daysDiff,
            ["Statement"] = (lastPoint.StatementCoverage - firstPoint.StatementCoverage) / daysDiff
        };
    }
}

public interface ICoverageHistoryProvider
{
    Task<List<HistoricalCoverageData>> GetHistoricalCoverageAsync(ValidationContext context);
}

public interface ICommitInfoProvider
{
    Task<List<CommitInfo>> GetCommitInfoAsync(List<DateTime> timestamps);
}

public class HistoricalCoverageData
{
    public DateTime Timestamp { get; set; }
    public double OverallCoverage { get; set; }
    public double BranchCoverage { get; set; }
    public double StatementCoverage { get; set; }
}

public class CommitInfo
{
    public string CommitId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Author { get; set; }
    public string Message { get; set; }
}