using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Errors;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Recovery strategy for resource exhaustion errors
    /// </summary>
    public class ResourceExhaustionRecoveryStrategy : BaseErrorRecoveryStrategy
    {
        private const string MEMORY_THRESHOLD = "MemoryThreshold";
        private const string CPU_THRESHOLD = "CpuThreshold";
        private const double DEFAULT_MEMORY_THRESHOLD = 85.0;
        private const double DEFAULT_CPU_THRESHOLD = 80.0;

        public ResourceExhaustionRecoveryStrategy(RetryPolicy retryPolicy, IErrorAnalyticsService analytics) 
            : base(retryPolicy, analytics)
        {
        }

        public override async Task<bool> AttemptRecoveryAsync(ErrorRecoveryContext context)
        {
            try
            {
                var error = context.Error as ResourceError;
                if (error == null)
                    return false;

                switch (error.ResourceType?.ToLowerInvariant())
                {
                    case "memory":
                        return await HandleMemoryExhaustionAsync(context);
                    case "cpu":
                        return await HandleCpuExhaustionAsync(context);
                    case "disk":
                        return await HandleDiskExhaustionAsync(context);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                TrackException(context, ex);
                return false;
            }
        }

        public override bool CanHandle(ValidationError error)
        {
            return error.Category == ErrorCategory.Resource
                && error is ResourceError resourceError
                && resourceError.ResourceType != null;
        }

        private async Task<bool> HandleMemoryExhaustionAsync(ErrorRecoveryContext context)
        {
            var threshold = GetThreshold(context, MEMORY_THRESHOLD, DEFAULT_MEMORY_THRESHOLD);
            var currentMemory = GetMemoryUsagePercentage();

            if (currentMemory < threshold)
            {
                // Memory pressure has reduced, safe to retry
                return true;
            }

            // Force GC collection and wait
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(1000);

            return GetMemoryUsagePercentage() < threshold;
        }

        private async Task<bool> HandleCpuExhaustionAsync(ErrorRecoveryContext context)
        {
            var threshold = GetThreshold(context, CPU_THRESHOLD, DEFAULT_CPU_THRESHOLD);
            var currentCpu = GetCpuUsagePercentage();

            if (currentCpu < threshold)
            {
                // CPU usage has reduced, safe to retry
                return true;
            }

            // Wait for CPU to cool down
            await Task.Delay(2000);
            return GetCpuUsagePercentage() < threshold;
        }

        private async Task<bool> HandleDiskExhaustionAsync(ErrorRecoveryContext context)
        {
            var error = context.Error as ResourceError;
            if (string.IsNullOrEmpty(error?.ResourceName))
                return false;

            var drive = new DriveInfo(error.ResourceName);
            if (!drive.IsReady)
                return false;

            // Check if disk space has been freed
            return drive.AvailableFreeSpace > 100 * 1024 * 1024; // 100MB minimum
        }

        private double GetThreshold(ErrorRecoveryContext context, string key, double defaultValue)
        {
            if (context.Error?.AdditionalData?.TryGetValue(key, out var thresholdStr) == true
                && double.TryParse(thresholdStr, out var threshold))
            {
                return threshold;
            }
            return defaultValue;
        }

        private double GetMemoryUsagePercentage()
        {
            var process = Process.GetCurrentProcess();
            var totalMemory = process.WorkingSet64;
            var machineMemory = new PerformanceCounter("Memory", "Available Bytes").NextValue();
            
            return (totalMemory / machineMemory) * 100;
        }

        private double GetCpuUsagePercentage()
        {
            var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            return cpuCounter.NextValue();
        }
    }
}