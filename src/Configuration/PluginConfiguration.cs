using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.NextUpMerge.Configuration;

/// <summary>
/// Plugin configuration – kept minimal for now.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Maximum number of Continue Watching items to include.
    /// </summary>
    public int ContinueWatchingLimit { get; set; } = 20;

    /// <summary>
    /// Maximum number of Next Up items to include.
    /// </summary>
    public int NextUpLimit { get; set; } = 20;
}
