using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Extensions.Json;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jellyfin.Plugin.NextUpMerge.Api;

public class ResumeInterceptMiddleware
{
    private static readonly Regex _userItemsResumePattern =
        new(@"^/UserItems/Resume$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly ItemFields[] _clientFields =
    {
        ItemFields.PrimaryImageAspectRatio,
        ItemFields.ChildCount,
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ResumeInterceptMiddleware> _logger;

    public ResumeInterceptMiddleware(RequestDelegate next, ILogger<ResumeInterceptMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!string.Equals(context.Request.Method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        Guid userId;

        if (_userItemsResumePattern.IsMatch(path) &&
            Guid.TryParse(context.Request.Query["userId"].ToString(), out userId))
        {
            var mediaTypes = context.Request.Query["mediaTypes"].ToString();
            if (!string.IsNullOrEmpty(mediaTypes) &&
                !mediaTypes.Split(',').Any(t => t.Trim().Equals("Video", StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }
        }
        else
        {
            await _next(context);
            return;
        }

        await HandleMergedAsync(context, userId);
    }

    private async Task HandleMergedAsync(HttpContext context, Guid userId)
    {
        var userManager = context.RequestServices.GetRequiredService<IUserManager>();
        var libraryMgr  = context.RequestServices.GetRequiredService<ILibraryManager>();
        var tvMgr       = context.RequestServices.GetRequiredService<ITVSeriesManager>();
        var dtoService  = context.RequestServices.GetRequiredService<IDtoService>();

        var user = userManager.GetUserById(userId);
        if (user is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var qs = context.Request.Query;

        var startIndex       = TryGetInt(qs, "startIndex");
        var limit            = TryGetInt(qs, "limit");
        var fields           = qs.TryGetValue("fields", out var fv)  ? fv.ToString() : null;
        var enableImages     = TryGetBool(qs, "enableImages");
        var imgTypeLimit     = TryGetInt(qs, "imageTypeLimit");
        var imgTypes         = qs.TryGetValue("enableImageTypes", out var itv) ? itv.ToString() : null;
        var enableUD         = TryGetBool(qs, "enableUserData");
        var enableTotalCount = TryGetBool(qs, "enableTotalRecordCount") ?? true;

        var config  = Plugin.Instance?.Configuration;
        int cwLimit = config?.ContinueWatchingLimit ?? 20;
        int nuLimit = config?.NextUpLimit           ?? 20;

        var dtoOptions = BuildDtoOptions(fields, enableImages, imgTypeLimit, imgTypes, enableUD);

        // 1. Continue Watching
        var cwQuery = new InternalItemsQuery(user)
        {
            OrderBy    = new[] { (ItemSortBy.DatePlayed, SortOrder.Descending) },
            IsResumable = true,
            StartIndex = 0,
            Limit      = cwLimit,
            Recursive  = true,
            DtoOptions = dtoOptions,
        };
        var cwItems = libraryMgr.GetItemsResult(cwQuery).Items.ToList();

        // 2. Next Up
        var nuQuery = new NextUpQuery
        {
            User                   = user,
            StartIndex             = 0,
            Limit                  = nuLimit,
            EnableTotalRecordCount = false,
        };
        var nuItems = tvMgr.GetNextUp(nuQuery, dtoOptions).Items.ToList();

        // 3. Merge & deduplicate — CW takes priority; skip NU if series already in CW
        var seenSeries = new HashSet<Guid>();
        var seenItems  = new HashSet<Guid>();
        var merged     = new List<BaseItem>();
        var nuIds      = new HashSet<Guid>();

        foreach (var item in cwItems)
        {
            merged.Add(item);
            seenItems.Add(item.Id);
            if (item is Episode ep && ep.SeriesId != Guid.Empty)
                seenSeries.Add(ep.SeriesId);
        }

        foreach (var item in nuItems)
        {
            if (seenItems.Contains(item.Id)) continue;
            if (item is Episode ep && seenSeries.Contains(ep.SeriesId)) continue;
            merged.Add(item);
            seenItems.Add(item.Id);
            nuIds.Add(item.Id);
            if (item is Episode ep2 && ep2.SeriesId != Guid.Empty)
                seenSeries.Add(ep2.SeriesId);
        }

        _logger.LogDebug(
            "[NextUpMerge] {Path}: {CW} continue-watching + {NU} next-up = {Total} total",
            context.Request.Path, cwItems.Count, nuItems.Count, merged.Count);

        // 4. Page
        var start = startIndex ?? 0;
        IEnumerable<BaseItem> paged = merged.Skip(start);
        if (limit.HasValue)
            paged = paged.Take(limit.Value);

        // 5. Convert to DTOs and return
        var dtos = dtoService.GetBaseItemDtos(paged.ToList(), dtoOptions, user);

        // Clear any residual progress on Next Up items so clients don't show a fake progress bar.
        foreach (var dto in dtos)
        {
            if (nuIds.Contains(dto.Id) && dto.UserData is not null)
            {
                dto.UserData.PlaybackPositionTicks = 0;
                dto.UserData.PlayedPercentage = null;
            }
        }

        var result = new QueryResult<BaseItemDto>
        {
            Items            = dtos,
            TotalRecordCount = enableTotalCount ? merged.Count : 0,
            StartIndex       = start,
        };

        var serializerOptions = context.RequestServices
            .GetService<IOptions<JsonOptions>>()?.Value.JsonSerializerOptions
            ?? JsonDefaults.CamelCaseOptions;

        context.Response.ContentType = "application/json; charset=utf-8";
        var json = JsonSerializer.Serialize(result, serializerOptions);
        await context.Response.WriteAsync(json).ConfigureAwait(false);
    }

    private static int? TryGetInt(IQueryCollection qs, string key)
        => qs.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : null;

    private static bool? TryGetBool(IQueryCollection qs, string key)
        => qs.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : null;

    private static DtoOptions BuildDtoOptions(
        string? fields, bool? enableImages, int? imageTypeLimit,
        string? enableImageTypes, bool? enableUserData)
    {
        var options = new DtoOptions
        {
            EnableImages   = enableImages   ?? true,
            ImageTypeLimit = imageTypeLimit ?? 1,
            EnableUserData = enableUserData ?? true,
        };

        var fieldList = new List<ItemFields>(_clientFields);

        if (!string.IsNullOrEmpty(fields))
        {
            fieldList.AddRange(fields
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => Enum.TryParse<ItemFields>(f.Trim(), true, out var p) ? p : (ItemFields?)null)
                .Where(f => f.HasValue).Select(f => f!.Value));
        }

        options.Fields = fieldList.Distinct().ToArray();

        if (!string.IsNullOrEmpty(enableImageTypes))
        {
            options.ImageTypes = enableImageTypes
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => Enum.TryParse<ImageType>(t.Trim(), true, out var p) ? p : (ImageType?)null)
                .Where(t => t.HasValue).Select(t => t!.Value)
                .ToArray();
        }

        return options;
    }
}
