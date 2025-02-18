using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.Coverage;
using CodeBuddy.Core.Models.TestCoverage;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation;

public class UnifiedCoverageDashboardTests
{
    private readonly Mock<ITestCoverageGenerator> _coverageGenerator;
    private readonly Mock<ICoverageTrendAnalyzer> _trendAnalyzer;
    private readonly Mock<ICoverageHistoryRepository> _historyRepository;
    private readonly Mock<IRecommendationEngine> _recommendationEngine;
    private readonly Mock<ISourceControlProvider> _sourceControlProvider;
    private readonly IUnifiedCoverageDashboard _dashboard;

    public UnifiedCoverageDashboardTests()
    {
        _coverageGenerator = new Mock<ITestCoverageGenerator>();
        _trendAnalyzer = new Mock<ICoverageTrendAnalyzer>();
        _historyRepository = new Mock<ICoverageHistoryRepository>();
        _recommendationEngine = new Mock<IRecommendationEngine>();
        _sourceControlProvider = new Mock<ISourceControlProvider>();

        _dashboard = new UnifiedCoverageDashboard(
            _coverageGenerator.Object,
            _trendAnalyzer.Object,
            _historyRepository.Object,
            _recommendationEngine.Object,
            _sourceControlProvider.Object);
    }

    [Fact]
    public async Task GetUnifiedMetrics_ReturnsAggregatedMetrics()
    {
        // Arrange
        var projectPath = "/test/project";
        var testReport = new TestCoverageReport
        {
            OverallCoveragePercentage = 85.5,
            BranchCoverage = new BranchCoverageStatistics { BranchCoveragePercentage = 80.0 },
            StatementCoverage = new StatementCoverageMetrics { StatementCoveragePercentage = 90.0 },
            CoverageByModule = new Dictionary<string, ModuleCoverage>(),
            UncoveredSections = new List<UncoveredCodeSection>()
        };

        var history = new List<HistoricalCoveragePoint>
        {
            new() { Timestamp = DateTime.UtcNow.AddDays(-1), OverallCoverage = 84.0 }
        };

        _coverageGenerator.Setup(g => g.GenerateReportAsync(It.IsAny<ValidationContext>()))
            .ReturnsAsync(testReport);
        _historyRepository.Setup(h => h.GetHistoricalCoverageAsync(projectPath))
            .ReturnsAsync(history);
        _sourceControlProvider.Setup(s => s.GetCurrentCommitAsync())
            .ReturnsAsync("abc123");

        // Act
        var metrics = await _dashboard.GetUnifiedMetrics(projectPath);

        // Assert
        Assert.Equal(85.5, metrics.OverallCoverage);
        Assert.Equal(80.0, metrics.BranchCoverage);
        Assert.Equal(90.0, metrics.StatementCoverage);
        Assert.Equal("abc123", metrics.CommitId);
        Assert.Single(metrics.HistoricalTrend);
    }

    [Fact]
    public async Task GetCoverageTrends_ReturnsTrendData()
    {
        // Arrange
        var projectPath = "/test/project";
        var trends = new List<CoverageTrendPoint>
        {
            new() { Timestamp = DateTime.UtcNow.AddDays(-2), OverallCoverage = 82.0 },
            new() { Timestamp = DateTime.UtcNow.AddDays(-1), OverallCoverage = 84.0 },
            new() { Timestamp = DateTime.UtcNow, OverallCoverage = 85.5 }
        };

        _trendAnalyzer.Setup(t => t.AnalyzeTrendsAsync(It.IsAny<ValidationContext>()))
            .ReturnsAsync(trends);

        // Act
        var result = await _dashboard.GetCoverageTrendsAsync(projectPath);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(82.0, result[0].OverallCoverage);
        Assert.Equal(85.5, result[2].OverallCoverage);
    }

