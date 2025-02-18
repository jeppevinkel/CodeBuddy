using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.ValidationModels
{
    public class ValidationType
    {
        public IReadOnlyList<ValidationSubType> SubTypes { get; }
        
        public ValidationType(IEnumerable<ValidationSubType> subTypes)
        {
            SubTypes = new List<ValidationSubType>(subTypes);
        }
    }

    public enum ValidationSubType
    {
        Syntax,
        Security,
        Style,
        BestPractices,
        ErrorHandling,
        Custom
    }

    public class ResourceAllocation : IDisposable
    {
        public MemoryPool MemoryPool { get; set; }
        public FileHandlePool FileHandles { get; set; }
        public ValidationPriority Priority { get; set; }
        public DateTime AllocationTime { get; } = DateTime.UtcNow;
        public string AllocationId { get; } = Guid.NewGuid().ToString();

        public void Dispose()
        {
            MemoryPool?.Dispose();
            FileHandles?.Dispose();
        }
    }

    public class ResourcePrediction
    {
        public long EstimatedMemoryNeeded { get; set; }
        public int EstimatedFileHandles { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public ValidationPriority Priority { get; set; }
        public double ConfidenceScore { get; set; }
    }

    public enum ValidationPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }
}