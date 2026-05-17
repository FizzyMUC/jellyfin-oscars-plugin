# Jellyfin Oscars

<img width="702" height="401" alt="logo" src="https://github.com/user-attachments/assets/4d9f8948-65a4-4939-9231-4d2d29d654f1" />

A Jellyfin plugin that enriches movie metadata with Oscar (Academy Awards) information, including tags, collections, and Jellyfin Web badges.

## Features

- Adds Oscar winner and nominee tags to movies
- Displays an Oscar badge in Jellyfin Web for tagged movies when `JavaScript Injector` is installed
- Automatically creates and maintains `Oscar Winners` and `Oscar Nominees` collections
- Supports collection posters for Oscar collections
- Uses async library scanning so manual scans start immediately and continue in the background
- Processes libraries in batches with persistent progress across runs and restarts
- Prevents overlapping manual scans and provides clearer logging during refreshes
- Uses `JavaScript Injector` for Jellyfin Web badge integration
- Uses OMDb for metadata enrichment

## How it works

The plugin requires an OMDb API key and uses IMDb IDs that are already present in Jellyfin metadata. For eligible movies, it requests the OMDb `Awards` field, parses Oscar-related results, normalizes them into a simple internal state, and applies the result to Jellyfin items as tags. Those tags are then used for the web badge and for Oscar collection membership.

## Important

- `JavaScript Injector` is required for Oscar badges in Jellyfin Web
- Without `JavaScript Injector`, the plugin still enriches metadata and manages collections, but badges will not appear in the web UI
- Jellyfin Oscars no longer modifies Jellyfin Web files directly

## Screenshots

<img width="831" height="444" alt="Screenshot 2026-03-19 at 12 24 06" src="https://github.com/user-attachments/assets/9dcc94fb-902d-40a2-80bc-7ace9d266ead" />


Jellyfin Oscars neatly adds curated Collections for Oscar Nominees or Oscar Winners. Technically each winner was a nominee once so you decide whether you want to include them.

<img width="1483" height="767" alt="Screenshot 2026-03-19 at 12 24 52" src="https://github.com/user-attachments/assets/d6a32505-5b9f-4c49-9690-b4ecac532b68" />

Collections include all movies that match said criteria. They are sorted automatically.

<img width="977" height="411" alt="Screenshot 2026-03-19 at 12 25 09" src="https://github.com/user-attachments/assets/19641480-ac7b-4859-9c36-f1fc8cc8d435" />

Each movie detail screen gets a badge next to the ratings that indicates if it is an Oscar Winner or Oscar Nominee.


## Installation

### Latest release

Current public release: `v1.0.6`

Release highlights:

- Removed legacy direct Jellyfin Web `index.html` patching
- Switched badge injection to `JavaScript Injector` only
- Added visible badge integration status in plugin settings
- Fixed scheduled task crashes when `scanstate.json` is empty or corrupted
- Added graceful scan-state recovery by rebuilding persisted state
- Improved scan-state persistence reliability with temp-file writes
- Fixed scan-state persistence failures caused by file locks
- Added retry handling for transient scan-state move failures
- Kept scans running when scan-state persistence fails

### Add Plugin Repository

1. Open the Jellyfin dashboard.
2. Go to `Plugins` -> `Repositories`.
3. Add a new repository.
4. Use this repository URL:

   `https://raw.githubusercontent.com/FizzyMUC/jellyfin-oscars-plugin/main/manifest.json`

### Install Plugin

1. Go to `Catalog`.
2. Search for `Jellyfin Oscars`.
3. Install the plugin.
4. Restart Jellyfin.
5. Install `JavaScript Injector` if you want Oscar badges in Jellyfin Web.
6. Reload your browser after both plugins are installed.

### Manual installation

1. Download the latest release:

   `https://github.com/FizzyMUC/jellyfin-oscars-plugin/releases/download/v1.0.6/jellyfin-oscars-v1.0.6.zip`

2. Extract the release contents into:

   `/config/plugins/Oscars`

3. Restart Jellyfin.

Project repository:

`https://github.com/FizzyMUC/jellyfin-oscars-plugin`

## Configuration

