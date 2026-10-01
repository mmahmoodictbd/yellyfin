using Jellyfin.Plugin.YouTubeHome.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.YouTubeHome;

/// <summary>
/// Plugin entry point. Jellyfin discovers this class via reflection on startup.
/// <see cref="BasePlugin{TConfigurationType}"/> handles loading/saving PluginConfiguration as XML
/// in the server's plugin configuration folder; <see cref="IHasWebPages"/> lets us expose the admin page.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>Stable GUID. Never change it after release, or users lose their settings.</summary>
    public static readonly Guid PluginGuid = Guid.Parse("6d8c1e52-3f0a-4b57-9c1d-2a7e5b9f4c10");

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>Static access so the API controller can reach the current configuration.</summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override Guid Id => PluginGuid;

    /// <inheritdoc />
    public override string Name => "YouTubeHome";

    /// <inheritdoc />
    public override string Description =>
        "Turns selected libraries into a YouTube-style home feed (shuffled recommendations, channel rows, recent uploads).";

    /// <summary>
    /// Registers the embedded admin config page. It appears under Dashboard -> Plugins -> YouTubeHome.
    /// </summary>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = "YouTubeHome",
            EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
        };
    }
}
