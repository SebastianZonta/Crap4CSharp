# crap4csharp

CRAP metric analyzer for C# — port of `crap4java`.

Combines method cyclomatic complexity (Roslyn) with Cobertura method coverage
and reports CRAP scores. Quality gate: fails when the worst score exceeds the threshold.

## Formula

`CRAP = CC^2 * (1 - coverage)^3 + CC`

- `CC` is cyclomatic complexity (integer `>= 1` for concrete methods).
- `coverage` is the method coverage fraction `0.0..1.0` from Cobertura line-rate data.
- Unknown coverage is reported as `N/A` and sorts after numeric scores.
- Every concrete method-like member is its own row: constructors and
  nested-type methods included; local functions score on their own row, not
  folded into the outer method.
- One branch rule: each `case`/`default` label and each switch-expression arm
  adds 1 to `CC`, so statement-switch and switch-expression forms of the same
  logic score equally.
- Coverage is matched to the method it belongs to (same class and method
  name, line within a few lines); farther matches score `N/A` instead of
  borrowing another method's coverage.

Threshold: `8.0` by default (configurable via `--threshold`). Max numeric CRAP `>` threshold fails the run.

## Prerequisite

Framework-dependent tool on **.NET 10 runtime**.
Install the runtime first or the command fails to start with a missing-framework error.

## Install

Global (public feed):

```bash
dotnet tool install -g crap4csharp
```

Prerelease or pinned version:

```bash
dotnet tool install -g crap4csharp --prerelease
dotnet tool install -g crap4csharp --version x.y.z
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
crap4csharp --diff origin/develop
crap4csharp src/MyApp/Sample.cs
crap4csharp module-a module-b
crap4csharp --path ./MySolution.sln
crap4csharp --threshold 15 --format json --output reports/crap.json
crap4csharp --top 20 --min-cc 3 --no-coverage
```

- `(no args)` — analyze all C# files under `<root>/src/`
- `--changed` — analyze changed C# files under `<root>/src/` (`git status --porcelain`), else whole tree when no `src/` exists
- `--diff <ref>` — Analyze committed C# changes between `<ref>` and HEAD (merge-base..HEAD):
  committed additions and modifications only (deletions excluded; untracked files excluded — use `--changed` for those),
  scoped to `<root>/src/`, else whole tree when no `src/` exists. Unknown ref or non-git directory fails
  with exit `1` plus a fetch hint. Cannot be combined with file arguments or `--changed`.
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
- `--violations-only` — show only methods with CRAP above the threshold
  (`N/A` rows hidden; the exit-`2` gate still uses the full result set).
- `--no-coverage` — skip the `dotnet test` coverage run; reuse existing
  `TestResults/**/coverage.cobertura.xml` (missing → `N/A`).
- `--no-progress` — silence the progress log on stderr.
- `--help, -h` (also `-?`) — print usage; `--version` — print version.
- `--` — treat all following args as paths (e.g. files starting with `-`).
- Both `--flag value` and `--flag=value` forms are accepted.
- Unknown `--*` options fail with exit `1`.

The progress log (`Found N C# file(s).`, `$ dotnet test …` echoes with live
output, `Analyzing <file>` lines) goes to stderr; stdout carries only the
report, so `--output` files and pipes stay clean. `--no-progress` silences it.

Empty selection prints `No C# files to analyze.` and exits successfully.

## Sample output

```text
CRAP Report
===========
Method                         Class                                 CC    Cov%     CRAP
----------------------------------------------------------------------------------------
Monster                        Crap4Subject.Calculator               15   80.0%     16.8
Grade                          Crap4Subject.Calculator                7   86.7%      7.1
CRAP threshold exceeded: 16.8 > 8.0
```

Exit code `2`: the worst score (`16.8`) exceeds the default threshold (`8.0`).

## Exit codes

- `0` success, threshold respected (includes empty selection)
- `1` invalid CLI usage (unknown option, bad flag value,
  `--changed` cannot be combined with file arguments,
  `--diff` cannot be combined with file arguments or `--changed`)
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
