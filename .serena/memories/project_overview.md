# Project Overview

.NET 10 modular monolith template (`dotnet new allspice-modular`) by AllSpice Technologies. Combines Clean Architecture, CQRS (Mediator source-gen), MassTransit messaging, Quartz scheduling, SignalR realtime, and YARP reverse proxy in a single deployable unit.

## Key Modules
- **Notifications** — multi-channel (email/SMS/in-app) with templates, preferences, Quartz digest job
- **Identity** — Keycloak directory lookups, module role assignments, portal-aware JWT
- **RealTime** — SignalR AppHub with user group management
- **ApiGateway** — sole runnable host; FastEndpoints, YARP, rate limiting, output caching
- **AppHost** — Aspire orchestrator (PostgreSQL, Redis, Keycloak, Papercut SMTP)

## Tech Stack
.NET 10, EF Core + PostgreSQL, FastEndpoints, Mediator (source-gen), MassTransit (in-memory), Quartz.NET, SignalR, YARP, Serilog + OpenTelemetry, Blazor + Flowbite.Blazor, Ardalis libraries (Result, Specification, GuardClauses, SmartEnum), xunit + Moq.
