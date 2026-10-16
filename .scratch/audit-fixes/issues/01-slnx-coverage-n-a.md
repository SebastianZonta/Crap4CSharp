Status: ready-for-human
Fixed in: d8a00e7 (not pushed)

# Solution-root run reports N/A coverage while path run reports numbers

## Observation

E2E against `/home/coffpa/Downloads/InterviewCentral` (777 C# files,
`--no-coverage`, existing `TestResults/` in the target), same binary,
same tree:

- `--path <target>` → 2019 rows with real coverage numbers (e.g. 0.0%),
  gate trips, exit 2.
- Positional `<target>/InterviewCentral.slnx` → 2019 rows, coverage N/A
  throughout, gate stays silent, exit 0.

Same files, same coverage XML on disk, different scores. One of the two
modes attributes wrong; the silent exit 0 is the dangerous direction for
coverage-gated CI.

## Suspected cause

Solution expansion and whole-tree discovery likely assign different module
roots per file (or resolve the workspace root differently), so the
per-module Cobertura lookup misses in solution mode and every method falls
back to N/A. Needs confirming by comparing the module root assigned to the
same file in each mode.

## Acceptance

- Both modes agree on coverage for the same file set (both numeric or
  both N/A with the warning), or the difference is documented in the
  README limitations.
- E2E check: slnx-root run and `--path` run on the same tree produce the
  same max CRAP and the same exit code.

## Comments

- Filed from E2E agent 3 findings (2026-10-16); E2E agent 2 confirms the
  `--path` side numbers.
