## Problem Statement

As a C# developer using crap4csharp in CI or locally, I see the full CRAP report (every analyzed method) even when I only care about the methods that fail the quality gate. With large codebases the passing rows bury the methods I must fix, and `--top N` shows the N worst methods rather than the N worst failures.

## Solution

Add a boolean flag `--violations-only` that filters the printed report (text, json, csv, plus the `--output` file copy) to only the violation rows: methods whose numeric CRAP score is strictly greater than the effective `--threshold`. The threshold gate (exit code, stderr message) keeps working on the full pre-filter result set, so filtering the report can never mask a failure.

## User Stories

1. As a developer, I want to run `crap4csharp --violations-only` so that the report shows only methods with CRAP above the default threshold (8.0).
2. As a developer, I want to combine `--violations-only --threshold 15` so that the report shows only methods with CRAP above my custom threshold.
3. As a CI engineer, I want `--violations-only` to work with `--format json` so that downstream tooling receives only the violation entries as a JSON array.
4. As a CI engineer, I want `--violations-only` to work with `--format csv` so that the CSV contains only the header plus violation rows.
5. As a developer, I want methods with unknown coverage (`N/A`, null CRAP) hidden when `--violations-only` is set, so that I only see proven violations.
6. As a developer, I want `--violations-only --min-cc 3` so that low-complexity methods are excluded before the violations filter is applied.
7. As a developer, I want `--violations-only --top 20` so that I see at most the 20 worst violators, not the 20 worst methods overall.
8. As a developer, I want an empty violators result to print the normal empty report (text header only, `[]` for json, header-only for csv) with exit `0`, so that scripts can distinguish "no violators" from "no files found".
9. As a developer, I want the exit-`2` gate unchanged when `--violations-only` is set, so that a failing run still fails even though passing rows are hidden.
10. As a developer, I want `--violations-only=value` to be rejected as a usage error (exit `1`), so that the flag behaves like the existing boolean flags (`--no-coverage`, `--no-progress`, `--changed`).
11. As a developer, I want `--violations-only` documented in `--help` and the tool README, so that I discover it alongside `--threshold`, `--top`, and `--min-cc`.
12. As a developer, I want `--violations-only` to work identically in all file-discovery modes (default src scan, `--changed`, explicit paths), so that I can focus on violators in any workflow.

## Implementation Decisions

- New domain term: **violation** = one analyzed method row whose numeric CRAP score is strictly greater than the effective threshold. Rows with `N/A` (null score) are never violations.
- New boolean option `--violations-only` on the CLI arguments record (default `false`), parsed alongside the existing boolean flags with `RejectValue` semantics (no inline `=value` form) and the bare-token `Missing value` / `Unknown option` handling for consistency.
- Filtering point: inside the application execution after analysis and worst-first sort, in the existing chain `--min-cc` filter → **violations filter** → `--top` truncation. The violations predicate is `CrapScore is not null && CrapScore > Threshold`.
- Gate point unchanged: maximum CRAP and the `ThresholdExceeded` check run against the full pre-filter metric set (same stderr message, same exit `2` semantics). Filtering affects only the formatted report and the `--output` file copy, which is rendered from the same filtered list.
- Report rendering needs no per-format change: text/json/csv formatters already render whatever entry list they receive, so the filtered list flows into the existing `FormatAs` path for stdout and file output.
- Help text: one new `Options:` line for `--violations-only` in the usage text, same style as neighboring lines.
- No new threshold value, no per-file aggregation, no short-flag alias.

## Testing Decisions

- Good tests assert external behavior through the highest seam (CLI args in, report text + exit code out), not parser or formatter internals.
- Primary seam (one seam total): the existing test seam `Program.Run(args, projectRoot, output, err, coverageRunner)` with an injected fake coverage runner — it covers parsing, filtering order, report content, file output, and exit codes end to end.
- Supporting parser-level cases at the `CliArgumentsParser.Parse` seam only for flag mechanics (default off, flag on, `=value` rejected), following existing parser test prior art.
- Prior art: parser tests for boolean flags (`ProgressSpinnerTest`, `CliArgumentsParserTest`); end-to-end report + exit-code tests via the `Program.Run` seam (`MainTest`, `CliApplicationTest`); report-shape tests (`ReportFormatterTest`) are not extended because no formatter change is needed.
- Cases to cover through the primary seam: violations-only hides passing and `N/A` rows; custom `--threshold` is respected; `--top` applies after the violations filter (top-N violators); empty violators prints the empty report shape per format with exit `0`; exit `2` still fires on the full set when violators exist; `--output` file matches filtered stdout.

## Out of Scope

- Per-file aggregation (one row per file with worst CRAP); the report stays one row per method.
- A separate threshold for the filter; the gate threshold is reused.
- Short-flag alias (e.g. `-v`); long form only.
- Showing `N/A` rows in violations mode; they stay hidden.
- Formatter changes (sorting, columns, number formatting) beyond passing the filtered list.
- E2E matrix additions beyond the standard build/test/format checks.

## Further Notes

- Glossary follow-up: add the one-line **violation** definition to `GLOSSARY.md` when this is implemented.
- Naming note: `--over-threshold` was considered and rejected as too vague; `--violations-only` was chosen to state the intent (show only gate violations).
