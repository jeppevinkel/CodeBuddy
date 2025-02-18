using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Generates cross-references between related components
    /// </summary>
    public class CrossReferenceGenerator
    {
        /// <summary>
        /// Finds methods that are related to the given method
        /// </summary>
        public List<MethodReferenceInfo> FindMethodReferences(MethodInfo method)
        {
            var references = new List<MethodReferenceInfo>();

            // Find overridden methods
            if (method.GetBaseDefinition() != method)
            {
                references.Add(new MethodReferenceInfo
                {
                    TargetMethod = method.GetBaseDefinition().DeclaringType.FullName + "." + method.Name,
                    Type = "Overrides",
                    Description = "Base implementation"
                });
            }

            // Find interface implementations
            var implementedInterfaces = method.DeclaringType.GetInterfaces();
            foreach (var iface in implementedInterfaces)
            {
                var map = method.DeclaringType.GetInterfaceMap(iface);
                var index = Array.IndexOf(map.TargetMethods, method);
                if (index != -1)
                {
                    references.Add(new MethodReferenceInfo
                    {
                        TargetMethod = iface.FullName + "." + map.InterfaceMethods[index].Name,
                        Type = "Implements",
                        Description = "Interface implementation"
                    });
                }
            }

            return references;
        }

        /// <summary>
        /// Generates cross-references between components
        /// </summary>
        public async Task<CrossReferenceResult> GenerateCrossReferences(IEnumerable<Assembly> assemblies)
        {
            var result = new CrossReferenceResult();
            var dependencyGraph = new Dictionary<string, List<string>>();

            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes().Where(t => t.IsPublic);
                foreach (var type in types)
                {
                    var references = new List<ComponentReference>();
                    var dependencies = new List<string>();

                    // Base type references
                    if (type.BaseType != null && type.BaseType != typeof(object))
                    {
                        references.Add(new ComponentReference
                        {
                            SourceComponent = type.FullName,
                            TargetComponent = type.BaseType.FullName,
                            ReferenceType = "Inherits",
                            Description = "Base class",
                            LinkUrl = $"api/{type.BaseType.FullName.Replace(".", "/")}.html"
                        });
                        dependencies.Add(type.BaseType.FullName);
                    }

                    // Interface implementations
                    foreach (var iface in type.GetInterfaces())
                    {
                        references.Add(new ComponentReference
                        {
                            SourceComponent = type.FullName,
                            TargetComponent = iface.FullName,
                            ReferenceType = "Implements",
                            Description = "Interface implementation",
                            LinkUrl = $"api/{iface.FullName.Replace(".", "/")}.html"
                        });
                        dependencies.Add(iface.FullName);
                    }

                    // Property dependencies
                    foreach (var prop in type.GetProperties())
                    {
                        if (IsCodeBuddyType(prop.PropertyType))
                        {
                            references.Add(new ComponentReference
                            {
                                SourceComponent = type.FullName,
                                TargetComponent = prop.PropertyType.FullName,
                                ReferenceType = "Uses",
                                Description = $"Property {prop.Name}",
                                LinkUrl = $"api/{prop.PropertyType.FullName.Replace(".", "/")}.html"
                            });
                            dependencies.Add(prop.PropertyType.FullName);
                        }
                    }

                    // Method dependencies
                    foreach (var method in type.GetMethods())
                    {
                        // Return type
                        if (IsCodeBuddyType(method.ReturnType))
                        {
                            references.Add(new ComponentReference
                            {
                                SourceComponent = type.FullName,
                                TargetComponent = method.ReturnType.FullName,
                                ReferenceType = "Returns",
                                Description = $"Return type of {method.Name}",
                                LinkUrl = $"api/{method.ReturnType.FullName.Replace(".", "/")}.html"
                            });
                            dependencies.Add(method.ReturnType.FullName);
                        }

                        // Parameter types
                        foreach (var param in method.GetParameters())
                        {
                            if (IsCodeBuddyType(param.ParameterType))
                            {
                                references.Add(new ComponentReference
                                {
                                    SourceComponent = type.FullName,
                                    TargetComponent = param.ParameterType.FullName,
                                    ReferenceType = "Uses",
                                    Description = $"Parameter {param.Name} of {method.Name}",
                                    LinkUrl = $"api/{param.ParameterType.FullName.Replace(".", "/")}.html"
                                });
                                dependencies.Add(param.ParameterType.FullName);
                            }
                        }
                    }

                    result.References.AddRange(references);
                    dependencyGraph[type.FullName] = dependencies.Distinct().ToList();

                    // Validate references
                    foreach (var reference in references)
                    {
                        if (!types.Any(t => t.FullName == reference.TargetComponent))
                        {
                            result.Issues.Add(new CrossReferenceIssue
                            {
                                Component = reference.SourceComponent,
                                ReferencedComponent = reference.TargetComponent,
                                IssueType = "BrokenReference",
                                Description = $"Referenced component not found in codebase"
                            });
                        }
                    }
                }
            }

            result.DependencyGraph = dependencyGraph;
            ValidateDependencyGraph(result);

            return result;
        }

        private bool IsCodeBuddyType(Type type)
        {
            return type.FullName?.StartsWith("CodeBuddy.") == true;
        }

        private void ValidateDependencyGraph(CrossReferenceResult result)
        {
            // Check for circular dependencies
            foreach (var component in result.DependencyGraph.Keys)
            {
                var visited = new HashSet<string>();
                var stack = new HashSet<string>();
                if (HasCircularDependency(component, result.DependencyGraph, visited, stack))
                {
                    result.Issues.Add(new CrossReferenceIssue
                    {
                        Component = component,
                        IssueType = "CircularDependency",
                        Description = $"Circular dependency detected"
                    });
                }
            }
        }

        private bool HasCircularDependency(string component, 
            Dictionary<string, List<string>> graph,
            HashSet<string> visited,
            HashSet<string> stack)
        {
            if (!visited.Contains(component))
            {
                visited.Add(component);
                stack.Add(component);

                if (graph.TryGetValue(component, out var dependencies))
                {
                    foreach (var dependency in dependencies)
                    {
                        if (!visited.Contains(dependency) && 
                            HasCircularDependency(dependency, graph, visited, stack))
                        {
                            return true;
                        }
                        else if (stack.Contains(dependency))
                        {
                            return true;
                        }
                    }
                }
            }
            stack.Remove(component);
            return false;
        }
    }

    /// <summary>
    /// Information about a method reference
    /// </summary>
    public class MethodReferenceInfo
    {
        public string TargetMethod { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
    }
}