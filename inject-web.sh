#!/usr/bin/env bash

set -euo pipefail

WEB_ROOT="${1:-/usr/share/jellyfin/web}"
INDEX_FILE="$WEB_ROOT/index.html"
BACKUP_FILE="$INDEX_FILE.youtubehome.bak"
SCRIPT_TAG='<script src="/YouTubeHome/client.js"></script>'

[[ -f "$INDEX_FILE" ]] || {
    echo "error: Jellyfin web index not found at $INDEX_FILE" >&2
    exit 1
}

[[ -w "$INDEX_FILE" ]] || {
    echo "error: $INDEX_FILE is not writable; run this script with sudo" >&2
    exit 1
}

if grep -Fq "$SCRIPT_TAG" "$INDEX_FILE"; then
    echo "YouTubeHome is already injected into $INDEX_FILE"
    exit 0
fi

grep -Fq '</body>' "$INDEX_FILE" || {
    echo "error: $INDEX_FILE does not contain a closing body tag" >&2
    exit 1
}

[[ -f "$BACKUP_FILE" ]] || cp -p "$INDEX_FILE" "$BACKUP_FILE"
TEMP_FILE="$(mktemp)"
trap 'rm -f "$TEMP_FILE"' EXIT
sed "s#</body>#$SCRIPT_TAG</body>#" "$INDEX_FILE" > "$TEMP_FILE"
cat "$TEMP_FILE" > "$INDEX_FILE"

grep -Fq "$SCRIPT_TAG" "$INDEX_FILE" || {
    echo "error: failed to inject YouTubeHome client script" >&2
    exit 1
}

echo "Injected YouTubeHome into $INDEX_FILE"
echo "Backup: $BACKUP_FILE"