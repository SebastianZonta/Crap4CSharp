# 04: Descubrimiento y agrupado CLI sin churn

**What to build:** descubrir archivos, resolver el módulo de cada uno y parsear argumentos cuesta una sola pasada con normalizaciones hechas una vez. `--help`, escaneo sin args, paths explícitos y detección de encoding se comportan igual; dirs inaccesibles ya no tumban la corrida y las extensiones en mayúsculas se encuentran.

**Blocked by:** 01 (higiene de build).

**Status:** resolved

- [x] Sorts estáticos con nulls-last explícito; lookup de XML por mínimo en vez de ordenar; normalización de root y prefijos hecha una vez por corrida; argv en un solo pase
- [x] Enumeración resiliente (ignora inaccesibles, case-insensitive) y texto de ayuda como constante
- [x] Tests CLI, parser de args, finder y detector verdes; corridas manuales de `--help`, sin args y con paths

## Completion

Done 2026-10-10 (build `0 warnings / 0 errors`, suite `62/62` incl. targeted CLI/parser/finder/detector/Main `26/26`; manual `--help`/no-args/explicit-path/empty-dir/illegal-combo runs byte-compatible with unchanged exit codes; uppercase `.CS` discovery verified on Linux via throwaway fixture). Notes: two-axis review clean — Standards no hard breaches (Duplicated-Code `NewEnumeration` pair and `(root, prefix)` Data-Clumps noted as judgement calls, kept: shared factory would couple modules for 3 lines, rooted-path type would be speculative); Spec no missing/creep/wrong (sync git execution and span parsing correctly left to 05, report ordering untouched for 06; public `ModuleRootFor` keeps per-call normalization as test-compat shim, hot path uses hoisted prefix).
