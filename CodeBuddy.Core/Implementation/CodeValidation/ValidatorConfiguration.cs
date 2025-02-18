using System.Collections.Generic;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    public class ValidatorConfiguration
    {
        public Dictionary<string, ValidatorMapping> Validators { get; set; } = new Dictionary<string, ValidatorMapping>();
    }

    public class ValidatorMapping
    {
        public string AssemblyName { get; set; }
        public string TypeName { get; set; }
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
        public bool IsEnabled { get; set; } = true;
        public int Priority { get; set; }
    }
}