using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.ValidationModels;

namespace CodeBuddy.Core.Implementation.CodeValidation.ResourceManagement
{
    public class ResourceTrendAnalyzer
    {
        private readonly ConcurrentDictionary<string, List<ResourceUsageRecord>> _usageHistory;
        private readonly TimeSpan _historyWindow = TimeSpan.FromHours(1);
        private const int MinRecordsForPrediction = 10;

        public ResourceTrendAnalyzer()
        {
            _usageHistory = new ConcurrentDictionary<string, List<ResourceUsageRecord>>();
        }

        public async Task<ResourcePrediction> PredictResourceNeeds(ValidationContext context)
        {
            var recordKey = GetContextKey(context);
            var relevantHistory = GetRelevantHistory(recordKey);

            if (relevantHistory.Count < MinRecordsForPrediction)
            {
                return FallbackPrediction(context);
            }

            return await Task.Run(() =>
            {
                var prediction = new ResourcePrediction
                {
                    EstimatedMemoryNeeded = PredictMemoryNeeds(relevantHistory, context),
                    EstimatedFileHandles = PredictFileHandles(relevantHistory, context),
                    EstimatedDuration = PredictDuration(relevantHistory, context),
                    Priority = DeterminePriority(context),
                    ConfidenceScore = CalculateConfidenceScore(relevantHistory)
                };

                return prediction;
            });
        }

        private ResourcePrediction FallbackPrediction(ValidationContext context)
        {
            // Conservative estimates when insufficient history
            return new ResourcePrediction
            {
                EstimatedMemoryNeeded = context.CodeSize * 10, // 10x code size
                EstimatedFileHandles = 10, // Conservative default
                EstimatedDuration = TimeSpan.FromSeconds(5),
                Priority = context.IsHighPriority ? ValidationPriority.High : ValidationPriority.Normal,
                ConfidenceScore = 0.5 // Moderate confidence for fallback
            };
        }

        private string GetContextKey(ValidationContext context)
        {
            return $"{string.Join(",", context.ValidationType.SubTypes)}_{context.CodeSize / 1024}KB";
        }

        private List<ResourceUsageRecord> GetRelevantHistory(string contextKey)
        {
            if (!_usageHistory.TryGetValue(contextKey, out var history))
            {
                return new List<ResourceUsageRecord>();
            }

            var cutoff = DateTime.UtcNow - _historyWindow;
            return history.Where(r => r.Timestamp >= cutoff).ToList();
        }

        private long PredictMemoryNeeds(List<ResourceUsageRecord> history, ValidationContext context)
        {
            if (!history.Any()) return context.CodeSize * 10;

            var recentRecords = history.OrderByDescending(r => r.Timestamp).Take(5);
            var avgMemory = recentRecords.Average(r => r.MemoryUsed);
            var stdDev = CalculateStdDev(recentRecords.Select(r => (double)r.MemoryUsed));
            
            // Add safety margin based on standard deviation
            return (long)(avgMemory + stdDev);
        }

        private int PredictFileHandles(List<ResourceUsageRecord> history, ValidationContext context)
        {
            if (!history.Any()) return 10;

            var recentRecords = history.OrderByDescending(r => r.Timestamp).Take(5);
            return (int)Math.Ceiling(recentRecords.Average(r => r.FileHandlesUsed));
        }

        private TimeSpan PredictDuration(List<ResourceUsageRecord> history, ValidationContext context)
        {
            if (!history.Any()) return TimeSpan.FromSeconds(5);

            var recentRecords = history.OrderByDescending(r => r.Timestamp).Take(5);
            var avgDuration = recentRecords.Average(r => r.Duration.TotalMilliseconds);
            return TimeSpan.FromMilliseconds(avgDuration);
        }

        private ValidationPriority DeterminePriority(ValidationContext context)
        {
            if (context.IsHighPriority) return ValidationPriority.High;
            
            if (context.EstimatedComplexity > 100) return ValidationPriority.High;
            if (context.EstimatedComplexity > 50) return ValidationPriority.Normal;
            return ValidationPriority.Low;
        }

        private double CalculateConfidenceScore(List<ResourceUsageRecord> history)
        {
            if (history.Count < MinRecordsForPrediction) return 0.5;

            var recentRecords = history.OrderByDescending(r => r.Timestamp).Take(5).ToList();
            
            // Calculate variance in memory usage
            var memoryVariance = CalculateVarianceCoefficient(
                recentRecords.Select(r => (double)r.MemoryUsed));

            // Calculate variance in duration
            var durationVariance = CalculateVarianceCoefficient(
                recentRecords.Select(r => r.Duration.TotalMilliseconds));

            // Higher consistency = higher confidence
            var consistencyScore = 1 - ((memoryVariance + durationVariance) / 2);
            
            // Scale by history size
            var historyScore = Math.Min(1.0, history.Count / (double)MinRecordsForPrediction);
            
            // Combine scores
            return (consistencyScore * 0.7) + (historyScore * 0.3);
        }

        private double CalculateStdDev(IEnumerable<double> values)
        {
            var avg = values.Average();
            var sumOfSquares = values.Sum(x => Math.Pow(x - avg, 2));
            return Math.Sqrt(sumOfSquares / values.Count());
        }

        private double CalculateVarianceCoefficient(IEnumerable<double> values)
        {
            var avg = values.Average();
            if (avg == 0) return 1;
            var stdDev = CalculateStdDev(values);
            return stdDev / avg;
        }

        public async Task<LoadTrend> GetLoadTrendAsync()
        {
            return await Task.Run(() =>
            {
                var recentUsage = _usageHistory.Values
                    .SelectMany(h => h)
                    .Where(r => r.Timestamp >= DateTime.UtcNow - TimeSpan.FromMinutes(5))
                    .OrderBy(r => r.Timestamp)
                    .ToList();

                if (recentUsage.Count < 2)
                {
                    return new LoadTrend { IsIncreasing = false, Rate = 0 };
                }

                var oldestMemory = recentUsage.First().MemoryUsed;
                var newestMemory = recentUsage.Last().MemoryUsed;
                var timeDiff = recentUsage.Last().Timestamp - recentUsage.First().Timestamp;
                
                var rate = (newestMemory - oldestMemory) / timeDiff.TotalSeconds;
                
                return new LoadTrend 
                { 
                    IsIncreasing = rate > 0,
                    Rate = rate
                };
            });
        }

        public void RecordUsage(ValidationContext context, ResourceUsageStats stats)
        {
            var record = new ResourceUsageRecord
            {
                Timestamp = DateTime.UtcNow,
                ContextKey = GetContextKey(context),
                MemoryUsed = stats.MemoryUsed,
                FileHandlesUsed = stats.FileHandlesUsed,
                Duration = stats.Duration
            };

            _usageHistory.AddOrUpdate(
                record.ContextKey,
                new List<ResourceUsageRecord> { record },
                (_, list) =>
                {
                    list.Add(record);
                    // Trim old records
                    var cutoff = DateTime.UtcNow - _historyWindow;
                    return list.Where(r => r.Timestamp >= cutoff).ToList();
                });
        }
    }

    public class ResourceUsageRecord
    {
        public DateTime Timestamp { get; set; }
        public string ContextKey { get; set; }
        public long MemoryUsed { get; set; }
        public int FileHandlesUsed { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class ResourceUsageStats
    {
        public long MemoryUsed { get; set; }
        public int FileHandlesUsed { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class LoadTrend
    {
        public bool IsIncreasing { get; set; }
        public double Rate { get; set; }
    }
}