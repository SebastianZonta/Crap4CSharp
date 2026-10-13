# 01: Streaming executor sink (prefactor)

**What to build:** the command executor gains a streaming sink so live child output can be forwarded to the tool stderr; the old silent call shape keeps working so existing callers and tests stay green. No user-visible behavior change in this ticket.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Executor contract has a streaming form that forwards child stdout+stderr live and verbatim to the caller-provided writer, then returns the exit code
- [ ] Real executor streams without buffering to end-of-run and preserves existing exit-code and working-directory-not-found behavior
- [ ] Old silent call shape still works (delegates to the streaming form with a null sink) so current tests pass unmodified
- [ ] Cancellation still kills the child process tree
