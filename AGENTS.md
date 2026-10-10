# crap4csharp — CRAP analyzer for C# (port of crap4java)

`src/Crap4CSharp` (net10.0 Exe, packed as dotnet tool `crap4csharp`) +
`test/Crap4CSharp.Tests` (xunit, references src). ImplicitUsings + Nullable on.

## Commands (verified)

- `dotnet build Crap4CSharp.sln` — full build (~3s, 0 warnings)
- `dotnet test Crap4CSharp.sln` — full suite
- `dotnet test --filter "FullyQualifiedName~<TestName>"` — single test
- `dotnet run --project src/Crap4CSharp -- <args>` — run locally
  (`--help`, no args = all of `src/`, `--changed`, `<path...>`)

## Testing (mandatory delegation)

- Dispatch all unit-test work (writing, fixing, running: single files and full suite) through vanilla subagent delegation; the main session never authors or runs tests itself.
- Each testing subagent validates through the delegate plugin: it spawns one nested validator child via its `execute` → `tools.delegate` call, which re-checks the work (diff scope, green build + suite) and steers it back on course. No exceptions, every time.

## Architecture

`Program.Main` → `CliApplication.Execute` (`src/Crap4CSharp/CliApplication.cs:19`):
find files → group by module root → coverage per module → analyze → sort
worst-first → gate on max CRAP `> 8.0`.

- Module root = nearest dir walking up from each file containing `*.csproj`
  (`CliApplication.cs:138`); coverage runs once per module.
- Coverage XML expected at `<moduleRoot>/TestResults/**/coverage.cobertura.xml`
  (first ordinal match); missing → `Warning: Cobertura XML not found…`,
  scores `N/A` (`CliApplication.cs:63-85`).
- `--changed` shells `git status --porcelain`; not a git repo here, so that
  mode has nothing to detect until the tree is versioned.
- Exit codes: `0` ok (incl. empty selection), `1` usage error, `2` threshold.
- Test seam: `Program.Run(args, projectRoot, output, err, coverageRunner)` —
  inject a fake `CoverageRunner`; see `CliApplicationTest.cs` / `MainTest.cs`.
- Complexity via Roslyn (`CSharpMethodParser.cs`, `Microsoft.CodeAnalysis.CSharp`
  5.9.0); formula `CRAP = CC^2 * (1-coverage)^3 + CC` (`CrapScore.cs`).

## Release

- Local `Version` in csproj is dev-only; release version comes from the `v*`
  git tag (see `PUBLISHING.md`). Requires `.NET 10` runtime at install.

## Agent skills

### Issue tracker

Issues live as local markdown files under `.scratch/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Default five canonical labels used as-is. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout (one `GLOSSARY.md` + `docs/adr/` at root). See `docs/agents/domain.md`.
