namespace CodeBuddy.Core.Models.Exceptions;

/// <summary>
/// Exception thrown when an unauthorized plugin operation is attempted
/// </summary>
public class UnauthorizedPluginOperationException : Exception
{
    public string PluginId { get; }
    public string Operation { get; }
    public string UserId { get; }

    public UnauthorizedPluginOperationException(string pluginId, string operation, string userId)
        : base($"Unauthorized plugin operation: {operation} on plugin {pluginId} by user {userId}")
    {
        PluginId = pluginId;
        Operation = operation;
        UserId = userId;
    }

    public UnauthorizedPluginOperationException(string pluginId, string operation, string userId, string message)
        : base(message)
    {
        PluginId = pluginId;
        Operation = operation;
        UserId = userId;
    }

    public UnauthorizedPluginOperationException(string pluginId, string operation, string userId, string message, Exception inner)
        : base(message, inner)
    {
        PluginId = pluginId;
        Operation = operation;
        UserId = userId;
    }
}