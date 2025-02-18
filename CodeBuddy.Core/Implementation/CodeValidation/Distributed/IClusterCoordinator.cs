using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Distributed;

public interface IClusterCoordinator
{
    void RegisterNode(string nodeId, NodeCapabilities capabilities);
    void UnregisterNode(string nodeId);
    Task<bool> ParticipateInLeaderElection(string nodeId);
    Task UpdateClusterState();
    Task<ValidationResult> ForwardValidationRequest(string targetNodeId, ValidationContext context);
    Task BroadcastValidationResult(string codeHash, ValidationResult result);
}

public class NodeCapabilities
{
    public int MaxConcurrentValidations { get; set; }
    public string[] SupportedLanguages { get; set; }
    public ResourceCapacity ResourceCapacity { get; set; }
}

public class ResourceCapacity
{
    public int CpuCores { get; set; }
    public long TotalMemoryMB { get; set; }
    public long DiskSpaceGB { get; set; }
}