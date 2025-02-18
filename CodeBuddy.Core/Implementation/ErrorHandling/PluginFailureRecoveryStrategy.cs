using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Errors;
using CodeBuddy.Core.Interfaces;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Recovery strategy for plugin failures
    /// </summary>
    public class PluginFailureRecoveryStrategy : BaseErrorRecoveryStrategy
    {
        private readonly IPluginManager _pluginManager;
        private const string PLUGIN_ID = "PluginId";
        private const string RESTART_COUNT = "RestartCount";
        private const int MAX_RESTART_ATTEMPTS = 3;

        public PluginFailureRecoveryStrategy(
            RetryPolicy retryPolicy, 
            IErrorAnalyticsService analytics,
            IPluginManager pluginManager) 
            : base(retryPolicy, analytics)
        {
            _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
        }

        public override async Task<bool> AttemptRecoveryAsync(ErrorRecoveryContext context)
        {
            try
            {
                var pluginId = context.Error.ResourceId;
                if (string.IsNullOrEmpty(pluginId))
                    return false;

                context.State[PLUGIN_ID] = pluginId;
                
                if (!context.State.ContainsKey(RESTART_COUNT))
                    context.State[RESTART_COUNT] = 0;

                var restartCount = (int)context.State[RESTART_COUNT];
                if (restartCount >= MAX_RESTART_ATTEMPTS)
                    return false;

                // Attempt to restart the plugin
                await _pluginManager.StopPluginAsync(pluginId);
                await Task.Delay(1000); // Cool-down period
                var success = await _pluginManager.StartPluginAsync(pluginId);

                context.State[RESTART_COUNT] = restartCount + 1;
                return success;
            }
            catch (Exception ex)
            {
                TrackException(context, ex);
                return false;
            }
        }

        public override bool CanHandle(ValidationError error)
        {
            return error.Category == ErrorCategory.System
                && !string.IsNullOrEmpty(error.ResourceId)
                && error.ErrorCode?.Contains("PLUGIN_ERROR", StringComparison.OrdinalIgnoreCase) == true;
        }

        public override async Task CleanupAsync(ErrorRecoveryContext context)
        {
            try
            {
                // If plugin never recovered, disable it
                if (context.State.TryGetValue(PLUGIN_ID, out var pluginIdObj)
                    && context.State.TryGetValue(RESTART_COUNT, out var restartCountObj))
                {
                    var pluginId = pluginIdObj.ToString();
                    var restartCount = (int)restartCountObj;

                    if (restartCount >= MAX_RESTART_ATTEMPTS)
                    {
                        await _pluginManager.DisablePluginAsync(pluginId);
                        await Analytics.TrackPluginDisabledAsync(pluginId);
                    }
                }
            }
            finally
            {
                await base.CleanupAsync(context);
            }
        }
    }
}