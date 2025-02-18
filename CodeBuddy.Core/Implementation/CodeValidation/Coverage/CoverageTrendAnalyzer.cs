using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.TestCoverage;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public class CoverageTrendAnalyzer : ICoverageTrendAnalyzer
{
    private readonly ICoverageHistoryRepository _historyRepository;
    private readonly ISourceControlProvider _sourceControlProvider;

    public CoverageTrendAnalyzer(
        ICoverageHistoryRepository historyRepository,
        ISourceControlProvider sourceControlProvider)
    {
        _historyRepository = historyRepository;
        _sourceControlProvider = sourceControlProvider;
    }

    public async Task<List<CoverageTrendPoint>> AnalyzeTrendsAsync(ValidationContext context)
    {
        // Get historical coverage data
        var historicalData = await _historyRepository.GetHistoricalCoverageAsync(context.ProjectPath);
        var currentCommit = await _sourceControlProvider.GetCurrentCommitAsync();

        var trends = new List<CoverageTrendPoint>();

        // Process historical data points
        foreach (var point in historicalData.OrderBy(h => h.Timestamp))
        {
            trends.Add(new CoverageTrendPoint
            {
                Timestamp = point.Timestamp,
                OverallCoverage = point.OverallCoverage,
                BranchCoverage = point.BranchCoverage,
                StatementCoverage = point.StatementCoverage,
                CommitId = point.CommitId
            });
        }

        // Add current point if it doesn't exist
        if (!trends.Any() || trends.Last().CommitId != currentCommit)
        {
            var currentCoverage = await _historyRepository.GetCurrentCoverageAsync(context.ProjectPath);
            trends.Add(new CoverageTrendPoint
            {
                Timestamp = DateTime.UtcNow,
                OverallCoverage = currentCoverage.OverallCoverage,
                BranchCoverage = currentCoverage.BranchCoverage,
                StatementCoverage = currentCoverage.StatementCoverage,
                CommitId = currentCommit
            });
        }

        return trends;
    }
}

public interface ICoverageHistoryRepository
{
    Task<List<HistoricalCoveragePoint>> GetHistoricalCoverageAsync(string projectPath);
    Task<CoverageMetrics> GetCurrentCoverageAsync(string projectPath);
    Task SaveCoverageDataAsync(string projectPath, CoverageMetrics metrics);
}

public interface ISourceControlProvider
{
    Task<string> GetCurrentCommitAsync();
    Task<DateTime> GetCommitTimestampAsync(string commitId);
}

public class HistoricalCoveragePoint
{
    public DateTime Timestamp { get; set; }
    public double OverallCoverage { get; set; }
    public double BranchCoverage { get; set; }
    public double StatementCoverage { get; set; }
    public string CommitId { get; set; }
}

public class CoverageMetrics
{
    public double OverallCoverage { get; set; }
    public double BranchCoverage { get; set; }
    public double StatementCoverage { get; set; }
}