    [Fact]
    public async Task GetRecommendations_ReturnsRecommendationsForLowCoverageModules()
    {
        // Arrange
        var projectPath = "/test/project";
        var testReport = new TestCoverageReport
        {
            CoverageByModule = new Dictionary<string, ModuleCoverage>
            {
                ["Module1"] = new ModuleCoverage { CoveragePercentage = 75.0 },
                ["Module2"] = new ModuleCoverage { CoveragePercentage = 85.0 },
                ["Module3"] = new ModuleCoverage { CoveragePercentage = 65.0 }
            }
        };

        var recommendations = new List<CoverageRecommendation>
        {
            new() { ModuleName = "Module1", Recommendation = "Add more tests" },
            new() { ModuleName = "Module3", Recommendation = "Increase coverage" }
        };

        _coverageGenerator.Setup(g => g.GenerateReportAsync(It.IsAny<ValidationContext>()))
            .ReturnsAsync(testReport);
        _recommendationEngine.Setup(r => r.GenerateRecommendationsAsync(testReport, It.IsAny<List<string>>()))
            .ReturnsAsync(recommendations);

        // Act
        var result = await _dashboard.GetRecommendationsAsync(projectPath);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.ModuleName == "Module1");
        Assert.Contains(result, r => r.ModuleName == "Module3");
    }

    [Fact]
    public async Task CompareBranchCoverage_ReturnsDetailedComparison()
    {
        // Arrange
        var projectPath = "/test/project";
        var baseBranch = "main";
        var targetBranch = "feature";

        var baseReport = new TestCoverageReport
        {
            OverallCoveragePercentage = 80.0,
            CoverageByModule = new Dictionary<string, ModuleCoverage>
            {
                ["Module1"] = new ModuleCoverage { CoveragePercentage = 75.0 },
                ["Module2"] = new ModuleCoverage { CoveragePercentage = 85.0 }
            }
        };

        var targetReport = new TestCoverageReport
        {
            OverallCoveragePercentage = 82.0,
            CoverageByModule = new Dictionary<string, ModuleCoverage>
            {
                ["Module1"] = new ModuleCoverage { CoveragePercentage = 78.0 },
                ["Module2"] = new ModuleCoverage { CoveragePercentage = 85.0 },
                ["Module3"] = new ModuleCoverage { CoveragePercentage = 83.0 }
            }
        };

        _coverageGenerator.Setup(g => g.GenerateReportAsync(It.Is<ValidationContext>(c => c.Branch == baseBranch)))
            .ReturnsAsync(baseReport);
        _coverageGenerator.Setup(g => g.GenerateReportAsync(It.Is<ValidationContext>(c => c.Branch == targetBranch)))
            .ReturnsAsync(targetReport);

        // Act
        var comparison = await _dashboard.CompareBranchCoverageAsync(projectPath, baseBranch, targetBranch);

        // Assert
        Assert.Equal(2.0, comparison.CoverageDifference);
        Assert.Equal(3, comparison.ModuleDifferences.Count);
        Assert.Contains(comparison.ModuleDifferences, d => d.ModuleName == "Module3" && d.Status == "New");
        Assert.Contains(comparison.ModuleDifferences, d => d.ModuleName == "Module1" && d.Status == "Increased");
        Assert.Contains(comparison.ModuleDifferences, d => d.ModuleName == "Module2" && d.Status == "Unchanged");
    }

    [Fact]
    public async Task DetectCoverageRegressions_IdentifiesRegressions()
    {
        // Arrange
        var projectPath = "/test/project";
        var history = new List<HistoricalCoveragePoint>
        {
            new()
            {
                Timestamp = DateTime.UtcNow.AddDays(-2),
                OverallCoverage = 85.0,
                BranchCoverage = 80.0,
                CommitId = "abc123"
            },
            new()
            {
                Timestamp = DateTime.UtcNow,
                OverallCoverage = 82.0,
                BranchCoverage = 75.0,
                CommitId = "def456"
            }
        };

        _historyRepository.Setup(h => h.GetHistoricalCoverageAsync(projectPath))
            .ReturnsAsync(history);

        // Act
        var regressions = await _dashboard.DetectCoverageRegressionsAsync(projectPath);

        // Assert
        Assert.Equal(2, regressions.Count);
        Assert.Contains(regressions, r => r.Type == "Overall" && r.Difference == -3.0);
        Assert.Contains(regressions, r => r.Type == "Branch" && r.Difference == -5.0);
    }

    [Fact]
    public async Task IdentifyLowCoverageAreas_ReturnsAreasNeedingImprovement()
    {
        // Arrange
        var projectPath = "/test/project";
        var testReport = new TestCoverageReport
        {
            CoverageByModule = new Dictionary<string, ModuleCoverage>
            {
                ["Module1"] = new ModuleCoverage 
                { 
                    CoveragePercentage = 45.0,
                    FunctionCoverage = new Dictionary<string, double>
                    {
                        ["Function1"] = 55.0,
                        ["Function2"] = 35.0
                    }
                },
                ["Module2"] = new ModuleCoverage { CoveragePercentage = 85.0 },
                ["Module3"] = new ModuleCoverage { CoveragePercentage = 75.0 }
            },
            BranchCoverage = new BranchCoverageStatistics
            {
                UncoveredBranches = new List<BranchInfo>
                {
                    new() { Location = "Module1/File1.cs" }
                }
            }
        };

        _coverageGenerator.Setup(g => g.GenerateReportAsync(It.IsAny<ValidationContext>()))
            .ReturnsAsync(testReport);

        // Act
        var lowCoverageAreas = await _dashboard.IdentifyLowCoverageAreasAsync(projectPath);

        // Assert
        Assert.Equal(2, lowCoverageAreas.Count);
        var criticalModule = Assert.Single(lowCoverageAreas, a => a.Name == "Module1");
        Assert.Equal("High", criticalModule.Risk);
        Assert.Contains(criticalModule.RecommendedActions, a => a.Contains("unit tests"));
        Assert.Contains(criticalModule.RecommendedActions, a => a.Contains("conditional branches"));
    }

    [Fact]
    public async Task TrackCoverageProgress_SavesMetricsToHistory()
    {
        // Arrange
        var projectPath = "/test/project";
        var metrics = new UnifiedCoverageMetrics
        {
            OverallCoverage = 85.0,
            BranchCoverage = 80.0,
            StatementCoverage = 90.0
        };

        // Act
        await _dashboard.TrackCoverageProgressAsync(projectPath, metrics);

        // Assert
        _historyRepository.Verify(h => h.SaveCoverageDataAsync(projectPath, 
            It.Is<CoverageMetrics>(m => 
                m.OverallCoverage == 85.0 && 
                m.BranchCoverage == 80.0 && 
                m.StatementCoverage == 90.0)), 
            Times.Once);
    }

    [Theory]
    [InlineData("html")]
    [InlineData("json")]
    public async Task GenerateUnifiedReport_GeneratesCorrectFormat(string format)
    {
        // Arrange
        var projectPath = "/test/project";
        var testReport = new TestCoverageReport();
        
        _coverageGenerator.Setup(g => g.GenerateReportAsync(It.IsAny<ValidationContext>()))
            .ReturnsAsync(testReport);
        _coverageGenerator.Setup(g => g.GenerateHtmlReportAsync(testReport))
            .ReturnsAsync("<html>Report</html>");
        _coverageGenerator.Setup(g => g.GenerateJsonReportAsync(testReport))
            .ReturnsAsync("{\"report\": \"data\"}");

        // Act
        var report = await _dashboard.GenerateUnifiedReportAsync(projectPath, format);

        // Assert
        if (format == "html")
        {
            _coverageGenerator.Verify(g => g.GenerateHtmlReportAsync(testReport), Times.Once);
            Assert.Contains("<html>", report);
        }
        else
        {
            _coverageGenerator.Verify(g => g.GenerateJsonReportAsync(testReport), Times.Once);
            Assert.Contains("report", report);
        }
    }

    [Fact]
    public async Task GenerateUnifiedReport_ThrowsOnInvalidFormat()
    {
        // Arrange
        var projectPath = "/test/project";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _dashboard.GenerateUnifiedReportAsync(projectPath, "invalid"));
    }
}