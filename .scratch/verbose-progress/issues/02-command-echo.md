# 02: Command echo + live dotnet test stream

**What to build:** every coverage run prints a `$ dotnet test … in <dir>` echo line on stderr followed by the live verbatim child output, so a run is as transparent as invoking the coverage command by hand.

**Blocked by:** 01-streaming-sink.md (needs the streaming sink).

**Status:** ready-for-agent

- [ ] One `$ … in <relative dir>` echo line on stderr before each module coverage run, with full coverage arguments, relative paths, and minimal quoting for args with spaces
- [ ] Child output streamed live verbatim to stderr (no prefix, emission order preserved), not captured-and-dumped at the end
- [ ] `--no-progress` silences both the echo line and the streamed output; warnings/errors and the stdout report remain
- [ ] `--no-coverage` skips the echo + stream block entirely
- [ ] Non-zero child exit keeps the streamed output plus the existing failure error and exit `1`
- [ ] No spinner output around the coverage phase
