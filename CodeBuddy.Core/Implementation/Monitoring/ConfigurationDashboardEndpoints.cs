using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Configuration;
using CodeBuddy.Core.Implementation.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace CodeBuddy.Core.Implementation.Monitoring
{
    [Route("api/dashboard/configuration")]
    [ApiController]
    public class ConfigurationDashboardEndpoints : ControllerBase
    {
        private readonly ConfigurationValidationDashboard _dashboard;
        private readonly SystemHealthDashboard _systemDashboard;

        public ConfigurationDashboardEndpoints(
            ConfigurationValidationDashboard dashboard,
            SystemHealthDashboard systemDashboard)
        {
            _dashboard = dashboard;
            _systemDashboard = systemDashboard;
        }

        [HttpGet("health")]
        public async Task<ActionResult<ConfigurationHealthStatus>> GetHealthOverview()
        {
            return await _dashboard.GetConfigurationHealthOverviewAsync();
        }

        [HttpGet("components")]
        public async Task<ActionResult<Dictionary<string, ComponentValidationStatus>>> GetComponentStatus()
        {
            return await _dashboard.GetValidationStatusByComponentAsync();
        }

        [HttpGet("migrations")]
        public async Task<ActionResult<List<MigrationHistoryEntry>>> GetMigrationHistory()
        {
            return await _dashboard.GetMigrationHistoryAsync();
        }

        [HttpGet("environment/{envName}")]
        public async Task<ActionResult<EnvironmentConfigurationStatus>> GetEnvironmentStatus(string envName)
        {
            return await _dashboard.GetEnvironmentConfigurationStatusAsync(envName);
        }

        [HttpGet("warnings")]
        public async Task<ActionResult<List<ConfigurationWarning>>> GetActiveWarnings()
        {
            return await _dashboard.GetConfigurationWarningsAsync();
        }

        [HttpGet("metrics")]
        public async Task<ActionResult<object>> GetDashboardMetrics()
        {
            var health = await _dashboard.GetConfigurationHealthOverviewAsync();
            var components = await _dashboard.GetValidationStatusByComponentAsync();
            var warnings = await _dashboard.GetConfigurationWarningsAsync();

            return new
            {
                Timestamp = DateTime.UtcNow,
                OverallHealth = health,
                ComponentStatus = components,
                ActiveWarnings = warnings,
                ValidationResults = health.ValidationResults,
                SchemaCompliance = health.SchemaVersionCompliance,
                MigrationStatus = health.MigrationStatus,
                PluginStates = health.PluginConfigurationStates
            };
        }

        [HttpGet("visualization")]
        public ActionResult GetVisualizationEndpoints()
        {
            var endpoints = new
            {
                HealthDashboard = Url.Action(nameof(GetHealthOverview)),
                ComponentStatus = Url.Action(nameof(GetComponentStatus)),
                MigrationHistory = Url.Action(nameof(GetMigrationHistory)),
                EnvironmentStatus = Url.Action(nameof(GetEnvironmentStatus), new { envName = "{environment}" }),
                Warnings = Url.Action(nameof(GetActiveWarnings)),
                Metrics = Url.Action(nameof(GetDashboardMetrics))
            };

            return Ok(endpoints);
        }
    }
}