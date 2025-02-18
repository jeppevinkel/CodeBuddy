using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Generates PlantUML diagrams for code documentation
    /// </summary>
    public class DiagramGenerator
    {
        /// <summary>
        /// Generates a PlantUML class diagram for the given types
        /// </summary>
        public string GenerateClassDiagram(IEnumerable<Type> types)
        {
            var sb = new StringBuilder();
            sb.AppendLine("@startuml");
            sb.AppendLine("skinparam classAttributeIconSize 0");
            sb.AppendLine("skinparam classFontStyle bold");
            sb.AppendLine();

            foreach (var type in types.OrderBy(t => t.Namespace))
            {
                // Skip compiler-generated types
                if (type.Name.Contains("<")) continue;

                if (type.IsInterface)
                {
                    GenerateInterfaceDiagram(type, sb);
                }
                else if (type.IsClass)
                {
                    GenerateClassDiagram(type, sb);
                }
                else if (type.IsEnum)
                {
                    GenerateEnumDiagram(type, sb);
                }
            }

            // Generate relationships
            foreach (var type in types)
            {
                GenerateRelationships(type, sb);
            }

            sb.AppendLine("@enduml");
            return sb.ToString();
        }

        /// <summary>
        /// Generates a PlantUML dependency graph for the given types
        /// </summary>
        public string GenerateDependencyGraph(IEnumerable<Type> types)
        {
            var sb = new StringBuilder();
            sb.AppendLine("digraph Dependencies {");
            sb.AppendLine("  rankdir=LR;");
            sb.AppendLine("  node [shape=box, style=filled, fillcolor=lightgray];");
            sb.AppendLine();

            var typesByNamespace = types
                .GroupBy(t => t.Namespace ?? "Global")
                .OrderBy(g => g.Key);

            // Generate nodes
            foreach (var ns in typesByNamespace)
            {
                sb.AppendLine($"  subgraph cluster_{SanitizeName(ns.Key)} {{");
                sb.AppendLine($"    label = \"{ns.Key}\";");
                
                foreach (var type in ns.OrderBy(t => t.Name))
                {
                    var name = GetFullTypeName(type);
                    sb.AppendLine($"    {SanitizeName(name)} [label=\"{name}\"];");
                }
                
                sb.AppendLine("  }");
            }

            // Generate edges
            foreach (var type in types)
            {
                var dependencies = GetTypeDependencies(type);
                var typeName = SanitizeName(GetFullTypeName(type));
                
                foreach (var dep in dependencies)
                {
                    var depName = SanitizeName(GetFullTypeName(dep));
                    sb.AppendLine($"  {typeName} -> {depName};");
                }
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private void GenerateClassDiagram(Type type, StringBuilder sb)
        {
            var classModifier = type.IsAbstract ? "abstract" : "class";
            sb.AppendLine($"{classModifier} {type.Name} {{");

            // Properties
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => !p.GetIndexParameters().Any());
            foreach (var prop in properties)
            {
                sb.AppendLine($"  +{prop.PropertyType.Name} {prop.Name}");
            }

            // Methods
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName); // Exclude property accessors
            foreach (var method in methods)
            {
                var parameters = string.Join(", ", method.GetParameters()
                    .Select(p => $"{p.ParameterType.Name} {p.Name}"));
                sb.AppendLine($"  +{method.ReturnType.Name} {method.Name}({parameters})");
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        private void GenerateInterfaceDiagram(Type type, StringBuilder sb)
        {
            sb.AppendLine($"interface {type.Name} {{");

            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType == type);
            foreach (var member in members)
            {
                if (member is MethodInfo method)
                {
                    var parameters = string.Join(", ", method.GetParameters()
                        .Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    sb.AppendLine($"  +{method.ReturnType.Name} {method.Name}({parameters})");
                }
                else if (member is PropertyInfo property)
                {
                    sb.AppendLine($"  +{property.PropertyType.Name} {property.Name}");
                }
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        private void GenerateEnumDiagram(Type type, StringBuilder sb)
        {
            sb.AppendLine($"enum {type.Name} {{");
            
            var values = Enum.GetNames(type);
            foreach (var value in values)
            {
                sb.AppendLine($"  {value}");
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        private void GenerateRelationships(Type type, StringBuilder sb)
        {
            if (type.BaseType != null && type.BaseType != typeof(object))
            {
                sb.AppendLine($"{type.Name} -up-|> {type.BaseType.Name}");
            }

            foreach (var iface in type.GetInterfaces())
            {
                sb.AppendLine($"{type.Name} .up.|> {iface.Name}");
            }
        }

        private string GetFullTypeName(Type type)
        {
            var ns = type.Namespace ?? "Global";
            return $"{ns}.{type.Name}";
        }

        private string SanitizeName(string name)
        {
            return name.Replace(".", "_").Replace("<", "_").Replace(">", "_");
        }

        private IEnumerable<Type> GetTypeDependencies(Type type)
        {
            var dependencies = new HashSet<Type>();

            // Base type
            if (type.BaseType != null && type.BaseType != typeof(object))
            {
                dependencies.Add(type.BaseType);
            }

            // Interfaces
            foreach (var iface in type.GetInterfaces())
            {
                dependencies.Add(iface);
            }

            // Property types
            foreach (var prop in type.GetProperties())
            {
                dependencies.Add(prop.PropertyType);
            }

            // Method return types and parameter types
            foreach (var method in type.GetMethods())
            {
                dependencies.Add(method.ReturnType);
                foreach (var param in method.GetParameters())
                {
                    dependencies.Add(param.ParameterType);
                }
            }

            return dependencies.Where(t => t.Namespace?.StartsWith("CodeBuddy") == true);
        }
    }
}