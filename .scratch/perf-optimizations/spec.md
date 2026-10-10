# Spec: Optimizaciones .NET nivel senior (perf + robustez + higiene)

Status: ready-for-agent

## Problem Statement

La herramienta funciona y el build está verde, pero el análisis desperdicia recursos de formas que un senior detecta al primer profiling: el cruce cobertura↔métodos es cuadrático, el XML de Cobertura se carga como DOM completo, el parser Roslyn hace múltiples pasadas con allocations evitables, el descubrimiento de archivos y el agrupado por módulo repiten normalizaciones de paths, y la ejecución de procesos (git + coverage) puede colgarse sin cancelación. Nada de esto rompe hoy con repos chicos, pero escala mal y es frágil en repos grandes o entornos con dirs inaccesibles.

## Solution

Aplicar en 6 etapas revisables manualmente el paquete de mejoras propuesto por la revisión senior: indexar cobertura una vez, parsear XML en streaming, optimizar el parser Roslyn y usar value types, limpiar allocs del CLI, hacer async con cancelación la ejecución de procesos, y cerrar con higiene de build y tests deterministas. Cada etapa termina con build + suite verde y es revisable por diff antes de seguir.

## User Stories

1. As a developer running the tool on a large repo, I want the coverage-to-method matching to be indexed instead of quadratic, so that analysis finishes in seconds not minutes.
2. As a developer with a large Cobertura file, I want XML parsed in a single streaming pass, so that memory stays constant instead of loading the whole DOM.
3. As a developer running analysis repeatedly, I want the Roslyn parse to skip doc-comment trivia, so that each parse is faster.
4. As a developer analyzing a file with many methods, I want a single syntax walk instead of LINQ-per-node enumeration, so that CPU waste disappears.
5. As a developer, I want ancestor checks without LINQ enumerators per method, so that deep files don't allocate thousands of throwaway objects.
6. As a developer, I want the complexity walker reused across methods, so that N methods don't cost N heap objects.
7. As a developer, I want line lookups batched over cached line tables, so that position resolution costs half.
8. As a developer, I want coverage entries as value types, so that thousands of entries don't pressure the GC.
9. As a developer, I want method descriptors as value types, so that short-lived analysis objects never hit the heap.
10. As a developer, I want the CRAP scoring inlined in the hot loop, so that per-method call overhead disappears.
11. As a developer, I want the namespace regex compile-time generated, so that there's no runtime JIT cost and the build stays AOT-friendly.
12. As a developer, I want span-based integer parsing without exceptions, so that malformed coverage values cost nanoseconds not exception handling.
13. As a developer, I want zero-alloc filename handling, so that per-file string churn disappears.
14. As a developer using `--changed`, I want git args passed as a real argument list, so that paths with spaces or quotes work correctly.
15. As a developer with uncommitted work, I want changed-file detection immune to pipe deadlocks, so that large status outputs never hang the tool.
16. As a developer, I want changed-line parsing over spans without intermediate arrays, so that thousands of changed files don't allocate per line.
17. As a developer, I want module-root resolution computed once per run, so that N files don't repeat N path normalizations.
18. As a developer, I want file discovery resilient to denied directories, so that one unreadable folder doesn't crash the run.
19. As a developer on Linux, I want case-insensitive extension matching, so that uppercase `.CS` files are found.
20. As a developer passing CLI paths, I want single-pass argument parsing, so that argv isn't scanned three times.
21. As a developer running coverage, I want async execution with cancellation, so that a hung test run is killable with Ctrl-C.
22. As a developer, I want recursive directory deletion done natively, so that cleanup doesn't enumerate, sort and stat every entry.
23. As a developer reading errors, I want exit codes and actionable messages from failed commands, so that failures are diagnosable.
24. As a developer reading the report, I want identical output produced with half the comparer work and no per-row format re-parsing, so that thousand-method reports render fast.
25. As a maintainer, I want warnings-as-errors plus full analysis mode in the build, so that nullable and style regressions fail the build instead of shipping.
26. As a maintainer, I want the test suite locale-independent and OS-aware, so that CI is green on any runner and culture.
27. As a maintainer, I want test fakes to copy their inputs, so that caller mutation can't corrupt assertions.

