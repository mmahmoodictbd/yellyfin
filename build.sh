#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$ROOT_DIR/Jellyfin.Plugin.YouTubeHome.csproj"
CONFIGURATION="${1:-Release}"
BUILD_DIR="$ROOT_DIR/artifacts/build"
DIST_DIR="$ROOT_DIR/dist"
ASSEMBLY_NAME="Jellyfin.Plugin.YouTubeHome"

command -v dotnet >/dev/null 2>&1 || {
    echo "error: dotnet SDK is required" >&2
    exit 1
}

command -v zip >/dev/null 2>&1 || {
    echo "error: zip is required" >&2
    exit 1
}

VERSION="$(dotnet msbuild "$PROJECT" -nologo -getProperty:Version | tail -n 1 | tr -d '\r')"
ASSEMBLY_VERSION="$(dotnet msbuild "$PROJECT" -nologo -getProperty:AssemblyVersion | tail -n 1 | tr -d '\r')"
PACKAGE_NAME="youtubehome_${VERSION}"
STAGE_DIR="$ROOT_DIR/artifacts/$PACKAGE_NAME"
ZIP_PATH="$DIST_DIR/$PACKAGE_NAME.zip"

rm -rf "$BUILD_DIR" "$STAGE_DIR"
mkdir -p "$BUILD_DIR" "$STAGE_DIR" "$DIST_DIR"

dotnet restore "$PROJECT"
dotnet build "$PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-restore \
    --output "$BUILD_DIR"

cp "$BUILD_DIR/$ASSEMBLY_NAME.dll" "$STAGE_DIR/"
rm -f "$ZIP_PATH" "$ZIP_PATH.sha256"
(
    cd "$STAGE_DIR"
    zip -q "$ZIP_PATH" "$ASSEMBLY_NAME.dll"
)

if command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$ZIP_PATH" > "$ZIP_PATH.sha256"
elif command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$ZIP_PATH" > "$ZIP_PATH.sha256"
fi

echo "Created $ZIP_PATH"
[[ -f "$ZIP_PATH.sha256" ]] && echo "Created $ZIP_PATH.sha256"

if [[ -n "${GITHUB_REPOSITORY:-}" ]]; then
    if [[ ! "$GITHUB_REPOSITORY" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]]; then
        echo "error: GITHUB_REPOSITORY must use the owner/repository format" >&2
        exit 1
    fi

    if command -v md5 >/dev/null 2>&1; then
        JELLYFIN_CHECKSUM="$(md5 -q "$ZIP_PATH")"
    elif command -v md5sum >/dev/null 2>&1; then
        JELLYFIN_CHECKSUM="$(md5sum "$ZIP_PATH" | awk '{print $1}')"
    else
        echo "error: md5 or md5sum is required to generate manifest.json" >&2
        exit 1
    fi

    command -v jq >/dev/null 2>&1 || {
        echo "error: jq is required to generate manifest.json" >&2
        exit 1
    }

    RELEASE_TAG="${RELEASE_TAG:-v$VERSION}"
    SOURCE_URL="https://github.com/$GITHUB_REPOSITORY/releases/download/$RELEASE_TAG/$PACKAGE_NAME.zip"
    TIMESTAMP="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
    CHANGELOG="${CHANGELOG:-Release $VERSION}"
    MANIFEST_PATH="$DIST_DIR/manifest.json"
    NEW_MANIFEST="$DIST_DIR/manifest.json.new"

    jq -n \
        --arg owner "${GITHUB_REPOSITORY%%/*}" \
        --arg version "$ASSEMBLY_VERSION" \
        --arg changelog "$CHANGELOG" \
        --arg sourceUrl "$SOURCE_URL" \
        --arg checksum "$JELLYFIN_CHECKSUM" \
        --arg timestamp "$TIMESTAMP" \
        '[{
            category: "General",
            guid: "6d8c1e52-3f0a-4b57-9c1d-2a7e5b9f4c10",
            name: "YouTubeHome",
            description: "Turns selected libraries into a YouTube-style home feed with shuffled recommendations, channel rows, and recent uploads.",
            owner: $owner,
            overview: "A YouTube-style home feed for Jellyfin",
            versions: [{
                version: $version,
                changelog: $changelog,
                targetAbi: "10.10.7.0",
                sourceUrl: $sourceUrl,
                checksum: $checksum,
                timestamp: $timestamp
            }]
        }]' > "$NEW_MANIFEST"

    if [[ -f "$MANIFEST_PATH" ]]; then
        jq --slurpfile existing "$MANIFEST_PATH" \
            '.[0].versions[0].version as $version
             | .[0].versions += ($existing[0][0].versions | map(select(.version != $version)))' \
            "$NEW_MANIFEST" > "$NEW_MANIFEST.merged"
        mv "$NEW_MANIFEST.merged" "$MANIFEST_PATH"
        rm "$NEW_MANIFEST"
    else
        mv "$NEW_MANIFEST" "$MANIFEST_PATH"
    fi

    echo "Created $MANIFEST_PATH"
else
    echo "Set GITHUB_REPOSITORY=owner/repository to also create dist/manifest.json"
fi