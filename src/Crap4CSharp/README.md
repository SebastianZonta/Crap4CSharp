# crap4csharp

CRAP metric analyzer for C# — port of `crap4java`.

Combines method cyclomatic complexity (Roslyn) with Cobertura method coverage
and reports CRAP scores. Quality gate: fails when the worst score exceeds the threshold.

## Formula

`CRAP = CC^2 * (1 - coverage)^3 + CC`

- `CC` is cyclomatic complexity (integer `>= 1` for concrete methods).
- `coverage` is the method coverage fraction `0.0..1.0` from Cobertura line-rate data.
- Unknown coverage is reported as `N/A` and sorts after numeric scores.

Threshold: `8.0`. Max numeric CRAP `> 8.0` fails the run.

## Prerequisite

Framework-dependent tool on **.NET 10 runtime**.
Install the runtime first or the command fails to start with a missing-framework error.

## Install

Global (public feed):

```bash
dotnet tool install -g crap4csharp
```

Pinned per repo (local tool manifest):

```bash
dotnet new tool-manifest
dotnet tool install crap4csharp
```

Update / uninstall:

```bash
dotnet tool update -g crap4csharp
dotnet tool uninstall -g crap4csharp
```

## Usage

```bash
crap4csharp --help
crap4csharp
crap4csharp --changed
crap4csharp src/MyApp/Sample.cs
crap4csharp module-a module-b
```

- `(no args)` — analyze all C# files under `src/`
- `--changed` — analyze changed C# files under `src/` (`git status --porcelain`)
- `<path...>` — analyze files, or for directory args analyze `<dir>/src/**/*.cs`
- `--help` — print usage

Empty selection prints `No C# files to analyze.` and exits successfully.

## Exit codes

- `0` success, threshold respected (includes empty selection)
- `1` invalid CLI usage (`--changed` cannot be combined with file arguments)
- `2` CRAP threshold exceeded (`> 8.0`)

## Limitations (known)

- Multi-project attribution is approximate: coverage is matched per module by the
  nearest available entry when no exact match exists.
- Test assemblies are excluded from coverage by coverlet defaults.

## Not the other similarly-named tool

An older CRAP tool for .NET exists under a nearby name (`crap4dotnet` by a third
party). Same CRAP family, different product: it uses `dotnet-crap analyze` with
JSON output, threshold 30, and exits `1`=threshold / `2`=error. This tool keeps
the `crap4java` contract: `crap4csharp` command, text table sorted worst-first,
threshold `8.0`, exits `1`=usage / `2`=threshold.

## License

MIT — see `LICENSE` (includes port attribution to `crap4java`).
