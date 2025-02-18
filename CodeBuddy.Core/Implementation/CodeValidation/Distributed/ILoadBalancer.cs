using System.Threading.Tasks;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Distributed;

public interface ILoadBalancer
{
    Task<NodeAssignment> GetAssignmentAsync(ValidationContext context);
    Task<NodeAssignment> SelectNodeAsync(ValidationContext context);
    Task RebalanceWorkload();
}

public class NodeAssignment
{
    public string NodeId { get; set; }
    public double LoadFactor { get; set; }
    public string[] SupportedLanguages { get; set; }
    public ResourceMetrics CurrentResourceMetrics { get; set; }
}