using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Errors;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Orchestrates error recovery strategies and manages circuit breakers
    /// </summary>
    public class ErrorRecoveryOrchestrator
    {
        private readonly IEnumerable<IErrorRecoveryStrategy> _strategies;
        private readonly IErrorAnalyticsService _analytics;
        private readonly Dictionary<string, CircuitBreaker> _circuitBreakers;
        private readonly RetryPolicy _retryPolicy;

        public ErrorRecoveryOrchestrator(
            IEnumerable<IErrorRecoveryStrategy> strategies,
            IErrorAnalyticsService analytics,
            RetryPolicy retryPolicy)
        {
            _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
            _circuitBreakers = new Dictionary<string, CircuitBreaker>();
        }

        public async Task<bool> AttemptRecoveryAsync(ValidationError error)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            var context = ErrorRecoveryContext.Create(error);
            var strategy = _strategies.FirstOrDefault(s => s.CanHandle(error));

            if (strategy == null)
            {
                await _analytics.TrackNoStrategyFoundAsync(error);
                return false;
            }

            var circuitBreaker = GetOrCreateCircuitBreaker(error.Category);
            var operationKey = $"{error.Category}_{error.ErrorCode}";

            try
            {
                return await circuitBreaker.ExecuteAsync(async () =>
                {
                    await strategy.PrepareNextAttemptAsync(context);
                    var success = await strategy.AttemptRecoveryAsync(context);
                    
                    if (success)
                        await _analytics.TrackRecoverySuccessAsync(context);
                    else
                        await _analytics.TrackRecoveryFailureAsync(context);

                    return success;
                }, operationKey);
            }
            catch (CircuitBreakerOpenException)
            {
                await _analytics.TrackCircuitBreakerOpenAsync(error.Category);
                return false;
            }
            catch (Exception ex)
            {
                await _analytics.TrackRecoveryExceptionAsync(context, ex);
                return false;
            }
            finally
            {
                await strategy.CleanupAsync(context);
            }
        }

        private CircuitBreaker GetOrCreateCircuitBreaker(ErrorCategory category)
        {
            var key = category.ToString();
            if (!_circuitBreakers.TryGetValue(key, out var breaker))
            {
                lock (_circuitBreakers)
                {
                    if (!_circuitBreakers.TryGetValue(key, out breaker))
                    {
                        breaker = new CircuitBreaker(
                            _retryPolicy.CircuitBreakerThreshold,
                            TimeSpan.FromMilliseconds(_retryPolicy.CircuitBreakerResetMs));
                        _circuitBreakers[key] = breaker;
                    }
                }
            }
            return breaker;
        }

        public CircuitBreakerMetrics GetCircuitBreakerMetrics(ErrorCategory category, string errorCode)
        {
            var key = category.ToString();
            if (_circuitBreakers.TryGetValue(key, out var breaker))
            {
                return breaker.GetMetrics($"{category}_{errorCode}");
            }
            return null;
        }

        public CircuitState GetCircuitState(ErrorCategory category)
        {
            var key = category.ToString();
            if (_circuitBreakers.TryGetValue(key, out var breaker))
            {
                return breaker.State;
            }
            return CircuitState.Closed;
        }
    }
}