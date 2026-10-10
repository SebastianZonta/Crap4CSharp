# 03: Parser Roslyn de un pase + datos como value types

**What to build:** el parseo de métodos cuesta una sola pasada sin objetos descartables por método y los datos de corta vida no presionan el GC. Complejidades, posiciones y scores idénticos a hoy.

**Blocked by:** 02 (índice cobertura + XML).

**Status:** ready-for-agent

- [ ] Un solo walker por archivo, chequeo de ancestros manual con salida temprana, walker de complejidad reusado, tabla de líneas cacheada, trivia de doc-comments desactivada
- [ ] Entradas de cobertura y descriptores de método como records struct readonly (igualdad usada por tests preservada); scoring con costo de llamada eliminado
- [ ] Tests de parser, scores y analizador verdes con valores idénticos
