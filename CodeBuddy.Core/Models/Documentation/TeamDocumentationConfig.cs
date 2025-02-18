using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Documentation
{
    public class TeamDocumentationConfig
    {
        public string TeamId { get; set; }
        public string TeamName { get; set; }
        public DocumentationRequirements Requirements { get; set; }
        public List<string> ManagedNamespaces { get; set; } = new List<string>();
        public Dictionary<string, double> TypeSpecificThresholds { get; set; } = new Dictionary<string, double>();
        public List<string> RequiredSections { get; set; } = new List<string>();
        public StyleValidationRules StyleRules { get; set; } = new StyleValidationRules();
        public bool EnforceInPullRequests { get; set; } = true;
    }

    public class StyleValidationRules
    {
        public bool RequireVerbsInMethodDocs { get; set; } = true;
        public bool RequireFullSentences { get; set; } = true;
        public bool RequireCodeExamplesForPublicApi { get; set; } = true;
        public bool RequireParameterConstraints { get; set; } = true;
        public bool RequireExceptionConditions { get; set; } = true;
        public int MinWordCountInSummary { get; set; } = 10;
        public List<string> ProhibitedTerms { get; set; } = new List<string>();
        public List<string> RequiredTermsInClassDocs { get; set; } = new List<string>();
        public Dictionary<string, string> TermReplacements { get; set; } = new Dictionary<string, string>();
    }
}