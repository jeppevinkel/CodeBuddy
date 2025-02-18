using System;
using CodeBuddy.Core.Implementation.CodeValidation;

namespace CodeBuddy.Core.Models;

public class ValidatorInfo
{
    public string Language { get; }
    public Type ValidatorType { get; }
    public IValidatorCapabilities Capabilities { get; }
    public ValidatorHealthInfo HealthInfo { get; }
    public string AssemblyPath { get; }
    public DateTime LoadTime { get; }

    public ValidatorInfo(string language, Type validatorType, IValidatorCapabilities capabilities)
    {
        Language = language;
        ValidatorType = validatorType;
        Capabilities = capabilities;
        AssemblyPath = validatorType.Assembly.Location;
        LoadTime = DateTime.UtcNow;
        
        HealthInfo = new ValidatorHealthInfo
        {
            Language = language,
            AssemblyPath = AssemblyPath,
            IsHealthy = true,
            LastChecked = DateTime.UtcNow,
            Version = validatorType.Assembly.GetName().Version?.ToString() ?? "0.0.0.0",
            LoadTime = TimeSpan.Zero,
            MemoryUsageBytes = 0
        };
    }
}