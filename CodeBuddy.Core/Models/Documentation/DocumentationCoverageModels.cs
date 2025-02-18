using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Documentation
{
    public class DocumentationCoverageReport
    {
        public double OverallCoverage { get; set; }
        public List<TypeCoverageInfo> Types { get; set; } = new List<TypeCoverageInfo>();
        public List<DocumentationIssue> Issues { get; set; } = new List<DocumentationIssue>();
        public List<string> Recommendations { get; set; } = new List<string>();
        public bool MeetsThreshold { get; set; }
    }

    public class TypeCoverageInfo
    {
        public string TypeName { get; set; }
        public string Namespace { get; set; }
        public double TypeCoverage { get; set; }
        public double MethodCoverage { get; set; }
        public double PropertyCoverage { get; set; }
        public List<MemberCoverageInfo> MissingDocumentation { get; set; } = new List<MemberCoverageInfo>();
    }

    public class MemberCoverageInfo
    {
        public string MemberName { get; set; }
        public string MemberType { get; set; }
        public List<string> MissingElements { get; set; } = new List<string>();
        public IssueSeverity Severity { get; set; }
    }

    public class DocumentationRequirements
    {
        public double MinimumOverallCoverage { get; set; } = 80.0;
        public double MinimumTypeCoverage { get; set; } = 90.0;
        public double MinimumMethodCoverage { get; set; } = 85.0;
        public double MinimumPropertyCoverage { get; set; } = 75.0;
        public bool RequireExamples { get; set; } = true;
        public bool RequireParameterDocs { get; set; } = true;
        public bool RequireReturnDocs { get; set; } = true;
        public bool RequireExceptionDocs { get; set; } = true;
        public IssueSeverity MissingDocumentationSeverity { get; set; } = IssueSeverity.Warning;
    }
}