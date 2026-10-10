# 01 — Default coverage run yields N/A on src/test-split layouts

Status: resolved

Fixed: `CliApplication.TestTargetFor` now resolves the `dotnet test` target —
when the workspace root holds exactly one `.sln`/`.slnx`, coverage runs against
that solution (results still land under each module's `TestResults/`); otherwise
the module root is used as before. New `CoverageRunner.GenerateCoverageAsync`
overload takes the target explicitly; old overload delegates unchanged.
Verified live on `../crap4subject`: default mode now reports real scores
(Monster CC 15 @ 80% -> CRAP 16.8, gate exit 2) instead of N/A.
26 regression tests added (suite 192/192 green).

## Observed

`crap4csharp --path ../crap4subject` (default mode, with coverage) prints:

```
Warning: Cobertura XML not found at .../crap4subject/src/Crap4Subject/TestResults. Coverage will be N/A.
```

and every method scores N/A, even though the subject has coverage XML under
`test/Crap4Subject.Tests/TestResults/` and `TestResults/`.

## Root cause

`CliApplication.AnalyzeByModuleAsync` runs `dotnet test <moduleRoot>` per
module, where the module root is the nearest `*.csproj` dir above each file
under `src/` — i.e. the *library* project (e.g. `src/Crap4Subject`), not a test
project. `dotnet test` on a non-test project exits 0 but produces no Cobertura
file, so `FindCoberturaXml` finds nothing and coverage silently degrades to N/A.

Verified: `dotnet test ../crap4subject/src/Crap4Subject --collect:"XPlat Code Coverage" ...`
exits 0 and creates no results directory.

## Workaround (verified)

Copy (or generate) `coverage.cobertura.xml` under `<moduleRoot>/TestResults/`
and pass `--no-coverage`. Then scores are real
(Monster CC 15 @ 80% -> CRAP 16.8) and the `--threshold` gate exits 2 correctly.

## Options

1. Run `dotnet test` against the workspace root / solution instead of each
   `src/` module root.
2. Discover test projects referencing each module and run those.
3. Keep current behavior and document that split layouts require `--no-coverage`
   with a prior `dotnet test --collect` run.

Needs a maintainer decision; not fixed by the flag-matrix pass.

## Comments

- Filed from the CLI flag-matrix verification pass (`.scratch/cli-flag-matrix/spec.md`).
