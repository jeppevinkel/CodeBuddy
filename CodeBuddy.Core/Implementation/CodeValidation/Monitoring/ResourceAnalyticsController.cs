using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Analytics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    [ApiController]
    [Route("api/[controller]")]
    public class ResourceAnalyticsController : ControllerBase
    {
        private readonly ResourceMonitoringDashboard _dashboard;
        private readonly ILogger<ResourceAnalyticsController> _logger;

        public ResourceAnalyticsController(ResourceMonitoringDashboard dashboard, ILogger<ResourceAnalyticsController> logger)
        {
            _dashboard = dashboard;
            _logger = logger;
        }

        [HttpGet("current")]
        public async Task<ActionResult<ResourceMetricsModel>> GetCurrentMetrics()
        {
            try
            {
                var metrics = await _dashboard.GetCurrentMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current metrics");
                return StatusCode(500, "Error retrieving metrics");
            }
        }

        [HttpGet("trends")]
        public ActionResult<ResourceTrendData> GetTrends([FromQuery] int minutes = 30)
        {
            try
            {
                var timeSpan = TimeSpan.FromMinutes(minutes);
                var trends = _dashboard.GetTrendData(timeSpan);
                return Ok(trends);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trend data");
                return StatusCode(500, "Error retrieving trends");
            }
        }

        [HttpGet("alerts")]
        public ActionResult<ResourceAlert[]> GetActiveAlerts()
        {
            try
            {
                var alerts = _dashboard.GetActiveAlerts();
                return Ok(alerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active alerts");
                return StatusCode(500, "Error retrieving alerts");
            }
        }

        [HttpPost("thresholds")]
        public ActionResult UpdateThresholds([FromBody] ResourceThresholds thresholds)
        {
            try
            {
                // Validate thresholds
                if (thresholds.MemoryWarningThresholdBytes >= thresholds.MemoryCriticalThresholdBytes)
                {
                    return BadRequest("Warning threshold must be less than critical threshold");
                }
                if (thresholds.CpuWarningThresholdPercent >= thresholds.CpuCriticalThresholdPercent)
                {
                    return BadRequest("CPU warning threshold must be less than critical threshold");
                }

                // Update thresholds logic would go here
                // For now just return success
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating thresholds");
                return StatusCode(500, "Error updating thresholds");
            }
        }

        [HttpGet("health")]
        public ActionResult<string> GetHealthStatus()
        {
            try
            {
                var metrics = _dashboard.GetCurrentMetricsAsync().Result;
                return Ok(metrics.HealthStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving health status");
                return StatusCode(500, "Error retrieving health status");
            }
        }
    }
}