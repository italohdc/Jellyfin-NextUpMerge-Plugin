using Jellyfin.Plugin.NextUpMerge.Api;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.NextUpMerge;

/// <summary>
/// Registers plugin services with Jellyfin's DI container.
/// Jellyfin scans for IPluginServiceRegistrator on startup.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // The controller is discovered automatically by ASP.NET Core because
        // Jellyfin calls AddApplicationPart for every plugin assembly.
        // Nothing extra to register here – this class simply ensures the
        // assembly is recognised as a plugin service provider.
    }
}
