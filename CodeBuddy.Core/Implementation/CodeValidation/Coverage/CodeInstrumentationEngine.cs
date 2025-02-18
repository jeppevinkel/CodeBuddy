using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public class CodeInstrumentationEngine : ICodeInstrumentationEngine
{
    private readonly Dictionary<string, string> _originalCode = new();
    private readonly string _instrumentationMarker = "// CodeBuddy Coverage Tracking";

    public async Task<Dictionary<string, string>> InstrumentCodeAsync(string[] filePaths)
    {
        var instrumentedFiles = new Dictionary<string, string>();

        foreach (var filePath in filePaths)
        {
            if (!File.Exists(filePath))
                continue;

            // Store original code
            _originalCode[filePath] = await File.ReadAllTextAsync(filePath);

            // Instrument the code with coverage tracking
            var instrumentedCode = await InstrumentSingleFileAsync(_originalCode[filePath]);
            await File.WriteAllTextAsync(filePath, instrumentedCode);

            instrumentedFiles[filePath] = instrumentedCode;
        }

        return instrumentedFiles;
    }

    public async Task RestoreCodeAsync(Dictionary<string, string> instrumentedFiles)
    {
        foreach (var file in instrumentedFiles)
        {
            if (_originalCode.TryGetValue(file.Key, out var originalCode))
            {
                await File.WriteAllTextAsync(file.Key, originalCode);
            }
        }
        _originalCode.Clear();
    }

    private async Task<string> InstrumentSingleFileAsync(string sourceCode)
    {
        // Add coverage tracking to method entries
        var methodPattern = @"(public|private|protected|internal)\s+\w+\s+\w+\s*\([^)]*\)\s*{";
        var instrumentedCode = Regex.Replace(sourceCode, methodPattern, match =>
        {
            return $"{match.Value}\n    {_instrumentationMarker}\n    CoverageTracker.TrackMethodEntry(\"{Guid.NewGuid()}\");";
        });

        // Add coverage tracking to branches
        var branchPattern = @"(if|while|for|foreach|switch)\s*\([^)]*\)\s*{";
        instrumentedCode = Regex.Replace(instrumentedCode, branchPattern, match =>
        {
            return $"{match.Value}\n    {_instrumentationMarker}\n    CoverageTracker.TrackBranch(\"{Guid.NewGuid()}\");";
        });

        // Add line coverage tracking
        var lines = instrumentedCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Trim().StartsWith("//") && !lines[i].Contains(_instrumentationMarker) && !string.IsNullOrWhiteSpace(lines[i]))
            {
                lines[i] = $"    {_instrumentationMarker}\n    CoverageTracker.TrackLine({i + 1});\n{lines[i]}";
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}