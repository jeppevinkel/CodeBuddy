using CodeBuddy.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using System.Reflection;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public class CSharpCodeValidator : BaseCodeValidator
{
    private readonly CSharpParseOptions _parseOptions;
    private readonly List<DiagnosticAnalyzer> _analyzers;

    public CSharpCodeValidator(ILogger logger) : base(logger)
    {
        _parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        _analyzers = LoadAnalyzers();
    }

    private List<DiagnosticAnalyzer> LoadAnalyzers()
    {
        var analyzers = new List<DiagnosticAnalyzer>();

        // Load analyzers from referenced assemblies
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("StyleCop") == true ||
                       a.GetName().Name?.StartsWith("Roslynator") == true ||
                       a.GetName().Name?.StartsWith("SecurityCodeScan") == true);

        foreach (var assembly in assemblies)
        {
            try
            {
                var analyzerTypes = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(t));

                foreach (var analyzerType in analyzerTypes)
                {
                    if (Activator.CreateInstance(analyzerType) is DiagnosticAnalyzer analyzer)
                    {
                        analyzers.Add(analyzer);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load analyzers from assembly: {AssemblyName}", assembly.GetName().Name);
            }
        }

        return analyzers;
    }

    protected override async Task ValidateSyntaxAsync(string code, ValidationResult result)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code, _parseOptions);
        var diagnostics = (await syntaxTree.GetDiagnosticsAsync()).Where(d => d.Severity != DiagnosticSeverity.Hidden);

        foreach (var diagnostic in diagnostics)
        {
            var location = diagnostic.Location.GetLineSpan();
            result.Issues.Add(new ValidationIssue
            {
                Code = diagnostic.Id,
                Message = diagnostic.GetMessage(),
                Severity = MapSeverity(diagnostic.Severity),
                Location = $"Line {location.StartLinePosition.Line + 1}, Column {location.StartLinePosition.Character + 1}",
                Suggestion = diagnostic.GetMessage() // In a real implementation, you might want to provide more specific suggestions
            });
        }

        result.IsValid = !result.Issues.Any(i => i.Severity == ValidationSeverity.Error);
    }

    protected override async Task ValidateSecurityAsync(string code, ValidationResult result)
    {
        var securityAnalyzers = _analyzers.Where(a => a.GetType().Assembly.GetName().Name?.Contains("SecurityCodeScan") == true);
        var issues = await AnalyzeCodeWithAnalyzers(code, securityAnalyzers);

        foreach (var issue in issues)
        {
            var location = issue.Location.GetLineSpan();
            result.Issues.Add(new ValidationIssue
            {
                Code = issue.Id,
                Message = issue.GetMessage(),
                Severity = ValidationSeverity.SecurityVulnerability,
                Location = $"Line {location.StartLinePosition.Line + 1}, Column {location.StartLinePosition.Character + 1}",
                Suggestion = issue.GetMessage()
            });
        }

        result.Statistics.SecurityIssues = issues.Count();
        result.IsValid = result.IsValid && !issues.Any();
    }

    protected override async Task ValidateStyleAsync(string code, ValidationResult result)
    {
        var styleCopAnalyzers = _analyzers.Where(a => a.GetType().Assembly.GetName().Name?.Contains("StyleCop") == true);
        var issues = await AnalyzeCodeWithAnalyzers(code, styleCopAnalyzers);

        foreach (var issue in issues)
        {
            var location = issue.Location.GetLineSpan();
            result.Issues.Add(new ValidationIssue
            {
                Code = issue.Id,
                Message = issue.GetMessage(),
                Severity = MapSeverity(issue.Severity),
                Location = $"Line {location.StartLinePosition.Line + 1}, Column {location.StartLinePosition.Character + 1}",
                Suggestion = GetStyleCopSuggestion(issue)
            });
        }

        result.Statistics.StyleIssues = issues.Count();
        result.IsValid = result.IsValid && !issues.Any(i => i.Severity == DiagnosticSeverity.Error);
    }

    protected override async Task ValidateBestPracticesAsync(string code, ValidationResult result)
    {
        var roslynatorAnalyzers = _analyzers.Where(a => a.GetType().Assembly.GetName().Name?.Contains("Roslynator") == true);
        var issues = await AnalyzeCodeWithAnalyzers(code, roslynatorAnalyzers);

        foreach (var issue in issues)
        {
            var location = issue.Location.GetLineSpan();
            result.Issues.Add(new ValidationIssue
            {
                Code = issue.Id,
                Message = issue.GetMessage(),
                Severity = MapSeverity(issue.Severity),
                Location = $"Line {location.StartLinePosition.Line + 1}, Column {location.StartLinePosition.Character + 1}",
                Suggestion = GetRoslynatorSuggestion(issue)
            });
        }

        result.Statistics.BestPracticeIssues = issues.Count();
        result.IsValid = result.IsValid && !issues.Any(i => i.Severity == DiagnosticSeverity.Error);
    }

    protected override async Task ValidateErrorHandlingAsync(string code, ValidationResult result)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code, _parseOptions);
        var compilation = CSharpCompilation.Create("CodeAnalysis")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);

        var model = compilation.GetSemanticModel(syntaxTree);
        var root = await syntaxTree.GetRootAsync();

        // Find all try-catch blocks
        var tryCatchBlocks = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TryStatementSyntax>();

        foreach (var tryBlock in tryCatchBlocks)
        {
            // Check for empty catch blocks
            foreach (var catchClause in tryBlock.Catches)
            {
                if (!catchClause.Block.Statements.Any())
                {
                    var location = catchClause.GetLocation().GetLineSpan();
                    result.Issues.Add(new ValidationIssue
                    {
                        Code = "EH001",
                        Message = "Empty catch block detected. Consider logging or handling the exception.",
                        Severity = ValidationSeverity.Warning,
                        Location = $"Line {location.StartLinePosition.Line + 1}",
                        Suggestion = "Add appropriate exception handling logic or logging."
                    });
                }
            }

            // Check for overly broad exception catching
            foreach (var catchClause in tryBlock.Catches)
            {
                if (catchClause.Declaration?.Type is Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax identifier &&
                    identifier.Identifier.ValueText == "Exception")
                {
                    var location = catchClause.GetLocation().GetLineSpan();
                    result.Issues.Add(new ValidationIssue
                    {
                        Code = "EH002",
                        Message = "Catching System.Exception is too broad. Consider catching more specific exceptions.",
                        Severity = ValidationSeverity.Warning,
                        Location = $"Line {location.StartLinePosition.Line + 1}",
                        Suggestion = "Catch specific exception types that you expect and know how to handle."
                    });
                }
            }
        }
    }

    protected override async Task ValidateCustomRulesAsync(string code, ValidationResult result, Dictionary<string, object> customRules)
    {
        if (customRules == null || !customRules.Any())
            return;

        var syntaxTree = CSharpSyntaxTree.ParseText(code, _parseOptions);
        var root = await syntaxTree.GetRootAsync();

        foreach (var rule in customRules)
        {
            if (rule.Value is CustomValidationRule customRule)
            {
                var matches = customRule.Pattern switch
                {
                    "class" => root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>(),
                    "method" => root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>(),
                    "property" => root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>(),
                    _ => Enumerable.Empty<Microsoft.CodeAnalysis.SyntaxNode>()
                };

                foreach (var match in matches)
                {
                    if (customRule.Condition(match))
                    {
                        var location = match.GetLocation().GetLineSpan();
                        result.Issues.Add(new ValidationIssue
                        {
                            Code = rule.Key,
                            Message = customRule.Message,
                            Severity = customRule.Severity,
                            Location = $"Line {location.StartLinePosition.Line + 1}",
                            Suggestion = customRule.Suggestion
                        });
                    }
                }
            }
        }
    }

    private async Task<IEnumerable<Diagnostic>> AnalyzeCodeWithAnalyzers(string code, IEnumerable<DiagnosticAnalyzer> analyzers)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code, _parseOptions);
        var compilation = CSharpCompilation.Create("CodeAnalysis")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);

        var diagnostics = new List<Diagnostic>();
        foreach (var analyzer in analyzers)
        {
            try
            {
                var compilationWithAnalyzer = compilation.WithAnalyzers(
                    ImmutableArray.Create(analyzer));
                var analyzerDiagnostics = await compilationWithAnalyzer.GetAnalyzerDiagnosticsAsync();
                diagnostics.AddRange(analyzerDiagnostics);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to run analyzer: {AnalyzerName}", analyzer.GetType().Name);
            }
        }

        return diagnostics;
    }

    private ValidationSeverity MapSeverity(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => ValidationSeverity.Error,
        DiagnosticSeverity.Warning => ValidationSeverity.Warning,
        DiagnosticSeverity.Info => ValidationSeverity.Info,
        _ => ValidationSeverity.Info
    };

    private string GetStyleCopSuggestion(Diagnostic diagnostic)
    {
        // StyleCop-specific suggestions could be added here
        return diagnostic.GetMessage();
    }

    private string GetRoslynatorSuggestion(Diagnostic diagnostic)
    {
        // Roslynator-specific suggestions could be added here
        return diagnostic.GetMessage();
    }
}

public class CustomValidationRule
{
    public string Pattern { get; set; }
    public Func<SyntaxNode, bool> Condition { get; set; }
    public string Message { get; set; }
    public string Suggestion { get; set; }
    public ValidationSeverity Severity { get; set; }
}
}