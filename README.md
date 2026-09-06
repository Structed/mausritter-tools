# mausritter-tools

Browser-based tools for the [Mausritter](https://mausritter.com/) tabletop roleplaying game.

Built as a [Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/hosting-models#blazor-webassembly)
standalone app, so the whole site is static files and runs entirely in the visitor's browser.

**Live site:** https://structed.github.io/mausritter-tools/

## Tools

### Settlement generator

Rolls a complete mouse settlement from a seed: its name, size, governance, inhabitants, notable
features, industry and what is happening as the players arrive, plus the tavern and every shop with
its keeper, quirks and priced stock. Stock is also rendered as Mausritter-style item cards.

- **Deterministic.** A settlement is a pure function of its seed and settings, so the same link
  always rebuilds the same place.
- **Lock and re-roll.** Lock anything worth keeping, hand-edit anything you would rather write
  yourself, or re-roll a single entry without disturbing its neighbours.
- **Share, export, print.** The address bar carries the seed and settings; the JSON export carries
  the full state including locks and edits; the print stylesheet produces a clean settlement sheet
  and a cut-out card sheet.

## Repository layout

```
.github/workflows/deploy.yml     Build + deploy to GitHub Pages
src/MausritterTools.Core/        Domain logic: tables, generators, serialisation
src/MausritterTools.Web/         Blazor WebAssembly app
tests/MausritterTools.Core.Tests/  Unit tests
tools/Import-SrdTables.ps1       Regenerates the SRD data files
MausritterTools.slnx             Solution
global.json                      Pinned .NET SDK band
```

`MausritterTools.Core` holds everything that is not UI, so the generators can be tested without a
browser. `MausritterTools.Web` is a thin Blazor layer over it.

## Running locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```pwsh
dotnet run --project src/MausritterTools.Web
```

The app is then served at the URL printed in the console (http://localhost:5xxx).

```pwsh
dotnet test                                              # run the unit tests
dotnet publish src/MausritterTools.Web -c Release -o publish   # what deployment publishes
```

> [!TIP]
> Installing the `wasm-tools` workload (`dotnet workload install wasm-tools`) enables
> runtime relinking and AOT, which produces a noticeably smaller download. It is optional.

## Data

Table data ships as JSON under `src/MausritterTools.Web/wwwroot/data/`, split by provenance. Every
file records where it came from and under what licence, and the About page renders that record
directly rather than restating it by hand.

| Directory | Contents | Editing |
| --- | --- | --- |
| `data/srd/` | Settlement, non-player mice, gear, hireling and spell tables | **Generated.** Change the importer and re-run it. |
| `data/house/` | Services and shops, mouse names, host objects | Hand-edited. |

### Regenerating the SRD tables

The Mausritter SRD is published as clean GitHub-flavoured markdown with the dice notation carried in
each table header, which makes it a far better source than retyping. The importer parses it and
writes the JSON:

```pwsh
pwsh ./tools/Import-SrdTables.ps1            # uses a local cache if present
pwsh ./tools/Import-SrdTables.ps1 -Refresh   # re-download the SRD first
```

The importer fails loudly if the SRD layout changes, rather than silently emitting a smaller table.

### A note on the shops

**Mausritter has no shops or services table.** The SRD provides only scattered hooks: taverns appear
in hamlets and larger, warbands are recruited in a town or city, human-made goods are found near
human populations, a bank charges 1% to retrieve what it holds, repairs cost 10% of an item's price
per usage dot, and "the size of the settlement determines what types of hireling are available"
without ever saying how.

Everything else about shops here — which services exist, how settlement size gates them, their
stock, quirks and names — is **an unofficial house rule original to this project**, and is labelled
as such in the UI. Each service records the SRD rule it was extrapolated from in its `srdBasis`
field, which the app shows under "where this comes from".

Mouse names are original for the same reason: the SRD has no name tables, and the lists used by the
official generator are not published under a licence that permits reuse.

## Implementation notes

A few decisions that are easy to undo by accident:

- **Randomness is PCG32, not `System.Random`.** `System.Random`'s seeded output is not stable across
  .NET versions, so using it would silently invalidate every previously shared seed URL on an SDK
  upgrade. `Pcg32Tests` pins the generator against the reference vectors.
- **Each field draws from its own stream**, derived as `SplitMix64(rootSeed ^ FNV1a(fieldPath))`.
  This is what lets one shop be re-rolled without shifting any other value. String hashing must not
  use `string.GetHashCode()`, which is randomised per process.
- **The JSON source generator discards property initialisers.** A property absent from a data file
  arrives as `null` regardless of any `= ""` or `= []` default, so the data models coerce null in
  their getters. `JsonDefaultsTests` guards this; without it, the first optional field anyone adds
  to a data file becomes a `NullReferenceException` during generation.

## Deployment

Every push to `main` runs `.github/workflows/deploy.yml`, which publishes the app and
deploys `publish/wwwroot` to GitHub Pages. Pull requests build the same way but do not deploy.

Two things are needed to make a Blazor WASM app work on GitHub Pages, and the workflow
handles both:

- **Base path** — a project page is served from `/<repo-name>/`, not `/`, so the workflow
  rewrites `<base href="/" />` in the published `index.html`. If the repository is ever
  renamed the correct path is picked up automatically; a `<user>.github.io` repository
  keeps `/`.
- **Client-side routing** — GitHub Pages has no SPA fallback, so `index.html` is copied to
  `404.html`. Deep links return a 404 status but still boot the app, and the Blazor router
  takes over from there.

A `.nojekyll` marker is also published so the `_framework` directory is never stripped.

### One-time setup

In **Settings → Pages**, set **Source** to **GitHub Actions**. Without this the deploy job
fails with a "Pages not enabled" error.

## Licence and attribution

This work is based on [Mausritter](https://mausritter.com), a product of Losing Games and Isaac
Williams, and is licensed for use under the
[Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/)
licence.

Mausritter Tools is an independent production by the mausritter-tools contributors and is not
affiliated with Losing Games. It is published under the Mausritter Third Party Licence.

Mausritter is copyright Losing Games.

Both notices are also shown in the site footer, because the Third Party Licence requires its text to
appear on the website where the work is promoted, not only in the repository. The Mausritter and
Losing Games logos are deliberately not used anywhere in this project.
