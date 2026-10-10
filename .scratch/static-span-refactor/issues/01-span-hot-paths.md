# 01: Span hot paths without behavior change

**What to build:** the same analysis outputs with fewer allocations: coverage-key building, class-name resolution, CLI flag/value handling, report escaping/formatting/sorting, and path/prefix checks all work over spans and materialize strings only at framework boundaries.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Coverage keys built once per key through a single shared helper; analyzer lookups, method index, and XML parser agree on key semantics with identical scores
- [x] Class-name resolution uses a forward span scan instead of a whole-source regex, producing identical names on existing parser cases
- [x] CLI flag split, case-insensitive name/format matching, and numeric validation parse from spans with identical error messages and exit codes
- [x] Report JSON/CSV escaping is single-pass over spans, per-row numbers format from stack buffers with invariant culture, and row order is unchanged (in-place span sort)
- [x] Path checks (extension, under-directory/root-containment, rename-target, class-name normalization) stay as spans until the single final materialization
- [x] No span types leak into test-visible signatures; full build plus full suite green; text/JSON/CSV outputs byte-identical on a sample repo

## Completion

Done 2026-10-10 (build `0 warnings / 0 errors`, suite `154/154`; `--format text/json/csv` smoke runs well-formed, CSV 79/79 lines with 5 columns). Notes: `ParseTrailingLine` gained a `ReadOnlySpan<char>` overload (string overload kept, delegating — no test-visible change); private helpers take spans directly (`string` converts implicitly, so no `AsSpan()` clutter at call sites). One self-found bug fixed: missing `,` between class and cc in the new `FormatCsv` (caught by `FormatCsvHasHeaderRowAndEmptyNulls`). Work uncommitted — shares the tree with other efforts' uncommitted work (perf 01-06 follow-ups, 03 struct work, JSON/CSV formatter + tests); pending maintainer commit.
