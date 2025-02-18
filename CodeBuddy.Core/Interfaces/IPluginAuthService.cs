using CodeBuddy.Core.Models.Auth;

namespace CodeBuddy.Core.Interfaces;

/// <summary>
/// Defines the contract for plugin authentication and authorization services
/// </summary>
public interface IPluginAuthService
{
    /// <summary>
    /// Verifies if the current authentication context has the required permission
    /// </summary>
    Task<bool> HasPermissionAsync(PluginAuthContext authContext, PluginPermissions permission);
    
    /// <summary>
    /// Validates a plugin's digital signature against trusted sources
    /// </summary>
    Task<bool> ValidatePluginSignatureAsync(string pluginPath, PluginAuthContext authContext);
    
    /// <summary>
    /// Creates a new authentication session for plugin configuration operations
    /// </summary>
    Task<PluginAuthContext> CreateSessionAsync(string userId, string authProvider);
    
    /// <summary>
    /// Validates an existing authentication session
    /// </summary>
    Task<bool> ValidateSessionAsync(PluginAuthContext authContext);
    
    /// <summary>
    /// Terminates an authentication session
    /// </summary>
    Task EndSessionAsync(string sessionId);
    
    /// <summary>
    /// Retrieves stored credentials for a plugin
    /// </summary>
    Task<string> GetPluginCredentialsAsync(string pluginId, PluginAuthContext authContext);
    
    /// <summary>
    /// Securely stores credentials for a plugin
    /// </summary>
    Task StorePluginCredentialsAsync(string pluginId, string credentials, PluginAuthContext authContext);
    
    /// <summary>
    /// Records an audit log entry for a plugin operation
    /// </summary>
    Task LogAuditEventAsync(string pluginId, string operation, PluginAuthContext authContext, bool success);
}