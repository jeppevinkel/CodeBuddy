using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public class CoverageHistoryRepository : ICoverageHistoryRepository
{
    private readonly string _historyPath;

    public CoverageHistoryRepository()
    {
        _historyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodeBuddy",
            "CoverageHistory");
        
        Directory.CreateDirectory(_historyPath);
    }

    public async Task<List<HistoricalCoveragePoint>> GetHistoricalCoverageAsync(string projectPath)
    {
        var historyFile = GetHistoryFilePath(projectPath);
        if (!File.Exists(historyFile))
            return new List<HistoricalCoveragePoint>();

        try
        {
            var json = await File.ReadAllTextAsync(historyFile);
            var history = JsonSerializer.Deserialize<List<HistoricalCoveragePoint>>(json);
            return history ?? new List<HistoricalCoveragePoint>();
        }
        catch (Exception)
        {
            return new List<HistoricalCoveragePoint>();
        }
    }

    public async Task<CoverageMetrics> GetCurrentCoverageAsync(string projectPath)
    {
        var history = await GetHistoricalCoverageAsync(projectPath);
        var latest = history.OrderByDescending(h => h.Timestamp).FirstOrDefault();

        if (latest == null)
        {
            return new CoverageMetrics
            {
                OverallCoverage = 0,
                BranchCoverage = 0,
                StatementCoverage = 0
            };
        }

        return new CoverageMetrics
        {
            OverallCoverage = latest.OverallCoverage,
            BranchCoverage = latest.BranchCoverage,
            StatementCoverage = latest.StatementCoverage
        };
    }

    public async Task SaveCoverageDataAsync(string projectPath, CoverageMetrics metrics)
    {
        var history = await GetHistoricalCoverageAsync(projectPath);
        
        var newPoint = new HistoricalCoveragePoint
        {
            Timestamp = DateTime.UtcNow,
            OverallCoverage = metrics.OverallCoverage,
            BranchCoverage = metrics.BranchCoverage,
            StatementCoverage = metrics.StatementCoverage,
            CommitId = await GetCurrentCommitId()
        };

        // Maintain only last 100 points to prevent file growth
        history.Add(newPoint);
        if (history.Count > 100)
        {
            history = history.OrderByDescending(h => h.Timestamp).Take(100).ToList();
        }

        var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(GetHistoryFilePath(projectPath), json);
    }

    private string GetHistoryFilePath(string projectPath)
    {
        var projectName = new DirectoryInfo(projectPath).Name;
        var sanitizedName = string.Join("_", projectName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_historyPath, $"{sanitizedName}_coverage_history.json");
    }

    private async Task<string> GetCurrentCommitId()
    {
        try
        {
            var gitHeadPath = Path.Combine(Directory.GetCurrentDirectory(), ".git", "HEAD");
            if (!File.Exists(gitHeadPath))
                return "unknown";

            var headContent = await File.ReadAllTextAsync(gitHeadPath);
            var refPath = headContent.Trim();

            if (refPath.StartsWith("ref: "))
            {
                var gitRef = refPath.Substring(5);
                var refFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".git", gitRef);
                
                if (File.Exists(refFilePath))
                {
                    return (await File.ReadAllTextAsync(refFilePath)).Trim();
                }
            }

            return headContent.Trim();
        }
        catch
        {
            return "unknown";
        }
    }
}