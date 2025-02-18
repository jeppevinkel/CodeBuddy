using System.Collections.Generic;
using System.Linq;
using CodeBuddy.Core.Models.AST;

namespace CodeBuddy.Core.Implementation.CodeValidation.AST
{
    /// <summary>
    /// Registry for language-specific AST validation rules with cross-language type checking support
    /// </summary>
    public class ASTValidationRegistry
    {
        private readonly Dictionary<string, Dictionary<string, TypeInfo>> _languageTypeRules;
        private readonly Dictionary<string, List<RelationshipRule>> _relationshipRules;
        private readonly UnifiedTypeSystem _typeSystem;
        private readonly Dictionary<string, Dictionary<string, CrossLanguageTypeRule>> _crossLanguageRules;

        public ASTValidationRegistry(UnifiedTypeSystem typeSystem = null)
        {
            _languageTypeRules = new Dictionary<string, Dictionary<string, TypeInfo>>();
            _relationshipRules = new Dictionary<string, List<RelationshipRule>>();
            _crossLanguageRules = new Dictionary<string, Dictionary<string, CrossLanguageTypeRule>>();
            _typeSystem = typeSystem ?? new UnifiedTypeSystem();
            InitializeDefaultRules();
        }

        /// <summary>
        /// Registers type validation rules for a specific language
        /// </summary>
        public void RegisterLanguageTypes(string language, Dictionary<string, TypeInfo> types)
        {
            if (!_languageTypeRules.ContainsKey(language))
            {
                _languageTypeRules[language] = new Dictionary<string, TypeInfo>();
            }

            foreach (var type in types)
            {
                _languageTypeRules[language][type.Key] = type.Value;
            }
        }

        /// <summary>
        /// Registers relationship validation rules for a specific language
        /// </summary>
        public void RegisterRelationshipRules(string language, List<RelationshipRule> rules)
        {
            if (!_relationshipRules.ContainsKey(language))
            {
                _relationshipRules[language] = new List<RelationshipRule>();
            }

            _relationshipRules[language].AddRange(rules);
        }

        /// <summary>
        /// Gets type validation rules for a specific language
        /// </summary>
        public TypeInfo GetTypeRules(string language, string nodeType)
        {
            return _languageTypeRules.TryGetValue(language, out var languageRules) &&
                   languageRules.TryGetValue(nodeType, out var typeRules)
                ? typeRules
                : null;
        }

        /// <summary>
        /// Gets relationship validation rules for a specific language
        /// </summary>
        public IEnumerable<RelationshipRule> GetRelationshipRules(string language)
        {
            return _relationshipRules.TryGetValue(language, out var rules)
                ? rules
                : Enumerable.Empty<RelationshipRule>();
        }

        /// <summary>
        /// Registers a cross-language type validation rule
        /// </summary>
        public void RegisterCrossLanguageRule(string sourceLanguage, string targetLanguage, CrossLanguageTypeRule rule)
        {
            var key = GetCrossLanguageKey(sourceLanguage, targetLanguage);
            if (!_crossLanguageRules.ContainsKey(key))
            {
                _crossLanguageRules[key] = new Dictionary<string, CrossLanguageTypeRule>();
            }

            _crossLanguageRules[key][rule.SourceType] = rule;
        }

        /// <summary>
        /// Gets cross-language validation rules for the given languages
        /// </summary>
        public CrossLanguageTypeRule GetCrossLanguageRule(
            string sourceLanguage, string targetLanguage, string sourceType)
        {
            var key = GetCrossLanguageKey(sourceLanguage, targetLanguage);
            return _crossLanguageRules.TryGetValue(key, out var rules) &&
                   rules.TryGetValue(sourceType, out var rule)
                ? rule
                : null;
        }

        private string GetCrossLanguageKey(string sourceLanguage, string targetLanguage)
        {
            return $"{sourceLanguage}->{targetLanguage}";
        }

        private void InitializeDefaultRules()
        {
            InitializeLanguageRules();
            InitializeCrossLanguageRules();
        }

