# 03: File list (Found + Analyzing per file), spinner out

**What to build:** discovery and analysis explain themselves with a `Found N C# file(s).` summary plus one `Analyzing <path>` line per file on stderr, prettier/lint style, with the spinner gone.

**Blocked by:** 01-streaming-sink.md (shares the execution flow; independent of 02-command-echo.md and can run in parallel with it).

**Status:** ready-for-agent

- [ ] `Found N C# file(s).` on stderr after discovery; stdout carries only the report
- [ ] One `Analyzing <relative path>` stderr line per file in analysis order, paths relative to the workspace root
- [ ] Multi-module order preserved: global `Found` first, then each module's file lines with its own coverage block
- [ ] `--no-progress` silences the summary and per-file lines, keeping warnings/errors/report
- [ ] Log lines print even when stderr is redirected (no auto-silence for the new lines)
- [ ] Empty selection prints only the existing message with exit `0` and no log lines
- [ ] No spinner output remains in discovery/analysis phases
