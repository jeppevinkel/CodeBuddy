using System;
using System.Collections.Generic;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    public class ValidationContext
    {
        public ICodeValidator Validator { get; set; }
        public string Code { get; set; }
        public string Language { get; set; }
        public ValidationOptions Options { get; set; }
        public MetricsCollection Metrics { get; } = new MetricsCollection();
        public ValidationResult Result { get; set; } = new ValidationResult();
    }

    public class MetricsCollection
    {
        private readonly Dictionary<string, StepMetrics> _stepMetrics = new();
        private readonly List<ResourceSnapshot> _resourceSnapshots = new();

        public void RecordStepStart(string step)
        {
            _stepMetrics[step] = new StepMetrics { StartTime = DateTime.UtcNow };
        }

        public void RecordStepEnd(string step, bool success)
        {
            if (_stepMetrics.TryGetValue(step, out var metrics))
            {
                metrics.EndTime = DateTime.UtcNow;
                metrics.Success = success;
            }
        }

        public void RecordResourceSnapshot(ResourceMetrics metrics)
        {
            _resourceSnapshots.Add(new ResourceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                Metrics = metrics
            });
        }

        public IReadOnlyDictionary<string, StepMetrics> GetStepMetrics() => _stepMetrics;
        public IReadOnlyList<ResourceSnapshot> GetResourceSnapshots() => _resourceSnapshots;
    }

    public class StepMetrics
    {
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool Success { get; set; }
        public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
    }

    public class ResourceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public ResourceMetrics Metrics { get; set; }
    }
}