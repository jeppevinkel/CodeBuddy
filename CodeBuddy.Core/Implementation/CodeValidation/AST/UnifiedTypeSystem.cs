using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeBuddy.Core.Implementation.CodeValidation.AST
{
    /// <summary>
    /// Provides unified type mapping and validation across different programming languages
    /// </summary>
    public class UnifiedTypeSystem
    {
        private readonly Dictionary<string, Dictionary<string, UnifiedType>> _typeMap;
        private readonly Dictionary<(string, string), TypeConversionInfo> _conversionMap;

        public UnifiedTypeSystem()
        {
            _typeMap = new Dictionary<string, Dictionary<string, UnifiedType>>();
            _conversionMap = new Dictionary<(string, string), TypeConversionInfo>();
            InitializeTypeSystem();
        }

        /// <summary>
        /// Maps a language-specific type to the unified type system
        /// </summary>
        public UnifiedType MapToUnifiedType(string language, string nativeType)
        {
            return _typeMap.TryGetValue(language, out var languageTypes) &&
                   languageTypes.TryGetValue(nativeType, out var unifiedType)
                ? unifiedType
                : UnifiedType.Unknown;
        }

        /// <summary>
        /// Checks if two types from different languages are compatible
        /// </summary>
        public bool AreTypesCompatible(string sourceLanguage, string sourceType, string targetLanguage, string targetType)
        {
            var unifiedSource = MapToUnifiedType(sourceLanguage, sourceType);
            var unifiedTarget = MapToUnifiedType(targetLanguage, targetType);

            return unifiedSource.IsCompatibleWith(unifiedTarget);
        }

        /// <summary>
        /// Gets type conversion information between two language-specific types
        /// </summary>
        public TypeConversionInfo GetTypeConversion(string sourceLanguage, string sourceType, 
                                                  string targetLanguage, string targetType)
        {
            var key = (GetTypeKey(sourceLanguage, sourceType), GetTypeKey(targetLanguage, targetType));
            return _conversionMap.TryGetValue(key, out var conversion)
                ? conversion
                : null;
        }

        /// <summary>
        /// Determines if a type conversion might result in data loss
        /// </summary>
        public (bool isPossible, string warning) CanConvertWithoutLoss(string sourceLanguage, string sourceType,
                                                                      string targetLanguage, string targetType)
        {
            var conversion = GetTypeConversion(sourceLanguage, sourceType, targetLanguage, targetType);
            if (conversion == null)
            {
                return (false, "No conversion path available");
            }

            return (conversion.IsSafe, conversion.Warning);
        }

        /// <summary>
        /// Gets suggestions for type conversion when data loss is possible
        /// </summary>
        public IEnumerable<string> GetConversionSuggestions(string sourceLanguage, string sourceType,
                                                          string targetLanguage, string targetType)
        {
            var conversion = GetTypeConversion(sourceLanguage, sourceType, targetLanguage, targetType);
            return conversion?.Suggestions ?? Enumerable.Empty<string>();
        }

        private string GetTypeKey(string language, string type) => $"{language}:{type}";

        private void InitializeTypeSystem()
        {
            // Initialize basic numeric types
            RegisterNumericTypes();
            
            // Initialize string types
            RegisterStringTypes();
            
            // Initialize collection types
            RegisterCollectionTypes();
            
            // Initialize special types (nullable, optional, etc.)
            RegisterSpecialTypes();
            
            // Initialize type conversions
            RegisterTypeConversions();
        }

        private void RegisterNumericTypes()
        {
            // C# numeric types
            var csharpTypes = new Dictionary<string, UnifiedType>
            {
                ["int"] = new UnifiedType("Integer", 32, false),
                ["long"] = new UnifiedType("Integer", 64, false),
                ["float"] = new UnifiedType("Float", 32, false),
                ["double"] = new UnifiedType("Float", 64, false),
                ["decimal"] = new UnifiedType("Decimal", 128, false)
            };

            // Python numeric types
            var pythonTypes = new Dictionary<string, UnifiedType>
            {
                ["int"] = new UnifiedType("Integer", 0, false), // Python has arbitrary precision
                ["float"] = new UnifiedType("Float", 64, false)
            };

            // JavaScript numeric types
            var javascriptTypes = new Dictionary<string, UnifiedType>
            {
                ["number"] = new UnifiedType("Float", 64, false)
            };

            _typeMap["C#"] = csharpTypes;
            _typeMap["Python"] = pythonTypes;
            _typeMap["JavaScript"] = javascriptTypes;
        }

        private void RegisterStringTypes()
        {
            foreach (var language in new[] { "C#", "Python", "JavaScript" })
            {
                if (!_typeMap.ContainsKey(language))
                {
                    _typeMap[language] = new Dictionary<string, UnifiedType>();
                }

                _typeMap[language]["string"] = new UnifiedType("String", 0, false);
                if (language == "Python")
                {
                    _typeMap[language]["str"] = new UnifiedType("String", 0, false);
                }
            }
        }

        private void RegisterCollectionTypes()
        {
            // Register array types
            _typeMap["C#"]["Array"] = new UnifiedType("Array", 0, false);
            _typeMap["Python"]["list"] = new UnifiedType("Array", 0, false);
            _typeMap["JavaScript"]["Array"] = new UnifiedType("Array", 0, false);

            // Register dictionary types
            _typeMap["C#"]["Dictionary"] = new UnifiedType("Dictionary", 0, false);
            _typeMap["Python"]["dict"] = new UnifiedType("Dictionary", 0, false);
            _typeMap["JavaScript"]["Object"] = new UnifiedType("Dictionary", 0, false);
        }

        private void RegisterSpecialTypes()
        {
            // Register nullable types
            _typeMap["C#"]["Nullable<T>"] = new UnifiedType("Optional", 0, true);
            _typeMap["TypeScript"]["T | null"] = new UnifiedType("Optional", 0, true);
            _typeMap["Python"]["Optional[T]"] = new UnifiedType("Optional", 0, true);
        }

        private void RegisterTypeConversions()
        {
            // Register numeric conversions
            RegisterNumericConversions();
            
            // Register string conversions
            RegisterStringConversions();
            
            // Register collection conversions
            RegisterCollectionConversions();
        }

        private void RegisterNumericConversions()
        {
            // C# int -> JavaScript number
            _conversionMap[("C#:int", "JavaScript:number")] = new TypeConversionInfo
            {
                IsSafe = true,
                Warning = null,
                Suggestions = new[] { "Direct conversion is safe" }
            };

            // JavaScript number -> C# int
            _conversionMap[("JavaScript:number", "C#:int")] = new TypeConversionInfo
            {
                IsSafe = false,
                Warning = "Possible loss of precision. JavaScript numbers are always 64-bit floating-point.",
                Suggestions = new[]
                {
                    "Use Math.floor() before conversion",
                    "Check if number is within Int32 range",
                    "Consider using double instead of int"
                }
            };

            // Python int -> C# int
            _conversionMap[("Python:int", "C#:int")] = new TypeConversionInfo
            {
                IsSafe = false,
                Warning = "Python integers have unlimited precision",
                Suggestions = new[]
                {
                    "Check if value is within Int32 range",
                    "Consider using long for larger numbers",
                    "Use decimal for high-precision calculations"
                }
            };
        }

        private void RegisterStringConversions()
        {
            // String conversions are generally safe between these languages
            var languages = new[] { "C#", "Python", "JavaScript" };
            foreach (var source in languages)
            {
                foreach (var target in languages)
                {
                    if (source != target)
                    {
                        var sourceType = source == "Python" ? "str" : "string";
                        var targetType = target == "Python" ? "str" : "string";
                        
                        _conversionMap[(GetTypeKey(source, sourceType), GetTypeKey(target, targetType))] =
                            new TypeConversionInfo
                            {
                                IsSafe = true,
                                Warning = null,
                                Suggestions = new[] { "Direct string conversion is safe" }
                            };
                    }
                }
            }
        }

        private void RegisterCollectionConversions()
        {
            // Array conversions
            RegisterArrayConversions();
            
            // Dictionary conversions
            RegisterDictionaryConversions();
        }

        private void RegisterArrayConversions()
        {
            var arrayTypes = new Dictionary<string, string>
            {
                ["C#"] = "Array",
                ["Python"] = "list",
                ["JavaScript"] = "Array"
            };

            foreach (var (sourceLanguage, sourceType) in arrayTypes)
            {
                foreach (var (targetLanguage, targetType) in arrayTypes)
                {
                    if (sourceLanguage != targetLanguage)
                    {
                        _conversionMap[(GetTypeKey(sourceLanguage, sourceType), 
                                      GetTypeKey(targetLanguage, targetType))] = new TypeConversionInfo
                        {
                            IsSafe = true,
                            Warning = "Element types should be checked separately",
                            Suggestions = new[]
                            {
                                "Verify element type compatibility",
                                "Consider handling null values",
                                "Check array bounds if relevant"
                            }
                        };
                    }
                }
            }
        }

        private void RegisterDictionaryConversions()
        {
            var dictTypes = new Dictionary<string, string>
            {
                ["C#"] = "Dictionary",
                ["Python"] = "dict",
                ["JavaScript"] = "Object"
            };

            foreach (var (sourceLanguage, sourceType) in dictTypes)
            {
                foreach (var (targetLanguage, targetType) in dictTypes)
                {
                    if (sourceLanguage != targetLanguage)
                    {
                        _conversionMap[(GetTypeKey(sourceLanguage, sourceType),
                                      GetTypeKey(targetLanguage, targetType))] = new TypeConversionInfo
                        {
                            IsSafe = true,
                            Warning = "Key and value types should be checked separately",
                            Suggestions = new[]
                            {
                                "Verify key type compatibility",
                                "Verify value type compatibility",
                                "Handle null values appropriately",
                                "Consider key case sensitivity"
                            }
                        };
                    }
                }
            }
        }
    }

    /// <summary>
    /// Represents a type in the unified type system
    /// </summary>
    public class UnifiedType
    {
        public string Category { get; }
        public int BitSize { get; }
        public bool IsNullable { get; }
        public static UnifiedType Unknown => new UnifiedType("Unknown", 0, false);

        public UnifiedType(string category, int bitSize, bool isNullable)
        {
            Category = category;
            BitSize = bitSize;
            IsNullable = isNullable;
        }

        public bool IsCompatibleWith(UnifiedType other)
        {
            if (this == Unknown || other == Unknown)
                return false;

            if (Category != other.Category)
                return false;

            if (BitSize > 0 && other.BitSize > 0 && BitSize > other.BitSize)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Provides information about type conversion between languages
    /// </summary>
    public class TypeConversionInfo
    {
        public bool IsSafe { get; set; }
        public string Warning { get; set; }
        public IEnumerable<string> Suggestions { get; set; }
    }
}