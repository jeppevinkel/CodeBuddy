using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Generates class diagrams and dependency graphs for the codebase
    /// </summary>
    public class DiagramGenerator
    {
        /// <summary>
        /// Generates a PlantUML class diagram for a set of types
        /// </summary>
        public string GenerateClassDiagram(IEnumerable<Type> types)
        {
            var diagram = new List<string>
            {
                "@startuml",
                "skinparam classAttributeIconSize 0",
                "skinparam classFontSize 12",
                "skinparam classFontName Helvetica"
            };

            foreach (var type in types)
            {
                // Add class/interface definition
                if (type.IsInterface)
                    diagram.Add($"interface {type.Name}");
                else if (type.IsAbstract)
                    diagram.Add($"abstract class {type.Name}");
                else
                    diagram.Add($"class {type.Name}");

                // Add properties
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    diagram.Add($"  +{prop.Name}: {prop.PropertyType.Name}");
                }

                // Add methods
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (!method.IsSpecialName) // Skip property accessors
                    {
                        var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.Name}: {p.ParameterType.Name}"));
                        diagram.Add($"  +{method.Name}({parameters}): {method.ReturnType.Name}");
                    }
                }

                diagram.Add("");

                // Add inheritance
                if (type.BaseType != null && type.BaseType != typeof(object))
                {
                    diagram.Add($"{type.BaseType.Name} <|-- {type.Name}");
                }

                // Add interface implementations
                foreach (var iface in type.GetInterfaces())
                {
                    diagram.Add($"{iface.Name} <|.. {type.Name}");
                }
            }

            diagram.Add("@enduml");
            return string.Join("\n", diagram);
        }

        /// <summary>
        /// Generates a dependency graph in DOT format
        /// </summary>
        public string GenerateDependencyGraph(IEnumerable<Type> types)
        {
            var graph = new List<string>
            {
                "digraph DependencyGraph {",
                "  rankdir=LR;",
                "  node [shape=box, style=filled, fillcolor=lightgray];",
                "  edge [arrowhead=vee];"
            };

            var dependencies = new HashSet<string>();

            foreach (var type in types)
            {
                // Add node
                graph.Add($"  \"{type.Name}\" [label=\"{type.Name}\"];");

                // Add dependencies from constructor parameters
                var constructors = type.GetConstructors();
                foreach (var ctor in constructors)
                {
                    foreach (var param in ctor.GetParameters())
                    {
                        var dep = $"  \"{param.ParameterType.Name}\" -> \"{type.Name}\"";
                        if (!dependencies.Contains(dep))
                        {
                            dependencies.Add(dep);
                            graph.Add(dep);
                        }
                    }
                }

                // Add dependencies from property types
                foreach (var prop in type.GetProperties())
                {
                    if (!prop.PropertyType.IsPrimitive && prop.PropertyType != typeof(string))
                    {
                        var dep = $"  \"{prop.PropertyType.Name}\" -> \"{type.Name}\" [style=dotted]";
                        if (!dependencies.Contains(dep))
                        {
                            dependencies.Add(dep);
                            graph.Add(dep);
                        }
                    }
                }
            }

            graph.Add("}");
            return string.Join("\n", graph);
        }
    }
}