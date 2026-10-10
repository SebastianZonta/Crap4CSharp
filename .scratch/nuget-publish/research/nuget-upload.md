# Publish `crap4csharp` to nuget.org — research

Goal: `dotnet tool install -g crap4csharp` works from any machine/CI.

## What's already sufficient (no change needed)

- `PackAsTool=true` + `ToolCommandName=crap4csharp` in
  `src/Crap4CSharp/Crap4CSharp.csproj:19-20` — exactly the two properties
  required to package a console app as a tool
  ([source](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create)).
- Package metadata present: `PackageId`, `Description`, `PackageTags`,
  `PackageLicenseExpression=MIT`, `PackageReadmeFile` (csproj:9-18).
- `PUBLISHING.md` secret guidance is correct: API key with **Push** scope,
  created at nuget.org Account settings → API Keys, stored as repo secret
  `NUGET_API_KEY`. Matches the documented key-creation flow (Push scope +
  glob pattern + copy-once secret)
  ([source](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package),
  [scopes](https://learn.microsoft.com/en-us/nuget/nuget-org/scoped-api-keys)).
- Version-from-`v*`-tag flow (`-p:Version=`) is sound; csproj `Version` is
  dev-only. Note first release is prerelease (`0.1.0-beta.1`).
- Package ID `crap4csharp` satisfies nuget.org naming rules and currently
  returns **404** on nuget.org → appears available; first push reserves it
  and assigns ownership to the pushing account
  ([source](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package)).

## Gaps (must fix before publish)

1. **BLOCKER — `dotnet pack` fails today.** Verified 2026-10-10 with SDK
   10.0.112: `error NU5019: File not found:
   '.../src/LICENSE'`. The csproj packs `../LICENSE` (resolves to
   `src/LICENSE`, missing); the real file is the repo-root `LICENSE`
   (`../../LICENSE` from the csproj dir). Fix the `None Include` path
   (src change, no doc change fixes this).
2. **`.github/workflows/publish.yml` does not exist** (no `.github/`
   dir at all), though `PUBLISHING.md` references it. Someone must create
   the workflow: build → test → `dotnet pack -p:Version=<tag>` →
   `dotnet nuget push <nupkg> --api-key ${{ secrets.NUGET_API_KEY }}
   --source https://api.nuget.org/v3/index.json`
   ([source](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-push)).

## Publish / install notes (all sourced)

- Push command: `dotnet nuget push <file> --api-key <key> --source
  https://api.nuget.org/v3/index.json`
  ([source](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-push)).
  The `NUGET_API_KEY` env-var alternative needs NuGet 7.6 / SDK ≥ 10.0.300
  ([source](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package))
  — local SDK is 10.0.112, so pass `--api-key` explicitly unless CI uses a
  newer SDK.
- Scope the key's glob to `crap4csharp` (least privilege) with a real
  expiry; keys are copy-once secrets
  ([source](https://learn.microsoft.com/en-us/nuget/nuget-org/scoped-api-keys)).
- Validation/indexing after push usually takes < 15 min; package shows
  under Unlisted until indexed
  ([source](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package)).
  Dry-run feed available: `https://int.nugettest.org` (same source).
- Install: `dotnet tool install -g crap4csharp` once a **stable** version
  exists. While only prerelease exists, consumers need `--prerelease` or
  `--version`
  ([source](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install)).
  Consumers need the .NET 10 runtime (or `--allow-roll-forward`) since the
  tool targets `net10.0` (same source).
- Signing is optional — unsigned packages are accepted; only signed ones
  must pre-register their certificate (same publish-a-package source).
