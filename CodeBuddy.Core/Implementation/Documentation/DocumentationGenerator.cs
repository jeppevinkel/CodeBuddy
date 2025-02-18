using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Generates comprehensive documentation for the CodeBuddy codebase
    /// </summary>
    public class DocumentationGenerator : IDocumentationGenerator
    {
        private readonly IConfigurationManager _configManager;
        private readonly IPluginManager _pluginManager;
        private readonly IFileOperations _fileOps;
        private readonly DiagramGenerator _diagramGenerator;
        private readonly UsageExampleGenerator _exampleGenerator;
        private readonly XmlDocumentationParser _xmlParser;
        private readonly CodeExampleValidator _exampleValidator;
        private readonly DocumentationAnalyzer _docAnalyzer;
        private readonly CrossReferenceGenerator _crossRefGenerator;
        private readonly DocumentationVersionManager _versionManager;
        private readonly TemplateManager _templateManager;

        public DocumentationGenerator(
            IConfigurationManager configManager,
            IPluginManager pluginManager,
            IFileOperations fileOps,
            ITemplateManager templateManager = null)
        {
            _configManager = configManager;
            _pluginManager = pluginManager;
            _fileOps = fileOps;
            _diagramGenerator = new DiagramGenerator();
            _exampleGenerator = new UsageExampleGenerator(pluginManager, configManager, fileOps);
            _xmlParser = new XmlDocumentationParser();
            _exampleValidator = new CodeExampleValidator();
            _docAnalyzer = new DocumentationAnalyzer();
            _crossRefGenerator = new CrossReferenceGenerator();
            _versionManager = new DocumentationVersionManager(fileOps);
            _templateManager = templateManager ?? new TemplateManager(fileOps);
        }

        /// <summary>
        /// Generates complete API documentation for the codebase
        /// </summary>
        public async Task<DocumentationResult> GenerateApiDocumentationAsync()
        {
            var result = new DocumentationResult();
            
            try
            {
                // Get all assemblies in the codebase
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.FullName.StartsWith("CodeBuddy"));

                foreach (var assembly in assemblies)
                {
                    var types = assembly.GetTypes()
                        .Where(t => t.IsPublic);

                    foreach (var type in types)
                    {
                        var typeDoc = new TypeDocumentation
                        {
                            Name = type.Name,
                            Namespace = type.Namespace,
                            Description = GetTypeDescription(type),
                            Methods = GetMethodDocumentation(type),
                            Properties = GetPropertyDocumentation(type),
                            Interfaces = type.GetInterfaces().Select(i => i.Name).ToList()
                        };

                        result.Types.Add(typeDoc);
                    }
                }

                // Generate markdown files
                // Generate class diagrams and dependency graphs
                var types = assemblies.SelectMany(a => a.GetTypes().Where(t => t.IsPublic));
                var classDiagram = _diagramGenerator.GenerateClassDiagram(types);
                var dependencyGraph = _diagramGenerator.GenerateDependencyGraph(types);
                
                await _fileOps.WriteFileAsync("docs/diagrams/classes.puml", classDiagram);
                await _fileOps.WriteFileAsync("docs/diagrams/dependencies.dot", dependencyGraph);

                // Generate usage examples
                var validationExamples = await _exampleGenerator.GenerateValidationExamplesAsync();
                var pluginExamples = await _exampleGenerator.GeneratePluginExamplesAsync();
                var errorHandlingExamples = await _exampleGenerator.GenerateErrorHandlingExamplesAsync();

                result.Examples = new List<CodeExample>();
                result.Examples.AddRange(validationExamples);
                result.Examples.AddRange(pluginExamples);
                result.Examples.AddRange(errorHandlingExamples);

                await GenerateMarkdownFiles(result);
                await GenerateTypeScriptDefinitions(result);
                
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Generates documentation for the plugin system
        /// </summary>
        public async Task<DocumentationResult> GeneratePluginDocumentationAsync()
        {
            var result = new DocumentationResult();

            try
            {
                var plugins = await _pluginManager.GetAvailablePluginsAsync();
                var pluginDocs = new List<PluginDocumentation>();

                foreach (var plugin in plugins)
                {
                    var pluginDoc = new PluginDocumentation
                    {
                        Name = plugin.Name,
                        Description = plugin.Description,
                        Version = plugin.Version,
                        Dependencies = plugin.Dependencies,
                        Configuration = await _configManager.GetPluginConfigurationAsync(plugin.Name),
                        Interfaces = GetPluginInterfaces(plugin),
                        Examples = GetPluginExamples(plugin)
                    };

                    pluginDocs.Add(pluginDoc);
                }

                result.Plugins = pluginDocs;
                result.Success = true;

                // Generate plugin documentation markdown
                await GeneratePluginMarkdownFiles(result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Generates documentation for the validation pipeline
        /// </summary>
        public async Task<DocumentationResult> GenerateValidationDocumentationAsync()
        {
            var result = new DocumentationResult();

            try
            {
                var validationDoc = new ValidationDocumentation
                {
                    Components = GetValidationComponents(),
                    Pipeline = GetPipelineStages(),
                    ErrorHandling = GetErrorHandlingPatterns(),
                    Performance = GetPerformanceConsiderations(),
                    BestPractices = GetValidationBestPractices()
                };

                result.Validation = validationDoc;
                result.Success = true;

                // Generate validation documentation markdown
                await GenerateValidationMarkdownFiles(result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        private string GetTypeDescription(Type type)
        {
            var xmlDoc = _xmlParser.GetTypeDocumentation(type);
            if (!string.IsNullOrEmpty(xmlDoc))
                return xmlDoc;

            var descAttr = type.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            return descAttr?.Description ?? string.Empty;
        }

        public async Task<List<CodeExample>> ExtractCodeExamplesAsync()
        {
            var examples = new List<CodeExample>();

            // Extract examples from unit tests
            var testAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName.Contains(".Tests"));

            foreach (var assembly in testAssemblies)
            {
                var testTypes = assembly.GetTypes()
                    .Where(t => t.GetCustomAttributes<TestClassAttribute>().Any());

                foreach (var type in testTypes)
                {
                    var testMethods = type.GetMethods()
                        .Where(m => m.GetCustomAttributes<TestMethodAttribute>().Any());

                    foreach (var method in testMethods)
                    {
                        var example = await _exampleGenerator.ExtractExampleFromTestMethod(method);
                        if (example != null)
                        {
                            examples.Add(example);
                        }
                    }
                }
            }

            // Extract examples from XML documentation
            examples.AddRange(await _xmlParser.ExtractCodeExamplesFromXmlDocs());

            // Validate examples
            foreach (var example in examples)
            {
                await _exampleValidator.ValidateExample(example);
            }

            return examples;
        }

        public async Task<DocumentationValidationResult> ValidateDocumentationAsync()
        {
            var result = new DocumentationValidationResult();
            
            // Analyze XML documentation coverage
            var coverageResult = await AnalyzeDocumentationCoverageAsync();
            result.Coverage = coverageResult.Coverage;

            // Validate code examples
            var examples = await ExtractCodeExamplesAsync();
            foreach (var example in examples)
            {
                var validation = await _exampleValidator.ValidateExample(example);
                if (!validation.IsValid)
                {
                    result.Issues.AddRange(validation.Issues.Select(i => new DocumentationIssue
                    {
                        Component = example.Title,
                        IssueType = "InvalidExample",
                        Description = i,
                        Severity = IssueSeverity.Error
                    }));
                }
            }

            // Validate cross-references
            var crossRefs = await GenerateCrossReferencesAsync();
            foreach (var issue in crossRefs.Issues)
            {
                result.Issues.Add(new DocumentationIssue
                {
                    Component = issue.Component,
                    IssueType = "InvalidCrossReference",
                    Description = issue.Description,
                    Severity = IssueSeverity.Warning
                });
            }

            // Add recommendations
            result.Recommendations.AddRange(_docAnalyzer.GenerateRecommendations(result.Issues));

            result.IsValid = !result.Issues.Any(i => i.Severity == IssueSeverity.Error);
            return result;
        }

        public async Task<CrossReferenceResult> GenerateCrossReferencesAsync()
        {
            return await _crossRefGenerator.GenerateCrossReferences(AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName.StartsWith("CodeBuddy")));
        }

        public async Task<DocumentationCoverageResult> AnalyzeDocumentationCoverageAsync()
        {
            return await _docAnalyzer.AnalyzeCoverage(AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName.StartsWith("CodeBuddy")));
        }

        public async Task<ResourcePatternDocumentation> GenerateResourcePatternsAsync()
        {
            var result = new ResourcePatternDocumentation();

            // Generate common resource management patterns
            result.Patterns.AddRange(await GenerateResourcePatterns());

            // Extract real usage examples
            result.Examples.AddRange(await ExtractResourceUsageExamples());

            // Compile best practices
            result.BestPractices.AddRange(GenerateResourceBestPractices());

            return result;
        }

        private async Task<List<ResourcePattern>> GenerateResourcePatterns()
        {
            var patterns = new List<ResourcePattern>();
            var resourceClasses = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName.StartsWith("CodeBuddy"))
                .SelectMany(a => a.GetTypes())
                .Where(t => t.Name.Contains("Resource") || t.GetInterfaces().Any(i => i.Name.Contains("Resource")));

            foreach (var resourceClass in resourceClasses)
            {
                var pattern = new ResourcePattern
                {
                    Name = resourceClass.Name,
                    Description = GetTypeDescription(resourceClass),
                    UseCase = _xmlParser.GetResourceUseCase(resourceClass),
                    Benefits = _xmlParser.GetResourceBenefits(resourceClass),
                    Considerations = _xmlParser.GetResourceConsiderations(resourceClass),
                    Examples = await _exampleGenerator.GenerateResourceExamples(resourceClass)
                };
                
                patterns.Add(pattern);
            }

            return patterns;
        }

        private async Task<List<ResourceUsageExample>> ExtractResourceUsageExamples()
        {
            return await _exampleGenerator.ExtractResourceUsageExamples();
        }

        private List<ResourceBestPractice> GenerateResourceBestPractices()
        {
            return _docAnalyzer.GenerateResourceBestPractices();
        }

        private List<MethodDocumentation> GetMethodDocumentation(Type type)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => !m.IsSpecialName); // Filter out property accessors

            return methods.Select(m =>
            {
                var doc = new MethodDocumentation
                {
                    Name = m.Name,
                    ReturnType = GetFullTypeName(m.ReturnType),
                    Description = GetMethodDescription(m),
                    Parameters = GetParameterDocumentation(m)
                };

                // Add XML documentation details
                var xmlDoc = _xmlParser.GetMethodDocumentation(m);
                if (xmlDoc != null)
                {
                    doc.Description = xmlDoc.Summary;
                    doc.Parameters.ForEach(p =>
                    {
                        var paramDoc = xmlDoc.Parameters.FirstOrDefault(xp => xp.Name == p.Name);
                        if (paramDoc != null)
                        {
                            p.Description = paramDoc.Description;
                        }
                    });
                }

                // Add cross-references
                doc.References = _crossRefGenerator.FindMethodReferences(m)
                    .Select(r => new MethodReference
                    {
                        ReferencedMethod = r.TargetMethod,
                        ReferenceType = r.Type,
                        Description = r.Description
                    }).ToList();

                // Add usage examples
                doc.Examples = _exampleGenerator.FindMethodExamples(m);

                // Add test coverage info
                doc.TestMethods = FindRelatedTestMethods(m)
                    .Select(t => new TestMethodReference
                    {
                        TestClass = t.DeclaringType.Name,
                        TestMethod = t.Name,
                        TestDescription = GetMethodDescription(t)
                    }).ToList();

                return doc;
            }).ToList();
        }

        private string GetFullTypeName(Type type)
        {
            if (type.IsGenericType)
            {
                var genericArgs = type.GetGenericArguments()
                    .Select(GetFullTypeName);
                return $"{type.Name.Split('`')[0]}<{string.Join(", ", genericArgs)}>";
            }
            return type.Name;
        }

        private List<ParameterDocumentation> GetParameterDocumentation(MethodInfo method)
        {
            return method.GetParameters().Select(p => new ParameterDocumentation
            {
                Name = p.Name,
                Type = GetFullTypeName(p.ParameterType),
                Description = GetParameterDescription(method, p),
                IsOptional = p.IsOptional,
                DefaultValue = p.IsOptional ? p.DefaultValue?.ToString() : null,
                Attributes = p.GetCustomAttributes()
                    .Select(a => a.GetType().Name.Replace("Attribute", ""))
                    .ToList()
            }).ToList();
        }

        private IEnumerable<MethodInfo> FindRelatedTestMethods(MethodInfo method)
        {
            var testAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName.Contains(".Tests"));

            return testAssemblies.SelectMany(a => a.GetTypes())
                .Where(t => t.GetCustomAttributes<TestClassAttribute>().Any())
                .SelectMany(t => t.GetMethods())
                .Where(m => m.GetCustomAttributes<TestMethodAttribute>().Any()
                    && m.Name.Contains(method.Name));
        }

        private List<PropertyDocumentation> GetPropertyDocumentation(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Select(p => new PropertyDocumentation
                {
                    Name = p.Name,
                    Type = p.PropertyType.Name,
                    Description = GetPropertyDescription(p)
                }).ToList();
        }

        private string GetMethodDescription(MethodInfo method)
        {
            var xmlDoc = method.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            return xmlDoc?.Description ?? string.Empty;
        }

        private string GetParameterDescription(MethodInfo method, ParameterInfo parameter)
        {
            // Extract parameter documentation from XML comments
            return string.Empty; // TODO: Implement XML comment parsing
        }

        private string GetPropertyDescription(PropertyInfo property)
        {
            var xmlDoc = property.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            return xmlDoc?.Description ?? string.Empty;
        }

        private List<PluginInterface> GetPluginInterfaces(IPlugin plugin)
        {
            // Extract interface information from plugin
            return new List<PluginInterface>();
        }

        private List<CodeExample> GetPluginExamples(IPlugin plugin)
        {
            // Extract example code for plugin usage
            return new List<CodeExample>();
        }

        private List<ValidationComponent> GetValidationComponents()
        {
            // Extract validation component documentation
            return new List<ValidationComponent>();
        }

        private List<PipelineStage> GetPipelineStages()
        {
            // Document validation pipeline stages
            return new List<PipelineStage>();
        }

        private List<ErrorPattern> GetErrorHandlingPatterns()
        {
            // Document error handling patterns
            return new List<ErrorPattern>();
        }

        private List<PerformanceConsideration> GetPerformanceConsiderations()
        {
            // Document performance considerations
            return new List<PerformanceConsideration>();
        }

        private List<BestPractice> GetValidationBestPractices()
        {
            // Document validation best practices
            return new List<BestPractice>();
        }

        private async Task GenerateMarkdownFiles(DocumentationResult result)
        {
            // Create version for new documentation
            var version = await _versionManager.CreateVersionAsync(
                result.Version ?? DateTime.UtcNow.ToString("yyyyMMdd.HHmmss"),
                "Generated documentation update");

            // Generate main documentation files
            await _fileOps.WriteFileAsync("docs/README.md", await GenerateMainReadmeAsync(result));
            await _fileOps.WriteFileAsync("docs/ARCHITECTURE.md", await GenerateArchitectureDocAsync(result));
            await _fileOps.WriteFileAsync("docs/CONTRIBUTING.md", await GenerateContributingGuideAsync());

            // Generate API documentation
            await _fileOps.WriteFileAsync("docs/api/overview.md", GenerateApiOverviewMarkdown(result));
            foreach (var type in result.Types)
            {
                var path = $"docs/api/{type.Namespace?.Replace(".", "/")}/{type.Name}.md";
                await _fileOps.EnsureDirectoryExistsAsync(Path.GetDirectoryName(path));
                await _fileOps.WriteFileAsync(path, GenerateTypeMarkdown(type));
            }

            // Generate conceptual documentation
            await _fileOps.WriteFileAsync("docs/concepts/validation-pipeline.md", GenerateValidationPipelineDoc(result));
            await _fileOps.WriteFileAsync("docs/concepts/plugin-system.md", GeneratePluginSystemDoc(result));
            await _fileOps.WriteFileAsync("docs/concepts/resource-management.md", GenerateResourceManagementDoc(result));

            // Generate architecture decision records
            var adrTemplate = await _templateManager.GetTemplateAsync("adr");
            foreach (var decision in result.ArchitectureDecisions)
            {
                var adrPath = $"docs/adr/{decision.Date:yyyyMMdd}-{decision.Title.ToLower().Replace(" ", "-")}.md";
                var adrContent = adrTemplate.Replace("{{title}}", decision.Title)
                                         .Replace("{{date}}", decision.Date.ToString("yyyy-MM-dd"))
                                         .Replace("{{status}}", decision.Status)
                                         .Replace("{{context}}", decision.Context)
                                         .Replace("{{decision}}", decision.Decision)
                                         .Replace("{{consequences}}", decision.Consequences);
                await _fileOps.WriteFileAsync(adrPath, adrContent);
            }

            // Generate search index
            var searchIndex = GenerateSearchIndex(result);
            await _fileOps.WriteFileAsync("docs/search-index.json", 
                System.Text.Json.JsonSerializer.Serialize(searchIndex, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            // Create documentation map
            var docMap = new DocumentationMap
            {
                Version = version.Version,
                Generated = DateTime.UtcNow,
                Categories = new Dictionary<string, List<DocFile>>
                {
                    ["API"] = result.Types.Select(t => new DocFile 
                    { 
                        Path = $"api/{t.Namespace?.Replace(".", "/")}/{t.Name}.md",
                        Title = t.Name,
                        Description = t.Description
                    }).ToList(),
                    ["Concepts"] = new List<DocFile>
                    {
                        new DocFile { Path = "concepts/validation-pipeline.md", Title = "Validation Pipeline" },
                        new DocFile { Path = "concepts/plugin-system.md", Title = "Plugin System" },
                        new DocFile { Path = "concepts/resource-management.md", Title = "Resource Management" }
                    },
                    ["Architecture"] = result.ArchitectureDecisions.Select(d => new DocFile
                    {
                        Path = $"adr/{d.Date:yyyyMMdd}-{d.Title.ToLower().Replace(" ", "-")}.md",
                        Title = d.Title,
                        Description = d.Context
                    }).ToList()
                }
            };

            await _fileOps.WriteFileAsync("docs/doc-map.json",
                System.Text.Json.JsonSerializer.Serialize(docMap, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            // Generate search index
            await GenerateSearchIndex(result);
        }

        private async Task GenerateSearchIndex(DocumentationResult result)
        {
            var searchIndex = new List<SearchIndexEntry>();
            
            // Index API types
            foreach (var type in result.Types)
            {
                // Index type
                searchIndex.Add(new SearchIndexEntry
                {
                    Title = type.Name,
                    Path = $"api/{type.Namespace?.Replace(".", "/")}/{type.Name}.md",
                    Content = type.Description,
                    Category = "API",
                    Type = "Type"
                });
                
                // Index methods
                foreach (var method in type.Methods ?? Enumerable.Empty<MethodDocumentation>())
                {
                    searchIndex.Add(new SearchIndexEntry
                    {
                        Title = $"{type.Name}.{method.Name}",
                        Path = $"api/{type.Namespace?.Replace(".", "/")}/{type.Name}.md#{method.Name.ToLower()}",
                        Content = method.Description,
                        Category = "API",
                        Type = "Method",
                        Parent = type.Name
                    });
                }
                
                // Index properties
                foreach (var prop in type.Properties ?? Enumerable.Empty<PropertyDocumentation>())
                {
                    searchIndex.Add(new SearchIndexEntry
                    {
                        Title = $"{type.Name}.{prop.Name}",
                        Path = $"api/{type.Namespace?.Replace(".", "/")}/{type.Name}.md#{prop.Name.ToLower()}",
                        Content = prop.Description,
                        Category = "API",
                        Type = "Property",
                        Parent = type.Name
                    });
                }
            }
            
            // Index plugins
            foreach (var plugin in result.Plugins ?? Enumerable.Empty<PluginDocumentation>())
            {
                searchIndex.Add(new SearchIndexEntry
                {
                    Title = plugin.Name,
                    Path = $"plugins/{plugin.Name}.md",
                    Content = plugin.Description,
                    Category = "Plugins",
                    Type = "Plugin"
                });
            }
            
            // Index validation documentation
            if (result.Validation != null)
            {
                // Index components
                foreach (var component in result.Validation.Components ?? Enumerable.Empty<ValidationComponent>())
                {
                    searchIndex.Add(new SearchIndexEntry
                    {
                        Title = component.Name,
                        Path = "validation/components.md#" + component.Name.ToLower().Replace(" ", "-"),
                        Content = component.Description,
                        Category = "Validation",
                        Type = "Component"
                    });
                }
                
                // Index pipeline stages
                foreach (var stage in result.Validation.Pipeline ?? Enumerable.Empty<PipelineStage>())
                {
                    searchIndex.Add(new SearchIndexEntry
                    {
                        Title = stage.Name,
                        Path = "validation/pipeline.md#" + stage.Name.ToLower().Replace(" ", "-"),
                        Content = stage.Description,
                        Category = "Validation",
                        Type = "Pipeline Stage"
                    });
                }
            }
            
            // Index architecture decisions
            foreach (var decision in result.ArchitectureDecisions ?? Enumerable.Empty<ArchitectureDecision>())
            {
                searchIndex.Add(new SearchIndexEntry
                {
                    Title = decision.Title,
                    Path = $"adr/{decision.Date:yyyyMMdd}-{decision.Title.ToLower().Replace(" ", "-")}.md",
                    Content = decision.Context,
                    Category = "Architecture",
                    Type = "Decision Record"
                });
            }
            
            // Write search index
            await _fileOps.WriteFileAsync("docs/search-index.json",
                System.Text.Json.JsonSerializer.Serialize(searchIndex, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        private async Task GeneratePluginMarkdownFiles(DocumentationResult result)
        {
            // Generate plugin system documentation
            await _fileOps.WriteFileAsync("docs/plugins/overview.md", GeneratePluginOverviewMarkdown(result));
            foreach (var plugin in result.Plugins)
            {
                await _fileOps.WriteFileAsync(
                    $"docs/plugins/{plugin.Name}.md",
                    GeneratePluginMarkdown(plugin));
            }
        }

        private async Task GenerateValidationMarkdownFiles(DocumentationResult result)
        {
            // Generate validation system documentation
            await _fileOps.WriteFileAsync("docs/validation/overview.md", GenerateValidationOverviewMarkdown(result));
            await _fileOps.WriteFileAsync("docs/validation/components.md", GenerateValidationComponentsMarkdown(result));
            await _fileOps.WriteFileAsync("docs/validation/pipeline.md", GenerateValidationPipelineMarkdown(result));
            await _fileOps.WriteFileAsync("docs/validation/error-handling.md", GenerateErrorHandlingMarkdown(result));
            await _fileOps.WriteFileAsync("docs/validation/performance.md", GeneratePerformanceMarkdown(result));
        }

        private async Task GenerateTypeScriptDefinitions(DocumentationResult result)
        {
            // Generate TypeScript definition files
            var typeScriptDefs = GenerateTypeScriptDefinitions(result.Types);
            await _fileOps.WriteFileAsync("docs/typescript/codebuddy.d.ts", typeScriptDefs);
        }

        private string GenerateApiOverviewMarkdown(DocumentationResult result)
        {
            var builder = new System.Text.StringBuilder();
            
            builder.AppendLine("# API Documentation");
            builder.AppendLine();
            builder.AppendLine("## Overview");
            builder.AppendLine();
            builder.AppendLine("This section contains comprehensive documentation for the CodeBuddy API.");
            builder.AppendLine();
            
            // Add namespace summary
            var namespaces = result.Types
                .GroupBy(t => t.Namespace)
                .OrderBy(g => g.Key);

            builder.AppendLine("## Namespaces");
            builder.AppendLine();
            
            foreach (var ns in namespaces)
            {
                builder.AppendLine($"### {ns.Key}");
                builder.AppendLine();
                
                // List types in namespace
                foreach (var type in ns.OrderBy(t => t.Name))
                {
                    builder.AppendLine($"- [{type.Name}]({type.Name}.md): {type.Description}");
                }
                builder.AppendLine();
            }

            // Add quick links to common types
            builder.AppendLine("## Common Types");
            builder.AppendLine();
            var commonTypes = result.Types
                .Where(t => t.Name.Contains("Validator") || 
                           t.Name.Contains("Manager") ||
                           t.Name.Contains("Service"))
                .OrderBy(t => t.Name);

            foreach (var type in commonTypes)
            {
                builder.AppendLine($"- [{type.Name}]({type.Namespace?.Replace(".", "/")}/{type.Name}.md)");
            }

            return builder.ToString();
        }

        private string GenerateTypeMarkdown(TypeDocumentation type)
        {
            var builder = new System.Text.StringBuilder();
            
            // Header
            builder.AppendLine($"# {type.Name}");
            builder.AppendLine();
            builder.AppendLine($"**Namespace:** {type.Namespace}");
            builder.AppendLine();
            
            // Description
            if (!string.IsNullOrEmpty(type.Description))
            {
                builder.AppendLine(type.Description);
                builder.AppendLine();
            }

            // Inheritance
            if (type.Interfaces?.Any() == true)
            {
                builder.AppendLine("## Implements");
                builder.AppendLine();
                foreach (var iface in type.Interfaces)
                {
                    builder.AppendLine($"- {iface}");
                }
                builder.AppendLine();
            }

            // Properties
            if (type.Properties?.Any() == true)
            {
                builder.AppendLine("## Properties");
                builder.AppendLine();
                builder.AppendLine("| Name | Type | Description |");
                builder.AppendLine("|------|------|-------------|");
                foreach (var prop in type.Properties)
                {
                    builder.AppendLine($"| {prop.Name} | {prop.Type} | {prop.Description} |");
                }
                builder.AppendLine();
            }

            // Methods
            if (type.Methods?.Any() == true)
            {
                builder.AppendLine("## Methods");
                builder.AppendLine();
                
                foreach (var method in type.Methods)
                {
                    builder.AppendLine($"### {method.Name}");
                    builder.AppendLine();
                    
                    if (!string.IsNullOrEmpty(method.Description))
                    {
                        builder.AppendLine(method.Description);
                        builder.AppendLine();
                    }

                    // Parameters
                    if (method.Parameters?.Any() == true)
                    {
                        builder.AppendLine("#### Parameters");
                        builder.AppendLine();
                        builder.AppendLine("| Name | Type | Description |");
                        builder.AppendLine("|------|------|-------------|");
                        foreach (var param in method.Parameters)
                        {
                            builder.AppendLine($"| {param.Name} | {param.Type} | {param.Description} |");
                        }
                        builder.AppendLine();
                    }

                    // Return type
                    builder.AppendLine("#### Returns");
                    builder.AppendLine();
                    builder.AppendLine($"{method.ReturnType}");
                    builder.AppendLine();

                    // Examples
                    if (method.Examples?.Any() == true)
                    {
                        builder.AppendLine("#### Examples");
                        builder.AppendLine();
                        foreach (var example in method.Examples)
                        {
                            builder.AppendLine($"```{example.Language}");
                            builder.AppendLine(example.Code);
                            builder.AppendLine("```");
                            builder.AppendLine();
                        }
                    }
                }
            }

            return builder.ToString();
        }

        private string GeneratePluginOverviewMarkdown(DocumentationResult result)
        {
            var builder = new System.Text.StringBuilder();
            
            builder.AppendLine("# Plugin System Overview");
            builder.AppendLine();
            builder.AppendLine("## Introduction");
            builder.AppendLine();
            builder.AppendLine("The CodeBuddy plugin system allows extending the core functionality through custom plugins.");
            builder.AppendLine("This document provides an overview of the plugin architecture and guidelines for plugin development.");
            builder.AppendLine();
            
            // Plugin Architecture
            builder.AppendLine("## Plugin Architecture");
            builder.AppendLine();
            builder.AppendLine("Plugins in CodeBuddy follow a modular architecture:");
            builder.AppendLine();
            builder.AppendLine("1. Each plugin is a separate assembly that implements the `IPlugin` interface");
            builder.AppendLine("2. Plugins are loaded dynamically at runtime");
            builder.AppendLine("3. Plugin lifecycle is managed by the `PluginManager`");
            builder.AppendLine("4. Plugins can define their own configuration schema");
            builder.AppendLine();
            
            // Available Plugins
            if (result.Plugins?.Any() == true)
            {
                builder.AppendLine("## Available Plugins");
                builder.AppendLine();
                builder.AppendLine("| Plugin | Version | Description |");
                builder.AppendLine("|--------|----------|-------------|");
                foreach (var plugin in result.Plugins)
                {
                    builder.AppendLine($"| [{plugin.Name}]({plugin.Name}.md) | {plugin.Version} | {plugin.Description} |");
                }
                builder.AppendLine();
            }
            
            // Development Guide
            builder.AppendLine("## Plugin Development Guide");
            builder.AppendLine();
            builder.AppendLine("### Getting Started");
            builder.AppendLine();
            builder.AppendLine("1. Create a new class library project");
            builder.AppendLine("2. Add a reference to CodeBuddy.Core");
            builder.AppendLine("3. Implement the `IPlugin` interface");
            builder.AppendLine("4. Define plugin configuration (optional)");
            builder.AppendLine();
            
            builder.AppendLine("### Example Plugin");
            builder.AppendLine();
            builder.AppendLine("```csharp");
            builder.AppendLine("public class ExamplePlugin : IPlugin");
            builder.AppendLine("{");
            builder.AppendLine("    public string Name => \"ExamplePlugin\";");
            builder.AppendLine("    public string Version => \"1.0.0\";");
            builder.AppendLine();
            builder.AppendLine("    public Task InitializeAsync(IPluginContext context)");
            builder.AppendLine("    {");
            builder.AppendLine("        // Plugin initialization code");
            builder.AppendLine("        return Task.CompletedTask;");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine("```");
            builder.AppendLine();
            
            // Best Practices
            builder.AppendLine("## Best Practices");
            builder.AppendLine();
            builder.AppendLine("1. Follow semantic versioning");
            builder.AppendLine("2. Implement proper error handling");
            builder.AppendLine("3. Use dependency injection");
            builder.AppendLine("4. Include XML documentation");
            builder.AppendLine("5. Write unit tests");
            builder.AppendLine();
            
            return builder.ToString();
        }

        private string GeneratePluginMarkdown(PluginDocumentation plugin)
        {
            var builder = new System.Text.StringBuilder();
            
            // Header
            builder.AppendLine($"# {plugin.Name}");
            builder.AppendLine();
            builder.AppendLine($"Version: {plugin.Version}");
            builder.AppendLine();
            
            // Description
            builder.AppendLine("## Overview");
            builder.AppendLine();
            builder.AppendLine(plugin.Description);
            builder.AppendLine();
            
            // Dependencies
            if (plugin.Dependencies?.Any() == true)
            {
                builder.AppendLine("## Dependencies");
                builder.AppendLine();
                foreach (var dep in plugin.Dependencies)
                {
                    builder.AppendLine($"- {dep}");
                }
                builder.AppendLine();
            }
            
            // Configuration
            if (plugin.Configuration != null)
            {
                builder.AppendLine("## Configuration");
                builder.AppendLine();
                builder.AppendLine("```json");
                builder.AppendLine(System.Text.Json.JsonSerializer.Serialize(plugin.Configuration, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                builder.AppendLine("```");
                builder.AppendLine();
            }
            
            // Interfaces
            if (plugin.Interfaces?.Any() == true)
            {
                builder.AppendLine("## Interfaces");
                builder.AppendLine();
                foreach (var iface in plugin.Interfaces)
                {
                    builder.AppendLine($"### {iface.Name}");
                    builder.AppendLine();
                    builder.AppendLine(iface.Description);
                    builder.AppendLine();
                    
                    if (iface.Methods?.Any() == true)
                    {
                        builder.AppendLine("#### Methods");
                        builder.AppendLine();
                        foreach (var method in iface.Methods)
                        {
                            builder.AppendLine($"##### {method.Name}");
                            builder.AppendLine();
                            builder.AppendLine(method.Description);
                            builder.AppendLine();
                        }
                    }
                }
            }
            
            // Examples
            if (plugin.Examples?.Any() == true)
            {
                builder.AppendLine("## Examples");
                builder.AppendLine();
                foreach (var example in plugin.Examples)
                {
                    builder.AppendLine($"### {example.Title}");
                    builder.AppendLine();
                    builder.AppendLine(example.Description);
                    builder.AppendLine();
                    builder.AppendLine($"```{example.Language}");
                    builder.AppendLine(example.Code);
                    builder.AppendLine("```");
                    builder.AppendLine();
                }
            }
            
            return builder.ToString();
        }

        private string GenerateValidationOverviewMarkdown(DocumentationResult result)
        {
            var builder = new System.Text.StringBuilder();
            
            builder.AppendLine("# Validation System Overview");
            builder.AppendLine();
            builder.AppendLine("## Introduction");
            builder.AppendLine();
            builder.AppendLine("The CodeBuddy validation system provides comprehensive code analysis and validation across multiple programming languages.");
            builder.AppendLine("This document provides an overview of the validation system architecture and its key components.");
            builder.AppendLine();
            
            // Core Components
            builder.AppendLine("## Core Components");
            builder.AppendLine();
            foreach (var component in result.Validation.Components)
            {
                builder.AppendLine($"### {component.Name}");
                builder.AppendLine();
                builder.AppendLine(component.Description);
                builder.AppendLine();
                
                if (component.Responsibilities?.Any() == true)
                {
                    builder.AppendLine("#### Responsibilities:");
                    builder.AppendLine();
                    foreach (var responsibility in component.Responsibilities)
                    {
                        builder.AppendLine($"- {responsibility}");
                    }
                    builder.AppendLine();
                }
            }
            
            // Pipeline Overview
            builder.AppendLine("## Validation Pipeline");
            builder.AppendLine();
            builder.AppendLine("The validation process follows a pipeline architecture with the following stages:");
            builder.AppendLine();
            foreach (var stage in result.Validation.Pipeline)
            {
                builder.AppendLine($"1. {stage.Name}");
                builder.AppendLine($"   - {stage.Description}");
            }
            
            return builder.ToString();
        }

        private string GenerateValidationComponentsMarkdown(DocumentationResult result)
        {
            var builder = new System.Text.StringBuilder();
            
            builder.AppendLine("# Validation Components");
            builder.AppendLine();
            
            foreach (var component in result.Validation.Components)
            {
                builder.AppendLine($"## {component.Name}");
                builder.AppendLine();
                builder.AppendLine(component.Description);
                builder.AppendLine();
                
                // Configuration
                if (component.Configuration != null)
                {
                    builder.AppendLine("### Configuration");
                    builder.AppendLine();
                    builder.AppendLine("```json");
                    builder.AppendLine(System.Text.Json.JsonSerializer.Serialize(component.Configuration, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    builder.AppendLine("```");
                    builder.AppendLine();
                }
                
                // Interfaces
                if (component.Interfaces?.Any() == true)
                {
                    builder.AppendLine("### Interfaces");
                    builder.AppendLine();
                    foreach (var iface in component.Interfaces)
                    {
                        builder.AppendLine($"#### {iface.Name}");
                        builder.AppendLine();
                        builder.AppendLine(iface.Description);
                        builder.AppendLine();
                    }
                }
                
                // Examples
                if (component.Examples?.Any() == true)
                {
                    builder.AppendLine("### Examples");
                    builder.AppendLine();
                    foreach (var example in component.Examples)
                    {
                        builder.AppendLine($"#### {example.Title}");
                        builder.AppendLine();
                        builder.AppendLine($"```{example.Language}");
                        builder.AppendLine(example.Code);
                        builder.AppendLine("```");
                        builder.AppendLine();
                    }
                }
            }
            
            return builder.ToString();
        }

        private string GenerateValidationPipelineMarkdown(DocumentationResult result)
        {
            var builder = new System.Text.StringBuilder();
            
            builder.AppendLine("# Validation Pipeline");
            builder.AppendLine();
            builder.AppendLine("## Overview");
            builder.AppendLine();
            builder.AppendLine("The validation pipeline processes code through multiple stages to perform comprehensive analysis and validation.");
            builder.AppendLine();
            
            // Pipeline Stages
            builder.AppendLine("## Pipeline Stages");
            builder.AppendLine();
            foreach (var stage in result.Validation.Pipeline)
            {
                builder.AppendLine($"### {stage.Name}");
                builder.AppendLine();
                builder.AppendLine(stage.Description);
                builder.AppendLine();
                
                if (stage.InputTypes?.Any() == true)
                {
                    builder.AppendLine("#### Input Types");
                    builder.AppendLine();
                    foreach (var input in stage.InputTypes)
                    {
                        builder.AppendLine($"- {input}");
                    }
                    builder.AppendLine();
                }
                
                if (stage.OutputTypes?.Any() == true)
                {
                    builder.AppendLine("#### Output Types");
                    builder.AppendLine();
                    foreach (var output in stage.OutputTypes)
                    {
                        builder.AppendLine($"- {output}");
                    }
                    builder.AppendLine();
                }
                
                if (stage.Configuration != null)
                {
                    builder.AppendLine("#### Configuration");
                    builder.AppendLine();
                    builder.AppendLine("```json");
                    builder.AppendLine(System.Text.Json.JsonSerializer.Serialize(stage.Configuration, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    builder.AppendLine("```");
                    builder.AppendLine();
                }
            }
            
            return builder.ToString();
        }

        private string GenerateErrorHandlingMarkdown(DocumentationResult result)
        {
            var builder = new System.Text.StringBuilder();
            
            builder.AppendLine("# Error Handling");
            builder.AppendLine();
            builder.AppendLine("## Overview");
            builder.AppendLine();
            builder.AppendLine("The validation system implements comprehensive error handling to ensure reliability and provide meaningful feedback.");
            builder.AppendLine();
            
            // Error Handling Patterns
            if (result.Validation.ErrorHandling?.Any() == true)
            {
                builder.AppendLine("## Error Handling Patterns");
                builder.AppendLine();
                foreach (var pattern in result.Validation.ErrorHandling)
                {
                    builder.AppendLine($"### {pattern.Name}");
                    builder.AppendLine();
                    builder.AppendLine(pattern.Description);
                    builder.AppendLine();
                    
                    if (pattern.UseCases?.Any() == true)
                    {
                        builder.AppendLine("#### Use Cases");
                        builder.AppendLine();
                        foreach (var useCase in pattern.UseCases)
                        {
                            builder.AppendLine($"- {useCase}");
                        }
                        builder.AppendLine();
                    }
                    
                    if (pattern.Example != null)
                    {
                        builder.AppendLine("#### Example");
                        builder.AppendLine();
                        builder.AppendLine($"```{pattern.Example.Language}");
                        builder.AppendLine(pattern.Example.Code);
                        builder.AppendLine("```");
                        builder.AppendLine();
                    }
                }
            }
            
            // Recovery Strategies
            builder.AppendLine("## Recovery Strategies");
            builder.AppendLine();
            builder.AppendLine("The system implements the following error recovery strategies:");
            builder.AppendLine();
            builder.AppendLine("1. Retry with exponential backoff");
            builder.AppendLine("2. Circuit breaker pattern");
            builder.AppendLine("3. Fallback mechanisms");
            builder.AppendLine("4. Graceful degradation");
            
            return builder.ToString();
        }

        private string GeneratePerformanceMarkdown(DocumentationResult result)
        {
            var builder = new System.Text.StringBuilder();
            
            builder.AppendLine("# Performance Considerations");
            builder.AppendLine();
            builder.AppendLine("## Overview");
            builder.AppendLine();
            builder.AppendLine("This document outlines key performance aspects of the validation system and provides guidance for optimal usage.");
            builder.AppendLine();
            
            // Performance Considerations
            if (result.Validation.Performance?.Any() == true)
            {
                foreach (var consideration in result.Validation.Performance)
                {
                    builder.AppendLine($"## {consideration.Category}");
                    builder.AppendLine();
                    builder.AppendLine(consideration.Description);
                    builder.AppendLine();
                    
                    if (consideration.Guidelines?.Any() == true)
                    {
                        builder.AppendLine("### Guidelines");
                        builder.AppendLine();
                        foreach (var guideline in consideration.Guidelines)
                        {
                            builder.AppendLine($"- {guideline}");
                        }
                        builder.AppendLine();
                    }
                    
                    if (consideration.Metrics?.Any() == true)
                    {
                        builder.AppendLine("### Key Metrics");
                        builder.AppendLine();
                        builder.AppendLine("| Metric | Target | Notes |");
                        builder.AppendLine("|--------|---------|-------|");
                        foreach (var metric in consideration.Metrics)
                        {
                            builder.AppendLine($"| {metric.Name} | {metric.Target} | {metric.Notes} |");
                        }
                        builder.AppendLine();
                    }
                }
            }
            
            // Optimization Tips
            builder.AppendLine("## Optimization Tips");
            builder.AppendLine();
            builder.AppendLine("1. Use appropriate caching strategies");
            builder.AppendLine("2. Implement parallel processing where possible");
            builder.AppendLine("3. Optimize resource usage");
            builder.AppendLine("4. Monitor and tune system performance");
            
            return builder.ToString();
        }

        private string GenerateTypeScriptDefinitions(List<TypeDocumentation> types)
        {
            var builder = new System.Text.StringBuilder();
            
            builder.AppendLine("// Type definitions for CodeBuddy");
            builder.AppendLine("// Generated automatically - do not edit manually");
            builder.AppendLine();
            
            // Declare namespace
            builder.AppendLine("declare namespace CodeBuddy {");
            builder.AppendLine();
            
            // Generate interfaces
            foreach (var type in types.Where(t => t.Name.StartsWith("I")))
            {
                builder.AppendLine($"    export interface {type.Name} {{");
                
                // Properties
                if (type.Properties?.Any() == true)
                {
                    foreach (var prop in type.Properties)
                    {
                        var tsType = MapCSharpTypeToTypeScript(prop.Type);
                        builder.AppendLine($"        {prop.Name}: {tsType};");
                    }
                }
                
                // Methods
                if (type.Methods?.Any() == true)
                {
                    foreach (var method in type.Methods)
                    {
                        var parameters = string.Join(", ", method.Parameters?.Select(p => 
                            $"{p.Name}: {MapCSharpTypeToTypeScript(p.Type)}") ?? Array.Empty<string>());
                            
                        var returnType = MapCSharpTypeToTypeScript(method.ReturnType);
                        builder.AppendLine($"        {method.Name}({parameters}): {returnType};");
                    }
                }
                
                builder.AppendLine("    }");
                builder.AppendLine();
            }
            
            // Generate classes
            foreach (var type in types.Where(t => !t.Name.StartsWith("I")))
            {
                var implements = type.Interfaces?.Any() == true 
                    ? $" implements {string.Join(", ", type.Interfaces)}"
                    : "";
                    
                builder.AppendLine($"    export class {type.Name}{implements} {{");
                
                // Properties
                if (type.Properties?.Any() == true)
                {
                    foreach (var prop in type.Properties)
                    {
                        var tsType = MapCSharpTypeToTypeScript(prop.Type);
                        builder.AppendLine($"        {prop.Name}: {tsType};");
                    }
                }
                
                // Constructor
                builder.AppendLine("        constructor();");
                
                // Methods
                if (type.Methods?.Any() == true)
                {
                    foreach (var method in type.Methods)
                    {
                        var parameters = string.Join(", ", method.Parameters?.Select(p => 
                            $"{p.Name}: {MapCSharpTypeToTypeScript(p.Type)}") ?? Array.Empty<string>());
                            
                        var returnType = MapCSharpTypeToTypeScript(method.ReturnType);
                        builder.AppendLine($"        {method.Name}({parameters}): {returnType};");
                    }
                }
                
                builder.AppendLine("    }");
                builder.AppendLine();
            }
            
            // Close namespace
            builder.AppendLine("}");
            
            return builder.ToString();
        }
        
        private string MapCSharpTypeToTypeScript(string csharpType)
        {
            return csharpType switch
            {
                "string" => "string",
                "int" => "number",
                "long" => "number",
                "float" => "number",
                "double" => "number",
                "decimal" => "number",
                "bool" => "boolean",
                "void" => "void",
                "object" => "any",
                "Task" => "Promise<void>",
                var t when t.StartsWith("Task<") => $"Promise<{MapCSharpTypeToTypeScript(t[5..^1])}>",
                var t when t.StartsWith("List<") => $"Array<{MapCSharpTypeToTypeScript(t[5..^1])}>",
                var t when t.StartsWith("IList<") => $"Array<{MapCSharpTypeToTypeScript(t[6..^1])}>",
                var t when t.StartsWith("IEnumerable<") => $"Array<{MapCSharpTypeToTypeScript(t[12..^1])}>",
                var t when t.StartsWith("Dictionary<") => "{ [key: string]: any }",
                var t when t.StartsWith("IDictionary<") => "{ [key: string]: any }",
                _ => csharpType
            };
        }
    }
}