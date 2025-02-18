using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Auth;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.Auth;

/// <summary>
/// Default implementation of plugin authentication and authorization service
/// </summary>
public class DefaultPluginAuthService : IPluginAuthService
{
    private readonly ILogger<DefaultPluginAuthService> _logger;
    private readonly Dictionary<string, PluginAuthContext> _sessions = new();
    private readonly Dictionary<string, string> _pluginCredentials = new();
    private readonly List<AuditLogEntry> _auditLog = new();
    private readonly HashSet<string> _trustedCertificates = new();
    private readonly object _lock = new();

    public DefaultPluginAuthService(ILogger<DefaultPluginAuthService> logger)
    {
        _logger = logger;
    }

    public Task<bool> HasPermissionAsync(PluginAuthContext authContext, PluginPermissions permission)
    {
        if (!authContext.IsAuthenticated)
            return Task.FromResult(false);

        // Admin role has full permissions
        if (authContext.Roles.Contains("Admin"))
            return Task.FromResult(true);

        // Map roles to permissions
        var userPermissions = PluginPermissions.None;
        foreach (var role in authContext.Roles)
        {
            userPermissions |= role switch
            {
                "PluginManager" => PluginPermissions.Full,
                "PluginViewer" => PluginPermissions.View | PluginPermissions.ViewHealth,
                "PluginConfigurator" => PluginPermissions.Configure | PluginPermissions.ManageState,
                _ => PluginPermissions.None
            };
        }

        return Task.FromResult((userPermissions & permission) == permission);
    }

    public async Task<bool> ValidatePluginSignatureAsync(string pluginPath, PluginAuthContext authContext)
    {
        try
        {
            using var certificate = new X509Certificate2(pluginPath);
            
            // Verify certificate is trusted
            if (!_trustedCertificates.Contains(certificate.Thumbprint))
            {
                _logger.LogWarning("Untrusted certificate for plugin: {Path}", pluginPath);
                return false;
            }

            // Verify signature
            using var file = File.OpenRead(pluginPath);
            using var sha256 = SHA256.Create();
            var hash = await sha256.ComputeHashAsync(file);
            
            return certificate.PublicKey.Key.VerifyData(hash, hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating plugin signature: {Path}", pluginPath);
            return false;
        }
    }

    public Task<PluginAuthContext> CreateSessionAsync(string userId, string authProvider)
    {
        var sessionId = Guid.NewGuid().ToString();
        var expiry = DateTime.UtcNow.AddHours(8);
        var context = new PluginAuthContext(
            userId,
            $"user_{userId}",
            new[] { "PluginViewer" },
            new[] { new Claim("session_id", sessionId) },
            authProvider,
            sessionId,
            expiry
        );

        lock (_lock)
        {
            _sessions[sessionId] = context;
        }

        return Task.FromResult(context);
    }

    public Task<bool> ValidateSessionAsync(PluginAuthContext authContext)
    {
        if (!authContext.IsAuthenticated || string.IsNullOrEmpty(authContext.SessionId))
            return Task.FromResult(false);

        lock (_lock)
        {
            if (!_sessions.TryGetValue(authContext.SessionId, out var session))
                return Task.FromResult(false);

            if (session.SessionExpiry < DateTime.UtcNow)
            {
                _sessions.Remove(authContext.SessionId);
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
    }

    public Task EndSessionAsync(string sessionId)
    {
        lock (_lock)
        {
            _sessions.Remove(sessionId);
        }
        return Task.CompletedTask;
    }

    public Task<string> GetPluginCredentialsAsync(string pluginId, PluginAuthContext authContext)
    {
        if (!authContext.IsAuthenticated)
            throw new UnauthorizedAccessException("Not authenticated");

        lock (_lock)
        {
            return Task.FromResult(_pluginCredentials.TryGetValue(pluginId, out var creds) ? creds : null);
        }
    }

    public Task StorePluginCredentialsAsync(string pluginId, string credentials, PluginAuthContext authContext)
    {
        if (!authContext.IsAuthenticated)
            throw new UnauthorizedAccessException("Not authenticated");

        lock (_lock)
        {
            _pluginCredentials[pluginId] = credentials;
        }
        return Task.CompletedTask;
    }

    public Task LogAuditEventAsync(string pluginId, string operation, PluginAuthContext authContext, bool success)
    {
        var entry = new AuditLogEntry
        {
            Timestamp = DateTime.UtcNow,
            PluginId = pluginId,
            Operation = operation,
            UserId = authContext.UserId,
            UserName = authContext.UserName,
            AuthProvider = authContext.AuthProvider,
            Success = success
        };

        lock (_lock)
        {
            _auditLog.Add(entry);
        }

        _logger.LogInformation(
            "Plugin operation: {Operation} on {PluginId} by {User} ({Provider}): {Status}",
            operation, pluginId, authContext.UserName, authContext.AuthProvider, success ? "Success" : "Failed"
        );

        return Task.CompletedTask;
    }

    private class AuditLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string PluginId { get; set; }
        public string Operation { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string AuthProvider { get; set; }
        public bool Success { get; set; }
    }
}