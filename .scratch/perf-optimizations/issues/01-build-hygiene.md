# 01: Higiene de build como guardarraíl

**What to build:** el build falla ante warnings y aplica análisis completo, para que las etapas siguientes no puedan introducir regresiones silenciosas. Al terminar, compilar y testear da el mismo resultado visible que hoy, pero con guardarraíles activos.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Build con warnings-as-errors, estilo aplicado en build y modo de análisis completo, con versión de lenguaje fijada
- [ ] Atributo de visibilidad de internos movido a su lugar canónico (sin cambio de acceso efectivo)
- [ ] Test de reporte con cultura explícita invariante (mismo texto esperado, verde en cualquier locale)
- [ ] `dotnet build` + `dotnet test` verdes desde cero
