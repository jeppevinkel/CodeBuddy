using System.Security.Claims;

namespace CodeBuddy.Core.Models.Auth;

/// <summary>
/// Represents the authentication and authorization context for plugin operations
/// </summary>
public class PluginAuthContext
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public IEnumerable<string> Roles { get; set; }
    public IEnumerable<Claim> Claims { get; set; }
    public string AuthProvider { get; set; }
    public bool IsAuthenticated { get; set; }
    public string SessionId { get; set; }
    public DateTime SessionExpiry { get; set; }

    public PluginAuthContext(string userId, string userName, IEnumerable<string> roles, 
        IEnumerable<Claim> claims, string authProvider, string sessionId, DateTime sessionExpiry)
    {
        UserId = userId;
        UserName = userName;
        Roles = roles;
        Claims = claims;
        AuthProvider = authProvider;
        IsAuthenticated = true;
        SessionId = sessionId;
        SessionExpiry = sessionExpiry;
    }
}