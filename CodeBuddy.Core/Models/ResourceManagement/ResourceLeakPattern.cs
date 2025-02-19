using System.Collections.Generic;

namespace CodeBuddy.Core.Models.ResourceManagement
{
    public class ResourceLeakPattern
    {
        public string Id { get; set; }
        public string ResourceType { get; set; }
        public List<string> AllocationPatterns { get; set; } = new();
        public List<string> ReleasePatterns { get; set; } = new();
        public List<string> ProperHandlingPatterns { get; set; } = new();
        public List<string> ExceptionPatterns { get; set; } = new();
        public string Description { get; set; }
        public List<string> BestPractices { get; set; } = new();
        public Dictionary<string, string> Examples { get; set; } = new();
    }
}