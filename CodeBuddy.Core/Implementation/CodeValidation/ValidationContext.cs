using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.TestCoverage;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public static class ValidationContextExtensions
{
    public static T GetConfiguration<T>(this ValidationContext context) where T : new()
    {
        if (context.Configurations.TryGetValue(typeof(T), out var config))
            return (T)config;
        return new T();
    }
}

public class ValidationContext
{
    public string ProjectPath { get; set; }
    public string[] FilePaths { get; set; }
    public Dictionary<string, string> Settings { get; set; } = new();
    public List<string> Exclusions { get; set; } = new();
    public Dictionary<Type, object> Configurations { get; set; } = new();

    public void SetConfiguration<T>(T configuration)
    {
        Configurations[typeof(T)] = configuration;
    }
}