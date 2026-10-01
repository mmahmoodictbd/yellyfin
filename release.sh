#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$ROOT_DIR/Jellyfin.Plugin.YouTubeHome.csproj"
REPOSITORY="${GITHUB_REPOSITORY:-mmahmoodictbd/yellyfin}"

cd "$ROOT_DIR"

for command_name in dotnet gh git jq; do
    command -v "$command_name" >/dev/null 2>&1 || {
        echo "error: $command_name is required" >&2
        exit 1
    }
done

[[ -z "$(git status --porcelain)" ]] || {
    echo "error: commit or stash existing changes before releasing" >&2
    exit 1
}

[[ "$(git branch --show-current)" == "main" ]] || {
    echo "error: releases must be created from main" >&2
    exit 1
}

gh auth status >/dev/null
git fetch origin main --tags

read -r behind ahead < <(git rev-list --left-right --count origin/main...main)
[[ "$behind" == "0" && "$ahead" == "0" ]] || {
    echo "error: main must be synchronized with origin/main" >&2
    exit 1
}

VERSION="$(dotnet msbuild "$PROJECT" -nologo -getProperty:Version | tail -n 1 | tr -d '\r')"
TAG="v$VERSION"
CHANGELOG="${*:-Release $VERSION}"

if git rev-parse "$TAG" >/dev/null 2>&1 || gh release view "$TAG" --repo "$REPOSITORY" >/dev/null 2>&1; then
    echo "error: release $TAG already exists" >&2
    exit 1
fi

GITHUB_REPOSITORY="$REPOSITORY" CHANGELOG="$CHANGELOG" "$ROOT_DIR/build.sh"

git add dist/manifest.json
git commit -m "Release $VERSION"
git tag "$TAG"
git push origin main "$TAG"

gh release create "$TAG" \
    "$ROOT_DIR/dist/youtubehome_$VERSION.zip" \
    "$ROOT_DIR/dist/youtubehome_$VERSION.zip.sha256" \
    --repo "$REPOSITORY" \
    --title "YouTubeHome $VERSION" \
    --notes "$CHANGELOG"

echo "Released https://github.com/$REPOSITORY/releases/tag/$TAG"