using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Interfaces
{
    public interface IResourceMonitoringDashboard
    {
        // Real-time metrics collection
        Task<ResourceMetrics> GetCurrentResourceUtilization();
        Task<IEnumerable<ResourceMetrics>> GetHistoricalMetrics(DateTime startTime, DateTime endTime);
        
        // Bottleneck and performance analysis
        Task<BottleneckAnalysis> GetBottleneckAnalysis();
        Task<IEnumerable<ResourceCleanupEvent>> GetResourceCleanupEvents();
        Task<IEnumerable<MemoryPressureIncident>> GetMemoryPressureIncidents();
        
        // Resource prediction
        Task<ResourcePrediction> PredictResourceUsage(TimeSpan predictionWindow);
        
        // Alert configuration
        Task SetResourceAlert(ResourceAlertConfig alertConfig);
        Task<IEnumerable<ResourceAlert>> GetActiveAlerts();
        
        // Export capabilities
        Task<byte[]> ExportMetricsData(DateTime startTime, DateTime endTime, ExportFormat format);
        
        // Dashboard management
        Task StartMonitoring();
        Task StopMonitoring();
        Task<bool> IsMonitoring();
    }
}