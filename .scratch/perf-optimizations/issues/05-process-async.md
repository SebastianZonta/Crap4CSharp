# 05: Procesos async con cancelación + limpieza nativa

**What to build:** git y coverage corren async y cancelables (un coverage colgado se mata con Ctrl-C en vez de colgar la herramienta), los args de git viajan estructurados (paths con espacios/comillas funcionan) y la limpieza de directorios la hace el framework. Los errores llevan exit code accionable.

**Blocked by:** 04 (CLI paths, comparte área del detector de cambios).

**Status:** ready-for-agent

- [ ] Ejecución async con token de cancelación desde el seam de entrada; kill del árbol de proceso ante cancelación; validación fail-fast del directorio de trabajo
- [ ] Detección de cambios con lectura async anti-deadlock, parseo por spans sin arrays intermedios y argumentos estructurados
- [ ] Borrado recursivo nativo con manejo de carreras; tests de runner, executor y detector verdes
- [ ] Verificación manual de `--changed` donde haya git y de Ctrl-C en un coverage largo
