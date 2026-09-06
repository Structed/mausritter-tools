# mausritter-tools

Browser-based tools for the [Mausritter](https://mausritter.com/) tabletop roleplaying game.

Built as a [Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/hosting-models#blazor-webassembly)
standalone app, so the whole site is static files and runs entirely in the visitor's browser.

**Live site:** https://structed.github.io/mausritter-tools/

## Repository layout

```
.github/workflows/deploy.yml     Build + deploy to GitHub Pages
src/MausritterTools.Web/         Blazor WebAssembly app
MausritterTools.slnx             Solution
global.json                      Pinned .NET SDK band
```

## Running locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```pwsh
dotnet run --project src/MausritterTools.Web
```

The app is then served at the URL printed in the console (http://localhost:5xxx).

To produce the same output the deployment publishes:

```pwsh
dotnet publish src/MausritterTools.Web -c Release -o publish
```

> [!TIP]
> Installing the `wasm-tools` workload (`dotnet workload install wasm-tools`) enables
> runtime relinking and AOT, which produces a noticeably smaller download. It is optional.

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
