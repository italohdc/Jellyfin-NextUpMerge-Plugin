# Jellyfin.Plugin.NextUpMerge

A Jellyfin plugin that merges **Continue Watching** and **Next Up** into a single
resumable list — so Infuse (and any other client) shows next episodes alongside
in-progress ones in the top "Watching" shelf.

No proxy, no extra containers. The plugin runs inside Jellyfin itself.

---

## How it works

Jellyfin exposes `/Users/{userId}/Items/Resume` for the Continue Watching list.
This plugin registers its own controller on that same route (with higher priority
than the built-in one) and returns a merged, deduplicated list:

```
Continue Watching  (in-progress episodes)
       +
Next Up            (next unwatched episodes per series)
       ↓
  deduplicated by series  →  returned as one list
```

If a series already has an in-progress episode in Continue Watching, its Next Up
entry is skipped so you never see the same show twice.

---

## Requirements

- **Jellyfin 10.10.x**
- **.NET 8 SDK** to build (only needed on your build machine, not the server)

---

## Build

```bash
# 1. Install .NET 8 SDK if you don't have it:
#    https://dotnet.microsoft.com/download/dotnet/8.0

# 2. Build
chmod +x build.sh
./build.sh

# Output: dist/Jellyfin.Plugin.NextUpMerge.dll
```

---

## Install

### Find your Jellyfin config/plugins folder

**Docker (volume mount):**
```bash
docker inspect jellyfin | grep -A2 Mounts
# Look for the mount pointing to /config inside the container
# e.g.  /srv/jellyfin/config  →  /config
```

**Inside the container:**
```
/config/plugins/
```

### Copy the plugin

Create a versioned subfolder (Jellyfin requires this naming):
```bash
mkdir -p /your/jellyfin/config/plugins/NextUpMerge_1.0.0.0
cp dist/Jellyfin.Plugin.NextUpMerge.dll \
   /your/jellyfin/config/plugins/NextUpMerge_1.0.0.0/
```

Or copy directly into the container:
```bash
docker cp dist/Jellyfin.Plugin.NextUpMerge.dll \
    jellyfin:/config/plugins/NextUpMerge_1.0.0.0/Jellyfin.Plugin.NextUpMerge.dll
```

### Restart Jellyfin
```bash
docker restart jellyfin
```

### Verify

Check the Jellyfin logs for:
```
[NextUpMerge] Merged resume: X continue-watching + Y next-up = Z total
```

Or call the endpoint directly:
```bash
curl "http://localhost:8096/Users/{YOUR_USER_ID}/Items/Resume?api_key={YOUR_API_KEY}"
# Items array should now include both in-progress and next-up episodes
```

---

## Infuse setup

Nothing changes in Infuse — keep pointing it at your Jellyfin URL as normal.
The merged list is returned transparently from the same endpoint Infuse already calls.

---

## Troubleshooting

| Symptom | Check |
|---|---|
| Plugin not loading | Folder name must be `NextUpMerge_1.0.0.0` (exact) |
| No change in Infuse | Restart Jellyfin; check logs for `[NextUpMerge]` |
| Duplicate shows | Both CW and NU returned — check dedup logic in logs |
| Build errors | Make sure you have .NET 8 SDK, not just runtime |

---

## Configuration (optional)

Default limits: 20 Continue Watching + 20 Next Up items.
To change, edit `src/Configuration/PluginConfiguration.cs` before building:

```csharp
public int ContinueWatchingLimit { get; set; } = 20;
public int NextUpLimit           { get; set; } = 20;
```
