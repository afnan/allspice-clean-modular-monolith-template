# AllSpice Clean Modular Monolith (Work in Progress)

> **Status:** Authentication with Microsoft Entra ID is scaffolded but not fully wired yet. The template ships with a complete notifications module, real-time infrastructure, and shared service defaults. The original HR sample is kept only for reference and is excluded when you scaffold a new solution.

## Description

This template captures the opinionated modular-monolith architecture we use at **AllSpice Technologies**. It combines Clean Architecture, CQRS, MassTransit messaging, Quartz scheduling, and SignalR real-time delivery in a single deployable unit that can later be split into independent microservices.

The solution centers around the `Notifications` module, which exposes a multi-channel communication hub (email, SMS stub, in-app) and background orchestration. The repository also includes reusable infrastructure (API Gateway, Aspire AppHost, shared defaults) so teams can clone and immediately focus on business modules.

## Features

- **API Gateway with YARP** for routing, caching, rate limiting, and JWT validation.
- **Notifications module** with templates, preferences, MassTransit integration, SignalR in-app delivery, and a Quartz daily digest job.
- **Realtime hub (`AllSpice.CleanModularMonolith.RealTime`)** sharing SignalR infrastructure across modules.
- **Quartz.NET scheduling** registered in `AllSpice.CleanModularMonolith.ServiceDefaults` for consistent background job hosting.
- **Central package management** with .NET 10 (RC) support and Clean Architecture patterns powered by Ardalis libraries.
- **Aspire AppHost orchestration** to spin up PostgreSQL, Redis, and development containers like Papercut SMTP in one command.
- **MassTransit (in-memory by default)** for event-driven communication between modules.
- **Serilog + OpenTelemetry** logging and tracing defaults.

> **Warning:** The HR module that originally triggered onboarding notifications is intentionally excluded from the template output. A reference implementation still lives under `samples/AllSpice.CleanModularMonolith.HR/` for documentation purposes only.

## Design Guide

A detailed design guide that explains layering, module extension patterns, messaging vs. scheduling, and real-time delivery resides in [`docs/DesignGuide.md`](docs/DesignGuide.md). Highlights:

- Clean Architecture split across Domain, Application, Infrastructure, Api folders per module.
- Module registration via `Infrastructure/Extensions/*ModuleExtensions.cs` and the gateway's `RegisterGatewayModules` helper.
- Quartz jobs registered per module and hosted through the shared service defaults.
- SignalR hub groups authenticated connections automatically, letting any module publish realtime payloads.

## Getting Started

### Install the template (local checkout)

```bash
dotnet new install .
```

> This install command will be replaced with a NuGet package once the template is published.

### Scaffold a new solution

```bash
dotnet new allspice-modular -n Contoso.Erp
cd Contoso.Erp
```

This command replaces every `AllSpice.CleanModularMonolith` identifier with `Contoso.Erp`, creating:

- `Contoso.Erp.slnx`
- `Services/Contoso.Erp.Notifications`
- `Shared/Contoso.Erp.RealTime`
- `Contoso.Erp.ApiGateway`, `Contoso.Erp.AppHost`, and `Contoso.Erp.ServiceDefaults`

### Restore & build

```bash
dotnet restore Contoso.Erp.slnx
dotnet build Contoso.Erp.slnx -c Debug
```

### Run with Aspire

```bash
dotnet run --project Contoso.Erp.AppHost/Contoso.Erp.AppHost.csproj
```

- Spins up PostgreSQL (notifications database), Redis, and Papercut SMTP (development only).
- Launches the API Gateway with SignalR and module registration.

## Project Layout

```
{{ProjectName}}/
|- {{ProjectName}}.ApiGateway/
|- {{ProjectName}}.AppHost/
|- {{ProjectName}}.ServiceDefaults/
|- Services/{{ProjectName}}.Notifications/
|- Shared/{{ProjectName}}.RealTime/
|- Shared/{{ProjectName}}.Notifications.Contracts/
|- Shared/{{ProjectName}}.SharedKernel/
\- Directory.Packages.props
```

The repository also contains `samples/AllSpice.CleanModularMonolith.HR/` as an illustrative example. It is excluded from template output but kept in source control for documentation comparisons.

## Modules Included

| Module | Highlights |
| --- | --- |
| **Notifications** | Clean Architecture layers, FastEndpoints APIs, Quartz daily digest job, MassTransit consumer (`NotificationRequestedIntegrationEvent`), SignalR in-app channel, template rendering, user preferences. |
| **RealTime** | `AppHub` SignalR hub with automatic `user:{id}` groups, `IRealtimePublisher` abstraction for broadcasting payloads or notifications to users/groups. |
| **ServiceDefaults** | Shared OpenTelemetry logging/metrics, HTTP resilience, service discovery, Quartz hosting defaults. |
| **ApiGateway** | YARP reverse proxy, FastEndpoints registration, SignalR hub mapping, rate limiting, Redis output caching, Serilog, OpenTelemetry. |
| **AppHost** | Aspire orchestrator wiring PostgreSQL, Redis, Papercut SMTP, and environment parameters (Sinch credentials, CORS origins, Entra placeholders). |

## Scheduler & Background Jobs

- Quartz is registered globally via `AllSpice.CleanModularMonolith.ServiceDefaults`.
- The template ships with `NotificationDailyDigestJob` as a sample (currently logs pending notifications older than 24 hours).
- Add new jobs in each module by calling `.AddQuartz(...)` inside that module's extension method.

## Authentication (Work in Progress)

The solution includes configuration placeholders for Microsoft Entra External ID (authority + audience). The authentication middleware is registered, but no login UI or token issuance pipeline is provided yet. Future iterations will wire full JWT issuance and client onboarding.

## Roadmap

- Finish Entra ID integration (tokens, user provisioning, UI samples).
- Add persistence for in-app inbox (read/unread state) and admin endpoints.
- Publish reusable SignalR client helper (TypeScript/C#) for dashboards.
- Replace SMS stub with production gateway adapters (Sinch/Twilio) and rate limiting.
- Provide template packages via NuGet and the `dotnet new` short-name gallery.

## License

MIT

---

Built with love using .NET 10 RC, Clean Architecture, and modular monolith principles.
