using System;

namespace CodeBuddy.Core.Models;

public class ValidationResilienceConfig
{
    public int MaxRetryAttempts { get; set; } = 3;
    public int CircuitBreakerThreshold { get; set; } = 5;
    public TimeSpan CircuitBreakerResetTime { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan MiddlewareTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool EnableFallbackBehavior { get; set; } = true;
    public bool ContinueOnMiddlewareFailure { get; set; } = true;
    public LogLevel FailureLogLevel { get; set; } = LogLevel.Error;
    public RetryStrategy RetryStrategy { get; set; } = RetryStrategy.ExponentialBackoff;
    public Dictionary<string, MiddlewareResilienceConfig> MiddlewareSpecificConfig { get; set; } = new();
}

public class MiddlewareResilienceConfig
{
    public int? MaxRetryAttempts { get; set; }
    public int? CircuitBreakerThreshold { get; set; }
    public TimeSpan? Timeout { get; set; }
    public bool? EnableFallback { get; set; }
    public RetryStrategy? RetryStrategy { get; set; }
    public string FallbackAction { get; set; }
}

public enum RetryStrategy
{
    Immediate,
    LinearBackoff,
    ExponentialBackoff,
    Custom
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Critical
}