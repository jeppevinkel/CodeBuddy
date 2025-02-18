using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.TestCoverage;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public class CoverageDataCollector : ICoverageDataCollector
{
    private readonly ICoverageInstrumenter _instrumenter;
    private readonly ICoverageStorage _storage;

    public CoverageDataCollector(
        ICoverageInstrumenter instrumenter,
        ICoverageStorage storage)
    {
        _instrumenter = instrumenter;
        _storage = storage;
    }

    public async Task<CoverageData> CollectCoverageDataAsync(ValidationContext context)
    {
        // Get instrumented code data
        var instrumentationData = await _instrumenter.GetInstrumentationDataAsync(context);

        // Retrieve execution data from storage
        var executionData = await _storage.GetExecutionDataAsync(context);

        // Combine instrumentation and execution data
        var coverageData = new CoverageData();
        
        foreach (var module in instrumentationData.Modules)
        {
            var moduleCoverage = new ModuleCoverage
            {
                ModuleName = module.Name,
                FilePath = module.Path,
                ExcludedRegions = module.ExcludedRegions
            };

            var lineCoverage = new List<LineCoverage>();
            foreach (var line in module.Lines)
            {
                var executionInfo = executionData.GetExecutionInfo(module.Path, line.Number);
                
                lineCoverage.Add(new LineCoverage
                {
                    LineNumber = line.Number,
                    Content = line.Content,
                    IsCovered = executionInfo.Executed,
                    ExecutionCount = executionInfo.ExecutionCount,
                    IsExcluded = module.ExcludedRegions.Contains(line.Number)
                });
            }

            moduleCoverage.LineByLineCoverage = lineCoverage;
            coverageData.ModuleCoverageData[module.Path] = moduleCoverage;
            coverageData.LineCoverageData[module.Path] = lineCoverage;

            // Process branch data
            var branchInfo = new List<BranchInfo>();
            foreach (var branch in module.Branches)
            {
                var branchExecutionInfo = executionData.GetBranchExecutionInfo(module.Path, branch.Id);
                
                branchInfo.Add(new BranchInfo
                {
                    Location = $"{module.Path}:{branch.Line}",
                    Condition = branch.Condition,
                    BranchOutcomes = branchExecutionInfo.Outcomes
                });
            }

            coverageData.BranchData[module.Path] = branchInfo;
        }

        return coverageData;
    }
}

public interface ICoverageInstrumenter
{
    Task<InstrumentationData> GetInstrumentationDataAsync(ValidationContext context);
}

public interface ICoverageStorage
{
    Task<ExecutionData> GetExecutionDataAsync(ValidationContext context);
}

public class InstrumentationData
{
    public List<ModuleInstrumentationInfo> Modules { get; set; } = new();
}

public class ModuleInstrumentationInfo
{
    public string Name { get; set; }
    public string Path { get; set; }
    public List<LineInfo> Lines { get; set; } = new();
    public List<BranchInstrumentationInfo> Branches { get; set; } = new();
    public List<int> ExcludedRegions { get; set; } = new();
}

public class LineInfo
{
    public int Number { get; set; }
    public string Content { get; set; }
}

public class BranchInstrumentationInfo
{
    public string Id { get; set; }
    public int Line { get; set; }
    public string Condition { get; set; }
}

public class ExecutionData
{
    private readonly Dictionary<string, Dictionary<int, LineExecutionInfo>> _lineExecutionData = new();
    private readonly Dictionary<string, Dictionary<string, BranchExecutionInfo>> _branchExecutionData = new();

    public LineExecutionInfo GetExecutionInfo(string filePath, int lineNumber)
    {
        if (_lineExecutionData.TryGetValue(filePath, out var fileData))
        {
            if (fileData.TryGetValue(lineNumber, out var lineInfo))
            {
                return lineInfo;
            }
        }

        return new LineExecutionInfo();
    }

    public BranchExecutionInfo GetBranchExecutionInfo(string filePath, string branchId)
    {
        if (_branchExecutionData.TryGetValue(filePath, out var fileData))
        {
            if (fileData.TryGetValue(branchId, out var branchInfo))
            {
                return branchInfo;
            }
        }

        return new BranchExecutionInfo();
    }
}

public class LineExecutionInfo
{
    public bool Executed { get; set; }
    public int ExecutionCount { get; set; }
}

public class BranchExecutionInfo
{
    public Dictionary<string, bool> Outcomes { get; set; } = new();
}