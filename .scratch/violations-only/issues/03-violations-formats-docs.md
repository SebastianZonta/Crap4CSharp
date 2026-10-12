# 03: Violations format parity, output file, empty report, docs

**What to build:** The same filtered violation list reaches every report surface (text, json, csv, `--output` file copy); an empty violators result prints the normal empty report shape with success exit; user docs record the flag and the violation term.

**Blocked by:** 01 (Core violations-only filter (text)).

**Status:** ready-for-agent

- [ ] `--violations-only --format json` emits only violation entries; empty violators emits `[]` with exit `0`
- [ ] `--violations-only --format csv` emits header plus violation rows only; empty violators emits header-only with exit `0`
- [ ] `--violations-only --output <file>` writes the same filtered report that went to stdout (all formats)
- [ ] Empty violators in text format prints the header-only report with exit `0` (distinct from "no files" selection)
- [ ] Tool README documents the flag alongside threshold/top/min-cc; glossary gains the violation definition
