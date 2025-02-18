using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.Coverage;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.TestCoverage;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation.Coverage;

public class TestCoverageGeneratorTests
{
    private readonly Mock<ICoverageDataCollector> _mockDataCollector;
    private readonly Mock<ICoverageReportRenderer> _mockReportRenderer;
    private readonly Mock<ICoverageTrendAnalyzer> _mockTrendAnalyzer;
    private readonly Mock<IRecommendationEngine> _mockRecommendationEngine;
    private readonly TestCoverageGenerator _generator;

    public TestCoverageGeneratorTests()
    {
        _mockDataCollector = new Mock<ICoverageDataCollector>();
        _mockReportRenderer = new Mock<ICoverageReportRenderer>();
        _mockTrendAnalyzer = new Mock<ICoverageTrendAnalyzer>();
        _mockRecommendationEngine = new Mock<IRecommendationEngine>();

        _generator = new TestCoverageGenerator(
            _mockDataCollector.Object,
            _mockReportRenderer.Object,
            _mockTrendAnalyzer.Object,
            _mockRecommendationEngine.Object);
    }

    [Fact]
    public async Task GenerateReportAsync_ShouldCreateComprehensiveReport()
    {
        // Arrange
        var context = new ValidationContext();
        var coverageData = new CoverageData();
        var trendPoints = new[] { new CoverageTrendPoint { OverallCoverage = 85.5 } };
        var recommendations = new[] { new CoverageRecommendation { Priority = "High" } };

        _mockDataCollector.Setup(m => m.CollectCoverageDataAsync(context))
            .ReturnsAsync(coverageData);

        _mockTrendAnalyzer.Setup(m => m.AnalyzeTrendsAsync(context))
            .ReturnsAsync(trendPoints);

        _mockRecommendationEngine.Setup(m => m.GenerateRecommendationsAsync(It.IsAny<TestCoverageReport>()))
            .ReturnsAsync(recommendations);

        // Act
        var report = await _generator.GenerateReportAsync(context);

        // Assert
        Assert.NotNull(report);
        Assert.Equal(coverageData.CalculateOverallCoverage(), report.OverallCoveragePercentage);
        Assert.Equal(coverageData.GetModuleCoverage(), report.CoverageByModule);
        Assert.Single(report.CoverageTrends);
        Assert.Single(report.Recommendations);

        _mockDataCollector.Verify(m => m.CollectCoverageDataAsync(context), Times.Once);
        _mockTrendAnalyzer.Verify(m => m.AnalyzeTrendsAsync(context), Times.Once);
        _mockRecommendationEngine.Verify(
            m => m.GenerateRecommendationsAsync(It.IsAny<TestCoverageReport>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateHtmlReportAsync_ShouldRenderHtmlReport()
    {
        // Arrange
        var report = new TestCoverageReport { OverallCoveragePercentage = 85.5 };
        var expectedHtml = "<html>Test Report</html>";

        _mockReportRenderer.Setup(m => m.RenderHtmlReportAsync(report))
            .ReturnsAsync(expectedHtml);

        // Act
        var html = await _generator.GenerateHtmlReportAsync(report);

        // Assert
        Assert.Equal(expectedHtml, html);
        _mockReportRenderer.Verify(m => m.RenderHtmlReportAsync(report), Times.Once);
    }

    [Fact]
    public async Task GenerateJsonReportAsync_ShouldRenderJsonReport()
    {
        // Arrange
        var report = new TestCoverageReport { OverallCoveragePercentage = 85.5 };
        var expectedJson = "{\"coverage\":85.5}";

        _mockReportRenderer.Setup(m => m.RenderJsonReportAsync(report))
            .ReturnsAsync(expectedJson);

        // Act
        var json = await _generator.GenerateJsonReportAsync(report);

        // Assert
        Assert.Equal(expectedJson, json);
        _mockReportRenderer.Verify(m => m.RenderJsonReportAsync(report), Times.Once);
    }

    [Fact]
    public async Task ValidateCoverageThresholdsAsync_ShouldValidateAndGenerateRecommendations()
    {
        // Arrange
        var report = new TestCoverageReport { OverallCoveragePercentage = 75.0 };
        var result = new ValidationResult();
        var recommendations = new[] 
        { 
            new CoverageRecommendation 
            { 
                Priority = "High",
                ModuleName = "TestModule",
                PotentialCoverageGain = 15.0
            } 
        };

        _mockRecommendationEngine.Setup(m => m.GenerateRecommendationsAsync(
                report,
                It.IsAny<System.Collections.Generic.List<string>>()))
            .ReturnsAsync(recommendations);

        // Act
        await _generator.ValidateCoverageThresholdsAsync(report, result);

        // Assert
        Assert.NotNull(result.CoverageValidation);
        Assert.False(result.CoverageValidation.MeetsThreshold); // 75% < 80% threshold
        Assert.NotEmpty(result.CoverageValidation.ImprovementSuggestions);

        _mockRecommendationEngine.Verify(
            m => m.GenerateRecommendationsAsync(
                report,
                It.IsAny<System.Collections.Generic.List<string>>()),
            Times.Once);
    }
}