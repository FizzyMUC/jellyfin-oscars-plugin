# Jellyfin Oscars

<img width="702" height="401" alt="logo" src="https://github.com/user-attachments/assets/4d9f8948-65a4-4939-9231-4d2d29d654f1" />

A Jellyfin plugin that enriches movie metadata with Oscar (Academy Awards) information, including tags, collections, and optional web UI badges.

## Features

- Adds "Oscar Winner" and "Oscar Nominated" tags to movies
- Automatically creates collections for Oscar winners and nominees
- Optional Oscar badge in the Jellyfin web UI
- Uses OMDb for metadata enrichment

## How it works

The plugin requires an OMDb API key and uses IMDb IDs that are already present in Jellyfin metadata. For eligible movies, it requests the OMDb `Awards` field, parses Oscar-related results, normalizes them into a simple internal state, and applies the result to Jellyfin items as tags. Those tags are then used for the web badge and for Oscar collection membership.

## Screenshots

<img width="831" height="444" alt="Screenshot 2026-03-19 at 12 24 06" src="https://github.com/user-attachments/assets/9dcc94fb-902d-40a2-80bc-7ace9d266ead" />


Jellyfin Oscars neatly adds curated Collections for Oscar Nominees or Oscar Winners. Technically each winner was a nominee once so you decide whether you want to include them.

<img width="1483" height="767" alt="Screenshot 2026-03-19 at 12 24 52" src="https://github.com/user-attachments/assets/d6a32505-5b9f-4c49-9690-b4ecac532b68" />

Collections include all movies that match said criteria. They are sorted automatically.

<img width="977" height="411" alt="Screenshot 2026-03-19 at 12 25 09" src="https://github.com/user-attachments/assets/19641480-ac7b-4859-9c36-f1fc8cc8d435" />

Each movie detail screen gets a little badge/icon next to the ratings that indicates if it is an Oscar Winner or Nominee.


## Installation

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

### Manual installation

1. Download the latest release:

   `https://github.com/FizzyMUC/jellyfin-oscars-plugin/releases/download/v1.0.2/jellyfin-oscars-v1.0.2.zip`

2. Extract the release contents into:

   `/config/plugins/Oscars`

3. Restart Jellyfin.

Project repository:

`https://github.com/FizzyMUC/jellyfin-oscars-plugin`

## Enable Web UI Badge (Important)

This step is optional, but it is required if you want the Oscar badge to appear in Jellyfin Web.

### Install JavaScript Injector Plugin

1. Go to `Catalog`.
2. Install `JavaScript Injector`.
3. Restart Jellyfin.

Repository:

`https://github.com/n00bcodr/Jellyfin-JavaScript-Injector`

If you do not already have it installed, follow the installation instructions in that repository.

### Add Script in JavaScript Injector

Use this exact script:

```javascript
(function () {
    'use strict';

    if (window.__jellyfinOscarsLoaderInjected) {
        return;
    }
    window.__jellyfinOscarsLoaderInjected = true;

    if (document.querySelector('script[data-jellyfin-oscars-loader="true"]')) {
        return;
    }

    var s = document.createElement('script');
    s.src = '/plugins/Jellyfin.Oscars/scripts/oscarDetailBadge.js';
    s.setAttribute('data-jellyfin-oscars-loader', 'true');
    document.head.appendChild(s);
})();
```

1. Open `JavaScript Injector` settings.
2. Add a new script.
3. Paste the code above.
4. Enable it.
5. Save.
6. Reload your browser.

## Usage

- Run `Scan Library for Oscars` if that task is available, or use the manual scan/rebuild action in the plugin settings
- Or wait for automatic metadata enrichment to process your library
- Open a movie detail page in Jellyfin Web
- The badge appears for movies that have Oscar tags

## Notes

- The badge only works in Jellyfin Web (browser UI)
- Mobile and TV clients may not support it
- The badge requires the `JavaScript Injector` plugin

## Troubleshooting

- Badge not visible: verify `JavaScript Injector` is installed and enabled, then reload the browser
- No tags: make sure OMDb is configured correctly and the movie has an IMDb ID
- Still not working: try a manual scan or rebuild from the plugin settings page

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
