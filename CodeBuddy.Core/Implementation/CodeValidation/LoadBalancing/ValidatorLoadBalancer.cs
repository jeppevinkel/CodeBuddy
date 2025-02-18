using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation.LoadBalancing
{
    public class ValidatorLoadBalancer
    {
        private readonly ConcurrentDictionary<string, List<ValidatorInstance>> _validatorPools;
        private readonly ILogger<ValidatorLoadBalancer> _logger;
        private readonly Timer _loadBalanceTimer;

        private class ValidatorInstance
        {
            public ICodeValidator Validator { get; }
            public int ActiveTasks { get; set; }
            public DateTime LastUsed { get; set; }
            public bool IsAvailable => ActiveTasks < MaxConcurrentTasks;
            public int MaxConcurrentTasks { get; }

            public ValidatorInstance(ICodeValidator validator, int maxConcurrentTasks)
            {
                Validator = validator;
                MaxConcurrentTasks = maxConcurrentTasks;
                ActiveTasks = 0;
                LastUsed = DateTime.UtcNow;
            }
        }

        public ValidatorLoadBalancer(ILogger<ValidatorLoadBalancer> logger)
        {
            _validatorPools = new ConcurrentDictionary<string, List<ValidatorInstance>>();
            _logger = logger;
            _loadBalanceTimer = new Timer(BalanceLoad, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public void RegisterValidator(ICodeValidator validator, string language, int maxConcurrentTasks)
        {
            var instance = new ValidatorInstance(validator, maxConcurrentTasks);
            _validatorPools.AddOrUpdate(language,
                new List<ValidatorInstance> { instance },
                (_, list) =>
                {
                    list.Add(instance);
                    return list;
                });
        }

        public ICodeValidator GetValidator(string language, ValidatorSelectionContext context)
        {
            if (!_validatorPools.TryGetValue(language, out var pool))
            {
                return null;
            }

            var instance = pool
                .Where(v => v.IsAvailable)
                .OrderBy(v => v.ActiveTasks)
                .ThenBy(v => v.LastUsed)
                .FirstOrDefault();

            if (instance == null)
            {
                _logger.LogWarning("No available validator instances for language {Language}", language);
                return null;
            }

            Interlocked.Increment(ref instance.ActiveTasks);
            instance.LastUsed = DateTime.UtcNow;
            return instance.Validator;
        }

        public void ReleaseValidator(ICodeValidator validator)
        {
            foreach (var pool in _validatorPools.Values)
            {
                var instance = pool.FirstOrDefault(v => v.Validator == validator);
                if (instance != null)
                {
                    Interlocked.Decrement(ref instance.ActiveTasks);
                    break;
                }
            }
        }

        private void BalanceLoad(object state)
        {
            foreach (var pool in _validatorPools.Values)
            {
                var totalTasks = pool.Sum(v => v.ActiveTasks);
                var avgTasksPerValidator = totalTasks / pool.Count;

                foreach (var instance in pool.Where(v => v.ActiveTasks > avgTasksPerValidator * 1.5))
                {
                    _logger.LogInformation(
                        "High load detected on validator instance. Active tasks: {Tasks}", 
                        instance.ActiveTasks);
                }
            }
        }

        public void Dispose()
        {
            _loadBalanceTimer?.Dispose();
        }
    }

    public class ValidatorSelectionContext
    {
        public int CodeSize { get; set; }
        public string LanguageVersion { get; set; }
        public Dictionary<string, string> ValidationPreferences { get; set; }
        public bool RequiresFullAnalysis { get; set; }
    }
}