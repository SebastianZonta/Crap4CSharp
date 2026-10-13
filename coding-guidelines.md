# coding-guidelines.md — programming + agents only

Derived from ~1300 sessions / ~3500 user messages (opencode.db scan 2026-10-13).

## 1. Swarm first for parallel work

Fan out independent code slices with `delegate`/`subagent`, one subagent per slice.
Each prompt carries: exact files to read, forbidden files, acceptance criteria,
and exact output shape (verdict + what to change, under N words). The main agent
aggregates and never redoes slice work. Typical fan-outs: one subagent per
security-audit axis (structure/secrets/supply-chain/code/net), per web-search
query, per module or ticket.

## 2. Revalidate with the same agent

On code-review fix loops, resume the same subagent by `sessionID` with only the
delta and the same criterion. No re-explaining, no new agent with cold context.
Stop at approval or 3 rounds, then report.

## 3. Scope-guard every prompt

State `Work ONLY in <dir>, create ONLY these files, touch nothing else`,
plus `NEVER switch branches, merge, push, or touch <other dir>` and
`NO reviertas ni toques cambios sin commitear de otros tickets`.
Never implement or resolve unless the user said so; explain or review first.

## 4. Verify by running, show the tool

Run it yourself (tests, build, endpoint smoke test). Show tool invocations with
live verbatim output like `dotnet restore/build` and `prettier/lint` file lists;
no spinner hiding the work phase, no captured-and-dumped-at-the-end output.

## 5. Commits and code search

Conventional Commits with human author `Sebastian Vladimir <seba_12345@yahoo.com>`
(`git -c user.name=... -c user.email=... commit`); commit/push only when told.
In a codebase, search with codegraph first, fall back to native tools when it
does not apply. Research against primary sources (official docs, source, specs).
