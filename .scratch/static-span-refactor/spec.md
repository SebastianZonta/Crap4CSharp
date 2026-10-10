# Spec: Static-class, extension-method and Span refactor

Status: ready-for-agent

## Problem Statement

The tool works and the suite is green, but string-heavy helpers allocate on every call: coverage keys are concatenated per method lookup, the namespace fallback runs a regex over the whole source, the argument parser slices substrings per flag and normalizes case via allocations, the report escapers do multi-pass replaces, and path/prefix checks repeat string comparisons. Related helpers are also scattered (two enumeration-options factories, path logic inside the CLI orchestrator, key-building inline in two modules), so the next performance orreading pass pays the same discovery cost again. A prior full-refactor session toward static classes, extension methods and Span/ReadOnlySpan usage was lost when the session crashed, so this work needs a single written spec to redo it deterministically.

## Solution

Reorganize without changing behavior: move duplicated or misplaced helpers into small static classes, expose repeated operations as extension methods, and switch hot string handling to Span and ReadOnlySpan so intermediate allocations disappear. All public behavior (CLI surface, exit codes, report text, scores, threshold gate) stays byte-compatible; the change is internal structure plus fewer allocations per file, per method, and per report row.

## User Stories

1. As a developer running analysis on a large repo, I want coverage-key construction to allocate once per key, so that per-method lookups stop churning intermediate strings.
2. As a developer analyzing many files, I want class-name resolution without a whole-source regex scan, so that per-file overhead is a single span pass.
3. As a developer passing CLI flags, I want flag name/value splitting over spans, so that every argv element stops allocating substrings.
4. As a developer using `--format`, I want format-name matching case-insensitively over spans, so that no trim plus uppercase copy is allocated.
5. As a developer passing `--threshold` or `--top`, I want numeric parsing from spans, so that value validation costs no temporary strings.
6. As a developer reading the text report, I want coverage and CRAP numbers formatted from stack buffers, so that thousand-method reports allocate less per row.
7. As a developer exporting JSON, I want JSON escaping as a single span pass, so that backslash and quote handling costs one scan.
8. As a developer exporting CSV, I want CSV escaping as a single span pass, so that comma, quote and newline detection shares one scan.
9. As a developer sorting results, I want in-place span sorting of the already-collected list, so that report formatting stops allocating enumerator plus copy per format.
10. As a developer with mixed-case file extensions, I want extension checks over spans, so that path filtering stays allocation-free.
11. As a developer using changed mode, I want status-line and rename-target parsing to keep spans until the final path materialization, so that each status line allocates at most once.
12. As a developer with deep module trees, I want root-containment checks over spans, so that per-file module attribution avoids prefix-string temporaries.
13. As a developer combining workspace roots with relative paths, I want a span-based rooted-combine helper, so that callers pass through spans without double materialization.
14. As a developer reading coverage XML, I want class-name normalization in one span pass, so that slash and backslash conversion costs one allocation.
15. As a maintainer, I want a single shared enumeration-options factory, so that file discovery and coverage-file search stop drifting apart.
16. As a maintainer, I want path operations (under-directory check, extension strip, rooted combine, get-or-add grouping) as extension methods in one place, so that the CLI orchestrator reads as orchestration only.
17. As a maintainer, I want CLI span helpers (flag split, flag classification, value taking) as extensions in one place, so that the argument parser is a single reviewable loop.
18. As a maintainer, I want coverage-key build and trailing-line parse behind one static helper, so that the analyzer and the XML parser share key semantics.
19. As a maintainer, I want process-tree killing as a process extension, so that both process call sites share one cancellation path.
20. As a maintainer, I want sync-over-async wrappers unified behind one helper, so that repeated blocking calls read identically.
21. As a maintainer, I want max-score aggregation as an enumerable extension, so that the entry point and the CLI share one gate computation.
22. As a maintainer, I want stale-coverage deletion behind one file-system helper, so that race-tolerant delete logic lives in exactly one spot.
23. As a contributor, I want the same externally visible scores, report bytes, exit codes and warning messages as before the refactor, so that existing scripts and snapshots keep passing unchanged.

## Implementation Decisions

