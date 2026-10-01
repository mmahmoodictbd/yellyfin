using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.YouTubeHome.Configuration;

/// <summary>
/// Serialized to XML by Jellyfin. Keep to simple types (string[], int, bool) so XmlSerializer is happy.
/// Property names are PascalCase in the JSON the config page reads/writes.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    public PluginConfiguration()
    {
        SelectedLibraryIds = Array.Empty<string>();
        Enabled = true;
        ReplaceHomePage = true;
        ItemsPerRow = 24;
        ChannelRows = 4;
        CandidatePoolSize = 400;
    }

    /// <summary>Item IDs (GUID strings) of the libraries to build the feed from.</summary>
    public string[] SelectedLibraryIds { get; set; }

    /// <summary>Master switch. When false the feed endpoint returns no rows and the client script stays idle.</summary>
    public bool Enabled { get; set; }

    /// <summary>If true the client script replaces the default home content; otherwise it only exposes window.YouTubeHome.mount().</summary>
    public bool ReplaceHomePage { get; set; }

    /// <summary>Videos per row (the "Recommended" grid shows 3x this).</summary>
    public int ItemsPerRow { get; set; }

    /// <summary>Number of random "From channel X" rows.</summary>
    public int ChannelRows { get; set; }

    /// <summary>Max random videos pulled per library before shuffling. Bounds memory/CPU on huge libraries.</summary>
    public int CandidatePoolSize { get; set; }
}
