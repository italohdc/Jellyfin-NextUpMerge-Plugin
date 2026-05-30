using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextUpMerge.Api;

/// <summary>
/// Replaces the built-in /Users/{userId}/Items/Resume endpoint so that
/// Continue Watching and Next Up are merged into a single list.
///
/// Jellyfin's own UserLibraryController also maps this route; by placing
/// this controller in a plugin and giving it a higher Order, ASP.NET Core's
/// routing will prefer it when both match.
/// </summary>
[ApiController]
[Authorize]
public class MergedResumeController : ControllerBase
{
    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ITVSeriesManager _tvSeriesManager;
    private readonly IDtoService _dtoService;
    private readonly ILogger<MergedResumeController> _logger;

    public MergedResumeController(
        IUserManager userManager,
        ILibraryManager libraryManager,
        ITVSeriesManager tvSeriesManager,
        IDtoService dtoService,
        ILogger<MergedResumeController> logger)
    {
        _userManager = userManager;
        _libraryManager = libraryManager;
        _tvSeriesManager = tvSeriesManager;
        _dtoService = dtoService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a merged list of resumable items (Continue Watching + Next Up).
    /// This route shadows the built-in UserLibraryController.GetResumeItems.
    /// </summary>
    [HttpGet("Users/{userId}/Items/Resume")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<QueryResult<BaseItemDto>> GetMergedResumeItems(
        [FromRoute, Required] Guid userId,
        [FromQuery] int? startIndex,
        [FromQuery] int? limit,
        [FromQuery] string? searchTerm,
        [FromQuery] Guid? parentId,
        [FromQuery] string? fields,
        [FromQuery] string? mediaTypes,
        [FromQuery] bool? enableUserData,
        [FromQuery] int? imageTypeLimit,
        [FromQuery] string? enableImageTypes,
        [FromQuery] string? excludeItemTypes,
        [FromQuery] string? includeItemTypes,
        [FromQuery] bool? enableTotalRecordCount,
        [FromQuery] bool? enableImages)
    {
        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return NotFound();
        }

        var config = Plugin.Instance?.Configuration;
        int cwLimit  = config?.ContinueWatchingLimit ?? 20;
        int nuLimit  = config?.NextUpLimit ?? 20;

        // ── 1. Continue Watching ──────────────────────────────────────────────
        var cwQuery = new InternalItemsQuery(user)
        {
            OrderBy = new[] { (ItemSortBy.DatePlayed, SortOrder.Descending) },
            IsResumable = true,
            StartIndex = 0,
            Limit = cwLimit,
            Recursive = true,
            DtoOptions = GetDtoOptions(fields, enableImages, imageTypeLimit, enableImageTypes, enableUserData),
        };

        var cwResults = _libraryManager.GetItemsResult(cwQuery);
        var cwItems   = cwResults.Items.ToList();

        // ── 2. Next Up ────────────────────────────────────────────────────────
        var nextUpQuery = new NextUpQuery
        {
            UserId    = userId,
            StartIndex = 0,
            Limit     = nuLimit,
            EnableTotalRecordCount = false,
        };

        var nuResults = _tvSeriesManager.GetNextUp(nextUpQuery, user, GetDtoOptions(fields, enableImages, imageTypeLimit, enableImageTypes, enableUserData));
        var nuItems   = nuResults.Items.ToList();

        // ── 3. Merge & deduplicate ────────────────────────────────────────────
        // Continue Watching takes priority. Skip any Next Up entry whose series
        // already has an in-progress episode in Continue Watching.
        var seenSeriesIds = new HashSet<Guid>();
        var seenItemIds   = new HashSet<Guid>();

        var merged = new List<BaseItem>();

        foreach (var item in cwItems)
        {
            merged.Add(item);
            seenItemIds.Add(item.Id);
            if (item is Episode ep && ep.SeriesId != Guid.Empty)
                seenSeriesIds.Add(ep.SeriesId);
        }

        foreach (var item in nuItems)
        {
            if (seenItemIds.Contains(item.Id))
                continue;
            if (item is Episode ep && seenSeriesIds.Contains(ep.SeriesId))
                continue;

            merged.Add(item);
            seenItemIds.Add(item.Id);
            if (item is Episode ep2 && ep2.SeriesId != Guid.Empty)
                seenSeriesIds.Add(ep2.SeriesId);
        }

        _logger.LogDebug(
            "[NextUpMerge] Merged resume: {CW} continue-watching + {NU} next-up = {Total} total",
            cwItems.Count, nuItems.Count, merged.Count);

        // ── 4. Apply paging ───────────────────────────────────────────────────
        var start = startIndex ?? 0;
        var paged = merged.Skip(start);
        if (limit.HasValue)
            paged = paged.Take(limit.Value);

        var pagedList = paged.ToList();

        // ── 5. Convert to DTOs ────────────────────────────────────────────────
        var dtoOptions = GetDtoOptions(fields, enableImages, imageTypeLimit, enableImageTypes, enableUserData);
        var dtos = _dtoService.GetBaseItemDtos(pagedList, dtoOptions, user);

        return Ok(new QueryResult<BaseItemDto>
        {
            Items            = dtos,
            TotalRecordCount = merged.Count,
            StartIndex       = start,
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DtoOptions GetDtoOptions(
        string? fields,
        bool? enableImages,
        int? imageTypeLimit,
        string? enableImageTypes,
        bool? enableUserData)
    {
        var options = new DtoOptions
        {
            EnableImages   = enableImages ?? true,
            ImageTypeLimit = imageTypeLimit ?? 1,
            EnableUserData = enableUserData ?? true,
        };

        if (!string.IsNullOrEmpty(fields))
        {
            options.Fields = fields
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => Enum.TryParse<ItemFields>(f.Trim(), true, out var parsed) ? parsed : (ItemFields?)null)
                .Where(f => f.HasValue)
                .Select(f => f!.Value)
                .ToArray();
        }

        if (!string.IsNullOrEmpty(enableImageTypes))
        {
            options.ImageTypes = enableImageTypes
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => Enum.TryParse<ImageType>(t.Trim(), true, out var parsed) ? parsed : (ImageType?)null)
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .ToArray();
        }

        return options;
    }
}
