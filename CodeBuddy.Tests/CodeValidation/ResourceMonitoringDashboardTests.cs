using System;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using CodeBuddy.Core.Models.Analytics;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace CodeBuddy.Tests.CodeValidation
{
    [TestFixture]
    public class ResourceMonitoringDashboardTests
    {
        private Mock<ILogger> _loggerMock;
        private ResourceMonitoringDashboard _dashboard;
        private ResourceThresholds _thresholds;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger>();
            _thresholds = new ResourceThresholds
            {
                MemoryWarningThresholdBytes = 100 * 1024 * 1024, // 100MB
                MemoryCriticalThresholdBytes = 200 * 1024 * 1024, // 200MB
                CpuWarningThresholdPercent = 70,
                CpuCriticalThresholdPercent = 90,
                MaxHandleCount = 1000,
                MaxTemporaryFiles = 100,
                QueueSaturationThreshold = 0.8
            };
            _dashboard = new ResourceMonitoringDashboard(_loggerMock.Object, _thresholds);
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_dashboard != null)
            {
                await _dashboard.DisposeAsync();
            }
        }

        [Test]
        public async Task GetCurrentMetrics_ReturnsValidMetrics()
        {
            // Act
            var metrics = await _dashboard.GetCurrentMetrics();

            // Assert
            Assert.That(metrics, Is.Not.Null);
            Assert.That(metrics.Timestamp, Is.GreaterThan(DateTime.UtcNow.AddSeconds(-1)));
            Assert.That(metrics.MemoryUsageBytes, Is.GreaterThan(0));
            Assert.That(metrics.CpuUsagePercent, Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(100));
            Assert.That(metrics.ActiveHandles, Is.GreaterThan(0));
            Assert.That(metrics.HealthStatus, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GetTrendData_ReturnsValidTrendData()
        {
            // Arrange
            var timeSpan = TimeSpan.FromMinutes(5);

            // Generate some sample metrics
            for (int i = 0; i < 5; i++)
            {
                await _dashboard.GetCurrentMetrics();
                await Task.Delay(100);
            }

            // Act
            var trendData = _dashboard.GetTrendData(timeSpan);

            // Assert
            Assert.That(trendData, Is.Not.Null);
            Assert.That(trendData.StartTime, Is.LessThan(DateTime.UtcNow));
            Assert.That(trendData.EndTime, Is.GreaterThanOrEqualTo(trendData.StartTime));
            Assert.That(trendData.Metrics, Is.Not.Empty);
            Assert.That(trendData.AverageUtilization, Is.Not.Empty);
            Assert.That(trendData.PeakUtilization, Is.Not.Empty);
        }

        [Test]
        public async Task HighMemoryUsage_RaisesAlert()
        {
            // Arrange
            var metrics = new ResourceMetricsModel
            {
                Timestamp = DateTime.UtcNow,
                MemoryUsageBytes = _thresholds.MemoryCriticalThresholdBytes + 1024 * 1024, // Exceed critical threshold
                CpuUsagePercent = 50,
                ActiveHandles = 500
            };

            // Act
            await Task.Delay(1500); // Wait for monitoring cycle
            var alerts = _dashboard.GetActiveAlerts().ToList();

            // Assert
            Assert.That(alerts, Is.Not.Empty);
            var memoryAlert = alerts.FirstOrDefault(a => a.ResourceType == "Memory");
            Assert.That(memoryAlert, Is.Not.Null);
            Assert.That(memoryAlert.Severity, Is.EqualTo("Critical"));
        }

        [Test]
        public async Task QueueSaturation_RaisesAlert()
        {
            // Arrange
            var metrics = new ResourceMetricsModel
            {
                Timestamp = DateTime.UtcNow,
                MemoryUsageBytes = 50 * 1024 * 1024,
                CpuUsagePercent = 50,
                ActiveHandles = 500,
                ValidationQueueMetrics = new()
                {
                    ["QueueUtilization"] = 0.9 // 90% utilization
                }
            };

            // Act
            await Task.Delay(1500); // Wait for monitoring cycle
            var alerts = _dashboard.GetActiveAlerts().ToList();

            // Assert
            Assert.That(alerts, Is.Not.Empty);
            var queueAlert = alerts.FirstOrDefault(a => a.ResourceType == "Queue");
            Assert.That(queueAlert, Is.Not.Null);
            Assert.That(queueAlert.Severity, Is.EqualTo("Warning"));
        }

        [Test]
        public async Task MultipleMetrics_CalculatesAveragesCorrectly()
        {
            // Arrange
            for (int i = 0; i < 5; i++)
            {
                await _dashboard.GetCurrentMetrics();
                await Task.Delay(100);
            }

            // Act
            var trendData = _dashboard.GetTrendData(TimeSpan.FromSeconds(10));

            // Assert
            Assert.That(trendData.Metrics.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(trendData.AverageUtilization["MemoryMB"], Is.GreaterThan(0));
            Assert.That(trendData.AverageUtilization["CpuPercent"], Is.GreaterThanOrEqualTo(0));
            Assert.That(trendData.PeakUtilization["MemoryMB"], Is.GreaterThanOrEqualTo(trendData.AverageUtilization["MemoryMB"]));
        }
    }
}