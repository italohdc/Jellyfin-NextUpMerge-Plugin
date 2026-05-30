#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# build.sh – Build the NextUpMerge Jellyfin plugin
#
# Requirements:
#   • .NET 8 SDK  (https://dotnet.microsoft.com/download)
#
# Usage:
#   chmod +x build.sh
#   ./build.sh
#
# Output:
#   dist/Jellyfin.Plugin.NextUpMerge.dll   ← copy this into Jellyfin's plugins folder
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC_DIR="$SCRIPT_DIR/src"
OUT_DIR="$SCRIPT_DIR/dist"

echo "==> Cleaning previous build..."
rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

echo "==> Restoring NuGet packages..."
dotnet restore "$SRC_DIR/Jellyfin.Plugin.NextUpMerge.csproj"

echo "==> Building plugin (Release)..."
dotnet publish "$SRC_DIR/Jellyfin.Plugin.NextUpMerge.csproj" \
    --configuration Release \
    --output "$OUT_DIR" \
    --no-restore

echo ""
echo "✅  Build complete!"
echo ""
echo "Install the plugin:"
echo "  1. Copy  dist/Jellyfin.Plugin.NextUpMerge.dll"
echo "     into   <jellyfin-config>/plugins/NextUpMerge_1.0.0.0/"
echo "  2. Restart Jellyfin"
echo "  3. Infuse (and other clients) will now get merged Continue Watching + Next Up"
echo ""
