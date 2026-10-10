# 03: Parser Roslyn de un pase + datos como value types

**What to build:** el parseo de métodos cuesta una sola pasada sin objetos descartables por método y los datos de corta vida no presionan el GC. Complejidades, posiciones y scores idénticos a hoy.

**Blocked by:** 02 (índice cobertura + XML).

**Status:** ready-for-agent

- [x] Un solo walker por archivo, chequeo de ancestros manual con salida temprana, walker de complejidad reusado, tabla de líneas cacheada, trivia de doc-comments desactivada
- [x] Entradas de cobertura y descriptores de método como records struct readonly (igualdad usada por tests preservada); scoring con costo de llamada eliminado
- [x] Tests de parser, scores y analizador verdes con valores idénticos

## Completion

Done 2026-10-10 (build `0 warnings / 0 errors`, suite `62/62` incl. targeted parser+score+analyzer tests; complexities, positions and scores identical, verified via delegated build+test runs with nested validator). Notes: `MethodCollector`/`CcWalker` promoted to top-level types in `CSharpMethodParser.cs` and file comments stripped per maintainer request (no behavior change, suite stays `62/62`). Test-side mechanical adaptation only: `CrapAnalyzerTest.cs:119` `nearest!.CoveragePercent()` → `nearest!.Value.CoveragePercent()` (nullable-struct, same assertion). Work uncommitted — pending maintainer commit.
