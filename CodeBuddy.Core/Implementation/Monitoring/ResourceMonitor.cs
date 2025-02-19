using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;

namespace CodeBuddy.Core.Implementation.Monitoring
{
    public class ResourceMonitor : IResourceMonitor
    {
        private readonly IConfigurationManager _configManager;
        private readonly Dictionary<string, ConfigurationResourceLimit> _resourceLimits;

        public ResourceMonitor(IConfigurationManager configManager)
        {
            _configManager = configManager;
            _resourceLimits = new Dictionary<string, ConfigurationResourceLimit>();
        }

        public async Task<ResourceMetrics> GetResourceMetricsAsync()
        {
            var process = Process.GetCurrentProcess();
            var totalMemory = GC.GetTotalMemory(false);
            var workingSet = process.WorkingSet64;
            
            return new ResourceMetrics
            {
                MemoryUsagePercentage = (double)workingSet / (double)totalMemory * 100,
                MemoryThresholdPercentage = 85, // Configurable threshold
                CpuUsagePercentage = await GetCpuUsageAsync(),
                CpuThresholdPercentage = 80, // Configurable threshold
                DiskSpaceRemainingPercentage = await GetDiskSpacePercentageAsync(),
                DiskSpaceThresholdPercentage = 10 // Configurable threshold
            };
        }

        public async Task<IEnumerable<ConfigurationResourceLimit>> GetConfigurationResourceLimitsAsync()
        {
            // Get all active configurations and their resource limits
            var configs = await _configManager.GetAllConfigurationsAsync();
            var limits = new List<ConfigurationResourceLimit>();

            foreach (var config in configs)
            {
                if (_resourceLimits.TryGetValue(config.ToString(), out var limit))
                {
                    // Update current usage
                    limit.CurrentUsage = await CalculateConfigurationResourceUsageAsync(config);
                    limits.Add(limit);
                }
            }

            return limits;
        }

        private async Task<double> GetCpuUsageAsync()
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;

            await Task.Delay(100); // Sample over 100ms

            var endTime = DateTime.UtcNow;
            var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;

            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsElapsed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsElapsed);

            return cpuUsageTotal * 100;
        }

        private async Task<double> GetDiskSpacePercentageAsync()
        {
            var drive = new DriveInfo(AppDomain.CurrentDomain.BaseDirectory);
            return ((double)drive.AvailableFreeSpace / drive.TotalSize) * 100;
        }

        private async Task<double> CalculateConfigurationResourceUsageAsync(object config)
        {
            // Implementation would calculate resource usage for specific configuration
            // This could include memory usage, file handles, network connections, etc.
            return 0.0; // Placeholder implementation
        }
    }
}