using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models;
using CodeBuddy.E2ETests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeBuddy.E2ETests.Scenarios
{
    public class PluginSystemE2ETests : E2ETestBase
    {
        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);
            
            services.AddSingleton<PluginHealthMonitor>();
            services.AddSingleton<PluginWatcher>();
        }

        [Fact]
        public async Task Plugin_LoadAndExecute_ShouldWorkEndToEnd()
        {
            // Arrange
            var pluginManager = ServiceProvider.GetRequiredService<IPluginManager>();
            var healthMonitor = ServiceProvider.GetRequiredService<PluginHealthMonitor>();

            // Create test plugin configuration
            var pluginConfig = new PluginPermissions
            {
                AllowedOperations = new[] { "ReadFile", "WriteFile" },
                ResourceLimits = new
                {
                    MaxMemoryMB = 100,
                    MaxConcurrentOperations = 5
                }
            };

            // Act
            await pluginManager.RegisterPluginAsync("TestPlugin", pluginConfig);
            var plugin = await pluginManager.GetPluginAsync("TestPlugin");
            
            // Simulate plugin operation
            var result = await plugin.ExecuteAsync("TestOperation", new { param = "value" });

            // Assert
            result.Success.Should().BeTrue();
            
            var health = await healthMonitor.GetPluginHealthAsync("TestPlugin");
            health.Status.Should().Be(PluginHealthStatus.Healthy);
            health.LastExecutionSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task Plugin_ResourceExceeded_ShouldBeHandledGracefully()
        {
            // Arrange
            var pluginManager = ServiceProvider.GetRequiredService<IPluginManager>();
            var healthMonitor = ServiceProvider.GetRequiredService<PluginHealthMonitor>();

            // Register plugin with very limited resources
            var pluginConfig = new PluginPermissions
            {
                AllowedOperations = new[] { "ReadFile" },
                ResourceLimits = new
                {
                    MaxMemoryMB = 1, // Very limited memory
                    MaxConcurrentOperations = 1
                }
            };

            await pluginManager.RegisterPluginAsync("ResourceLimitedPlugin", pluginConfig);
            var plugin = await pluginManager.GetPluginAsync("ResourceLimitedPlugin");

            // Act
            // Attempt operation that would exceed resources
            var largeOperation = new { data = new string('x', 2 * 1024 * 1024) }; // 2MB data
            var result = await plugin.ExecuteAsync("ProcessData", largeOperation);

            // Assert
            result.Success.Should().BeFalse();
            result.Error.Should().Contain("Resource limit exceeded");

            var health = await healthMonitor.GetPluginHealthAsync("ResourceLimitedPlugin");
            health.ResourceUtilization.MemoryUsageMB.Should().BeLessThan(2);
        }

        [Fact]
        public async Task Plugin_HotReload_ShouldUpdateConfiguration()
        {
            // Arrange
            var pluginManager = ServiceProvider.GetRequiredService<IPluginManager>();
            var pluginWatcher = ServiceProvider.GetRequiredService<PluginWatcher>();

            var initialConfig = new PluginPermissions
            {
                AllowedOperations = new[] { "ReadFile" },
                ResourceLimits = new
                {
                    MaxMemoryMB = 50,
                    MaxConcurrentOperations = 2
                }
            };

            await pluginManager.RegisterPluginAsync("DynamicPlugin", initialConfig);

            // Act
            // Update configuration
            var newConfig = new PluginPermissions
            {
                AllowedOperations = new[] { "ReadFile", "WriteFile" },
                ResourceLimits = new
                {
                    MaxMemoryMB = 100,
                    MaxConcurrentOperations = 5
                }
            };

            await pluginManager.UpdatePluginConfigurationAsync("DynamicPlugin", newConfig);

            // Assert
            var updatedPlugin = await pluginManager.GetPluginAsync("DynamicPlugin");
            var permissions = await pluginManager.GetPluginPermissionsAsync("DynamicPlugin");

            permissions.AllowedOperations.Should().Contain("WriteFile");
            permissions.ResourceLimits.MaxMemoryMB.Should().Be(100);
        }
    }
}