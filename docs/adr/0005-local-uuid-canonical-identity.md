# 0005 — Local UUID is the canonical identity

- Status: Accepted
- Date: 2026-06-24

## Context

Users originate in Keycloak (external IdP) and are mirrored locally. Two identifiers exist: the Keycloak
external ID (the JWT `sub`) and a local `User.Id` (Guid). Mixing them causes identity drift — a value written
as one and compared as the other silently fails.

## Decision

The **local `Guid` is the canonical identity** across the application and all module data (foreign keys, audit
columns, notification recipients/preferences). The Keycloak external ID is used **only** for Keycloak admin
calls and the JWT/SignalR boundary. Conversion between the two goes through `IUserExternalIdResolver`.

`CurrentUserResolutionMiddleware` resolves the JWT subject to the local UUID once per authenticated request;
audit stamping uses that local UUID.

## Consequences

- No identity drift; cross-module references are uniform local Guids.
- A directory lookup is needed at the auth boundary to map `sub → local Guid` (cached per request).
- Code must never store/compare an external ID where a local Guid is expected (an `AGENTS.md` golden rule).