## Implementation Decisions

- The work is split into 6 sequential, manually reviewable stages: (1) build hygiene, (2) quadratic match + XML streaming, (3) Roslyn + value types, (4) CLI paths, (5) processes + async, (6) report + test determinism. Stages touch disjoint areas except stage 1 which is build-wide.
- Existing seams preferred: the injectable coverage-runner seam at the program entry (args, root, outputs, runner) stays the single seam for CLI-level tests; the command-executor abstraction stays the single seam for process tests. No new seams except the async variants of those two abstractions, grown at the same level (no new interfaces sprayed across the codebase).
- Coverage matching: build the per-method index once per analysis run (frozen collection keyed by class+method), then resolve each method only within its own entries. The regex-over-source namespace fallback is kept working but simplified; long-term it should derive from the already-parsed syntax tree.
- XML parsing: forward-only streaming reader carrying current-class state; malformed values fall back to zero (pinned by existing parser tests); fatal errors limited to XML/IO categories, never swallowed broadly; empty input returns a shared empty map.
- Roslyn parsing: single walker pass, manual ancestor loop with early exit, one reusable complexity walker, cached line table, doc-comment parsing disabled, latest language version.
- Short-lived data (coverage entries, method descriptors) becomes readonly value records; equality semantics used by tests are preserved.
- CLI: static sort lambdas with explicit nulls-last, single minimum lookup for the coverage file, one-shot path normalization per run, arrays instead of read-only wrappers, single-pass argv scan, resilient enumeration options.
- Processes: async with cancellation token plumbed from the entry seam; hung runs kill the whole process tree; recursive delete delegated to the framework primitive; argument lists passed structurally (no manual quoting); errors carry exit codes.
- Report: single-key descending ordering with nulls-last, invariant-culture interpolated formatting, pre-sized builder, hoisted constant header/separator, null guard with the correct exception contract.
- Build: warnings as errors, style enforced in build, full analysis mode, latest language version pinned; explicitly NOT AOT/trim publishing because the Roslyn dependency is not trim-safe.
- Test conventions from prior art kept: CLI tests inject a fake runner through the entry seam; process tests use a fake executor capturing commands; report tests assert exact text (now under explicit invariant culture).

## Testing Decisions

- What makes a good test here: assert externally visible behavior (exit codes, report text, matched scores, files analyzed), never implementation details (walker reuse, index type, builder capacity). The existing suite already follows this; new/changed tests must keep it.
- Modules tested per stage: (1) none new, whole suite must stay green under stricter build; (2) analyzer + Cobertura parser tests (scores unchanged, malformed-value fallback pinned); (3) method-parser + score + analyzer tests (identical scores/positions); (4) CLI app + argument parser + file-finder + changed-detector tests; (5) coverage-runner + process-executor + changed-detector tests incl. cancellation and large-output behavior; (6) report-formatter byte-identical text tests.
- Prior art: entry-seam tests with fake runner, fake command executor capturing command lists (now defensively copied), exact-text report assertions.
- Manual verification per stage: full build plus full suite; plus targeted manual runs (help text, no-arg scan, explicit paths, changed mode where git exists, large Cobertura file if available, Ctrl-C during a long coverage run).

## Out of Scope

- Changing the CRAP formula, the 8.0 threshold, exit-code contract, report text layout, or CLI surface. Output stays byte-compatible.
- Multi-project coverage attribution improvements beyond the existing nearest-match semantics.
- AOT/trim publishing support.
- New features, new commands, or config files.
- Splitting into multiple packages or renaming the tool command.

## Further Notes

- Stage order matters: 1 unlocks compiler guardrails for 2–6; 2 is the only stage with algorithmic (not just alloc) wins — do it first if short on time.
- The `--changed` path can't be fully exercised here (working tree is not a git repo); that manual check moves to a machine with versioned checkout.
- If any stage's diff grows beyond its area, split it before asking for review — the point of the staging is small reviewable diffs.