        private void InitializeLanguageRules()
        {
            // Initialize C# rules
            var csharpTypes = new Dictionary<string, TypeInfo>
            {
                ["Class"] = new TypeInfo
                {
                    TypeName = "Class",
                    Language = "C#",
                    ValidChildTypes = new HashSet<string> { "Method", "Property", "Field", "Constructor" },
                    AllowedProperties = new Dictionary<string, string>
                    {
                        ["Accessibility"] = "string",
                        ["IsStatic"] = "bool",
                        ["IsAbstract"] = "bool"
                    }
                },
                ["Method"] = new TypeInfo
                {
                    TypeName = "Method",
                    Language = "C#",
                    ValidChildTypes = new HashSet<string> { "Parameter", "Block" },
                    AllowedProperties = new Dictionary<string, string>
                    {
                        ["ReturnType"] = "string",
                        ["Accessibility"] = "string",
                        ["IsStatic"] = "bool",
                        ["IsAsync"] = "bool"
                    }
                }
                // Add more C# types...
            };

            var csharpRelationships = new List<RelationshipRule>
            {
                new RelationshipRule
                {
                    SourceType = "VariableReference",
                    TargetType = "VariableDeclaration",
                    ValidationType = RelationshipValidationType.Declaration,
                    ValidationLogic = (source, target, context) =>
                        context.Declarations.ContainsKey(source.Name) ||
                        context.ScopeStack.Any(s => s.LocalDeclarations.ContainsKey(source.Name))
                }
                // Add more C# relationships...
            };

            RegisterLanguageTypes("C#", csharpTypes);
            RegisterRelationshipRules("C#", csharpRelationships);

            // Initialize JavaScript rules with type coercion rules
            var jsTypes = new Dictionary<string, TypeInfo>
            {
                ["Function"] = new TypeInfo
                {
                    TypeName = "Function",
                    Language = "JavaScript",
                    ValidChildTypes = new HashSet<string> { "Parameter", "Block" },
                    AllowedProperties = new Dictionary<string, string>
                    {
                        ["IsAsync"] = "bool",
                        ["IsGenerator"] = "bool",
                        ["IsArrowFunction"] = "bool"
                    }
                }
                // Add more JavaScript types...
            };

            RegisterLanguageTypes("JavaScript", jsTypes);

            // Initialize Python rules with dynamic typing support
            var pythonTypes = new Dictionary<string, TypeInfo>
            {
                ["Function"] = new TypeInfo
                {
                    TypeName = "Function",
                    Language = "Python",
                    ValidChildTypes = new HashSet<string> { "Parameter", "Block" },
                    AllowedProperties = new Dictionary<string, string>
                    {
                        ["IsAsync"] = "bool",
                        ["Decorators"] = "string[]",
                        ["ReturnAnnotation"] = "string"
                    }
                }
                // Add more Python types...
            };

            RegisterLanguageTypes("Python", pythonTypes);
        }

        private void InitializeCrossLanguageRules()
        {
            // C# -> JavaScript type conversion rules
            RegisterCrossLanguageRules("C#", "JavaScript", new[]
            {
                new CrossLanguageTypeRule
                {
                    SourceType = "int",
                    TargetType = "number",
                    IsImplicitlyConvertible = true,
                    ValidationLogic = (source, target, context) => true, // Safe conversion
                    ConversionWarnings = new List<string>()
                },
                new CrossLanguageTypeRule
                {
                    SourceType = "decimal",
                    TargetType = "number",
                    IsImplicitlyConvertible = false,
                    ValidationLogic = (source, target, context) => true,
                    ConversionWarnings = new List<string>
                    {
                        "Possible loss of precision when converting decimal to JavaScript number"
                    }
                }
            });

            // JavaScript -> C# type conversion rules
            RegisterCrossLanguageRules("JavaScript", "C#", new[]
            {
                new CrossLanguageTypeRule
                {
                    SourceType = "number",
                    TargetType = "int",
                    IsImplicitlyConvertible = false,
                    ValidationLogic = (source, target, context) =>
                        double.TryParse(source.Properties["Value"]?.ToString() ?? "", out var value) &&
                        value >= int.MinValue && value <= int.MaxValue,
                    ConversionWarnings = new List<string>
                    {
                        "JavaScript numbers are always floating-point. Ensure value is within integer range.",
                        "Use Math.floor() or Math.round() before conversion to ensure whole number."
                    }
                }
            });

            // Python -> C# type conversion rules
            RegisterCrossLanguageRules("Python", "C#", new[]
            {
                new CrossLanguageTypeRule
                {
                    SourceType = "int",
                    TargetType = "int",
                    IsImplicitlyConvertible = false,
                    ValidationLogic = (source, target, context) => true,
                    ConversionWarnings = new List<string>
                    {
                        "Python integers have unlimited precision. Ensure value fits within C# int range."
                    }
                },
                new CrossLanguageTypeRule
                {
                    SourceType = "list",
                    TargetType = "List<T>",
                    IsImplicitlyConvertible = false,
                    ValidationLogic = (source, target, context) =>
                    {
                        // Check element type compatibility
                        var sourceElements = source.Children.Where(c => c.NodeType == "Element");
                        var targetType = target.GetTypeInformation().GenericParameters.FirstOrDefault();
                        return sourceElements.All(element =>
                        {
                            var (elementType, _) = context.TypeChecker.InferType(element, context);
                            return _typeSystem.AreTypesCompatible(
                                source.SourceLanguage, elementType,
                                target.SourceLanguage, targetType);
                        });
                    },
                    ConversionWarnings = new List<string>
                    {
                        "Ensure all list elements are of compatible types",
                        "Python lists can contain mixed types - ensure consistent typing"
                    }
                }
            });
        }

        private void RegisterCrossLanguageRules(string sourceLanguage, string targetLanguage,
            IEnumerable<CrossLanguageTypeRule> rules)
        {
            foreach (var rule in rules)
            {
                RegisterCrossLanguageRule(sourceLanguage, targetLanguage, rule);
            }
        }
    }

    /// <summary>
    /// Represents a rule for validating relationships between AST nodes
    /// </summary>
    public class RelationshipRule
    {
        public string SourceType { get; set; }
        public string TargetType { get; set; }
        public RelationshipValidationType ValidationType { get; set; }
        public System.Func<UnifiedASTNode, UnifiedASTNode, SemanticContext, bool> ValidationLogic { get; set; }
    }

    /// <summary>
    /// Types of relationships that can be validated
    /// </summary>
    public enum RelationshipValidationType
    {
        Declaration,
        Inheritance,
        Implementation,
        Usage,
        Composition
    }
}