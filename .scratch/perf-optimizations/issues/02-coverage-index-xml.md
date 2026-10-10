# 02: Cruce cobertura↔métodos indexado + XML en streaming

**What to build:** el análisis de repos grandes deja de ser cuadrático y el XML de Cobertura se lee en un solo pase con memoria constante. Scores, fallback a N/A y valor cero ante datos malformados se comportan exactamente igual que hoy.

**Blocked by:** 01 (higiene de build).

**Status:** ready-for-agent

- [x] Índice por clase+método construido una vez por corrida; cada método solo busca dentro de sus entradas
- [x] Regex de namespace generada en compile-time; parseo de línea e ints sin allocations ni excepciones
- [x] Parser Cobertura en lector forward-only con estado de clase actual; errores limitados a categorías XML/IO; entrada vacía retorna mapa compartido vacío
- [x] Tests de analizador y parser verdes con scores idénticos; verificación manual con un Cobertura grande si hay uno a mano

## Completion

Done 2026-10-10 (build `0 warnings / 0 errors`, suite `62/62` incl. targeted analyzer+parser `16/16`; scores, malformed-fallback, empty-input y Doctype-reject pinned por tests existentes). Notes: `LookupCoverage`/`ExactCoverage`/`NearestCoverage` públicas se conservan para compat con tests; `Analyze` usa la vía indexada (`BuildMethodIndex`+`LookupIndexed`). Sin Cobertura grande a mano: verificación manual limitada a smoke run sobre `src/` (reporte N/A correcto, namespaces resueltos).
