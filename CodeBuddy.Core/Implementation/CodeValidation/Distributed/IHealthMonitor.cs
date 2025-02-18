using System.Threading.Tasks;
using System.Collections.Generic;

namespace CodeBuddy.Core.Implementation.CodeValidation.Distributed;

public interface IHealthMonitor
{
    Task<bool> IsNodeHealthy(string nodeId);
    Task PerformClusterHealthCheck();
    Task<List<NodeHealth>> GetClusterHealth();
    Task RaiseHealthAlert(HealthAlert alert);
}

public class NodeHealth
{
    public string NodeId { get; set; }
    public bool IsHealthy { get; set; }
    public ResourceMetrics ResourceMetrics { get; set; }
    public List<string> Issues { get; set; }
    public double UptimeHours { get; set; }
}

public class HealthAlert
{
    public string NodeId { get; set; }
    public string AlertType { get; set; }
    public string Message { get; set; }
    public AlertSeverity Severity { get; set; }
    public Dictionary<string, string> Context { get; set; }
}