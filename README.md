# Jellyfin Oscars

Jellyfin Oscars is a Jellyfin server plugin that enriches movie metadata with Oscar (Academy Awards) information. It uses OMDb as its external data source and integrates the derived result into Jellyfin through tags, web UI detail-page badges, and optional library collections.

## Features

- Detects Oscar wins and nominations from OMDb
- Adds Jellyfin Oscar status tags that are then used by the UI badge and collections
- Displays an Oscar badge on the movie detail page in Jellyfin Web
- Optionally creates and maintains the `Oscar Winners` and `Oscar Nominees` collections
- Provides a scheduled background refresh task
- Provides manual scan and rebuild actions from the plugin settings page

## How it works

The plugin requires an OMDb API key and uses IMDb IDs that are already present in Jellyfin metadata. For eligible movies, it requests the OMDb `Awards` field, parses Oscar-related results, normalizes them into a simple internal state, and applies the result to Jellyfin items as tags. Those tags are then used for the web badge and for Oscar collection membership.

## Installation

### Repository installation

1. Open the Jellyfin dashboard.
2. Go to `Plugins` -> `Repositories`.
3. Add the repository URL:

   `<INSERT REPOSITORY URL>`

4. Open `Catalog` and install `Jellyfin Oscars`.
5. Restart Jellyfin.

### Manual installation

1. Download the latest release:

   `<INSERT RELEASE URL>`

2. Extract the release contents into:

   `/config/plugins/Oscars`

3. Restart Jellyfin.

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

Enables the plugin's background refresh task. When enabled, Jellyfin can run Oscar metadata refresh automatically without manual interaction.

### Refresh batch size

Controls how many movies are processed in one scheduled run. Lower values reduce load and spread work over more runs. Higher values refresh faster but may produce more OMDb traffic in a single pass.

### Create Oscar Winners collection

When enabled, the plugin maintains a collection named `Oscar Winners`.

### Create Oscar Nominees collection

When enabled, the plugin maintains a collection named `Oscar Nominees`.

### Include winners in nominees collection

When enabled, movies tagged as Oscar winners are also included in `Oscar Nominees`. When disabled, the nominees collection contains only non-winning nominees.

### Collection disable behavior

If `Create Oscar Winners collection` is disabled, the plugin deletes the `Oscar Winners` collection. If `Create Oscar Nominees collection` is disabled, the plugin deletes the `Oscar Nominees` collection. Deletion is handled during manual rebuild and scheduled sync runs.

## Collections

Oscar collections are maintained automatically when their corresponding settings are enabled. Membership is based on the Oscar tags currently stored on local Jellyfin items. Collection membership is updated during manual rebuilds and scheduled refresh runs. Users can still modify collections manually, but the plugin will continue to reconcile membership against the current Oscar tag state on later sync runs.

## UI integration

For tagged movies, the plugin adds an Oscar badge to the item detail page in Jellyfin Web. The badge appears in the metadata row alongside the existing item metadata such as ratings, runtime, and playback timing. The badge is derived from the Oscar tags created by the server-side enrichment flow.

## Limitations

- The plugin depends on OMDb data quality and completeness.
- Movies need valid IMDb IDs in Jellyfin metadata for OMDb lookups to work.
- OMDb request limits still apply, especially on the free tier.
- OMDb exposes awards information as text, so the plugin has to parse and normalize that field rather than consume a strongly structured awards model.

## Development

This project is implemented as a Jellyfin server plugin in .NET and uses Jellyfin plugin APIs together with the OMDb HTTP API.

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
