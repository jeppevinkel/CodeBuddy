using System;
using System.Collections.Concurrent;
using System.Linq;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public class ResponseTimeMonitor
    {
        private readonly ConcurrentQueue<ResponseTimeSample> _samples;
        private readonly ResponseTimeConfig _config;
        private readonly object _lockObject = new object();
        private double _currentConcurrencyLimit;
        private DateTime _warmupStartTime;
        private bool _isInWarmup;

        public ResponseTimeMonitor(ValidationResilienceConfig config)
        {
            _config = config.ResponseTimeConfig;
            _samples = new ConcurrentQueue<ResponseTimeSample>();
            _currentConcurrencyLimit = config.MaxConcurrentValidations * _config.WarmupConcurrencyMultiplier;
            _warmupStartTime = DateTime.UtcNow;
            _isInWarmup = true;
        }

        public void RecordResponseTime(TimeSpan duration)
        {
            var sample = new ResponseTimeSample
            {
                Timestamp = DateTime.UtcNow,
                Duration = duration
            };

            _samples.Enqueue(sample);
            TrimSamples();
            UpdateWarmupState();
        }

        public bool ShouldThrottle()
        {
            if (_samples.Count < _config.MinSamplesForAnalysis)
                return false;

            var stats = CalculateStatistics();
            return stats.SlowRequestPercentage > _config.SlowRequestPercentageThreshold ||
                   stats.ConsecutiveSlowRequests >= _config.ConsecutiveSlowRequests;
        }

        public double GetCurrentConcurrencyLimit()
        {
            if (!_isInWarmup)
                return _currentConcurrencyLimit;

            var warmupProgress = (DateTime.UtcNow - _warmupStartTime).TotalSeconds / 
                               _config.WarmupPeriod.TotalSeconds;
            return Math.Min(1.0, warmupProgress) * _currentConcurrencyLimit;
        }

        public ResponseTimeStatistics GetStatistics()
        {
            return CalculateStatistics();
        }

        private void TrimSamples()
        {
            var cutoffTime = DateTime.UtcNow - _config.SlidingWindowDuration;
            while (_samples.TryPeek(out var sample) && sample.Timestamp < cutoffTime)
            {
                _samples.TryDequeue(out _);
            }
        }

        private void UpdateWarmupState()
        {
            if (!_isInWarmup)
                return;

            if (DateTime.UtcNow - _warmupStartTime >= _config.WarmupPeriod)
            {
                _isInWarmup = false;
                _currentConcurrencyLimit = GetCurrentConcurrencyLimit();
            }
        }

        private ResponseTimeStatistics CalculateStatistics()
        {
            var samples = _samples.ToList();
            if (!samples.Any())
                return new ResponseTimeStatistics();

            var orderedDurations = samples.Select(s => s.Duration).OrderBy(d => d).ToList();
            var p95 = orderedDurations[(int)(orderedDurations.Count * 0.95)];
            var p99 = orderedDurations[(int)(orderedDurations.Count * 0.99)];

            var slowRequests = samples.Count(s => s.Duration > _config.TargetResponseTime);
            var slowRequestPercentage = (double)slowRequests / samples.Count * 100;

            var consecutiveSlowRequests = 0;
            var maxConsecutiveSlowRequests = 0;
            foreach (var sample in samples.OrderBy(s => s.Timestamp))
            {
                if (sample.Duration > _config.TargetResponseTime)
                {
                    consecutiveSlowRequests++;
                    maxConsecutiveSlowRequests = Math.Max(maxConsecutiveSlowRequests, consecutiveSlowRequests);
                }
                else
                {
                    consecutiveSlowRequests = 0;
                }
            }

            return new ResponseTimeStatistics
            {
                AverageResponseTime = samples.Average(s => s.Duration.TotalMilliseconds),
                P95ResponseTime = p95.TotalMilliseconds,
                P99ResponseTime = p99.TotalMilliseconds,
                SlowRequestPercentage = slowRequestPercentage,
                ConsecutiveSlowRequests = maxConsecutiveSlowRequests,
                TotalRequests = samples.Count,
                SlowRequests = slowRequests
            };
        }
    }

    public class ResponseTimeSample
    {
        public DateTime Timestamp { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class ResponseTimeStatistics
    {
        public double AverageResponseTime { get; set; }
        public double P95ResponseTime { get; set; }
        public double P99ResponseTime { get; set; }
        public double SlowRequestPercentage { get; set; }
        public int ConsecutiveSlowRequests { get; set; }
        public int TotalRequests { get; set; }
        public int SlowRequests { get; set; }
    }
}