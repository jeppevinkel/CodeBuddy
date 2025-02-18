using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Generates documentation coverage badges for the repository
    /// </summary>
    public class DocumentationBadgeGenerator
    {
        private readonly IFileOperations _fileOps;

        public DocumentationBadgeGenerator(IFileOperations fileOps)
        {
            _fileOps = fileOps;
        }

        public async Task GenerateCoverageBadgesAsync(DocumentationCoverageReport coverage)
        {
            // Generate overall coverage badge
            await GenerateBadge(
                "docs/badges/documentation-coverage.svg",
                "documentation",
                $"{coverage.PublicApiCoverage:P0}",
                GetBadgeColor(coverage.PublicApiCoverage));

            // Generate parameter documentation badge
            await GenerateBadge(
                "docs/badges/parameter-coverage.svg",
                "parameter docs",
                $"{coverage.ParameterDocumentationCoverage:P0}",
                GetBadgeColor(coverage.ParameterDocumentationCoverage));

            // Generate code examples badge
            await GenerateBadge(
                "docs/badges/examples-coverage.svg",
                "code examples",
                $"{coverage.CodeExampleCoverage:P0}",
                GetBadgeColor(coverage.CodeExampleCoverage));

            // Generate interface documentation badge
            await GenerateBadge(
                "docs/badges/interface-coverage.svg",
                "interface docs",
                $"{coverage.InterfaceImplementationCoverage:P0}",
                GetBadgeColor(coverage.InterfaceImplementationCoverage));
        }

        private async Task GenerateBadge(string path, string label, string value, string color)
        {
            var svg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""150"" height=""20"">
    <linearGradient id=""b"" x2=""0"" y2=""100%"">
        <stop offset=""0"" stop-color=""#bbb"" stop-opacity="".1""/>
        <stop offset=""1"" stop-opacity="".1""/>
    </linearGradient>
    <mask id=""a"">
        <rect width=""150"" height=""20"" rx=""3"" fill=""#fff""/>
    </mask>
    <g mask=""url(#a)"">
        <path fill=""#555"" d=""M0 0h90v20H0z""/>
        <path fill=""{color}"" d=""M90 0h60v20H90z""/>
        <path fill=""url(#b)"" d=""M0 0h150v20H0z""/>
    </g>
    <g fill=""#fff"" text-anchor=""middle"" font-family=""DejaVu Sans,Verdana,Geneva,sans-serif"" font-size=""11"">
        <text x=""45"" y=""15"" fill=""#010101"" fill-opacity="".3"">{label}</text>
        <text x=""45"" y=""14"">{label}</text>
        <text x=""120"" y=""15"" fill=""#010101"" fill-opacity="".3"">{value}</text>
        <text x=""120"" y=""14"">{value}</text>
    </g>
</svg>";

            await _fileOps.WriteFileAsync(path, svg);
        }

        private string GetBadgeColor(double coverage)
        {
            if (coverage >= 0.9) return "#4c1";      // Bright green
            if (coverage >= 0.8) return "#97CA00";   // Green
            if (coverage >= 0.7) return "#dfb317";   // Yellow
            if (coverage >= 0.6) return "#fe7d37";   // Orange
            return "#e05d44";                        // Red
        }
    }
}