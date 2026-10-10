# 04: Descubrimiento y agrupado CLI sin churn

**What to build:** descubrir archivos, resolver el módulo de cada uno y parsear argumentos cuesta una sola pasada con normalizaciones hechas una vez. `--help`, escaneo sin args, paths explícitos y detección de encoding se comportan igual; dirs inaccesibles ya no tumban la corrida y las extensiones en mayúsculas se encuentran.

**Blocked by:** 01 (higiene de build).

**Status:** ready-for-agent

- [ ] Sorts estáticos con nulls-last explícito; lookup de XML por mínimo en vez de ordenar; normalización de root y prefijos hecha una vez por corrida; argv en un solo pase
- [ ] Enumeración resiliente (ignora inaccesibles, case-insensitive) y texto de ayuda como constante
- [ ] Tests CLI, parser de args, finder y detector verdes; corridas manuales de `--help`, sin args y con paths
