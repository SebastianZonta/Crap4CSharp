# 06: Reporte eficiente + suite determinista

**What to build:** el reporte rinde igual en texto (byte-idéntico) con la mitad de trabajo de orden y sin re-parsear formatos por fila, y la suite queda verde en cualquier OS, cultura y estado de disco.

**Blocked by:** 01 (higiene de build).

**Status:** ready-for-agent

- [x] Orden de una sola key descendente con nulls-last, formato invariante interpolado, builder pre-dimensionado, header/separador constantes, guard de null con la excepción correcta
- [x] Fakes que copian inputs, tests con skip por OS donde invocan shell, cleanup best-effort en fixtures de disco
- [x] Test de reporte byte-idéntico verde + suite completa verde

## Completion

Done 2026-10-10 (build `0 warnings / 0 errors`, suite `70/70` incl. report byte-identical `2/2`). Notes: two-axis review clean — Standards no hard breaches (duplicated Dispose/OS-guard shapes across 7 test files and silent-return skips kept as judgement calls: shared fixture would couple modules, early-return follows the existing `PreCancelledToken` pattern); Spec no missing/creep/wrong (silent-return reports pass-not-skip on non-Linux, accepted: ticket requires green suite, and `Assert.Skip` would add a new pattern). `CrapAnalyzerTest.cs:133` `.Value` line belongs to uncommitted 03 struct work, excluded from this commit (only the Dispose hunk is staged here).
