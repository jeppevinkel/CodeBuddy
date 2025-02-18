using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.TestCoverage;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public class CoverageInstrumenter : ICoverageInstrumenter
{
    private readonly ISourceCodeProvider _sourceCodeProvider;
    private readonly IBranchAnalyzer _branchAnalyzer;

    public CoverageInstrumenter(
        ISourceCodeProvider sourceCodeProvider,
        IBranchAnalyzer branchAnalyzer)
    {
        _sourceCodeProvider = sourceCodeProvider;
        _branchAnalyzer = branchAnalyzer;
    }

    public async Task<InstrumentationData> GetInstrumentationDataAsync(ValidationContext context)
    {
        var data = new InstrumentationData();
        var sourceFiles = await _sourceCodeProvider.GetSourceFilesAsync(context);

        foreach (var file in sourceFiles)
        {
            var module = new ModuleInstrumentationInfo
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(file.Path),
                Path = file.Path
            };

            // Process source code lines
            var lines = file.Content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    module.Lines.Add(new LineInfo
                    {
                        Number = i + 1,
                        Content = line
                    });
                }
            }

            // Analyze branches (if/else, switch, loops)
            var branches = await _branchAnalyzer.AnalyzeBranchesAsync(file.Content);
            module.Branches.AddRange(branches);

            // Identify excluded regions (comments, attributes)
            module.ExcludedRegions = IdentifyExcludedRegions(lines);

            data.Modules.Add(module);
        }

        return data;
    }

    private List<int> IdentifyExcludedRegions(string[] lines)
    {
        var excludedLines = new List<int>();
        bool inMultiLineComment = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                excludedLines.Add(i + 1);
                continue;
            }

            // Handle multi-line comments
            if (inMultiLineComment)
            {
                excludedLines.Add(i + 1);
                if (line.Contains("*/"))
                {
                    inMultiLineComment = false;
                }
                continue;
            }

            // Check for comment start
            if (line.Contains("/*"))
            {
                inMultiLineComment = true;
                excludedLines.Add(i + 1);
                continue;
            }

            // Single line comments
            if (line.StartsWith("//"))
            {
                excludedLines.Add(i + 1);
                continue;
            }

            // Attributes and using statements
            if (line.StartsWith("[") || line.StartsWith("using "))
            {
                excludedLines.Add(i + 1);
            }
        }

        return excludedLines;
    }
}

public interface ISourceCodeProvider
{
    Task<List<SourceFile>> GetSourceFilesAsync(ValidationContext context);
}

public interface IBranchAnalyzer
{
    Task<List<BranchInstrumentationInfo>> AnalyzeBranchesAsync(string sourceCode);
}

public class SourceFile
{
    public string Path { get; set; }
    public string Content { get; set; }
}