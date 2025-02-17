using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public abstract class BaseCodeValidator : ICodeValidator
{
    protected readonly ILogger _logger;

    protected BaseCodeValidator(ILogger logger)
    {
        _logger = logger;
    }

    private readonly Stopwatch _totalStopwatch = new();
    private readonly Dictionary<string, Stopwatch> _phaseStopwatches = new();
    private readonly PerformanceMonitor _performanceMonitor = new();

    public async Task<ValidationResult> ValidateAsync(string code, string language, ValidationOptions options)
    {
        var result = new ValidationResult { Language = language };
        _totalStopwatch.Restart();
        _performanceMonitor.Start();

        try
        {
            // Sequential validation for syntax as it's a prerequisite
            if (options.ValidateSyntax)
            {
                await MeasurePhaseAsync("Syntax", () => ValidateSyntaxAsync(code, result));
                if (result.Issues.Any(i => i.Severity == ValidationSeverity.Error))
                {
                    result.IsValid = false;
                    return result;
                }
            }

            // Analyze phase dependencies and system resources
            var phaseAnalysis = await AnalyzeValidationPhasesAsync(options, code);
            var (availableCores, memoryUsage, cpuUsage) = GetSystemResources();
            
            // Determine optimal parallelization strategy
            var executionStrategy = DetermineExecutionStrategy(
                phaseAnalysis, 
                options.ParallelOptions,
                availableCores,
                memoryUsage,
                cpuUsage
            );

            // Build validation pipeline based on analysis
            var validationPhases = BuildValidationPipeline(options, code, result);

            // Execute validation phases according to strategy
            if (executionStrategy.EnableParallel)
            {
                result.Statistics.Performance.ParallelizationDecisions["Strategy"] = 
                    $"Parallel execution with {executionStrategy.MaxConcurrentTasks} concurrent tasks";
                await ExecuteInParallel(validationPhases, executionStrategy, result);
            }
            else
            {
                result.Statistics.Performance.ParallelizationDecisions["Strategy"] = 
                    $"Sequential execution due to {executionStrategy.Reason}";
                foreach (var phase in validationPhases)
                {
                    await MeasurePhaseAsync(phase.Name, phase.Task);
                }
            }

            // Run custom rules last as they might depend on other validation results
            await MeasurePhaseAsync("CustomRules", () => ValidateCustomRulesAsync(code, result, options.CustomRules));

            // Calculate statistics and performance metrics
            CalculateStatistics(result);
            CollectPerformanceMetrics(result);

            // Set overall validation status
            result.IsValid = !result.Issues.Any(i => i.Severity == ValidationSeverity.Error 
                || i.Severity == ValidationSeverity.SecurityVulnerability);

            // Check performance thresholds
            CheckPerformanceThresholds(result, options);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during code validation for language {Language}", language);
            result.Issues.Add(new ValidationIssue
            {
                Code = "VAL001",
                Message = $"Validation process failed: {ex.Message}",
                Severity = ValidationSeverity.Error
            });
            result.IsValid = false;
            return result;
        }
    }

    protected abstract Task ValidateSyntaxAsync(string code, ValidationResult result);
    protected abstract Task ValidateSecurityAsync(string code, ValidationResult result);
    protected abstract Task ValidateStyleAsync(string code, ValidationResult result);
    protected abstract Task ValidateBestPracticesAsync(string code, ValidationResult result);
    protected abstract Task ValidateErrorHandlingAsync(string code, ValidationResult result);
    protected abstract Task ValidateCustomRulesAsync(string code, ValidationResult result, Dictionary<string, object> customRules);

    private void CalculateStatistics(ValidationResult result)
    {
        result.Statistics.TotalIssues = result.Issues.Count;
        result.Statistics.SecurityIssues = result.Issues.Count(i => i.Severity == ValidationSeverity.SecurityVulnerability);
        result.Statistics.StyleIssues = result.Issues.Count(i => i.Code.StartsWith("STYLE"));
        result.Statistics.BestPracticeIssues = result.Issues.Count(i => i.Code.StartsWith("BP"));
    }

    private async Task MeasurePhaseAsync(string phaseName, Func<Task> phase)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        _performanceMonitor.Start(phaseName);
        await phase();
        stopwatch.Stop();
        _performanceMonitor.End(phaseName);
        _phaseStopwatches[phaseName] = stopwatch;
    }

    private async Task MeasureParallelPhaseAsync(string phaseName, Func<Task> phase)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        _performanceMonitor.Start(phaseName);
        await Task.Run(phase);
        stopwatch.Stop();
        _performanceMonitor.End(phaseName);
        _phaseStopwatches[phaseName] = stopwatch;
    }

    private void CalculatePhaseOverlap(PerformanceMetrics metrics, ParallelExecutionReport report)
    {
        foreach (var phase in report.PhaseTimings)
        {
            var otherPhases = report.PhaseTimings
                .Where(x => x.Key != phase.Key && x.Value.WasParallel)
                .ToList();

            if (!otherPhases.Any()) continue;

            var phaseSpan = phase.Value.Duration;
            var overlapTime = otherPhases.Sum(other => 
            {
                var overlap = Math.Min(
                    phase.Value.Duration.TotalMilliseconds,
                    other.Value.Duration.TotalMilliseconds
                );
                return overlap;
            });

            metrics.PhaseOverlapPercentages[phase.Key] = 
                (overlapTime / phaseSpan.TotalMilliseconds) * 100;
        }
    }

    private void CollectPerformanceMetrics(ValidationResult result)
    {
        _totalStopwatch.Stop();
        var metrics = result.Statistics.Performance;

        // Phase timings
        foreach (var (phase, stopwatch) in _phaseStopwatches)
        {
            metrics.PhaseTimings[phase] = stopwatch.Elapsed;
        }

        // Overall metrics
        metrics.AverageValidationTimeMs = _totalStopwatch.Elapsed.TotalMilliseconds;
        
        // Resource utilization and parallel execution metrics
        var resourceMetrics = _performanceMonitor.GetMetrics();
        metrics.PeakMemoryUsageBytes = resourceMetrics.PeakMemoryBytes;
        metrics.CpuUtilizationPercent = resourceMetrics.CpuPercent;
        metrics.ResourceUtilization["ThreadCount"] = resourceMetrics.ThreadCount;
        metrics.ResourceUtilization["HandleCount"] = resourceMetrics.HandleCount;
        
        // Parallel execution metrics
        metrics.ConcurrentOperationsCount = resourceMetrics.ConcurrentOps;
        metrics.ThreadPoolUtilizationPercent = resourceMetrics.ThreadPoolUtilization;
        metrics.ParallelEfficiencyPercent = _performanceMonitor.CalculateParallelEfficiency();
        
        // Calculate sequential vs parallel execution time
        var sequentialTime = metrics.PhaseTimings.Values.Sum(t => t.TotalMilliseconds);
        metrics.SequentialExecutionTime = TimeSpan.FromMilliseconds(sequentialTime);
        metrics.ParallelExecutionTime = _totalStopwatch.Elapsed;

        // Detect bottlenecks
        DetectBottlenecks(metrics);
    }

    private async Task ExecuteInParallel(
        List<(string Name, Func<Task> Task)> phases, 
        ExecutionStrategy strategy,
        ValidationResult result)
    {
        var parallelTasks = new List<Task>();
        var runningTasks = new Dictionary<string, Task>();
        var phasesByGroup = phases
            .GroupBy(p => strategy.PhaseGroupings.GetValueOrDefault(p.Name, 0))
            .OrderBy(g => g.Key);

        foreach (var group in phasesByGroup)
        {
            var groupTasks = new List<Task>();

            foreach (var phase in group)
            {
                var task = MeasureParallelPhaseAsync(phase.Name, phase.Task);
                runningTasks[phase.Name] = task;
                groupTasks.Add(task);
                
                result.Statistics.Performance.ParallelizationDecisions[phase.Name] = 
                    $"Executed in group {group.Key}";
            }

            // Execute each group of tasks
            await Task.WhenAll(groupTasks);
            parallelTasks.AddRange(groupTasks);

            // Update metrics after each group
            result.Statistics.Performance.OptimalConcurrentPhases = 
                Math.Max(result.Statistics.Performance.OptimalConcurrentPhases, 
                        group.Count());
        }

        // Record final execution metrics
        result.Statistics.Performance.AdaptiveParallelizationEnabled = true;
        result.Statistics.Performance.MaxConcurrentOperations = 
            strategy.MaxConcurrentTasks;
    }

    private (int Cores, double MemoryUsage, double CpuUsage) GetSystemResources()
    {
        var cores = Environment.ProcessorCount;
        var process = Process.GetCurrentProcess();
        var totalMemory = process.WorkingSet64;
        var systemMemory = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
        var memoryUsagePercent = (double)totalMemory / systemMemory * 100;
        
        var metrics = _performanceMonitor.GetMetrics();
        return (cores, memoryUsagePercent, metrics.CpuPercent);
    }

    private bool CanRunParallel(ParallelValidationOptions options, int availableCores, double memoryUsage, double cpuUsage)
    {
        if (!options.UseAdaptiveParallelization)
            return true;

        if (availableCores < options.MinimumCpuCores)
            return false;

        if (cpuUsage > options.MaxCpuUtilization)
            return false;

        if (memoryUsage > options.MaxMemoryUtilization)
            return false;

        return true;
    }

    private class ValidationPhaseAnalysis
    {
        public Dictionary<string, HashSet<string>> Dependencies { get; set; } = new();
        public Dictionary<string, double> EstimatedLoad { get; set; } = new();
        public HashSet<string> ParallelizablePhases { get; set; } = new();
    }

    private class ExecutionStrategy
    {
        public bool EnableParallel { get; set; }
        public int MaxConcurrentTasks { get; set; }
        public string Reason { get; set; }
        public Dictionary<string, int> PhaseGroupings { get; set; } = new();
    }

    private async Task<ValidationPhaseAnalysis> AnalyzeValidationPhasesAsync(ValidationOptions options, string code)
    {
        var analysis = new ValidationPhaseAnalysis();
        
        // Define known dependencies
        analysis.Dependencies["Security"] = new HashSet<string> { "Syntax" };
        analysis.Dependencies["Style"] = new HashSet<string> { "Syntax" };
        analysis.Dependencies["BestPractices"] = new HashSet<string> { "Syntax" };
        analysis.Dependencies["ErrorHandling"] = new HashSet<string> { "Syntax" };
        analysis.Dependencies["CustomRules"] = new HashSet<string> 
            { "Syntax", "Security", "Style", "BestPractices", "ErrorHandling" };

        // Estimate load based on code size and complexity
        var codeSize = code.Length;
        var complexity = EstimateCodeComplexity(code);
        
        analysis.EstimatedLoad["Syntax"] = codeSize * 0.3;
        analysis.EstimatedLoad["Security"] = codeSize * 0.2 * complexity;
        analysis.EstimatedLoad["Style"] = codeSize * 0.15;
        analysis.EstimatedLoad["BestPractices"] = codeSize * 0.2 * complexity;
        analysis.EstimatedLoad["ErrorHandling"] = codeSize * 0.15 * complexity;
        analysis.EstimatedLoad["CustomRules"] = codeSize * 0.1;

        // Identify parallelizable phases
        analysis.ParallelizablePhases = new HashSet<string> 
            { "Security", "Style", "BestPractices", "ErrorHandling" };

        return analysis;
    }

    private double EstimateCodeComplexity(string code)
    {
        // Simple complexity estimation based on code characteristics
        var complexity = 1.0;
        
        var lines = code.Split('\n').Length;
        var branchCount = code.Count(c => c == '{');
        var loopCount = code.Count(c => c == 'for' || c == 'while');
        
        complexity += (branchCount / Math.Max(lines, 1)) * 0.5;
        complexity += (loopCount / Math.Max(lines, 1)) * 0.3;
        
        return Math.Min(complexity, 3.0); // Cap at 3x base complexity
    }

    private ExecutionStrategy DetermineExecutionStrategy(
        ValidationPhaseAnalysis analysis,
        ParallelValidationOptions options,
        int availableCores,
        double memoryUsage,
        double cpuUsage)
    {
        var strategy = new ExecutionStrategy();

        // Check if parallelization is possible
        if (!options.UseAdaptiveParallelization || availableCores < options.MinimumCpuCores)
        {
            strategy.EnableParallel = false;
            strategy.Reason = "System requirements not met or adaptive parallelization disabled";
            return strategy;
        }

        // Check resource constraints
        if (cpuUsage > options.MaxCpuUtilization)
        {
            strategy.EnableParallel = false;
            strategy.Reason = "CPU utilization too high";
            return strategy;
        }

        if (memoryUsage > options.MaxMemoryUtilization)
        {
            strategy.EnableParallel = false;
            strategy.Reason = "Memory utilization too high";
            return strategy;
        }

        // Determine optimal parallelization
        strategy.EnableParallel = true;
        strategy.MaxConcurrentTasks = CalculateOptimalConcurrency(
            analysis, availableCores, options.MaxConcurrentPhases);

        // Group phases based on dependencies and load
        AssignPhaseGroups(strategy, analysis);

        return strategy;
    }

    private int CalculateOptimalConcurrency(
        ValidationPhaseAnalysis analysis,
        int availableCores,
        int maxConfiguredPhases)
    {
        var parallelizableLoad = analysis.ParallelizablePhases
            .Sum(phase => analysis.EstimatedLoad[phase]);
        var totalLoad = analysis.EstimatedLoad.Values.Sum();
        
        var optimalThreads = Math.Ceiling(parallelizableLoad / totalLoad * availableCores);
        var maxThreads = maxConfiguredPhases > 0 ? maxConfiguredPhases : availableCores - 1;
        
        return (int)Math.Min(optimalThreads, maxThreads);
    }

    private void AssignPhaseGroups(ExecutionStrategy strategy, ValidationPhaseAnalysis analysis)
    {
        var currentGroup = 0;
        var assignedPhases = new HashSet<string>();

        // Assign sequential phases to their own groups
        foreach (var phase in analysis.Dependencies.Keys)
        {
            if (!analysis.ParallelizablePhases.Contains(phase))
            {
                strategy.PhaseGroupings[phase] = currentGroup++;
                assignedPhases.Add(phase);
            }
        }

        // Group parallel phases by dependencies and load
        var remainingPhases = analysis.ParallelizablePhases
            .Where(p => !assignedPhases.Contains(p))
            .OrderByDescending(p => analysis.EstimatedLoad[p]);

        foreach (var phase in remainingPhases)
        {
            if (assignedPhases.Contains(phase)) continue;

            var compatibleGroup = strategy.PhaseGroupings
                .Where(g => CanAddToGroup(phase, g.Key, analysis, strategy))
                .OrderBy(g => g.Value)
                .FirstOrDefault();

            if (compatibleGroup.Key != null)
            {
                strategy.PhaseGroupings[phase] = compatibleGroup.Value;
            }
            else
            {
                strategy.PhaseGroupings[phase] = currentGroup++;
            }
            assignedPhases.Add(phase);
        }
    }

    private bool CanAddToGroup(
        string phase,
        string groupPhase,
        ValidationPhaseAnalysis analysis,
        ExecutionStrategy strategy)
    {
        // Check dependencies
        if (analysis.Dependencies[phase].Intersect(analysis.Dependencies[groupPhase]).Any())
            return false;

        // Check if adding to this group would exceed concurrent task limit
        var groupCount = strategy.PhaseGroupings.Count(x => x.Value == strategy.PhaseGroupings[groupPhase]);
        if (strategy.MaxConcurrentTasks > 0 && groupCount >= strategy.MaxConcurrentTasks)
            return false;

        return true;
    }

    private List<(string Name, Func<Task> Task)> BuildValidationPipeline(
        ValidationOptions options,
        string code,
        ValidationResult result)
    {
        var pipeline = new List<(string Name, Func<Task> Task)>();

        if (options.ValidateSecurity)
            pipeline.Add(("Security", () => ValidateSecurityAsync(code, result)));
        
        if (options.ValidateStyle)
            pipeline.Add(("Style", () => ValidateStyleAsync(code, result)));
        
        if (options.ValidateBestPractices)
            pipeline.Add(("BestPractices", () => ValidateBestPracticesAsync(code, result)));
        
        if (options.ValidateErrorHandling)
            pipeline.Add(("ErrorHandling", () => ValidateErrorHandlingAsync(code, result)));

        return pipeline;
    }

    private int DetermineMaxConcurrentTasks(ParallelValidationOptions options)
    {
        if (options.MaxConcurrentPhases > 0)
            return options.MaxConcurrentPhases;

        if (options.UseAdaptiveParallelization)
        {
            var (cores, memoryUsage, cpuUsage) = GetSystemResources();
            var availableCores = cores - 1; // Reserve one core for system operations
            return Math.Max(1, availableCores);
        }

        return 0; // Unlimited
    }

    private void DetectBottlenecks(PerformanceMetrics metrics)
    {
        // Identify phases that took more than 25% of total time
        var totalTime = metrics.PhaseTimings.Values.Sum(t => t.TotalMilliseconds);
        foreach (var (phase, timing) in metrics.PhaseTimings)
        {
            var percentage = (timing.TotalMilliseconds / totalTime) * 100;
            if (percentage > 25)
            {
                metrics.Bottlenecks.Add(new PerformanceBottleneck
                {
                    Phase = phase,
                    Description = $"Phase takes {percentage:F1}% of total validation time",
                    ImpactScore = percentage,
                    Recommendation = "Consider optimizing this phase or running it in parallel if possible"
                });
            }
        }

        // Check memory usage
        if (metrics.PeakMemoryUsageBytes > 500 * 1024 * 1024) // 500MB
        {
            metrics.Bottlenecks.Add(new PerformanceBottleneck
            {
                Phase = "Memory",
                Description = "High memory usage detected",
                ImpactScore = 80,
                Recommendation = "Review memory allocation patterns and consider implementing memory pooling"
            });
        }
    }

    private void CheckPerformanceThresholds(ValidationResult result, ValidationOptions options)
    {
        if (options.PerformanceThresholds == null) return;

        var metrics = result.Statistics.Performance;
        var thresholds = options.PerformanceThresholds;

        if (metrics.AverageValidationTimeMs > thresholds.MaxValidationTimeMs)
        {
            _logger.LogWarning("Validation exceeded time threshold: {ActualMs}ms > {ThresholdMs}ms",
                metrics.AverageValidationTimeMs, thresholds.MaxValidationTimeMs);
        }

        if (metrics.PeakMemoryUsageBytes > thresholds.MaxMemoryUsageBytes)
        {
            _logger.LogWarning("Validation exceeded memory threshold: {ActualMB}MB > {ThresholdMB}MB",
                metrics.PeakMemoryUsageBytes / (1024 * 1024), thresholds.MaxMemoryUsageBytes / (1024 * 1024));
        }
    }
}