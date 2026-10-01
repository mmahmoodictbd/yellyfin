using System.Net.Mime;
using System.Reflection;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.YouTubeHome.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.YouTubeHome.Api;

/// <summary>
/// Endpoints (all under /YouTubeHome):
///   GET /YouTubeHome/Feed       - shuffled, per-user feed (authenticated user)
///   GET /YouTubeHome/Config     - tiny settings payload for the client script (authenticated user)
///   GET /YouTubeHome/client.js  - the embedded browser script (anonymous, contains no secrets)
/// </summary>
[ApiController]
[Route("YouTubeHome")]
public class YouTubeHomeController : ControllerBase
{
    // Claim Jellyfin's auth handler puts on the principal (InternalClaimTypes.UserId).
    private const string UserIdClaim = "Jellyfin-UserId";

    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly IDtoService _dtoService;

    public YouTubeHomeController(
        ILibraryManager libraryManager,
        IUserManager userManager,
        IDtoService dtoService)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _dtoService = dtoService;
    }

    private static PluginConfiguration Config => Plugin.Instance!.Configuration;

    /// <summary>Small settings payload for the browser script.</summary>
    [HttpGet("Config")]
    [Authorize]
    [Produces(MediaTypeNames.Application.Json)]
    public ActionResult<ClientConfig> GetClientConfig() => new ClientConfig
    {
        Enabled = Config.Enabled && Config.SelectedLibraryIds.Length > 0,
        ReplaceHomePage = Config.ReplaceHomePage
    };

    /// <summary>
    /// Builds a YouTube-like home feed for the calling user. Because every query is built
    /// with the user, Jellyfin's library access and parental-control rules still apply.
    /// </summary>
    [HttpGet("Feed")]
    [Authorize]
    [Produces(MediaTypeNames.Application.Json)]
    public ActionResult<FeedResponse> GetFeed()
    {
        var cfg = Config;
        if (!cfg.Enabled || cfg.SelectedLibraryIds.Length == 0)
        {
            return new FeedResponse();
        }

        if (!Guid.TryParse(User.FindFirst(UserIdClaim)?.Value, out var userId))
        {
            return Unauthorized();
        }

        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        var perRow = Math.Clamp(cfg.ItemsPerRow, 4, 60);
        var poolSize = Math.Clamp(cfg.CandidatePoolSize, 50, 5000);

        // The selected libraries, resolved to folders (skips IDs of libraries that were deleted).
        var libraries = cfg.SelectedLibraryIds
            .Select(id => Guid.TryParse(id, out var g) ? _libraryManager.GetItemById(g) as Folder : null)
            .Where(f => f is not null)
            .Cast<Folder>()
            .ToList();

        // 1) Random candidate pool + 2) newest uploads, per library.
        var pool = new Dictionary<Guid, BaseItem>();
        var recent = new Dictionary<Guid, BaseItem>();
        foreach (var lib in libraries)
        {
            foreach (var item in Query(lib, user, poolSize, ItemSortBy.Random, SortOrder.Ascending))
            {
                pool[item.Id] = item;
            }

            foreach (var item in Query(lib, user, perRow, ItemSortBy.DateCreated, SortOrder.Descending))
            {
                recent[item.Id] = item;
            }
        }

        // 3) Build rows out of BaseItems first, convert to DTOs once at the end.
        var rows = new List<(string Kind, string Title, List<BaseItem> Items)>();

        var shuffled = Shuffle(pool.Values.ToList());
        rows.Add(("recommended", "Recommended", shuffled.Take(perRow * 3).ToList()));

        rows.Add((
            "recent",
            "Recently added",
            recent.Values.OrderByDescending(i => i.DateCreated).Take(perRow).ToList()));

        // Channel rows: group the pool by parent folder, pick random channels with >= 2 videos.
        var channels = Shuffle(
            pool.Values
                .GroupBy(i => i.ParentId)
                .Where(g => g.Count() >= 2)
                .ToList());
        foreach (var group in channels.Take(Math.Clamp(cfg.ChannelRows, 0, 12)))
        {
            var name = _libraryManager.GetItemById(group.Key)?.Name ?? "Channel";
            rows.Add(("channel", $"From {name}", Shuffle(group.ToList()).Take(perRow).ToList()));
        }

        // 4) One DTO conversion for all distinct items. Items keep standard Jellyfin metadata:
        //    Name, ImageTags (thumbnail), RunTimeTicks (duration), UserData (watched / progress).
        var distinct = rows.SelectMany(r => r.Items).GroupBy(i => i.Id).Select(g => g.First()).ToList();
        var dtoOptions = new DtoOptions(false)
        {
            EnableImages = true,
            ImageTypeLimit = 1,
            ImageTypes = new[] { ImageType.Primary, ImageType.Thumb, ImageType.Backdrop },
            Fields = new[] { ItemFields.PrimaryImageAspectRatio, ItemFields.DateCreated, ItemFields.Overview }
        };
        var dtoById = _dtoService
            .GetBaseItemDtos(distinct, dtoOptions, user)
            .ToDictionary(d => d.Id);

        FeedEntry ToEntry(BaseItem i)
        {
            var parent = _libraryManager.GetItemById(i.ParentId);
            return new FeedEntry
            {
                Item = dtoById[i.Id],
                ChannelName = parent?.Name ?? string.Empty,
                ChannelId = i.ParentId
            };
        }

        return new FeedResponse
        {
            Rows = rows
                .Where(r => r.Items.Count > 0)
                .Select(r => new FeedRow
                {
                    Kind = r.Kind,
                    Title = r.Title,
                    Entries = r.Items.Select(ToEntry).ToList()
                })
                .ToList()
        };
    }

    /// <summary>Serves the embedded browser script. Load it with a plain script tag (see README).</summary>
    [HttpGet("client.js")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    public ActionResult GetClientScript()
    {
        var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"{typeof(Plugin).Namespace}.Web.client.js");
        return stream is null ? NotFound() : File(stream, "application/javascript");
    }

    /// <summary>Recursive video query under one library, honouring the user's access rules.</summary>
    private static IReadOnlyList<BaseItem> Query(
        Folder library, User user, int limit, ItemSortBy sortBy, SortOrder order)
    {
        var query = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.Video, BaseItemKind.Movie, BaseItemKind.Episode },
            Recursive = true,
            Limit = limit,
            OrderBy = new[] { (sortBy, order) },
            DtoOptions = new DtoOptions(false)
        };
        // Same call Jellyfin's own /Items endpoint makes when a ParentId is a folder.
        return library.GetItems(query).Items;
    }

    /// <summary>Fisher-Yates shuffle (unbiased, unlike OrderBy(Guid.NewGuid())).</summary>
    private static List<T> Shuffle<T>(List<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }
}
