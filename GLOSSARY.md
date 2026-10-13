# Glossary

- **file discovery**: finding the C# files to analyze (all/src, changed, diff, or explicit paths).
- **changed files**: uncommitted `.cs` changes from `git status --porcelain` (tracked modifications plus untracked files), scoped to `<root>/src/`, or to the whole tree when no `src/` exists.
- **diff files**: committed `.cs` additions and modifications between `<ref>` and HEAD (merge-base..HEAD) from `git diff --name-only -z --no-renames --diff-filter=ACMR <ref>...HEAD --`; untracked files are excluded by construction (use changed files for those); same `src/` scope rule as changed files.
- **coverage run**: the `dotnet test` XPlat Code Coverage step per module.
- **analysis**: parsing methods (Roslyn) and scoring CRAP from complexity + Cobertura coverage.
- **command echo**: the one-line `$ dotnet test … in <dir>` summary of the coverage invocation on stderr.
- **file list**: the `Found N C# file(s).` summary plus one `Analyzing <path>` line per file on stderr.
- **progress log**: all human-facing stderr lines (command echo + streamed child output + file list + warnings/errors).
- **violation**: an analyzed method whose numeric CRAP score is strictly greater than the effective threshold (`N/A` rows are never violations).
