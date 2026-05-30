# How It Works

> This technical overview was generated with Claude


## Problem

Infuse (and some other clients) have a single "Up Next" home screen row populated exclusively from the `/UserItems/Resume` endpoint. That endpoint returns only items with active playback progress — episodes you paused mid-watch. Episodes that are truly "next up" (unwatched, but the logical continuation of a series you're watching) never appear there, even though Jellyfin tracks them internally via its own Next Up system.

## Solution

The plugin registers an ASP.NET Core middleware that intercepts `GET /UserItems/Resume` requests before they reach Jellyfin's built-in handler. It runs its own query, merges the two lists, and returns the combined result.

## Middleware Pipeline

```
Client request
     │
     ▼
ResumeInterceptMiddleware   ← intercepts /UserItems/Resume?mediaTypes=Video
     │
     ├─ 1. Query Continue Watching  (IsResumable = true, ordered by DatePlayed desc)
     ├─ 2. Query Next Up            (ITVSeriesManager.GetNextUp)
     ├─ 3. Merge & deduplicate
     │       • Continue Watching items come first
     │       • Next Up items are appended, skipping any series already represented
     ├─ 4. Apply pagination         (startIndex / limit from original request)
     ├─ 5. Convert to DTOs          (IDtoService, respecting all client-requested fields)
     └─ 6. Return JSON response     (same format as the native Jellyfin response)
```

Non-video requests (e.g. `mediaTypes=Audio`) are passed through untouched to preserve "Continue Listening" and "Continue Reading" sections.

## Deduplication

A series is tracked by its `SeriesId`. If an episode from a given series is already in Continue Watching, any Next Up episode for that same series is skipped. This prevents a show from appearing twice when you have a paused episode and the next episode is also queued.

## UserData Handling

Next Up items have their `PlaybackPositionTicks` and `PlayedPercentage` explicitly zeroed in the response. This prevents clients from rendering a fake progress bar on episodes that haven't been started yet.

## Registration

The middleware is registered via `IPluginServiceRegistrator` → `IStartupFilter`, which hooks into Jellyfin's ASP.NET Core startup pipeline without requiring any controller registration.
