# CLAUDE.md — working on the template itself

This file guides Claude Code (and other agents) when **developing and maintaining this template
repository**. It is intentionally **excluded from generated projects** (see `.template.config/template.json`)
because it is about the template, not about apps built from it.

**If you are building a project that was scaffolded from this template, this file is not for you.** Read:
- [`ARCHITECTURE.md`](./ARCHITECTURE.md) — how a generated project is structured (hosting, modules, CQRS, libraries, DB).
- [`AGENTS.md`](./AGENTS.md) — the prescriptive agent rules (golden rules, DO/DON'T, recipes, Definition of Done).

Both of those **ship with generated projects** and get `sourceName`-renamed; this file does not.

---

## What this repo is

A .NET 10 modular monolith **`dotnet new` template** (`shortName: allspice-modular`). The `sourceName`
`AllSpice.CleanModularMonolith` is replaced by the user's chosen project name on scaffold. Architecture,
patterns, and conventions are documented in `ARCHITECTURE.md` / `AGENTS.md` — keep those authoritative and
avoid duplicating them here.

## Template mechanics (the part unique to maintaining the template)

- **Config:** `.template.config/template.json`. `sourceName` drives the rename; the `sources.modifiers.exclude`
  list controls what is left out of generated projects.
- **Excluded from generated projects:** `.git/**`, `.serena/**`, `.mcp.json`, `.template.config/**`,
  `**/bin/**`, `**/obj/**`, and **`CLAUDE.md`** (this file). Everything else ships — including `AGENTS.md`,
  `ARCHITECTURE.md`, `README.md`, `GETTING_STARTED.md`, and `.github/`.
- **When you add a maintainer-only or machine-local file**, decide whether it should reach generated projects
  and update the exclude list accordingly. When you add agent rules or architecture that *should* reach users,
  put them in `AGENTS.md` / `ARCHITECTURE.md` (not here).
- **Test the template locally** before publishing changes:
  ```bash
  dotnet new install .            # install this template from the repo root
  dotnet new allspice-modular -n Acme.Demo -o ../_tmpl-smoketest
  dotnet build ../_tmpl-smoketest/Acme.Demo.slnx
  dotnet new uninstall AllSpice.CleanModularMonolith
  ```
  Verify the smoke-test output renamed correctly and that `CLAUDE.md`/`.serena` did **not** come across while
  `AGENTS.md`/`ARCHITECTURE.md` did, with their `sourceName` references rewritten.

## Build & test (same as a generated project)

```bash
dotnet build AllSpice.CleanModularMonolith.slnx     # 0 warnings (TreatWarningsAsErrors=true)
dotnet test  AllSpice.CleanModularMonolith.slnx
dotnet run --project AllSpice.CleanModularMonolith.AppHost/AllSpice.CleanModularMonolith.AppHost.csproj
```

The full command/architecture/migration reference is in `ARCHITECTURE.md`.

## Conventions for commits to this repo

- **No AI co-author trailer** on commits (`Co-Authored-By: Claude ...` etc.). Keep commit messages free of AI attribution.
- Follow the code conventions in `ARCHITECTURE.md` (file-scoped namespaces, `_camelCase` fields, central package
  versioning, warnings-as-errors) and the rules in `AGENTS.md`.
- Keep `AGENTS.md` and `ARCHITECTURE.md` up to date when you change patterns — they are what downstream projects inherit.

## Use Serena MCP for semantic code analysis

Serena MCP is available for symbol-based navigation and precise edits (prefer it over grep/sed when available):
`find_symbol`, `find_referencing_symbols`, `get_symbols_overview`, `read_file`. Serena memories live in
`.serena/` and are excluded from generated projects.