The plugin is configured from the Jellyfin plugin settings page.

### OMDb API Key

This setting is required for enrichment. You can request an OMDb API key at:

https://www.omdbapi.com/apikey.aspx

### Enable Oscar enrichment

Enables or disables Oscar metadata enrichment for eligible movies. When disabled, the plugin does not query OMDb or update Oscar tags.

### Cache duration

Controls how long OMDb-derived results are cached before they are refreshed. Longer values reduce OMDb requests. Shorter values refresh metadata more aggressively.

### Scheduled refresh

Enables the plugin's background refresh task. When enabled, Jellyfin can refresh Oscar metadata automatically without manual interaction.

### Refresh batch size

Controls how many movies are processed in one run. Lower values reduce load and spread work over more runs. Higher values refresh faster but may produce more OMDb traffic in a single pass.

### Create Oscar Winners collection

When enabled, the plugin maintains a collection named `Oscar Winners`.

### Create Oscar Nominees collection

When enabled, the plugin maintains a collection named `Oscar Nominees`.

### Include winners in nominees collection

When enabled, movies tagged as Oscar winners are also included in `Oscar Nominees`. When disabled, the nominees collection contains only non-winning nominees.

### Collection disable behavior

If `Create Oscar Winners collection` is disabled, the plugin deletes the `Oscar Winners` collection. If `Create Oscar Nominees collection` is disabled, the plugin deletes the `Oscar Nominees` collection. Deletion is handled during manual rebuild and scheduled sync runs.

## Badge integration

### JavaScript Injector

Jellyfin Oscars uses `JavaScript Injector` for Jellyfin Web badges. Install `JavaScript Injector` separately, then restart Jellyfin and reload your browser.

Without `JavaScript Injector`, Oscar metadata, tags, scans, and collections still work normally. Only the Jellyfin Web badge UI is unavailable.

### Install JavaScript Injector plugin

1. Go to `Catalog`.
2. Install `JavaScript Injector`.
3. Restart Jellyfin.
4. Reload your browser.

Repository:

`https://github.com/n00bcodr/Jellyfin-JavaScript-Injector`

If you do not already have it installed, follow the installation instructions in that repository.

## Usage

- Start a manual scan or rebuild from the plugin settings, or wait for the scheduled refresh
- Manual scans now run in the background, so the settings page returns immediately
- Library refreshes continue in batches and keep their progress across restarts
- Open a movie detail page in Jellyfin Web
- The badge appears for movies that have Oscar tags

## Notes

- The badge only works in Jellyfin Web (browser UI)
- Mobile and TV clients may not support it
- `JavaScript Injector` is required for Jellyfin Web badges
- Jellyfin Oscars no longer patches Jellyfin Web files directly

## Troubleshooting

- Badge not visible: make sure `JavaScript Injector` is installed, Jellyfin has been restarted, and your browser tab has been reloaded
- No tags: make sure OMDb is configured correctly and the movie has an IMDb ID
- Still not working: start a manual scan, then check the plugin logs for progress and OMDb errors

## Collections

Oscar collections are maintained automatically when their corresponding settings are enabled. Membership is based on the Oscar tags currently stored on local Jellyfin items. Collection membership is updated during manual rebuilds and scheduled refresh runs. Users can still modify collections manually, but the plugin will continue to reconcile membership against the current Oscar tag state on later sync runs.

## UI integration

For tagged movies, the plugin adds an Oscar badge to the item detail page in Jellyfin Web. The badge appears in the metadata row alongside the existing item metadata such as ratings, runtime, and playback timing. The badge is derived from the Oscar tags created by the server-side enrichment flow.

## Limitations

- The plugin depends on OMDb data quality and completeness.
- Movies need valid IMDb IDs in Jellyfin metadata for OMDb lookups to work.
- OMDb request limits still apply, especially on the free tier.
- OMDb exposes awards information as text, so the plugin has to parse and normalize that field rather than consume a strongly structured awards model.

## Roadmap

Longer term, the plugin is intended to move beyond OMDb by using Oscar Atlas as a dedicated structured awards source.

## License

MIT License

Copyright (c) 2026 Pascal Marter

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
