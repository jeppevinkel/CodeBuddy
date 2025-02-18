using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    public interface ICodeValidator
    {
        Task<ValidationResult> ValidateAsync(string code, ValidationOptions options, CancellationToken cancellationToken = default);
    }
}