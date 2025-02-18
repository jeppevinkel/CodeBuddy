using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring;

public interface ITelemetrySystem
{
    Task RecordValidationTelemetryAsync(ValidationTelemetryEvent telemetryEvent);
    Task<IEnumerable<ValidationTelemetryEvent>> QueryValidationTelemetryAsync(DateTime startTime, DateTime endTime, IDictionary<string, string> filters = null);
    Task<ValidationTelemetryAnalysis> AnalyzeValidationTelemetryAsync(DateTime startTime, DateTime endTime);
    Task<IEnumerable<TelemetryPattern>> DetectPatternsAsync(string metricName, DateTime startTime, DateTime endTime);
    Task<IEnumerable<TelemetryAnomaly>> DetectAnomaliesAsync(string metricName, DateTime startTime, DateTime endTime);
    Task<IDictionary<string, double>> CalculateCorrelationsAsync(string baseMetric, IEnumerable<string> correlatedMetrics, DateTime startTime, DateTime endTime);
}