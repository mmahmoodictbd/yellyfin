# YouTubeHome for Jellyfin

YouTubeHome is a Jellyfin server plugin that turns selected video libraries into a YouTube-style home feed. It adds shuffled recommendations, recently added videos, and rows grouped by channel folder while respecting each user's Jellyfin library access and parental controls.

**Latest release:** [YouTubeHome 1.0.0](https://github.com/mmahmoodictbd/yellyfin/releases/tag/v1.0.0)

## Compatibility

- Jellyfin Server 10.9 or 10.10
- .NET 8 SDK or newer to build
- A Jellyfin library organized with one folder per channel for useful channel rows

The project currently targets `net8.0` and Jellyfin Controller 10.10.7. Jellyfin 10.11 and later require retargeting the project to `net9.0` and updating the Jellyfin package reference.

## Build

On macOS or Linux:

```sh
chmod +x build.sh
./build.sh
```

The script restores dependencies, builds the plugin in Release mode, and creates:

```text
dist/youtubehome_1.0.0.zip
dist/youtubehome_1.0.0.zip.sha256
```

Pass a different build configuration as the first argument, for example `./build.sh Debug`.

## Install from a repository

1. In Jellyfin, open **Dashboard > Plugins > Repositories** and add:

	```text
	Repository name: YouTubeHome
	Repository URL:  https://raw.githubusercontent.com/mmahmoodictbd/yellyfin/main/dist/manifest.json
	```

2. Save, open **Catalog**, select **YouTubeHome**, and install it.
3. Restart Jellyfin, then configure the plugin under **Dashboard > Plugins > YouTubeHome**.

The latest package can also be downloaded directly from [GitHub Releases](https://github.com/mmahmoodictbd/yellyfin/releases/latest).

## Create a release

Maintainers must first update `Version`, `AssemblyVersion`, and `FileVersion` in `Jellyfin.Plugin.YouTubeHome.csproj`, then commit and push that change to `main`. Create the release with:

```sh
./release.sh "Summary of changes"
```

The script requires `dotnet`, `git`, `gh`, and `jq`. It refuses to release from a dirty, non-`main`, unsynchronized, or already-tagged checkout. It builds the ZIP, prepends the new entry while preserving older manifest versions, commits the manifest, pushes the release tag, and uploads the ZIP and SHA-256 checksum to GitHub Releases. Jellyfin's required MD5 checksum is stored in the manifest.

## Install

1. Stop Jellyfin.
2. Create a `YouTubeHome` directory under Jellyfin's plugin directory.
3. Extract `Jellyfin.Plugin.YouTubeHome.dll` from the ZIP into that directory.
4. Start Jellyfin.
5. Open **Dashboard > Plugins > YouTubeHome**.
6. Select one or more video libraries, adjust the feed settings, and save.

Common plugin directory locations:

| Installation | Plugin directory |
| --- | --- |
| Linux package | `/var/lib/jellyfin/plugins/YouTubeHome` |
| Docker | `/config/plugins/YouTubeHome` inside the container |
| macOS | `~/.local/share/jellyfin/plugins/YouTubeHome` |
| Windows | `%ProgramData%\Jellyfin\Server\plugins\YouTubeHome` |

Check **Dashboard > Logs** after restarting if the plugin does not appear.

## Load the web client

The server plugin supplies the feed API and serves its browser script at `/YouTubeHome/client.js`. To replace the standard Jellyfin home screen, add this tag immediately before `</body>` in the `index.html` used by your Jellyfin web client:

```html
<script src="/YouTubeHome/client.js"></script>
```

Restart Jellyfin and hard-refresh the browser after editing the file. Jellyfin upgrades may replace `index.html`, so the script tag may need to be added again. This injection affects only that hosted web client; native Jellyfin applications do not load the custom script.

For Docker, make the change in a derived image or startup script instead of editing the running container, because container changes are lost when it is recreated.

## Feed behavior

- **Recommended** contains a shuffled selection from the configured candidate pool.
- **Recently added** is ordered by Jellyfin's item creation date.
- **From channel** rows group videos by their immediate parent folder.
- Watched state, playback progress, metadata, and image URLs come from Jellyfin's standard item DTOs.

Disabling **Replace the default home screen** leaves the API active without changing the home page. The client also exposes `window.YouTubeHome.mount(element)` for mounting the feed in a custom web-client page.

## Uninstall

Stop Jellyfin, remove the plugin directory, remove the script tag from the web client's `index.html`, and start Jellyfin again.