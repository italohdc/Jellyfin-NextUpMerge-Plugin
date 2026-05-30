# Jellyfin NextUpMerge Plugin

A Jellyfin plugin that merges **Continue Watching** and **Next Up** into a single list.


## About this plugin

There's a reported issue on Infuse where the _Watching_ shelf will show TV Shows next episodes when using Plex, but not when using Jellyfin as a share. That happens because Plex returns a combined list of in-progress and next-up items from its API, while Jellyfin keeps them separate.

This plugin creates a middleware on Jellyfin that merges the _Continue Watching_ and _Next Up_ on a single endpoint, so Infuse shows both in its top shelf.

### More on the issue

This has been a known issue and requested feature on Firecore community:

- [Firecore Community - Jellyfin Merge Continue Watching and Up Next](https://community.firecore.com/t/jellyfin-merge-continue-watching-and-up-next/39898)
- [Firecore Community - Infuse, PMS and Jellyfin: “continue watching” only for PMS?](https://community.firecore.com/t/infuse-pms-and-jellyfin-continue-watching-only-for-pms/59923)

As this new feature is not yet implemented on Jellyfin or Infuse, this plugin is a **temporary workaround** to get the same experience as on Plex. As it is a PoC, Claude was used to help develop it.


## How to use this plugin

### Compatibility

- **Jellyfin 10.11.x**

### Option 1: Install from pre-built releases

The compiled plugin files are available on the [releases page](https://github.com/italohdc/Jellyfin-NextUpMerge-Plugin/releases). You need to download the latest release and extract its content into Jellyfin plugins folder.

In the end, for it to work, there must be a `.dll` file as in:
`plugins/NextUpMerge_1.0.0.0/Jellyfin.Plugin.NextUpMerge.dll`.

### Option 2: Build from source

#### Build `.dll` files from source

To build this plugin, you need [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) installed. The `build.sh` script will compile the plugin files into `dist/` folder.

```sh
# Install .NET 9 SDK
## macOS (Homebrew):
brew install --cask dotnet-sdk9

# Build plugin .dll files
./build.sh
```

### Add plugin to Jellyfin

Currently, this plugin files must be copied mannually to the Jellyfin plugins folder.

On you Jellyfin config directory, create a new folder `NextUpMerge_1.0.0.0` and copy the content of `dist/` into it.

In the end, for it to work, there must be a `.dll` file as in:
`plugins/NextUpMerge_1.0.0.0/Jellyfin.Plugin.NextUpMerge.dll`.

Jellyfin must be restarted after copying the plugin files for it to be loaded.
