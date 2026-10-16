Status: ready-for-agent

# Spec: audit fixes (gate correctness, coverage trust, output safety)

## Problem Statement

An audit of the repo found the quality gate can go green when it should go
red: mistyped file paths exit 0 with an empty report, stale coverage data can
be picked up silently, `git status` quoting drops files with spaces, and
coverage attribution can attach the wrong method's numbers. As a user running
this tool in CI, I cannot trust a passing run.

## Solution

Fix the gate-trust issues in three slices (must fix, should fix, nice to
have), each independently shippable: fail loudly on bad inputs, attribute
coverage honestly (bounded fallback or N/A), and harden machine-readable
output. No new flags, no new layers, no behavior change for already-correct
runs.

## User Stories

1. As a CI owner, I want a run with a mistyped or missing file path to fail
   with exit 1 naming the file (must fix), so that a typo can never look
   like clean code.
2. As a developer reading a report, I want coverage matched only to the
   method it belongs to and N/A otherwise (should fix), so that refactors
   that move lines never flip scores without a real coverage change.
3. As a tooling consumer parsing `--format json`, I want control characters
   in names escaped per JSON rules (nice to have), so that downstream
   parsers never choke on a valid report.

## Implementation Decisions

- Slice 1 (must fix, gate trust): reject nonexistent explicit paths with
  exit 1 before analysis; select the newest Cobertura report by write time
  instead of smallest path string; strip `git status` quoting before the
  extension check; map file/IO failures to exit 1 instead of unhandled
  crash; bound the nearest-line coverage fallback (far matches become N/A);
  include constructors and nested-type methods in analysis (or document the
  exclusion in the README if deliberately kept).
- Slice 2 (should fix, consistency): count `default:` and switch-expression
  arms under one documented rule; list local functions as their own rows
  instead of folding branches into the outer method; surface `--path`
  resolution failures instead of falling back to the startup directory;
  preserve prior coverage until a new coverage run succeeds; scan project
  files once per run and share across modules; treat malformed coverage
  numbers as skip-or-fail, never silent zero.
- Slice 3 (nice to have, output): escape control characters in JSON output;
  treat carriage returns like newlines in CSV quoting; truncate text-table
  fields to column width; broaden process-kill exception handling on the
  cancel path.
- Seam for all slices: the existing injectable-app seam (run the CLI
  application with a fake coverage runner) stays the single test seam; no
  new seams.
- Glossary terms used throughout: file discovery, changed files, diff
  files, coverage run, analysis, progress log, violation.
- No ADR conflicts: no `docs/adr/` records exist in this repo.

## Testing Decisions

- A good test here fails when the behavior breaks and passes for any
  refactor: assert exit codes and stderr/stdout content through the
  injectable-app seam, not internal call shapes.
- Slice 1 tests: nonexistent explicit path exits 1; two timestamped
  coverage reports select the newest; quoted `git status` name with spaces
  is analyzed; unreadable source / unwritable report path exits 1;
  far-away same-name coverage yields N/A; complex constructor appears in
  the report.
- Slice 2 tests: statement-switch and switch-expression forms of the same
  logic score equally; local-function branches score on the inner row;
  bad workspace root exits 1; failed coverage run keeps prior results.
- Slice 3 tests: names with newline/control characters round-trip through
  a strict JSON parser; long names keep table columns aligned.
- Prior art: the existing CLI-application tests (fake coverage runner,
  temp-dir workspaces) and the parser/coverage unit tests.

## Out of Scope

- New CLI flags or options.
- Parallel coverage runs or caching across invocations.
- Changing the CRAP formula or the default threshold.
- E2E matrix, NuGet publishing, or CI workflow changes.
- The ~6 cosmetic nits listed in the audit (progress interleaving,
  flag-ordering edges) unless touched incidentally.

## Further Notes

- Source: full-repo audit (all 21 source files read; build/tests not run).
  Most important first: slices are ordered by gate impact.
- Fixing slices 1–2 will move scores on existing repos (constructors newly
  visible, fallback matches becoming N/A): announce before enforcing the
  gate on them.
- Lean opportunity noted alongside: single coverage-lookup path, one sort
  location, removal of thin sync/duplicate wrappers (~60 lines, no
  dependency changes).