- Scope is internal reorganization plus allocation reduction only; CLI surface, CRAP formula, default threshold, exit-code contract, report layout, and message wording stay frozen.
- New static homes (one per concern, fewest that covers the duplication): coverage-key helpers, path/span helpers, argument-span helpers, enumeration-options factory, file-system helpers. Existing static holders stay where they are; only genuinely shared logic moves.
- Extension-method policy: extensions only for operations used from two or more call sites or that read better at the call site (path checks, span escaping, key building, process kill, max-score). Single-use private helpers stay private static methods — no extension for its own sake.
- Span policy: spans end at materialization boundaries (file-system APIs, XML attribute reads, Roslyn tree input, process argv, test-visible string signatures). No span types leak into public or test-visible signatures; callers that today take strings keep taking strings, with span overloads added only where the caller already holds a span.
- Class-name resolution: replace the whole-source regex fallback with a manual forward span scan for the namespace declaration plus span-based filename handling; the resulting fully qualified name must equal the current output for all existing parser tests.
- Coverage keys: single canonical builder for the class/method separator plus line separator, shared by the analyzer lookups, the method index, and the XML parser; trailing-line parsing reuses the existing span-based routine as the one implementation.
- Argument parsing: single-pass argv loop preserved; flag/value split returns spans; case-insensitive comparisons use ordinal span comparison; numeric validation uses span-based number parsing with identical error messages.
- Report formatting: single-key descending ordering with nulls-last preserved; header and separator constants hoisted as today; JSON and CSV escaping become single-pass span appends producing byte-identical output; per-row doubles format with invariant culture as today.
- Sorting: reuse the already-collected list with an in-place span sort at the formatting layer instead of enumerator-plus-copy ordering; observable row order unchanged.
- Process handling: argument lists stay structural (no shell quoting); cancellation still kills the whole process tree via the shared extension; sync wrappers delegate to the same async core.
- Deliberate non-goals encoded as decisions: no new abstractions with a single implementation, no config for fixed values, no framework-owned strings converted (XML attributes, process API, Roslyn input), no trim/AOT work (Roslyn dependency is not trim-safe).

## Testing Decisions

- What makes a good test here: assert externally visible behavior (exit codes, report bytes, matched scores, files analyzed, error messages), never implementation details (whether a helper is an extension, whether a loop uses spans, builder capacities, sort mechanism). The existing suite already follows this; refactored code must keep it.
- Modules tested through existing seams only (confirmed with user): the program entry seam with injected fake coverage runner for CLI-level behavior; the command-executor abstraction with fake executor for process tests; pure-function seams for argument parsing, coverage analysis/lookup, report formatting variants, and Cobertura parsing. No new seams; helpers moved into static classes or extensions are covered through these same entry points.
- Prior art to follow: entry-seam tests injecting a fake runner; process tests capturing command lists with a fake executor; exact-text report assertions under invariant culture; parser tests pinning malformed-value fallback to zero and empty-input empty-map behavior; changed-detector tests pinning line/rename/extension semantics.
- Manual verification: full build plus full suite must stay green; plus targeted runs (help text, no-arg scan, explicit paths, changed mode where versioned, JSON and CSV outputs diffed byte-for-byte against pre-refactor output on a sample repo).

## Out of Scope

- Changing the CRAP formula, the default threshold, exit codes, report layout, CLI flags, or user-facing message text.
- Algorithmic performance work beyond allocation removal (indexing, XML streaming, Roslyn single-pass) — owned by the parallel performance effort, not repeated here.
- Coverage attribution semantics (nearest-match behavior) changes.
- AOT/trim publishing support.
- New features, new commands, config files, multi-package splits, or tool renames.
- Test-authoring or test-running itself beyond the acceptance bar above (per repo policy that work is delegated separately).

## Further Notes

- Suggested implementation order by payoff: coverage keys plus class-name resolution first (per-method/per-file wins), then argument-parser spans, then report escaping plus sorting, then path/process/static-class consolidation.
- The `--changed` path cannot be fully exercised on machines whose checkout is not a versioned repo; that manual check moves to a versioned checkout.
- If any moved helper's diff grows beyond its owning concern, split it before review — the point is small reviewable moves with identical observable behavior.
