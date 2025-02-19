using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Configuration
{
    [AttributeUsage(AttributeTargets.Property)]
    public class EnvironmentExampleAttribute : Attribute
    {
        private readonly Dictionary<string, object> _examples;

        public EnvironmentExampleAttribute()
        {
            _examples = new Dictionary<string, object>();
        }

        public void SetExample(string environment, object value)
        {
            _examples[environment.ToLowerInvariant()] = value;
        }

        public object GetValueForEnvironment(string environment)
        {
            return _examples.TryGetValue(environment.ToLowerInvariant(), out var value) ? value : null;
        }
    }
}