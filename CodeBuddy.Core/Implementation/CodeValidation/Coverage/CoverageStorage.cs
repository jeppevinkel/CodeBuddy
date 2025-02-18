using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.TestCoverage;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public class CoverageStorage : ICoverageStorage
{
    private readonly Dictionary<string, Dictionary<int, LineExecutionInfo>> _lineExecutionData;
    private readonly Dictionary<string, Dictionary<string, BranchExecutionInfo>> _branchExecutionData;
    private readonly HashSet<string> _trackedFiles;

    public CoverageStorage()
    {
        _lineExecutionData = new Dictionary<string, Dictionary<int, LineExecutionInfo>>();
        _branchExecutionData = new Dictionary<string, Dictionary<string, BranchExecutionInfo>>();
        _trackedFiles = new HashSet<string>();
    }

    public async Task<ExecutionData> GetExecutionDataAsync(ValidationContext context)
    {
        var executionData = new ExecutionData();

        foreach (var filePath in _trackedFiles)
        {
            // Process line execution data
            if (_lineExecutionData.TryGetValue(filePath, out var lineData))
            {
                foreach (var lineInfo in lineData)
                {
                    executionData.UpdateLineExecution(filePath, lineInfo.Key, lineInfo.Value);
                }
            }

            // Process branch execution data
            if (_branchExecutionData.TryGetValue(filePath, out var branchData))
            {
                foreach (var branchInfo in branchData)
                {
                    executionData.UpdateBranchExecution(filePath, branchInfo.Key, branchInfo.Value);
                }
            }
        }

        return executionData;
    }

    public void TrackFile(string filePath)
    {
        if (!_trackedFiles.Contains(filePath))
        {
            _trackedFiles.Add(filePath);
            _lineExecutionData[filePath] = new Dictionary<int, LineExecutionInfo>();
            _branchExecutionData[filePath] = new Dictionary<string, BranchExecutionInfo>();
        }
    }

    public void RecordLineExecution(string filePath, int lineNumber)
    {
        if (!_lineExecutionData.ContainsKey(filePath))
        {
            _lineExecutionData[filePath] = new Dictionary<int, LineExecutionInfo>();
        }

        if (!_lineExecutionData[filePath].ContainsKey(lineNumber))
        {
            _lineExecutionData[filePath][lineNumber] = new LineExecutionInfo();
        }

        var lineInfo = _lineExecutionData[filePath][lineNumber];
        lineInfo.Executed = true;
        lineInfo.ExecutionCount++;
    }

    public void RecordBranchExecution(string filePath, string branchId, string outcome)
    {
        if (!_branchExecutionData.ContainsKey(filePath))
        {
            _branchExecutionData[filePath] = new Dictionary<string, BranchExecutionInfo>();
        }

        if (!_branchExecutionData[filePath].ContainsKey(branchId))
        {
            _branchExecutionData[filePath][branchId] = new BranchExecutionInfo();
        }

        var branchInfo = _branchExecutionData[filePath][branchId];
        branchInfo.Outcomes[outcome] = true;
    }

    public void Reset()
    {
        _lineExecutionData.Clear();
        _branchExecutionData.Clear();
        _trackedFiles.Clear();
    }
}

public static class ExecutionDataExtensions
{
    public static void UpdateLineExecution(this ExecutionData data, string filePath, int lineNumber, LineExecutionInfo info)
    {
        var field = data.GetType().GetField("_lineExecutionData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var lineExecutionData = (Dictionary<string, Dictionary<int, LineExecutionInfo>>)field.GetValue(data);

        if (!lineExecutionData.ContainsKey(filePath))
        {
            lineExecutionData[filePath] = new Dictionary<int, LineExecutionInfo>();
        }

        lineExecutionData[filePath][lineNumber] = info;
    }

    public static void UpdateBranchExecution(this ExecutionData data, string filePath, string branchId, BranchExecutionInfo info)
    {
        var field = data.GetType().GetField("_branchExecutionData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var branchExecutionData = (Dictionary<string, Dictionary<string, BranchExecutionInfo>>)field.GetValue(data);

        if (!branchExecutionData.ContainsKey(filePath))
        {
            branchExecutionData[filePath] = new Dictionary<string, BranchExecutionInfo>();
        }

        branchExecutionData[filePath][branchId] = info;
    }
}