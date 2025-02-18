using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    public class ValidatorDependencyResolver
    {
        private readonly IValidatorRegistrar _registrar;
        private readonly Dictionary<string, HashSet<string>> _dependencyGraph;
        private readonly object _graphLock = new object();

        public ValidatorDependencyResolver(IValidatorRegistrar registrar)
        {
            _registrar = registrar ?? throw new ArgumentNullException(nameof(registrar));
            _dependencyGraph = new Dictionary<string, HashSet<string>>();

            _registrar.ValidatorRegistered += OnValidatorRegistered;
            _registrar.ValidatorUnregistered += OnValidatorUnregistered;
        }

        private void OnValidatorRegistered(object sender, ValidatorRegistrationEventArgs e)
        {
            UpdateDependencyGraph(e.LanguageId, e.Metadata?.Dependencies);
        }

        private void OnValidatorUnregistered(object sender, ValidatorRegistrationEventArgs e)
        {
            RemoveFromDependencyGraph(e.LanguageId);
        }

        private void UpdateDependencyGraph(string validatorId, IList<ValidatorDependency> dependencies)
        {
            if (dependencies == null || !dependencies.Any()) return;

            lock (_graphLock)
            {
                if (!_dependencyGraph.ContainsKey(validatorId))
                {
                    _dependencyGraph[validatorId] = new HashSet<string>();
                }

                foreach (var dep in dependencies)
                {
                    _dependencyGraph[validatorId].Add(dep.Name);
                }

                // Check for circular dependencies
                if (HasCircularDependency(validatorId))
                {
                    throw new InvalidOperationException($"Circular dependency detected for validator {validatorId}");
                }
            }
        }

        private void RemoveFromDependencyGraph(string validatorId)
        {
            lock (_graphLock)
            {
                _dependencyGraph.Remove(validatorId);
                
                // Remove this validator from other validators' dependencies
                foreach (var deps in _dependencyGraph.Values)
                {
                    deps.Remove(validatorId);
                }
            }
        }

        private bool HasCircularDependency(string validatorId)
        {
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();

            bool IsCyclicUtil(string currentId)
            {
                if (!visited.Contains(currentId))
                {
                    visited.Add(currentId);
                    recursionStack.Add(currentId);

                    if (_dependencyGraph.TryGetValue(currentId, out var dependencies))
                    {
                        foreach (var dependency in dependencies)
                        {
                            if (!visited.Contains(dependency) && IsCyclicUtil(dependency))
                                return true;
                            else if (recursionStack.Contains(dependency))
                                return true;
                        }
                    }
                }

                recursionStack.Remove(currentId);
                return false;
            }

            return IsCyclicUtil(validatorId);
        }

        public void ValidateDependencies(string validatorId, IList<ValidatorDependency> dependencies)
        {
            if (dependencies == null || !dependencies.Any()) return;

            var missing = new List<string>();
            foreach (var dep in dependencies.Where(d => !d.IsOptional))
            {
                var metadata = _registrar.GetValidatorMetadata(dep.Name);
                if (metadata == null)
                {
                    missing.Add(dep.Name);
                    continue;
                }

                if (!string.IsNullOrEmpty(dep.VersionRequirement))
                {
                    if (!IsVersionCompatible(metadata.Version, dep.VersionRequirement))
                    {
                        throw new InvalidOperationException(
                            $"Validator {validatorId} requires {dep.Name} version {dep.VersionRequirement}, " +
                            $"but found version {metadata.Version}");
                    }
                }
            }

            if (missing.Any())
            {
                throw new InvalidOperationException(
                    $"Validator {validatorId} is missing required dependencies: {string.Join(", ", missing)}");
            }
        }

        private bool IsVersionCompatible(Version actual, string requirement)
        {
            // Basic version comparison - could be enhanced with semantic versioning
            if (Version.TryParse(requirement.TrimStart('>', '=', '<'), out Version required))
            {
                if (requirement.StartsWith(">=")) return actual >= required;
                if (requirement.StartsWith(">")) return actual > required;
                if (requirement.StartsWith("<=")) return actual <= required;
                if (requirement.StartsWith("<")) return actual < required;
                return actual == required;
            }
            return false;
        }

        public IEnumerable<string> GetDependentValidators(string validatorId)
        {
            lock (_graphLock)
            {
                return _dependencyGraph
                    .Where(kvp => kvp.Value.Contains(validatorId))
                    .Select(kvp => kvp.Key)
                    .ToList();
            }
        }
    }
}