using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation.ResourceManagement
{
    public class ResourceCleanupService : BackgroundService
    {
        private readonly ResourceReleaseMonitor _resourceMonitor;
        private readonly ILogger<ResourceCleanupService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

        public ResourceCleanupService(
            ResourceReleaseMonitor resourceMonitor,
            ILogger<ResourceCleanupService> logger)
        {
            _resourceMonitor = resourceMonitor;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _resourceMonitor.ProcessStuckAllocations();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing stuck allocations");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }
}