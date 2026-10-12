# 01: Core violations-only filter (text)

**What to build:** `crap4csharp --violations-only` as a boolean flag that filters the text report to violation rows only (numeric CRAP strictly greater than the effective threshold; `N/A` rows hidden), while the threshold gate still fires on the full pre-filter set. Includes the `--help` usage line.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] `--violations-only` defaults off; set flag shows only methods with CRAP above the default threshold in the text report
- [ ] `N/A` (unknown coverage) rows are hidden when the flag is set
- [ ] `--violations-only=value` is rejected as a usage error (exit `1`), like existing boolean flags
- [ ] Exit-`2` gate and stderr message still computed on the full pre-filter set (filtering never masks a failure)
- [ ] `--help` documents the new flag
