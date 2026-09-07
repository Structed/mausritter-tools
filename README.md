# mausritter-tools

Browser-based tools for the [Mausritter](https://mausritter.com/) tabletop roleplaying game,
in English and German.

Built as a [Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/hosting-models#blazor-webassembly)
standalone app, so the whole site is static files and runs entirely in the visitor's browser.

**Live site:** https://structed.github.io/mausritter-tools/

## Tools

### Settlement generator

Rolls a complete mouse settlement from a seed: its name, size, governance, inhabitants, notable
features, industry and what is happening as the players arrive, plus the tavern and every shop with
its keeper, quirks and priced stock. Stock is also rendered as Mausritter-style item cards, and the
whole place is drawn as a hand-inked map.

- **Deterministic.** A settlement is a pure function of its seed and settings, so the same link
  always rebuilds the same place, map included.
- **Lock and re-roll.** Lock anything worth keeping, hand-edit anything you would rather write
  yourself, or re-roll a single entry without disturbing its neighbours. The map can be re-drawn on
  its own, leaving the settlement untouched.
- **Share, export, print.** The address bar carries the seed, the settings and the language; the
  JSON export carries the full state including locks and edits; the settlement can also be exported
  for [Fantasia Archive](#fantasia-archive); the map downloads as SVG; and the print stylesheet
  produces a clean settlement sheet and a cut-out card sheet.
- **English or German.** The same seed produces the same settlement in either, so a link shared
  between a German and an English player shows the same place; only the words differ.

#### The map

The map is drawn inside the silhouette of the settlement's host object rather than on open ground,
because a Mausritter settlement is a human-scale object annotated at mouse scale: an oak hollow, a
farmhouse wall, a cow skull, a boot. Roads are grown first and buildings placed along them, so the
building count follows from the settlement's size instead of emerging from a subdivision. Shops and
the tavern are keyed to numbered buildings and cross-referenced in a legend.

#### Fantasia Archive

A settlement can be exported for [Fantasia Archive](https://github.com/vishiri/fantasia-archive), an
open-source worldbuilding database, and read back again.

A Fantasia Archive project is a *folder* of newline-delimited JSON files, one per document type, and
a page running in a browser cannot hand a folder over. The export is therefore a ZIP holding that
folder, which the reader unpacks and merges in through **Project → Advanced → Merge another project
into the current one**. Instructions ship inside the ZIP, deliberately *beside* the folder rather
than in it: the merge reads every file in the folder it is given as a database, so a readme sitting
next to the dumps would break the import.

The settlement becomes a Location, its tavern and shops become Organizations, their keepers become
Characters and their stock becomes Items, all cross-linked. One piece of gear is one Item however
many shops sell it, with a price per shop.

The settlement's document also carries the seed, the locks and the hand edits in a field no
blueprint declares, which the app therefore never renders, edits or discards. That is what makes the
journey a round trip: save the project back out of Fantasia Archive and import the folder here, and
the settlement returns intact and still re-rollable. A project this tool did not write cannot be
imported, and says so — generation is a pure function of a seed, and it does not run backwards.

Some things that are easy to get wrong here, all covered by `FantasiaArchiveExportTests`:

- **This targets Fantasia Archive v1**, the format every released version reads. The rewrite on the
  project's `master` branch replaces it with a single-file SQLite `.faproject`, whose own
  documentation still warns that pre-release files must be recreated after a schema change. It is
  not supported until it settles.
- **Fantasia Archive validates nothing on import.** No version, no checksum, no schema. Every
  mistake below imports "successfully" and simply produces a broken project, so the tests stand in
  for the validator that does not exist.
- **Every document needs a revision.** The loader writes with `new_edits: false` and will not invent
  one, so a document without `_rev` and a matching `_revisions` is dropped silently.
- **Both ends of a relationship must be written.** The app only fills in the far side when a user
  saves a document in its own UI, never on import.
- **Its select values are keys, not prose.** `locationType` and `groupType` are fixed English
  strings in the app's own dropdowns, so they are derived from the settlement's size value and the
  service id and stay English in a German export. This is the same rule the data files follow, just
  applied to somebody else's keys.
- **Document ids are derived from the settlement's whole generation state** — its seed, settings,
  locks and hand edits — not drawn at random, so re-exporting the same settlement produces the same
  file and merging it a second time changes nothing instead of duplicating it. Deriving them from
  the seed alone would be worse than useless: the same seed makes a different place once the size or
  a lock changes, and the app would then discard the second export in silence rather than import it.

Fantasia Archive is GPL-3.0. Only the identifiers needed to write a file it accepts are re-derived
here; no blueprint source, tooltip or value list is copied.

## Repository layout

```
.github/github-app.yml           GitHub Copilot app scripts and project instructions
.github/workflows/deploy.yml     Build + deploy to GitHub Pages
src/MausritterTools.Core/        Domain logic: tables, generators, mapping, rendering
src/MausritterTools.Web/         Blazor WebAssembly app
tests/MausritterTools.Core.Tests/  Unit tests
tools/Import-SrdTables.ps1       Regenerates the SRD data files, and checks the translations
MausritterTools.slnx             Solution
global.json                      Pinned .NET SDK band
```

`MausritterTools.Core` holds everything that is not UI, so the generators can be tested without a
browser. `MausritterTools.Web` is a thin Blazor layer over it.

`.github/github-app.yml` surfaces the commands below as buttons in the
[GitHub Copilot app](https://docs.github.com/copilot/reference/github-copilot-app-reference/repository-configuration),
so the app can be run, tested and re-imported without typing them. The app asks you to review the
file before it will run anything from it.

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
| `data/ui.json` | Every string the app shows that is not a table entry | Hand-edited. |
| `data/i18n/<locale>/` | Translations of all of the above | Hand-edited, including the translations of generated files. |

### Regenerating the SRD tables

The Mausritter SRD is published as clean GitHub-flavoured markdown with the dice notation carried in
each table header, which makes it a far better source than retyping. The importer parses it and
writes the JSON:

```pwsh
pwsh ./tools/Import-SrdTables.ps1            # uses a local cache if present
pwsh ./tools/Import-SrdTables.ps1 -Refresh   # re-download the SRD first
```

The importer fails loudly if the SRD layout changes, rather than silently emitting a smaller table.
It also re-checks every translation against what it has just written, because the generated files
are the one thing that can change underneath a translation and make it quietly wrong.

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

## Languages

The tools are published in English and German. A visitor's first arrival follows their browser; a
picker in the navigation overrides that and the choice is remembered, and a shared link carries
`?lang=` so a settlement written up in German opens in German for whoever it is sent to.

**Changing language does not change the settlement.** Every table is translated row for row, so the
same seed lands on the same rows in both languages and a link shared between a German and an English
player shows the same place — same size, same host, same shops in the same order, same map. Only the
words differ.

English is canonical. `data/srd/` and `data/house/` are written in it, and a translation is an
*overlay* under `data/i18n/<locale>/` that mirrors the same structure and is deep-merged onto the
original before it is deserialised. That keeps the loader, the validator and every generator working
on one set of tables that know nothing about languages.

Hand-writing `data/i18n/de/srd/` does not contradict the rule that `data/srd/` is generated: the
generated tree is still generated and still the only thing the importer writes. The overlay is a
separate, deliberately hand-maintained tree, and the importer re-checks it on every run.

A few things translations must respect, all of them guarded by `TranslationTests`:

- **A translated table has exactly as many rows as its original, in the same order.** Every value in
  a settlement is an index into a table, so one row fewer would shift every roll after it.
- **Keys are not prose.** `id`, dice expressions, gear category ids and the `stock.itemNames`
  allow-lists stay in English. So does a gear item's `name` and a hireling's `name`: those are the
  values three files join on, and the card rules match slot width and usage dots against them. A
  gear item shows its `label` instead. Translating a key does not fail loudly — it empties a shop's
  shelves or blanks an item card, which is why the tests check for it specifically.
- **Grammar lives in the data, not in code.** German needs an article that agrees with a noun's
  gender, so hosts carry a whole prepositional phrase (`"in einem hohlen Baumstumpf"`) and sign
  nouns carry a gender. Sign adjectives ship already declined, which works because the dative
  singular ending after a definite article is `-en` for every gender: only `Zum` versus `Zur`
  varies, so no declension code is needed anywhere.

The German text is an **unofficial fan translation by the mausritter-tools contributors**. It is not
the official German edition of Mausritter, and no official translation was used as a source. CC BY
4.0 permits translation provided the change is declared, which the data files and the site both do.

To add a language, add it to `Locale.All`, copy `data/ui.json` and the two table directories into
`data/i18n/<code>/`, and translate. The tests will tell you what you have missed.

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
- **The map's host outline is fitted to the canvas after it is generated.** Shape lobes and noise
  multiply together, so bounding each factor separately is guesswork that breaks the next time an
  archetype is tuned. Narrow archetypes also widen with settlement size, or a city inside a
  farmhouse wall ends up smaller than a hamlet inside a tree stump.
- **A lock stores a table position, not the words on the page.** Storing the words works in one
  language and falls apart in two: locking a settlement's industry in German and switching to
  English would leave one German line in the middle of an English sheet. Table-drawn values are
  pinned as `#12`, which resolves through whichever language is loaded; anything typed by hand is
  stored verbatim, because no table can reproduce someone's own words. Export format 2 carries this;
  version 1 files still open, since a pin that is not a position is read as the literal it was.
- **Shops are ordered by id, not by name.** A shop's position is its number on the map and part of
  the field path its keeper is locked under, so it has to be a property of the settlement rather
  than of the language it is read in. Stock *within* a shop is sorted by the name the reader sees,
  in their own alphabet, which affects only the order lines are printed in.
- **The ambient `CultureInfo` is never changed.** Blazor refuses a culture change during start-up
  unless the whole ICU dataset is bundled, which costs well over a megabyte of download. The only
  thing that needs a culture here is the thousands separator in a shopkeeper's purse, so that one
  number is formatted explicitly against `Locale.FormatCulture` instead.

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
