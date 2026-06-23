# Copilot instructions

The authoritative rules for AI agents in this repository live in [`AGENTS.md`](../AGENTS.md) — read it first.

- **Rules / DO & DON'T / recipes / Definition of Done:** [`AGENTS.md`](../AGENTS.md)
- **Architecture & patterns reference:** [`ARCHITECTURE.md`](../ARCHITECTURE.md)

Follow `AGENTS.md` exactly: one module's `DbContext` per command, cross-module communication via integration
events only, bespoke repository per aggregate, local `Guid` as the canonical identity, no secrets in source,
and build clean (warnings are errors) + tests passing before claiming done. Commit messages carry no AI
co-author trailer.
