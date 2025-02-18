using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Validates code examples against current API signatures
    /// </summary>
    public class CodeExampleSignatureValidator
    {
        private readonly Solution _solution;
        private readonly Dictionary<string, IMethodSymbol> _methodCache;
        private readonly Dictionary<string, ITypeSymbol> _typeCache;

        public CodeExampleSignatureValidator(Solution solution)
        {
            _solution = solution;
            _methodCache = new Dictionary<string, IMethodSymbol>();
            _typeCache = new Dictionary<string, ITypeSymbol>();
        }

        /// <summary>
        /// Validates code examples against current API signatures
        /// </summary>
        public async Task<List<SignatureValidationIssue>> ValidateCodeExampleAsync(string code, string context)
        {
            var issues = new List<SignatureValidationIssue>();
            
            try
            {
                // Parse the code example
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = await tree.GetRootAsync();

                // Get method calls and type references
                var methodCalls = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
                var typeRefs = root.DescendantNodes().OfType<TypeSyntax>();

                // Validate method signatures
                foreach (var methodCall in methodCalls)
                {
                    var methodName = GetFullMethodName(methodCall);
                    var actualMethod = await GetMethodSymbolAsync(methodName);
                    
                    if (actualMethod == null)
                    {
                        issues.Add(new SignatureValidationIssue
                        {
                            IssueType = SignatureIssueType.MethodNotFound,
                            Context = context,
                            Message = $"Method {methodName} not found in current API",
                            CodeLocation = methodCall.GetLocation().GetLineSpan().StartLinePosition.Line + 1
                        });
                        continue;
                    }

                    // Check parameter count and types
                    var parameters = methodCall.ArgumentList.Arguments;
                    if (parameters.Count != actualMethod.Parameters.Length)
                    {
                        issues.Add(new SignatureValidationIssue
                        {
                            IssueType = SignatureIssueType.ParameterCountMismatch,
                            Context = context,
                            Message = $"Method {methodName} expects {actualMethod.Parameters.Length} parameters but got {parameters.Count}",
                            CodeLocation = methodCall.GetLocation().GetLineSpan().StartLinePosition.Line + 1
                        });
                    }
                }

                // Validate type references
                foreach (var typeRef in typeRefs)
                {
                    var typeName = GetFullTypeName(typeRef);
                    var actualType = await GetTypeSymbolAsync(typeName);

                    if (actualType == null)
                    {
                        issues.Add(new SignatureValidationIssue
                        {
                            IssueType = SignatureIssueType.TypeNotFound,
                            Context = context,
                            Message = $"Type {typeName} not found in current API",
                            CodeLocation = typeRef.GetLocation().GetLineSpan().StartLinePosition.Line + 1
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                issues.Add(new SignatureValidationIssue
                {
                    IssueType = SignatureIssueType.ValidationError,
                    Context = context,
                    Message = $"Error validating code example: {ex.Message}"
                });
            }

            return issues;
        }

        private async Task<IMethodSymbol> GetMethodSymbolAsync(string methodName)
        {
            if (_methodCache.TryGetValue(methodName, out var cachedMethod))
                return cachedMethod;

            foreach (var project in _solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                var method = compilation.GetTypeByMetadataName(methodName)?.GetMembers()
                    .OfType<IMethodSymbol>().FirstOrDefault();
                
                if (method != null)
                {
                    _methodCache[methodName] = method;
                    return method;
                }
            }

            return null;
        }

        private async Task<ITypeSymbol> GetTypeSymbolAsync(string typeName)
        {
            if (_typeCache.TryGetValue(typeName, out var cachedType))
                return cachedType;

            foreach (var project in _solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                var type = compilation.GetTypeByMetadataName(typeName);
                
                if (type != null)
                {
                    _typeCache[typeName] = type;
                    return type;
                }
            }

            return null;
        }

        private string GetFullMethodName(InvocationExpressionSyntax methodCall)
        {
            // Extract full method name including namespace
            return methodCall.ToString();
        }

        private string GetFullTypeName(TypeSyntax typeRef)
        {
            // Extract full type name including namespace
            return typeRef.ToString();
        }
    }

    public class SignatureValidationIssue
    {
        public SignatureIssueType IssueType { get; set; }
        public string Context { get; set; }
        public string Message { get; set; }
        public int CodeLocation { get; set; }
    }

    public enum SignatureIssueType
    {
        MethodNotFound,
        TypeNotFound,
        ParameterCountMismatch,
        ParameterTypeMismatch,
        ValidationError
    }
}