using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Parses XML documentation comments from assemblies and source code
    /// </summary>
    public class XmlDocumentationParser
    {
        private readonly Dictionary<string, XDocument> _xmlDocs = new Dictionary<string, XDocument>();

        /// <summary>
        /// Gets XML documentation for a type
        /// </summary>
        public string GetTypeDocumentation(Type type)
        {
            var xmlDoc = LoadXmlDocumentation(type.Assembly);
            if (xmlDoc == null) return string.Empty;

            var typeName = GetXmlTypeName(type);
            var memberElement = xmlDoc.Descendants("member")
                .FirstOrDefault(m => m.Attribute("name")?.Value == typeName);

            return memberElement?.Element("summary")?.Value.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Gets XML documentation for a method
        /// </summary>
        public MethodXmlDocumentation GetMethodDocumentation(MethodInfo method)
        {
            var xmlDoc = LoadXmlDocumentation(method.DeclaringType.Assembly);
            if (xmlDoc == null) return null;

            var methodName = GetXmlMethodName(method);
            var memberElement = xmlDoc.Descendants("member")
                .FirstOrDefault(m => m.Attribute("name")?.Value == methodName);

            if (memberElement == null) return null;

            var doc = new MethodXmlDocumentation
            {
                Summary = memberElement.Element("summary")?.Value.Trim() ?? string.Empty,
                Returns = memberElement.Element("returns")?.Value.Trim() ?? string.Empty,
                Parameters = memberElement.Elements("param")
                    .Select(p => new ParameterXmlDocumentation
                    {
                        Name = p.Attribute("name")?.Value ?? string.Empty,
                        Description = p.Value.Trim()
                    })
                    .ToList()
            };

            return doc;
        }

        /// <summary>
        /// Gets resource use case documentation from XML comments
        /// </summary>
        public string GetResourceUseCase(Type type)
        {
            var xmlDoc = LoadXmlDocumentation(type.Assembly);
            if (xmlDoc == null) return string.Empty;

            var typeName = GetXmlTypeName(type);
            var memberElement = xmlDoc.Descendants("member")
                .FirstOrDefault(m => m.Attribute("name")?.Value == typeName);

            return memberElement?.Element("usecase")?.Value.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Gets resource benefits from XML comments
        /// </summary>
        public List<string> GetResourceBenefits(Type type)
        {
            var xmlDoc = LoadXmlDocumentation(type.Assembly);
            if (xmlDoc == null) return new List<string>();

            var typeName = GetXmlTypeName(type);
            var memberElement = xmlDoc.Descendants("member")
                .FirstOrDefault(m => m.Attribute("name")?.Value == typeName);

            return memberElement?.Elements("benefit")
                .Select(b => b.Value.Trim())
                .ToList() ?? new List<string>();
        }

        /// <summary>
        /// Gets resource considerations from XML comments
        /// </summary>
        public List<string> GetResourceConsiderations(Type type)
        {
            var xmlDoc = LoadXmlDocumentation(type.Assembly);
            if (xmlDoc == null) return new List<string>();

            var typeName = GetXmlTypeName(type);
            var memberElement = xmlDoc.Descendants("member")
                .FirstOrDefault(m => m.Attribute("name")?.Value == typeName);

            return memberElement?.Elements("consideration")
                .Select(c => c.Value.Trim())
                .ToList() ?? new List<string>();
        }

        /// <summary>
        /// Extracts code examples from XML documentation
        /// </summary>
        public async Task<List<CodeExample>> ExtractCodeExamplesFromXmlDocs()
        {
            var examples = new List<CodeExample>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName.StartsWith("CodeBuddy")))
            {
                var xmlDoc = LoadXmlDocumentation(assembly);
                if (xmlDoc == null) continue;

                var exampleElements = xmlDoc.Descendants("example");
                foreach (var element in exampleElements)
                {
                    var codeElement = element.Element("code");
                    if (codeElement != null)
                    {
                        examples.Add(new CodeExample
                        {
                            Title = element.Element("title")?.Value.Trim() ?? "Untitled Example",
                            Description = element.Element("description")?.Value.Trim() ?? string.Empty,
                            Code = codeElement.Value.Trim(),
                            Language = codeElement.Attribute("lang")?.Value ?? "csharp"
                        });
                    }
                }
            }

            return examples;
        }

        private XDocument LoadXmlDocumentation(Assembly assembly)
        {
            var assemblyName = assembly.GetName().Name;
            if (_xmlDocs.TryGetValue(assemblyName, out var doc))
            {
                return doc;
            }

            var xmlPath = assembly.Location.Replace(".dll", ".xml");
            if (System.IO.File.Exists(xmlPath))
            {
                try
                {
                    doc = XDocument.Load(xmlPath);
                    _xmlDocs[assemblyName] = doc;
                    return doc;
                }
                catch (Exception)
                {
                    // XML documentation file might be invalid or inaccessible
                    return null;
                }
            }

            return null;
        }

        private string GetXmlTypeName(Type type)
        {
            return $"T:{type.FullName}";
        }

        private string GetXmlMethodName(MethodInfo method)
        {
            return $"M:{method.DeclaringType.FullName}.{method.Name}";
        }
    }

    /// <summary>
    /// Represents XML documentation for a method
    /// </summary>
    public class MethodXmlDocumentation
    {
        public string Summary { get; set; }
        public string Returns { get; set; }
        public List<ParameterXmlDocumentation> Parameters { get; set; } = new List<ParameterXmlDocumentation>();
    }

    /// <summary>
    /// Represents XML documentation for a parameter
    /// </summary>
    public class ParameterXmlDocumentation
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }
}