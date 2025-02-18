using System.Collections.Generic;
using System.Linq;
using CodeBuddy.Core.Models.AST;

namespace CodeBuddy.Core.Implementation.CodeValidation.AST
{
    /// <summary>
    /// Registry for language-specific AST validation rules
    /// </summary>
    public class ASTValidationRegistry
    {
        private readonly Dictionary<string, Dictionary<string, TypeInfo>> _languageTypeRules;
        private readonly Dictionary<string, List<RelationshipRule>> _relationshipRules;

        public ASTValidationRegistry()
        {
            _languageTypeRules = new Dictionary<string, Dictionary<string, TypeInfo>>();
            _relationshipRules = new Dictionary<string, List<RelationshipRule>>();
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

        private void InitializeDefaultRules()
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

            // Initialize JavaScript rules
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

            // Initialize Python rules
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