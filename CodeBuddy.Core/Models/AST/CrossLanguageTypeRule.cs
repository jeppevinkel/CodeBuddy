using System;
using System.Collections.Generic;
using CodeBuddy.Core.Implementation.CodeValidation.AST;

namespace CodeBuddy.Core.Models.AST
{
    /// <summary>
    /// Defines a rule for type conversion and validation between different programming languages
    /// </summary>
    public class CrossLanguageTypeRule
    {
        /// <summary>
        /// The source language type
        /// </summary>
        public string SourceType { get; set; }

        /// <summary>
        /// The target language type
        /// </summary>
        public string TargetType { get; set; }

        /// <summary>
        /// Whether the type can be implicitly converted without explicit casting
        /// </summary>
        public bool IsImplicitlyConvertible { get; set; }

        /// <summary>
        /// Custom validation logic for the type conversion
        /// </summary>
        public Func<UnifiedASTNode, UnifiedASTNode, SemanticContext, bool> ValidationLogic { get; set; }

        /// <summary>
        /// Warnings about potential issues with the type conversion
        /// </summary>
        public List<string> ConversionWarnings { get; set; }

        /// <summary>
        /// Suggestions for handling type conversion edge cases
        /// </summary>
        public List<string> ConversionSuggestions { get; set; }

        public CrossLanguageTypeRule()
        {
            ConversionWarnings = new List<string>();
            ConversionSuggestions = new List<string>();
        }

        /// <summary>
        /// Validates the type conversion between source and target nodes
        /// </summary>
        public bool ValidateConversion(UnifiedASTNode sourceNode, UnifiedASTNode targetNode,
            SemanticContext context, out List<string> warnings)
        {
            warnings = new List<string>();

            // Check basic type compatibility
            if (sourceNode.SourceLanguage == null || targetNode.SourceLanguage == null)
            {
                warnings.Add("Source or target language information missing");
                return false;
            }

            // Get type information
            var sourceTypeInfo = sourceNode.GetTypeInformation();
            var targetTypeInfo = targetNode.GetTypeInformation();

            if (sourceTypeInfo?.BaseType == null || targetTypeInfo?.BaseType == null)
            {
                warnings.Add("Source or target type information missing");
                return false;
            }

            // Check nullability compatibility
            if (sourceTypeInfo.IsNullable && !targetTypeInfo.IsNullable)
            {
                warnings.Add("Potential null reference: converting nullable type to non-nullable type");
            }

            // Check generic parameter compatibility
            if (sourceTypeInfo.GenericParameters.Count != targetTypeInfo.GenericParameters.Count)
            {
                warnings.Add("Generic parameter count mismatch");
                return false;
            }

            // Apply custom validation logic if provided
            if (ValidationLogic != null)
            {
                if (!ValidationLogic(sourceNode, targetNode, context))
                {
                    warnings.AddRange(ConversionWarnings);
                    return false;
                }
            }

            // Add any general conversion warnings
            warnings.AddRange(ConversionWarnings);

            return true;
        }

        /// <summary>
        /// Gets suggestions for handling type conversion
        /// </summary>
        public IEnumerable<string> GetConversionSuggestions(UnifiedASTNode sourceNode, UnifiedASTNode targetNode)
        {
            var suggestions = new List<string>(ConversionSuggestions);

            // Add specific suggestions based on type characteristics
            var sourceType = sourceNode.GetTypeInformation();
            var targetType = targetNode.GetTypeInformation();

            if (sourceType.IsNullable && !targetType.IsNullable)
            {
                suggestions.Add("Add null check before conversion");
                suggestions.Add("Use null coalescing operator (??) to provide default value");
            }

            if (sourceType.GenericParameters.Count > 0)
            {
                suggestions.Add("Ensure generic type parameters are compatible");
                suggestions.Add("Consider using type constraints to ensure type safety");
            }

            // Add language-specific suggestions
            switch (targetNode.SourceLanguage)
            {
                case "C#":
                    suggestions.Add("Consider using explicit type conversion methods");
                    suggestions.Add("Implement custom conversion operator if needed");
                    break;

                case "JavaScript":
                    suggestions.Add("Use type coercion carefully");
                    suggestions.Add("Consider using TypeScript for better type safety");
                    break;

                case "Python":
                    suggestions.Add("Use explicit type conversion functions");
                    suggestions.Add("Consider using type hints for better clarity");
                    break;
            }

            return suggestions;
        }
    }
}