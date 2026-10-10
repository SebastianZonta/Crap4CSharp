# 02: Static-class and extension-method consolidation

**What to build:** the same behavior from a cleaner structure: duplicated or misplaced helpers live in small static homes and repeated operations read as extension methods at their call sites.

**Blocked by:** 01 (span hot paths) — builds on the helpers ticket 01 introduces to avoid conflicting edits to the same routines.

**Status:** resolved

- [x] Single shared enumeration-options factory serves both file discovery and coverage-file search
- [x] Path operations (under-directory check, extension strip, rooted combine, group-by-module) exposed as extensions in one place; CLI orchestrator reads as orchestration only
- [x] CLI span helpers (flag split/classification, value taking) and coverage-key helpers shared rather than inlined per module
- [x] Process-tree kill, sync-over-async wrappers, max-score aggregation, and stale-coverage deletion each live in exactly one helper and are reused by all call sites
- [x] No single-use private helper promoted to an extension; no new abstraction with one implementation
- [x] Full build plus full suite green; CLI surface, exit codes, report bytes, scores, and warning messages unchanged

## Completion

Done 2026-10-10 (build `0 warnings / 0 errors`, suite `154/154`; `--help` exit 0). New `FileSystemHelpers.cs`: `SharedEnumeration()` replaces `SourceFileFinder.NewEnumeration` + `CliApplication.NewCoberturaEnumeration` (finder sets `MatchCasing` on top, Cobertura search stays case-sensitive); `IsUnderDirectory` extensions replace `ChangedFileDetector.IsUnderDirectory` + `CliApplication.IsWithinOrEqual` (extension form, 2 call sites); `DeleteIfExists` moved verbatim from `CoverageRunner`. `CrapScore.MaxCrap` extension is the single gate implementation — `CliApplication` calls `metrics.MaxCrap()`, `Program.MaxCrap` stays as delegating test seam (`MainTest` uses it). Deliberately not moved (single-use, per extension policy): `GroupByModuleRoot`, CLI span helpers, extension-strip, `HasCsproj`; sync wrappers stay stdlib `GetAwaiter().GetResult()` (the shared mechanism); `TryKillTree`/`CombineRooted`/`CoverageKeyExtensions` already shared, untouched. Two-axis review: no hard violations, no scope creep, no behavior change.
