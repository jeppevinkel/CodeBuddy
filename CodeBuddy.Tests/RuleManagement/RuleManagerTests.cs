using System;
using System.Linq;
using CodeBuddy.Core.Implementation.RuleManagement;
using CodeBuddy.Core.Models.Rules;
using Xunit;

namespace CodeBuddy.Tests.RuleManagement
{
    public class RuleManagerTests
    {
        [Fact]
        public void RegisterRule_ValidRule_Succeeds()
        {
            var manager = new RuleManager();
            var rule = CreateValidRule("test-rule");

            manager.RegisterRule(rule);

            Assert.Equal(rule, manager.GetRule("test-rule"));
        }

        [Fact]
        public void RegisterRule_DuplicateWithLowerVersion_ThrowsException()
        {
            var manager = new RuleManager();
            var rule1 = CreateValidRule("test-rule", new Version(1, 0));
            var rule2 = CreateValidRule("test-rule", new Version(0, 9));

            manager.RegisterRule(rule1);

            Assert.Throws<ArgumentException>(() => manager.RegisterRule(rule2));
        }

        [Fact]
        public void GetOrderedRules_WithDependencies_ReturnsCorrectOrder()
        {
            var manager = new RuleManager();
            
            var rule1 = CreateValidRule("rule1", priority: 2);
            var rule2 = CreateValidRule("rule2", priority: 1);
            rule1.Dependencies.Add("rule2");

            manager.RegisterRule(rule1);
            manager.RegisterRule(rule2);

            var orderedRules = manager.GetOrderedRules().ToList();
            Assert.Equal(2, orderedRules.Count);
            Assert.Equal("rule2", orderedRules[0].Id);
            Assert.Equal("rule1", orderedRules[1].Id);
        }

        [Fact]
        public void GetOrderedRules_WithCircularDependencies_ThrowsException()
        {
            var manager = new RuleManager();
            
            var rule1 = CreateValidRule("rule1");
            var rule2 = CreateValidRule("rule2");
            rule1.Dependencies.Add("rule2");
            rule2.Dependencies.Add("rule1");

            manager.RegisterRule(rule1);
            manager.RegisterRule(rule2);

            Assert.Throws<InvalidOperationException>(() => manager.GetOrderedRules().ToList());
        }

        [Fact]
        public void GetRulesForLanguage_ReturnsMatchingRules()
        {
            var manager = new RuleManager();
            
            var rule1 = CreateValidRule("rule1");
            rule1.SupportedLanguages.Add("csharp");
            var rule2 = CreateValidRule("rule2");
            rule2.SupportedLanguages.Add("javascript");

            manager.RegisterRule(rule1);
            manager.RegisterRule(rule2);

            var csharpRules = manager.GetRulesForLanguage("csharp").ToList();
            Assert.Single(csharpRules);
            Assert.Equal("rule1", csharpRules[0].Id);
        }

        private CustomRule CreateValidRule(string id, Version version = null, int priority = 1)
        {
            return new CustomRule
            {
                Id = id,
                Version = version ?? new Version(1, 0),
                Priority = priority,
                ConfigurationSchema = "{}",
                PerformanceImpact = 1,
                SupportedLanguages = { "csharp" },
                Description = "Test rule",
                Documentation = "Test documentation"
            };
        }
    }
}