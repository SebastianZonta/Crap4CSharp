# 02: Violations ordering with top, min-cc, custom threshold

**What to build:** The violations filter composes with the existing report controls: low-complexity methods drop first, then non-violations drop, then truncation keeps the worst violators; a custom gate threshold drives both the filter and the gate together.

**Blocked by:** 01 (Core violations-only filter (text)).

**Status:** ready-for-agent

- [ ] `--violations-only --min-cc 3` excludes low-complexity methods before the violations filter
- [ ] `--violations-only --top 20` keeps at most the 20 worst violators (not the 20 worst methods overall)
- [ ] `--violations-only --threshold 15` shows only methods above 15 and gates on 15
- [ ] Filter order (min-cc, then violations, then top) holds regardless of flag order on the command line
