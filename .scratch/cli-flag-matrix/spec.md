# Spec: CLI flag-matrix verification (crap4subject)

Goal: exercise every CLI flag combination against `../crap4subject` and confirm
correct behavior and exit codes (0 ok, 1 usage error, 2 CRAP gate).

Matrix covered (all verified 2026-10-10 unless noted in issues/):
- Modes: no-args AllSrc, `--changed` (clean tree and dirty tree), explicit file
  (relative/absolute), explicit dir (workspace root), `--` separator
  (including `-- -h` and `-- --changed` treated as filenames).
- Options: `--path` (dir, `.slnx` file, missing dir), `--output`/`-o`
  (space, `=`, nested-dir creation, empty value error, missing value error),
  `--threshold`/`-t` (valid, `=` form, last-wins, invalid `0`/`abc`/`NaN`/`inf`/missing),
  `--format`/`-f` (`text`/`json`/`csv`, case-insensitive, `=` form, last-wins,
  invalid `yaml`), `--top` (valid, `=` form, last-wins, invalid `0`/`abc`/missing/empty),
  `--min-cc` (same shapes as `--top`), `--no-coverage` (value rejected),
  `--help`/`-h`/`-?`/`/?` (value rejected, help wins over `--version`),
  `--version` (value rejected), `--changed` (value rejected, conflict with file args).
- Errors: unknown option, missing value, `--changed` + file args, `--path` to
  nonexistent dir (exit 1, `Project directory not found`), `--changed` outside a
  git repo tree (exit 1 from git).
- Gate: with coverage XML staged under the module root, `--threshold 8`
  exits 2 with `CRAP threshold exceeded: 16.8 > 8.0`; `--threshold 1000` exits 0.
- Fixed during matrix: `-?` printed `Unknown option` instead of help (fix in
  `src/Crap4CSharp/CliArgumentsParser.cs`, `--help` condition now includes `-?`).
