using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Services;

namespace CodeBuddy.Core.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PerformanceDashboardController : ControllerBase
    {
        private readonly IPerformanceMetricsService _metricsService;

        public PerformanceDashboardController(IPerformanceMetricsService metricsService)
        {
            _metricsService = metricsService;
        }

        [HttpGet("metrics")]
        public async Task<IActionResult> GetMetrics([FromQuery] PerformanceMetricsFilter filter)
        {
            var metrics = await _metricsService.GetMetricsAsync(filter);
            return Ok(metrics);
        }

        [HttpGet("trends")]
        public async Task<IActionResult> GetTrends([FromQuery] PerformanceTrendFilter filter)
        {
            var trends = await _metricsService.GetTrendsAsync(filter);
            return Ok(trends);
        }

        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts()
        {
            var alerts = await _metricsService.GetActiveAlertsAsync();
            return Ok(alerts);
        }

        [HttpGet("comparison")]
        public async Task<IActionResult> GetComparison([FromQuery] PerformanceComparisonFilter filter)
        {
            var comparison = await _metricsService.GetComparisonAsync(filter);
            return Ok(comparison);
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportMetrics([FromQuery] PerformanceMetricsFilter filter)
        {
            var report = await _metricsService.GenerateReportAsync(filter);
            return File(report.Content, "application/json", $"performance-report-{DateTime.UtcNow:yyyyMMdd}.json");
        }
    }
}