using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Interfaces
{
    public interface IConfigurationValidator
    {
        Task<ValidationResult> ValidateAsync(object value, ValidationContext context);
    }

    public class ValidationContext
    {
        public string PropertyName { get; set; }
        public object Instance { get; set; }
        public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; } = new List<string>();
        public ValidationSeverity Severity { get; set; }
    }

    public enum ValidationSeverity
    {
        Error,
        Warning,
        Information
    }
}