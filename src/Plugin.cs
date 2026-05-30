using System;
using System.Collections.Generic;
using Jellyfin.Plugin.NextUpMerge.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.NextUpMerge;

/// <summary>
/// Plugin entry point. Jellyfin discovers this via MEF on startup.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static Plugin? Instance { get; private set; }

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Next Up Merge";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("4a3b2c1d-0e0f-4a3b-8c7d-6e5f4a3b2c1d");

    /// <inheritdoc />
    public override string Description =>
        "Merges Continue Watching and Next Up into a single resumable list for Infuse (and other clients).";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        // No UI page needed – the plugin works transparently.
        return Array.Empty<PluginPageInfo>();
    }
}
