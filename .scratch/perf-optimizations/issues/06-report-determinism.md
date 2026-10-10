# 06: Reporte eficiente + suite determinista

**What to build:** el reporte rinde igual en texto (byte-idéntico) con la mitad de trabajo de orden y sin re-parsear formatos por fila, y la suite queda verde en cualquier OS, cultura y estado de disco.

**Blocked by:** 01 (higiene de build).

**Status:** ready-for-agent

- [ ] Orden de una sola key descendente con nulls-last, formato invariante interpolado, builder pre-dimensionado, header/separador constantes, guard de null con la excepción correcta
- [ ] Fakes que copian inputs, tests con skip por OS donde invocan shell, cleanup best-effort en fixtures de disco
- [ ] Test de reporte byte-idéntico verde + suite completa verde
