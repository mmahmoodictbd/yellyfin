# YouTubeHome for Jellyfin

YouTubeHome is a Jellyfin server plugin that turns selected video libraries into a YouTube-style home feed. It adds shuffled recommendations, recently added videos, and rows grouped by channel folder while respecting each user's Jellyfin library access and parental controls.

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

Jellyfin repository installation requires the ZIP and `manifest.json` to be available at public URLs. GitHub Releases can host the ZIP while the repository itself hosts the manifest.

1. Create a public GitHub repository and push this project to its `main` branch.
2. Build with your GitHub repository name:

	```sh
	GITHUB_REPOSITORY=mmahmoodictbd/yellyfin ./build.sh
	```

3. Commit and push `dist/manifest.json`.
4. Create a GitHub release tagged `v1.0.0` and upload `dist/youtubehome_1.0.0.zip` as a release asset. The tag and filename must match the generated manifest.
5. In Jellyfin, open **Dashboard > Plugins > Repositories** and add:

	```text
	Repository name: YouTubeHome
	Repository URL:  https://raw.githubusercontent.com/mmahmoodictbd/yellyfin/main/dist/manifest.json
	```

6. Save, open **Catalog**, select **YouTubeHome**, and install it.
7. Restart Jellyfin, then configure the plugin under **Dashboard > Plugins > YouTubeHome**.

The manifest checksum is generated in Jellyfin's required MD5 format. The separate `.sha256` file remains available for manually verifying the release download. For later releases, update `Version`, `AssemblyVersion`, and `FileVersion` in the project, then build and publish with the matching `vVERSION` release tag. Keep older version entries in the manifest if users must be able to install them.

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