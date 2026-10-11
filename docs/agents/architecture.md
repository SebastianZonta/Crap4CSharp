# Architecture — execution flow, modules, coverage, exit codes

Entrypoint: `Program.Main` → `CliApplication.ExecuteAsync`
(`src/Crap4CSharp/Program.cs`, `src/Crap4CSharp/CliApplication.cs`):
resolve root → find files → group by module root → coverage per module →
analyze → sort worst-first → gate.

## File scope

- No args = `FindAllCsFilesForDirectory` (`src/` preferred, else whole tree);
  `bin`/`obj` always skipped. Directories, `.sln`/`.slnx`, `.csproj` args
  expand to C# files (`SourceFileFinder.cs`).
- `--changed` = `git status --porcelain` under `<root>/src/`
  (`ChangedFileDetector.cs`); outside a git repo it exits `1`
  (`git status failed`), never crashes.

## Modules and coverage

- Module root = nearest ancestor dir containing `*.csproj`, else workspace
  root (`ModuleRootForNormalized` in `CliApplication.cs`). Coverage runs once
  per module: `GenerateCoverageAsync` targets the single `.sln` if exactly one
  exists at the workspace root, else the sibling test project if found, else
  the module root (`TestTargetFor`, `FindSiblingTestProject`).
  `--no-coverage` skips the `dotnet test` run.
- Coverage XML = first ordinal match under
  `<moduleRoot>/TestResults/**/coverage.cobertura.xml` (`FindCoberturaXml`);
  missing → stderr warning (`CoverageMissingWarning` in `ToolConstants.cs`),
  scores `N/A` and sort last.

## Results and exit codes

- Metrics sorted worst-first (`CrapScore.CompareByCrapDesc`); `--min-cc`
  filters by complexity, `--top` truncates; report via `ReportFormatter`
  (text|json|csv), also written to `--output` with dirs created.
- Exit codes: `0` ok (incl. empty selection, prints `No C# files to analyze.`
  from `ToolConstants.cs`), `1` usage error (unknown option, bad value,
  `--changed` + file args), `2` max numeric CRAP `>` threshold
  (default `8.0`, `ThresholdExceeded`, stderr message).

## Test seam and analysis

- Test seam: `Program.Run(args, projectRoot, output, err, coverageRunner)` —
  inject a fake `CoverageRunner`; see `CliApplicationTest.cs` / `MainTest.cs`.
- Complexity via Roslyn (`CSharpMethodParser.cs`,
  `Microsoft.CodeAnalysis.CSharp` 5.9.0); coverage via `CoberturaCoverageParser.cs`;
  formula `CRAP = CC^2 * (1-coverage)^3 + CC` (`CrapScore.cs`).
