#!/usr/bin/env bash

set -euo pipefail

WEB_ROOT="${1:-/usr/share/jellyfin/web}"
INDEX_FILE="$WEB_ROOT/index.html"
BACKUP_FILE="$INDEX_FILE.youtubehome.bak"
CACHE_BUSTER="$(date +%s)"
SCRIPT_TAG="<script src=\"/YouTubeHome/client.js?v=$CACHE_BUSTER\"></script>"

[[ -f "$INDEX_FILE" ]] || {
    echo "error: Jellyfin web index not found at $INDEX_FILE" >&2
    exit 1
}

[[ -w "$INDEX_FILE" ]] || {
    echo "error: $INDEX_FILE is not writable; run this script with sudo" >&2
    exit 1
}

grep -Fq '</body>' "$INDEX_FILE" || {
    echo "error: $INDEX_FILE does not contain a closing body tag" >&2
    exit 1
}

[[ -f "$BACKUP_FILE" ]] || cp -p "$INDEX_FILE" "$BACKUP_FILE"
TEMP_FILE="$(mktemp)"
trap 'rm -f "$TEMP_FILE"' EXIT
sed -E 's#<script src="/YouTubeHome/client\.js(\?v=[^"]*)?"></script>##g' "$INDEX_FILE" \
    | sed "s#</body>#$SCRIPT_TAG</body>#" > "$TEMP_FILE"
cat "$TEMP_FILE" > "$INDEX_FILE"

grep -Fq "$SCRIPT_TAG" "$INDEX_FILE" || {
    echo "error: failed to inject YouTubeHome client script" >&2
    exit 1
}

echo "Refreshed YouTubeHome in $INDEX_FILE"
echo "Backup: $BACKUP_FILE"