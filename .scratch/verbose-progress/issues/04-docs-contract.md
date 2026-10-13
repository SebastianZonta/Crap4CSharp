# 04: Help/README + log contract lock-in

**What to build:** the progress log becomes documented and regression-proofed: help and README describe the stderr log and the silent switch, and end-to-end tests lock the full ordering and channel contracts.

**Blocked by:** 02-command-echo.md, 03-file-list.md.

**Status:** ready-for-agent

- [ ] `--help` documents that the progress log goes to stderr and that `--no-progress` disables it
- [ ] Tool README documents the same stderr/stdout contract alongside the existing flags
- [ ] End-to-end test locks full order: `Found` → per-module `$` echo + streamed output + `Analyzing` lines → warnings/gate messages, with stdout report pure for text/json/csv
- [ ] End-to-end test locks `--output` file content matching filtered stdout (no log lines in the file)
- [ ] Full suite (build with warnings-as-errors, format check, tests) passes
