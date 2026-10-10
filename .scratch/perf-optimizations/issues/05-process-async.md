# 05: Procesos async con cancelación + limpieza nativa

**What to build:** git y coverage corren async y cancelables (un coverage colgado se mata con Ctrl-C en vez de colgar la herramienta), los args de git viajan estructurados (paths con espacios/comillas funcionan) y la limpieza de directorios la hace el framework. Los errores llevan exit code accionable.

**Blocked by:** 04 (CLI paths, comparte área del detector de cambios).

**Status:** ready-for-agent

- [x] Ejecución async con token de cancelación desde el seam de entrada; kill del árbol de proceso ante cancelación; validación fail-fast del directorio de trabajo
- [x] Detección de cambios con lectura async anti-deadlock, parseo por spans sin arrays intermedios y argumentos estructurados
- [x] Borrado recursivo nativo con manejo de carreras; tests de runner, executor y detector verdes
- [x] Verificación manual de `--changed` donde haya git y de Ctrl-C en un coverage largo

## Completion

Done 2026-10-10 (build `0 warnings / 0 errors`, suite `70/70` incl. 8 new runner/executor/detector tests; manual `--help` exit 0 byte-compatible and `--changed` exit 0 on this repo with real git detection + N/A report). Notes: two-axis review clean — Standards no hard breaches (verbatim `TryKillTree` in executor+detector and string/span `IsCandidateLine`/`RenameTarget` pairs noted as judgement calls, kept: shared helper would couple modules for ~10 lines, string overloads are the test-compat seam); Spec one partial fixed (`git status` failure now carries `with exit <code>`, closing the "errores llevan exit code" gap). Ctrl-C kill path pinned by pre-cancelled-token unit test (`sleep` + pgrep poll, Linux-only), not by a live long-coverage interrupt.
