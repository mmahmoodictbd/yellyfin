using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.YouTubeHome.Api;

/// <summary>One video: the standard Jellyfin DTO (title, image tags, RunTimeTicks, UserData...) plus its channel.</summary>
public class FeedEntry
{
    public required BaseItemDto Item { get; init; }

    /// <summary>Name of the parent folder, i.e. the YouTube channel in a "one folder per channel" layout.</summary>
    public string ChannelName { get; init; } = string.Empty;

    public Guid ChannelId { get; init; }
}

/// <summary>A horizontal row (or the big grid) on the dashboard.</summary>
public class FeedRow
{
    /// <summary>"recommended", "recent" or "channel".</summary>
    public required string Kind { get; init; }

    public required string Title { get; init; }

    public required IReadOnlyList<FeedEntry> Entries { get; init; }
}

public class FeedResponse
{
    public IReadOnlyList<FeedRow> Rows { get; init; } = Array.Empty<FeedRow>();
}

/// <summary>Small settings payload the client script reads before deciding to take over the home page.</summary>
public class ClientConfig
{
    public bool Enabled { get; init; }
    public bool ReplaceHomePage { get; init; }
}
