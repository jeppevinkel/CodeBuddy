using System;

namespace CodeBuddy.Core.Implementation.CodeValidation.Discovery
{
    public interface IValidatorDiscoveryService : IDisposable
    {
        void DiscoverAndRegisterValidators();
    }
}