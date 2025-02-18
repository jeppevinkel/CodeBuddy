using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.TestCoverage;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public class CoverageDataCollector : ICoverageDataCollector
{
    private readonly ICodeInstrumentationEngine _instrumentationEngine;
    private readonly ITestRunner _testRunner;
    private readonly ICoverageTracker _coverageTracker;

    public CoverageDataCollector(
        ICodeInstrumentationEngine instrumentationEngine,
        ITestRunner testRunner,
        ICoverageTracker coverageTracker)
    {
        _instrumentationEngine = instrumentationEngine;
        _testRunner = testRunner;
        _coverageTracker = coverageTracker;
    }

    public async Task<CoverageData> CollectCoverageDataAsync(ValidationContext context)
    {
        var config = context.GetConfiguration<CoverageConfiguration>();
        var coverageData = new CoverageData();

        // Instrument code for coverage tracking
        var instrumentedFiles = await _instrumentationEngine.InstrumentCodeAsync(context.FilePaths);

        try
        {
            // Run tests and collect coverage data
            await _testRunner.RunTestsAsync(context.ProjectPath);
            var rawCoverageData = await _coverageTracker.GetCoverageDataAsync();

            // Process raw coverage data per module
            foreach (var module in rawCoverageData)
            {
                if (config.ShouldExclude(module.Key))
                    continue;

                var moduleCoverage = new ModuleCoverage
                {
                    ModuleName = module.Key,
                    FilePath = module.Key,
                    CoveragePercentage = CalculateModuleCoverage(module.Value),
                    LineByLineCoverage = module.Value.LineData,
                    FunctionCoverage = module.Value.FunctionData
                };

                coverageData.ModuleCoverageData[module.Key] = moduleCoverage;
                coverageData.LineCoverageData[module.Key] = module.Value.LineData;
                coverageData.BranchData[module.Key] = module.Value.BranchData;
            }
        }
        finally
        {
            // Restore original code
            await _instrumentationEngine.RestoreCodeAsync(instrumentedFiles);
        }

        return coverageData;
    }

    private double CalculateModuleCoverage(RawCoverageData data)
    {
        if (!data.LineData.Any())
            return 0;

        var executableLines = data.LineData.Count(l => !l.IsExcluded);
        if (executableLines == 0)
            return 0;

        var coveredLines = data.LineData.Count(l => !l.IsExcluded && l.IsCovered);
        return (coveredLines * 100.0) / executableLines;
    }
}

public interface ICodeInstrumentationEngine
{
    Task<Dictionary<string, string>> InstrumentCodeAsync(string[] filePaths);
    Task RestoreCodeAsync(Dictionary<string, string> instrumentedFiles);
}

public interface ITestRunner
{
    Task RunTestsAsync(string projectPath);
}

public interface ICoverageTracker
{
    Task<Dictionary<string, RawCoverageData>> GetCoverageDataAsync();
}

public class RawCoverageData
{
    public List<LineCoverage> LineData { get; set; } = new();
    public Dictionary<string, double> FunctionData { get; set; } = new();
    public List<BranchInfo> BranchData { get; set; } = new();
}