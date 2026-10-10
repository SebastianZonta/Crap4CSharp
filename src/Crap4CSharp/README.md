# crap4csharp

CRAP metric analyzer for C# — port of `crap4java`.

Combines method cyclomatic complexity (Roslyn) with Cobertura method coverage
and reports CRAP scores. Quality gate: fails when the worst score exceeds the threshold.

## Formula

`CRAP = CC^2 * (1 - coverage)^3 + CC`

- `CC` is cyclomatic complexity (integer `>= 1` for concrete methods).
- `coverage` is the method coverage fraction `0.0..1.0` from Cobertura line-rate data.
- Unknown coverage is reported as `N/A` and sorts after numeric scores.

Threshold: `8.0` by default (configurable via `--threshold`). Max numeric CRAP `>` threshold fails the run.

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
crap4csharp --version
crap4csharp
crap4csharp --changed
crap4csharp src/MyApp/Sample.cs
crap4csharp module-a module-b
crap4csharp --path ./MySolution.sln
crap4csharp --threshold 15 --format json --output reports/crap.json
crap4csharp --top 20 --min-cc 3 --no-coverage
```

- `(no args)` — analyze all C# files under `<root>/src/`
- `--changed` — analyze changed C# files under `<root>/src/` (`git status --porcelain`)
- `<path...>` — analyze files, or for directory args analyze `<dir>/src/**/*.cs`
- `--path <dir|sln|csproj>` — workspace root (default: current directory).
  A `.sln`/`.csproj` file resolves to its directory. Positional paths resolve
  against this root.
- `--output, -o <file>` — write the report to `<file>` too (directories created).
  The report is always printed to stdout as well.
- `--threshold, -t <n>` — CRAP gate (default `8.0`). Exit `2` when max CRAP `> n`.
- `--format, -f <text|json|csv>` — report format (default `text`).
- `--top <n>` — show only the `n` worst methods.
- `--min-cc <n>` — hide methods with complexity below `n` (default `1`).
- `--no-coverage` — skip the `dotnet test` coverage run; reuse existing
  `TestResults/**/coverage.cobertura.xml` (missing → `N/A`).
- `--help, -h` — print usage; `--version` — print version.
- `--` — treat all following args as paths (e.g. files starting with `-`).
- Both `--flag value` and `--flag=value` forms are accepted.
- Unknown `--*` options fail with exit `1`.

Empty selection prints `No C# files to analyze.` and exits successfully.

## Exit codes

- `0` success, threshold respected (includes empty selection)
- `1` invalid CLI usage (unknown option, bad flag value,
  `--changed` cannot be combined with file arguments)
- `2` CRAP threshold exceeded (`>` threshold, default `8.0`)

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
