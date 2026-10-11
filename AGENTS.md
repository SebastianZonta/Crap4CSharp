# crap4csharp — CRAP analyzer for C# (port of crap4java)

`src/Crap4CSharp` (net10.0 Exe, packed as dotnet tool `crap4csharp`) +
`test/Crap4CSharp.Tests` (xunit, references src). Requires .NET 10 SDK.

Read the linked doc when its topic comes up; each line below says when.

- [`docs/agents/commands.md`](docs/agents/commands.md) — build, test, local
  run, E2E matrix, and CI order. Read before running or verifying anything.
- [`docs/agents/architecture.md`](docs/agents/architecture.md) — execution
  flow, module roots, coverage lookup, exit codes, test seam. Read before
  changing CLI behavior or analysis.
- [`src/Crap4CSharp/README.md`](src/Crap4CSharp/README.md) — full CLI flag
  and exit-code reference. Read when adding flags or writing usage docs.
- [`PUBLISHING.md`](PUBLISHING.md) — release via `v*` tag + NuGet Trusted
  Publishing. Read only when publishing.
- [`docs/agents/issue-tracker.md`](docs/agents/issue-tracker.md) — local
  markdown issue tracker (`.scratch/`). Read when a skill says to publish
  or fetch a ticket.
- [`docs/agents/triage-labels.md`](docs/agents/triage-labels.md) — maps the
  five canonical triage labels to this repo's tracker. Read when triaging.
- [`docs/agents/domain.md`](docs/agents/domain.md) — glossary/ADR convention.
  (`GLOSSARY.md` / `docs/adr/` do not exist yet — proceed silently per that file.)
