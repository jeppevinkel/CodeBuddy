using System;
using System.Collections.Generic;
using System.Linq;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation;

/// <summary>
/// Analyzes resource usage trends and patterns to identify potential issues
/// </summary>
internal class ResourceTrendAnalyzer
{
    private readonly TimeSpan _analysisWindow;
    private const int MinDataPointsForAnalysis = 10;

    public ResourceTrendAnalyzer(TimeSpan analysisWindow)
    {
        _analysisWindow = analysisWindow;
    }

    public PerformanceMonitor.TrendInfo AnalyzeTrend((DateTime Timestamp, double Value)[] dataPoints)
    {
        if (dataPoints == null || dataPoints.Length < MinDataPointsForAnalysis)
        {
            return new PerformanceMonitor.TrendInfo
            {
                ChangeRate = 0,
                IsIncreasing = false,
                ProjectedTimeToThreshold = TimeSpan.Zero,
                ProjectedPeakValue = 0
            };
        }

        var windowStart = DateTime.UtcNow - _analysisWindow;
        var relevantPoints = dataPoints
            .Where(p => p.Timestamp >= windowStart)
            .OrderBy(p => p.Timestamp)
            .ToArray();

        if (relevantPoints.Length < MinDataPointsForAnalysis)
            return CreateDefaultTrend();

        var trend = CalculateLinearRegression(relevantPoints);
        
        return new PerformanceMonitor.TrendInfo
        {
            ChangeRate = trend.slope,
            IsIncreasing = trend.slope > 0,
            ProjectedTimeToThreshold = CalculateProjectedTimeToThreshold(relevantPoints, trend),
            ProjectedPeakValue = CalculateProjectedPeak(relevantPoints, trend)
        };
    }

    private (double slope, double intercept) CalculateLinearRegression((DateTime Timestamp, double Value)[] points)
    {
        var xValues = points.Select(p => p.Timestamp.Ticks / (double)TimeSpan.TicksPerSecond).ToArray();
        var yValues = points.Select(p => p.Value).ToArray();

        var n = points.Length;
        var sumX = xValues.Sum();
        var sumY = yValues.Sum();
        var sumXY = xValues.Zip(yValues, (x, y) => x * y).Sum();
        var sumX2 = xValues.Select(x => x * x).Sum();

        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        var intercept = (sumY - slope * sumX) / n;

        return (slope, intercept);
    }

    private TimeSpan CalculateProjectedTimeToThreshold((DateTime Timestamp, double Value)[] points, 
        (double slope, double intercept) trend)
    {
        if (Math.Abs(trend.slope) < 0.0001)
            return TimeSpan.MaxValue;

        var currentValue = points.Last().Value;
        var projectedSeconds = (currentValue * 1.5 - trend.intercept) / trend.slope;
        
        return projectedSeconds > 0 
            ? TimeSpan.FromSeconds(projectedSeconds)
            : TimeSpan.MaxValue;
    }

    private double CalculateProjectedPeak((DateTime Timestamp, double Value)[] points,
        (double slope, double intercept) trend)
    {
        if (trend.slope <= 0)
            return points.Max(p => p.Value);

        var projectedValue = trend.slope * (_analysisWindow.TotalSeconds) + trend.intercept;
        return Math.Max(points.Max(p => p.Value), projectedValue);
    }

    private PerformanceMonitor.TrendInfo CreateDefaultTrend()
    {
        return new PerformanceMonitor.TrendInfo
        {
            ChangeRate = 0,
            IsIncreasing = false,
            ProjectedTimeToThreshold = TimeSpan.MaxValue,
            ProjectedPeakValue = 0
        };
    }

    /// <summary>
    /// Detects patterns indicating potential memory leaks
    /// </summary>
    public bool DetectMemoryLeak((DateTime Timestamp, double Value)[] memoryPoints, out string pattern)
    {
        pattern = string.Empty;
        
        if (memoryPoints.Length < MinDataPointsForAnalysis)
            return false;

        var trend = CalculateLinearRegression(memoryPoints);
        
        // Check for steady increase over time
        if (trend.slope > 0)
        {
            var varianceFromTrend = CalculateVarianceFromTrend(memoryPoints, trend);
            
            // If variance is low, likely a steady leak
            if (varianceFromTrend < 0.1)
            {
                pattern = "Steady memory increase with low variance";
                return true;
            }
            
            // Check for saw-tooth pattern (possible leak in loop)
            if (DetectSawToothPattern(memoryPoints))
            {
                pattern = "Saw-tooth pattern indicating possible loop-based leak";
                return true;
            }
        }

        return false;
    }

    private double CalculateVarianceFromTrend((DateTime Timestamp, double Value)[] points,
        (double slope, double intercept) trend)
    {
        var predictedValues = points.Select(p =>
            trend.slope * (p.Timestamp.Ticks / (double)TimeSpan.TicksPerSecond) + trend.intercept);

        var actualValues = points.Select(p => p.Value);
        
        var sumSquaredDifferences = predictedValues
            .Zip(actualValues, (predicted, actual) => Math.Pow(predicted - actual, 2))
            .Sum();

        return Math.Sqrt(sumSquaredDifferences / points.Length) / points.Average(p => p.Value);
    }

    private bool DetectSawToothPattern((DateTime Timestamp, double Value)[] points)
    {
        var values = points.Select(p => p.Value).ToArray();
        var peaks = new List<int>();
        
        // Find local maxima
        for (int i = 1; i < values.Length - 1; i++)
        {
            if (values[i] > values[i - 1] && values[i] > values[i + 1])
            {
                peaks.Add(i);
            }
        }

        if (peaks.Count < 3)
            return false;

        // Check if peaks are roughly periodic
        var intervals = peaks.Zip(peaks.Skip(1), (a, b) => b - a).ToList();
        var avgInterval = intervals.Average();
        var intervalVariance = intervals.Select(i => Math.Pow(i - avgInterval, 2)).Average();

        return intervalVariance / avgInterval < 0.3; // Low variance indicates regular pattern
    }
}