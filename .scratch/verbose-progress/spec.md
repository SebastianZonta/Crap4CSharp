## Problem Statement

As a C# developer running crap4csharp locally or in CI, I cannot tell what the tool is doing while it runs. File discovery, the coverage run, and analysis are silent apart from a Braille spinner (`Discovering files…` / `Running coverage…` / `Analyzing…`), and the `dotnet test` child output (restore / build / test, including Roslyn output) is swallowed entirely — only the exit code is checked. By contrast, the EF CLI echoes the `dotnet` command it invokes and streams everything that command prints, and prettier/lint list files as they are processed.

## Solution

Make the tool self-explanatory on stderr with an EF-style command echo plus a prettier-style file list, with no spinner. Every normal run prints a `Found N C# file(s).` summary, then per module a `$ dotnet test … in <dir>` echo line followed by the live verbatim `dotnet test` output, then one `Analyzing <path>` line per file as it is analyzed. Stdout keeps only the report (plus the existing empty-selection message) so text/json/csv piping is unaffected. The existing `--no-progress` flag becomes the silent switch; `--no-coverage` skips the command-echo blocks.

## User Stories

1. As a developer, I want to see `Found N C# file(s).` on stderr after discovery, so that I know the scope before coverage runs.
2. As a developer, I want to see `$ dotnet test … in <dir>` on stderr before each coverage run, so that I can reproduce the exact invocation myself.
3. As a developer, I want the echoed command to quote args with spaces and show paths relative to the workspace root, so that I can copy-paste it.
4. As a developer, I want the child `dotnet test` stdout+stderr streamed live to the tool stderr verbatim (restore / build / Roslyn / test output, interleaved as dotnet emits it), so that a slow or failing coverage run is transparent like running `dotnet restore` by hand.
5. As a developer, I want one `Analyzing <relative path>` line per file on stderr during analysis, so that I can follow progress like a prettier/lint file list.
6. As a developer, I want `Analyzing` paths relative to the workspace root in analysis order, so that the log is readable in deep trees.
7. As a CI engineer, I want stdout to carry only the report (or the empty-selection message), so that `--format json|csv` piping and `--output` file content never contain log lines.
8. As a CI engineer, I want the log lines on stderr even when stderr is redirected to a file, so that redirected CI logs still explain the run (unlike the old spinner, which hid on redirect).
9. As a developer, I want `--no-progress` to silence all log lines (summary, command echo, child output, per-file lines) while keeping warnings, errors, and the stdout report, so that I can opt into quiet runs.
10. As a developer, I want `--no-coverage` runs to skip the `$ dotnet test` echo and child-output blocks but still print `Found` and `Analyzing` lines, so that reuse-XML runs stay transparent.
11. As a developer working in a multi-module workspace, I want the global `Found` line first, then per module in order (command echo + streamed output + that module's `Analyzing` lines), so that I can tell which module is slow or failing.
12. As a developer, I want a failing coverage run to still print its streamed output plus the existing `Coverage command failed…` error on stderr with exit `1`, so that diagnostics are not lost.
13. As a developer, I want the existing missing-coverage warning and threshold-exceeded error on stderr unchanged in position (after the log lines for their module/phase), so that I keep the diagnostics I rely on.
14. As a developer, I want an empty file selection to print only the existing empty-selection message with exit `0` and no log lines, so that scripts keep their current behavior.
15. As a developer, I want `--help` to document that progress logging goes to stderr and that `--no-progress` disables it, so that I discover the behavior alongside the other options.

## Implementation Decisions

- New domain terms: **command echo** = the one-line `$ dotnet test … in <dir>` summary of the coverage invocation; **file list** = the `Found` summary plus one `Analyzing <path>` line per file; **progress log** = all human-facing stderr lines (echo + streamed child output + file list + existing warnings/errors).
- Channel contract: stdout carries only the formatted report and the empty-selection message; everything in the progress log goes to stderr. No existing stdout contract changes.
- Default-on, one off switch: the progress log prints on every normal run; the existing boolean `--no-progress` option silences it (summary, echo, child stream, per-file lines). No new `--verbose` flag is added.
- Spinner removal: the Braille spinner component is removed from the execution flow (no `\r` rewrites), because rewrite-based animation corrupts the streamed child output and contradicts the self-explanatory log goal.
- Echo content: the full coverage command line (command name, target, coverage-collect arguments, results directory) plus the working directory rendered relative to the workspace root where possible, with minimal quoting for args containing spaces.
- Streaming contract: the command executor streams the child process stdout and stderr through to the tool stderr live and verbatim (no prefix, no buffering to end-of-run, order preserved as emitted), then returns the child exit code; the existing non-zero-exit failure path (error message, exit `1`) is unchanged.
- File-list content: one summary line with the discovered file count after discovery, then one `Analyzing <relative path>` line per file emitted as analysis visits it, in the existing analysis order. Discovery internals (directory walk order) are not logged per file — only the analysis list.
- Phase order preserved: global discovery → per module (coverage echo + stream → analysis file lines → missing-coverage warning if applicable) → report on stdout → threshold gate message on stderr. Multi-module runs repeat the per-module block in module order.
- Empty selection prints no log lines (no `Found 0` line); behavior and exit code unchanged.
- Help text: the `--no-progress` usage line is reworded from spinner language to progress-log language.
- Seam change (single seam, extended not added): the existing highest test seam (application entry with injected output/err writers and injected coverage runner backed by a fake command executor) is kept; the command-executor contract gains a streaming sink (child output forwarded to the injected err writer) so fake executors can assert echo content, streamed output, ordering, and silence behavior end to end. No second seam is introduced.

## Testing Decisions

- Good tests assert external behavior through the highest seam (CLI args in; stdout report + stderr log + exit code out), not executor or spinner internals.
- Primary seam (one seam total): the existing application-run seam with injected writers and fake coverage/command executors — it covers parsing, discovery count, echo content, stream passthrough and ordering, per-file lines, `--no-progress` silence, `--no-coverage` skip, warnings, and exit codes end to end.
- Supporting cases at the argument-parser seam only for `--no-progress` mechanics (default on, flag silences), following existing boolean-flag test prior art.
- Prior art: end-to-end stdout/stderr + exit-code tests via the application-run seam; fake-executor command-recording tests for the coverage runner; parser tests for boolean flags.
- Cases to cover through the primary seam: `Found` count line on stderr with stdout report pure; `$` echo contains the coverage arguments and working dir; fake child output appears verbatim on stderr in emission order; `Analyzing` lines list each analyzed file once with relative paths; `--no-progress` yields no log lines but keeps warnings/errors/report; `--no-coverage` skips echo/stream but keeps file lines; multi-module order (found → per-module echo/stream/file lines); coverage failure keeps streamed output plus the failure error and exit `1`; empty selection prints only the existing message with exit `0`; redirected-stderr runs still log (no auto-silence for the new lines).

## Out of Scope

- A new `--verbose`/`--quiet` flag or renaming `--no-progress`; the existing flag name is kept.
- Splitting the coverage run into separate `restore`/`build`/`test` invocations; the tool keeps its single coverage command per module.
- Color, prefixes, timestamps, counters (`i/N`), or machine-readable progress events; lines are plain and human-readable.
- Logging to stdout or to a log file; stderr only.
- Per-file timing, parallelism of analysis, or coverage attribution changes.
- Formatter, threshold, gate, or exit-code changes beyond preserving current contracts.
- E2E matrix additions beyond the standard build/test/format checks.

## Further Notes

- Naming note: `Found` / `$ … in …` / `Analyzing …` vocabulary was chosen to mirror EF (`$` command echo + live tool output) and prettier/lint (per-file list) in the user's own terms.
- Glossary follow-up: when implemented, record **command echo**, **file list**, and **progress log** as defined above if a glossary is adopted.
- Risk note: the executor streaming change alters the single test seam's fake; existing fake executors that only record commands and return an exit code will need a streaming-sink update.
