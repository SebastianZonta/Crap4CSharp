# 02: Cruce cobertura↔métodos indexado + XML en streaming

**What to build:** el análisis de repos grandes deja de ser cuadrático y el XML de Cobertura se lee en un solo pase con memoria constante. Scores, fallback a N/A y valor cero ante datos malformados se comportan exactamente igual que hoy.

**Blocked by:** 01 (higiene de build).

**Status:** ready-for-agent

- [ ] Índice por clase+método construido una vez por corrida; cada método solo busca dentro de sus entradas
- [ ] Regex de namespace generada en compile-time; parseo de línea e ints sin allocations ni excepciones
- [ ] Parser Cobertura en lector forward-only con estado de clase actual; errores limitados a categorías XML/IO; entrada vacía retorna mapa compartido vacío
- [ ] Tests de analizador y parser verdes con scores idénticos; verificación manual con un Cobertura grande si hay uno a mano
