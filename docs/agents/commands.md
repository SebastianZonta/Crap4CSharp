# Commands — build, test, run, E2E, CI

`src/Crap4CSharp` is a net10.0 Exe packed as dotnet tool `crap4csharp`;
`test/Crap4CSharp.Tests` is xunit and references src. ImplicitUsings +
Nullable on. Requires .NET 10 SDK to build; the packed tool needs the
.NET 10 runtime. Full CLI flag reference lives in
`src/Crap4CSharp/README.md` — what follows is only the workflow that file
does not state.

## Build / test

- `dotnet build Crap4CSharp.sln` — full build. Warnings fail the build
  (`TreatWarningsAsErrors`, `AnalysisMode All` in `Directory.Build.props`).
- `dotnet test Crap4CSharp.sln` — full suite.
- `dotnet test --filter "FullyQualifiedName~<TestName>"` — single test.

## Run locally

- `dotnet run --project src/Crap4CSharp -- <args>` — run without installing.
  Flags: `--help`, no args, `--changed`, `--diff <ref>`, `<path...>`, `--path`,
  `--threshold`, `--format text|json|csv`, `--top`, `--min-cc`,
  `--no-coverage` (see `src/Crap4CSharp/README.md` for semantics).

## E2E matrix

- `bash e2e/matrix.sh` — E2E CLI matrix against an installed tool build.
  Pack + install first:
  `dotnet pack src/Crap4CSharp/Crap4CSharp.csproj -c Release -p:Version=0.0.0-e2e -o ./nupkg`
  then `dotnet tool install -g crap4csharp --add-source ./nupkg --version 0.0.0-e2e`.

## CI order (`.github/workflows/ci.yml`)

`restore` → `dotnet format whitespace --verify-no-changes --no-restore` →
`build -c Release --no-restore` → `test -c Release --no-build`.
Run the format check before pushing; it fails on whitespace/style drift.
Style: LF, 4 spaces (`.editorconfig`).
