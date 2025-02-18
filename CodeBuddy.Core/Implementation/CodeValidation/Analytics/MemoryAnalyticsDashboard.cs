using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Analytics;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;

namespace CodeBuddy.Core.Implementation.CodeValidation.Analytics
{
    public class MemoryAnalyticsDashboard
    {
        private readonly MemoryLeakDetector _memoryLeakDetector;
        private readonly TimeSeriesStorage _timeSeriesStorage;
        private readonly ValidationResilienceConfig _config;
        private readonly MemoryAnalyticsConfig _analyticsConfig;

        public MemoryAnalyticsDashboard(
            MemoryLeakDetector memoryLeakDetector,
            TimeSeriesStorage timeSeriesStorage,
            ValidationResilienceConfig config,
            MemoryAnalyticsConfig analyticsConfig)
        {
            _memoryLeakDetector = memoryLeakDetector;
            _timeSeriesStorage = timeSeriesStorage;
            _config = config;
            _analyticsConfig = analyticsConfig;
        }

        public async Task<MemoryAnalyticsReport> GenerateReport(string componentId, DateTime startTime, DateTime endTime)
        {
            var analysis = await _memoryLeakDetector.AnalyzeMemoryPatterns(componentId);
            var timeSeriesData = await _timeSeriesStorage.GetMemoryMetrics(componentId, startTime, endTime);
            
            var report = new MemoryAnalyticsReport
            {
                TimeSeriesData = timeSeriesData,
                DetectedLeaks = await GetDetectedLeaks(componentId),
                Hotspots = await GetAllocationHotspots(componentId),
                FragmentationIndex = CalculateFragmentationIndex(timeSeriesData),
                GeneratedAt = DateTime.UtcNow
            };

            return report;
        }

        private async Task<List<MemoryLeakInfo>> GetDetectedLeaks(string componentId)
        {
            // Analyze object retention patterns and identify potential leaks
            var leaks = new List<MemoryLeakInfo>();
            var analysis = await _memoryLeakDetector.AnalyzeMemoryPatterns(componentId);

            if (analysis.LeakDetected && analysis.ConfidenceLevel >= _analyticsConfig.LeakConfidenceThreshold)
            {
                // Add detected leaks from analysis
                foreach (var metric in analysis.AdditionalMetrics)
                {
                    if (metric.Key.StartsWith("LeakType_"))
                    {
                        leaks.Add(new MemoryLeakInfo
                        {
                            ObjectType = metric.Key.Substring(9),
                            ConfidenceScore = analysis.ConfidenceLevel / 100.0,
                            AllocationStack = metric.Value
                        });
                    }
                }
            }

            return leaks;
        }

        private async Task<List<HeapAllocationHotspot>> GetAllocationHotspots(string componentId)
        {
            // Analyze allocation patterns to identify hotspots
            var hotspots = new List<HeapAllocationHotspot>();
            var metrics = await _timeSeriesStorage.GetAllocationMetrics(componentId);

            foreach (var metric in metrics.GroupBy(m => m.Location))
            {
                var allocationRate = CalculateAllocationRate(metric.ToList());
                if (allocationRate > _analyticsConfig.MemoryThresholdBytes)
                {
                    hotspots.Add(new HeapAllocationHotspot
                    {
                        Location = metric.Key,
                        AllocationRate = allocationRate,
                        TotalAllocations = metric.Sum(m => m.Size),
                        StackTrace = metric.First().StackTrace
                    });
                }
            }

            return hotspots.OrderByDescending(h => h.AllocationRate).ToList();
        }

        private double CalculateFragmentationIndex(List<MemoryMetrics> timeSeriesData)
        {
            if (!timeSeriesData.Any()) return 0;

            var latest = timeSeriesData.Last();
            var totalHeap = latest.TotalMemoryBytes;
            var usedHeap = latest.LargeObjectHeapBytes + latest.SmallObjectHeapBytes;

            return (1 - (usedHeap / (double)totalHeap)) * 100;
        }

        private long CalculateAllocationRate(List<dynamic> metrics)
        {
            if (metrics.Count < 2) return 0;

            var timeSpan = (metrics.Last().Timestamp - metrics.First().Timestamp).TotalSeconds;
            var totalAllocations = metrics.Sum(m => m.Size);

            return (long)(totalAllocations / timeSpan);
        }

        public async Task<bool> SetAlertThreshold(string metricName, long threshold)
        {
            if (string.IsNullOrEmpty(metricName)) return false;

            try
            {
                // Update configuration
                switch (metricName.ToLower())
                {
                    case "memory":
                        _analyticsConfig.MemoryThresholdBytes = threshold;
                        break;
                    case "leakconfidence":
                        _analyticsConfig.LeakConfidenceThreshold = threshold / 100.0;
                        break;
                    default:
                        return false;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<IEnumerable<MemoryMetrics>> GetHistoricalData(
            string componentId,
            DateTime startTime,
            DateTime endTime,
            string metricType = "all")
        {
            return await _timeSeriesStorage.GetMemoryMetrics(componentId, startTime, endTime);
        }
    }
}