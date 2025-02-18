namespace CodeBuddy.Core.Models.Auth;

/// <summary>
/// Defines the available permissions for plugin operations
/// </summary>
[Flags]
public enum PluginPermissions
{
    None = 0,
    View = 1,
    Install = 2,
    Update = 4,
    Configure = 8,
    Delete = 16,
    ManageState = 32, // Enable/Disable plugins
    ViewHealth = 64,
    ManageHealth = 128,
    Full = View | Install | Update | Configure | Delete | ManageState | ViewHealth | ManageHealth
}