# 01: Higiene de build como guardarraíl

**What to build:** el build falla ante warnings y aplica análisis completo, para que las etapas siguientes no puedan introducir regresiones silenciosas. Al terminar, compilar y testear da el mismo resultado visible que hoy, pero con guardarraíles activos.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Build con warnings-as-errors, estilo aplicado en build y modo de análisis completo, con versión de lenguaje fijada
- [x] Atributo de visibilidad de internos movido a su lugar canónico (sin cambio de acceso efectivo)
- [x] Test de reporte con cultura explícita invariante (mismo texto esperado, verde en cualquier locale)
- [x] `dotnet build` + `dotnet test` verdes desde cero

## Completion

Done 2026-10-10, commit `38aecb2` (build `0 warnings / 0 errors`, suite `62/62`, verified desde cero with clean `bin`/`obj` wipe; report test `2/2` under `de_DE`). Notes: 14 public types demoted to `internal` to satisfy `AnalysisMode All` (CA1515/CA1062) — Spec-axis review flags this as exceeding "sin cambio de acceso efectivo"; revert offered, pending maintainer call. `LangVersion latest` floats with the SDK rather than pinning numeric. Test-project CA1307/CA1861/CA2000 fixes included. Same-commit idiom follow-ups (collection expressions, list/type patterns) were maintainer-requested scope additions, not ticket scope.
