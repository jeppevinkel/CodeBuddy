using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Implementation.CodeValidation.ResourceManagement;
using CodeBuddy.Core.Models;
using CodeBuddy.E2ETests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeBuddy.E2ETests.Scenarios
{
    public class ResourceManagementE2ETests : E2ETestBase
    {
        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);
            
            services.AddSingleton<ResourcePreallocationManager>();
            services.AddSingleton<ResourceCleanupService>();
            services.AddSingleton<AdaptiveResourceManager>();
            services.AddSingleton<ResourceReleaseMonitor>();
        }

        [Fact]
        public async Task ResourcePreallocation_UnderHighLoad_ShouldAdaptively_ManageResources()
        {
            // Arrange
            var preallocationManager = ServiceProvider.GetRequiredService<ResourcePreallocationManager>();
            var adaptiveManager = ServiceProvider.GetRequiredService<AdaptiveResourceManager>();
            
            // Simulate high load conditions
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    // Simulate resource-intensive operation
                    await adaptiveManager.AllocateResourcesAsync(new ValidationOptions
                    {
                        MaxMemoryMB = 100,
                        TimeoutSeconds = 30
                    });
                });
            }

            // Act
            await Task.WhenAll(tasks);

            // Assert
            var metrics = await adaptiveManager.GetResourceMetricsAsync();
            metrics.TotalAllocatedMemoryMB.Should().BeLessThan(1000); // Less than 1GB total
            metrics.ResourceUtilization.Should().BeLessThan(0.9); // Less than 90% utilization
        }

        [Fact]
        public async Task ResourceCleanup_AfterValidation_ShouldReleaseAllResources()
        {
            // Arrange
            var cleanupService = ServiceProvider.GetRequiredService<ResourceCleanupService>();
            var releaseMonitor = ServiceProvider.GetRequiredService<ResourceReleaseMonitor>();

            // Act
            await using (var scope = await cleanupService.CreateResourceScope())
            {
                // Simulate resource allocation
                await scope.AllocateResourcesAsync(100); // 100MB
            } // Scope disposal should trigger cleanup

            // Assert
            var metrics = await releaseMonitor.GetMetricsAsync();
            metrics.UnreleasedResources.Should().BeEmpty();
            metrics.LastCleanupSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task ResourceMonitoring_UnderStress_ShouldTrackAndAlert()
        {
            // Arrange
            var releaseMonitor = ServiceProvider.GetRequiredService<ResourceReleaseMonitor>();
            var alerts = 0;
            releaseMonitor.OnResourceAlert += (_, _) => alerts++;

            // Act
            var tasks = new Task[5];
            for (int i = 0; i < 5; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    // Simulate resource-intensive operations
                    await using var scope = await ServiceProvider
                        .GetRequiredService<ResourceCleanupService>()
                        .CreateResourceScope();
                    
                    await scope.AllocateResourcesAsync(200); // 200MB each
                    await Task.Delay(100); // Simulate work
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            var metrics = await releaseMonitor.GetMetricsAsync();
            metrics.PeakMemoryUsageMB.Should().BeGreaterThan(0);
            metrics.ResourceLeaks.Should().Be(0);
        }
    }
}