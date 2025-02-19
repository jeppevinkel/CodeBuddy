using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Interfaces
{
    public interface IResourceMonitor
    {
        Task<ResourceMetrics> GetResourceMetricsAsync();
        Task<IEnumerable<ConfigurationResourceLimit>> GetConfigurationResourceLimitsAsync();
    }

    public class ResourceMetrics
    {
        public double MemoryUsagePercentage { get; set; }
        public double MemoryThresholdPercentage { get; set; }
        public double CpuUsagePercentage { get; set; }
        public double CpuThresholdPercentage { get; set; }
        public double DiskSpaceRemainingPercentage { get; set; }
        public double DiskSpaceThresholdPercentage { get; set; }
    }

    public class ConfigurationResourceLimit
    {
        public string ConfigurationName { get; set; }
        public string ResourceType { get; set; }
        public double CurrentUsage { get; set; }
        public double MaxLimit { get; set; }
    }
}