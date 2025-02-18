using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using CodeBuddy.Core.Models.TestCoverage;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public class CoverageTracker : ICoverageTracker
{
    private static readonly ConcurrentDictionary<string, RawCoverageData> _coverageData = new();
    private static readonly ConcurrentDictionary<string, HashSet<string>> _executedBranches = new();
    private static readonly ConcurrentDictionary<string, int> _lineExecutionCounts = new();
    private static readonly object _lock = new();

    public void Initialize()
    {
        _coverageData.Clear();
        _executedBranches.Clear();
        _lineExecutionCounts.Clear();
    }

    public static void TrackMethodEntry(string methodId)
    {
        var stackTrace = new System.Diagnostics.StackTrace(true);
        var frame = stackTrace.GetFrame(1); // Get calling frame
        var filePath = frame.GetFileName();

        if (string.IsNullOrEmpty(filePath))
            return;

        var data = _coverageData.GetOrAdd(filePath, _ => new RawCoverageData());
        
        var methodName = frame.GetMethod()?.Name ?? methodId;
        lock (_lock)
        {
            if (!data.FunctionData.ContainsKey(methodName))
            {
                data.FunctionData[methodName] = 100.0; // Method was executed
            }
        }
    }

    public static void TrackBranch(string branchId)
    {
        var stackTrace = new System.Diagnostics.StackTrace(true);
        var frame = stackTrace.GetFrame(1);
        var filePath = frame.GetFileName();
        var lineNumber = frame.GetFileLineNumber();

        if (string.IsNullOrEmpty(filePath))
            return;

        var data = _coverageData.GetOrAdd(filePath, _ => new RawCoverageData());
        var branches = _executedBranches.GetOrAdd(filePath, _ => new HashSet<string>());

        lock (_lock)
        {
            branches.Add($"{lineNumber}_{branchId}");

            var branchInfo = new BranchInfo
            {
                Location = $"{filePath}:{lineNumber}",
                Condition = $"Branch {branchId}",
                BranchOutcomes = new Dictionary<string, bool>
                {
                    { "executed", true }
                }
            };

            data.BranchData.Add(branchInfo);
        }
    }

    public static void TrackLine(int lineNumber)
    {
        var stackTrace = new System.Diagnostics.StackTrace(true);
        var frame = stackTrace.GetFrame(1);
        var filePath = frame.GetFileName();

        if (string.IsNullOrEmpty(filePath))
            return;

        var key = $"{filePath}:{lineNumber}";
        _lineExecutionCounts.AddOrUpdate(key, 1, (_, count) => count + 1);

        var data = _coverageData.GetOrAdd(filePath, _ => new RawCoverageData());
        
        lock (_lock)
        {
            var lineCoverage = data.LineData.Find(l => l.LineNumber == lineNumber);
            if (lineCoverage == null)
            {
                lineCoverage = new LineCoverage
                {
                    LineNumber = lineNumber,
                    Content = GetLineContent(filePath, lineNumber),
                    IsCovered = true,
                    ExecutionCount = _lineExecutionCounts[key],
                    IsExcluded = false
                };
                data.LineData.Add(lineCoverage);
            }
            else
            {
                lineCoverage.ExecutionCount = _lineExecutionCounts[key];
            }
        }
    }

    public async Task<Dictionary<string, RawCoverageData>> GetCoverageDataAsync()
    {
        return await Task.FromResult(new Dictionary<string, RawCoverageData>(_coverageData));
    }

    private static string GetLineContent(string filePath, int lineNumber)
    {
        try
        {
            var lines = System.IO.File.ReadAllLines(filePath);
            return lineNumber <= lines.Length ? lines[lineNumber - 1] : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}