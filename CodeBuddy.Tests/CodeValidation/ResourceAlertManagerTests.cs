using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class ResourceAlertManagerTests
    {
        private readonly Mock<ILogger<ResourceAlertManager>> _loggerMock;
        private readonly AlertConfiguration _configuration;
        private readonly ResourceAlertManager _alertManager;

        public ResourceAlertManagerTests()
        {
            _loggerMock = new Mock<ILogger<ResourceAlertManager>>();
            _configuration = new AlertConfiguration
            {
                Thresholds = new Dictionary<ResourceMetricType, ResourceThreshold>
                {
                    [ResourceMetricType.CPU] = new ResourceThreshold
                    {
                        MetricType = ResourceMetricType.CPU,
                        WarningThreshold = 70,
                        CriticalThreshold = 85,
                        EmergencyThreshold = 95,
                        SustainedDuration = TimeSpan.FromMinutes(5),
                        RateOfChangeThreshold = 10
                    }
                }
            };

            _alertManager = new ResourceAlertManager(_loggerMock.Object);
        }

        [Fact]
        public async Task ProcessMetrics_WhenThresholdExceeded_GeneratesAlert()
        {
            // Arrange
            await _alertManager.ConfigureAsync(_configuration);
            var metrics = new Dictionary<ResourceMetricType, double>
            {
                [ResourceMetricType.CPU] = 90
            };

            ResourceAlert capturedAlert = null;
            _alertManager.Subscribe(alert =>
            {
                capturedAlert = alert;
                return Task.CompletedTask;
            });

            // Act
            await _alertManager.ProcessMetricsAsync(metrics, "TestContext");

            // Assert
            Assert.NotNull(capturedAlert);
            Assert.Equal(AlertSeverity.Critical, capturedAlert.Severity);
            Assert.Equal(ResourceMetricType.CPU, capturedAlert.MetricType);
            Assert.Equal(90, capturedAlert.CurrentValue);
            Assert.Equal("TestContext", capturedAlert.ValidationContext);
        }

        [Fact]
        public async Task ProcessMetrics_WhenBelowThreshold_DoesNotGenerateAlert()
        {
            // Arrange
            await _alertManager.ConfigureAsync(_configuration);
            var metrics = new Dictionary<ResourceMetricType, double>
            {
                [ResourceMetricType.CPU] = 50
            };

            bool alertGenerated = false;
            _alertManager.Subscribe(_ =>
            {
                alertGenerated = true;
                return Task.CompletedTask;
            });

            // Act
            await _alertManager.ProcessMetricsAsync(metrics, "TestContext");

            // Assert
            Assert.False(alertGenerated);
        }

        [Fact]
        public async Task ProcessMetrics_WhenResourceLeak_DetectsAndAlerts()
        {
            // Arrange
            await _alertManager.ConfigureAsync(_configuration);
            var metrics = new Dictionary<ResourceMetricType, double>
            {
                [ResourceMetricType.CPU] = 60
            };

            List<ResourceAlert> generatedAlerts = new List<ResourceAlert>();
            _alertManager.Subscribe(alert =>
            {
                generatedAlerts.Add(alert);
                return Task.CompletedTask;
            });

            // Simulate increasing CPU usage pattern
            for (int i = 0; i < 10; i++)
            {
                metrics[ResourceMetricType.CPU] += 3;
                await _alertManager.ProcessMetricsAsync(metrics, "TestContext");
                await Task.Delay(100); // Small delay to simulate time passing
            }

            // Assert
            Assert.Contains(generatedAlerts, alert => 
                alert.Message.Contains("Rapid resource usage increase detected") || 
                alert.Message.Contains("Potential resource leak detected"));
        }

        [Fact]
        public async Task GetActiveAlerts_ReturnsCurrentAlerts()
        {
            // Arrange
            await _alertManager.ConfigureAsync(_configuration);
            var metrics = new Dictionary<ResourceMetricType, double>
            {
                [ResourceMetricType.CPU] = 96 // Emergency level
            };

            // Act
            await _alertManager.ProcessMetricsAsync(metrics, "TestContext");
            var activeAlerts = await _alertManager.GetActiveAlertsAsync();

            // Assert
            Assert.NotEmpty(activeAlerts);
            Assert.Contains(activeAlerts, alert => alert.Severity == AlertSeverity.Emergency);
        }

        [Fact]
        public async Task AlertAggregation_PreventsAlertFlooding()
        {
            // Arrange
            _configuration.AlertAggregationWindow = TimeSpan.FromSeconds(1);
            _configuration.MaxAlertsPerWindow = 2;
            await _alertManager.ConfigureAsync(_configuration);

            var metrics = new Dictionary<ResourceMetricType, double>
            {
                [ResourceMetricType.CPU] = 90
            };

            int alertCount = 0;
            _alertManager.Subscribe(_ =>
            {
                alertCount++;
                return Task.CompletedTask;
            });

            // Act
            for (int i = 0; i < 5; i++)
            {
                await _alertManager.ProcessMetricsAsync(metrics, "TestContext");
            }

            // Assert
            Assert.True(alertCount <= _configuration.MaxAlertsPerWindow);
        }
    }
}