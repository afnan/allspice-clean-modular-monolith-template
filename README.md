# AllSpice Clean Modular Monolith (Work in Progress)

> **Status:** Authentication with Authentik is scaffolded but not fully wired yet. The template ships with a complete notifications module, real-time infrastructure, and shared service defaults. The original HR sample is kept only for reference and is excluded when you scaffold a new solution.

## Description

This template captures the opinionated modular-monolith architecture we use at **AllSpice Technologies**. It combines Clean Architecture, CQRS, MassTransit messaging, Quartz scheduling, and SignalR real-time delivery in a single deployable unit that can later be split into independent microservices.

The solution centers around the `Notifications` module, which exposes a multi-channel communication hub (email, SMS stub, in-app) and background orchestration. The repository also includes reusable infrastructure (API Gateway, Aspire AppHost, shared defaults) so teams can clone and immediately focus on business modules.

## Features

- **API Gateway with YARP** for routing, caching, rate limiting, and JWT validation.
- **Notifications module** with templates, preferences, MassTransit integration, SignalR in-app delivery, and a Quartz daily digest job.
- **Realtime hub (`{{ProjectName}}.RealTime`)** sharing SignalR infrastructure across modules.
- **Quartz.NET scheduling** registered in `{{ProjectName}}.ServiceDefaults` for consistent background job hosting.
- **Central package management** with .NET 10 (RC) support and Clean Architecture patterns powered by Ardalis libraries.
- **Aspire AppHost orchestration** to spin up PostgreSQL, Redis, and development containers like Papercut SMTP in one command.
- **MassTransit (in-memory by default)** for event-driven communication between modules.
- **Serilog + OpenTelemetry** logging and tracing defaults.

> **Warning:** The HR module that originally triggered onboarding notifications is intentionally excluded from the template output. A reference implementation still lives under `samples/{{ProjectName}}.HR/` for documentation purposes only.

## Design Guide

A detailed design guide that explains layering, module extension patterns, messaging vs. scheduling, and real-time delivery resides in [`docs/DesignGuide.md`](docs/DesignGuide.md). Highlights:

- Clean Architecture split across Domain, Application, Infrastructure, Api folders per module.
- Module registration via `Infrastructure/Extensions/*ModuleExtensions.cs` and the gateway's `RegisterGatewayModules` helper.
- Quartz jobs registered per module and hosted through the shared service defaults.
- SignalR hub groups authenticated connections automatically, letting any module publish realtime payloads.

## Getting Started

### Install the Template

From the root directory of this template:

```bash
dotnet new install .
```

This registers the template with `dotnet new` using the short name `allspice-modular`.

> **Note:** This install command will be replaced with a NuGet package once the template is published.

### Create a New Project

```bash
dotnet new allspice-modular -n Contoso.Erp
cd Contoso.Erp
```

The `-n Contoso.Erp` parameter triggers two types of replacements:

1. **Automatic `sourceName` replacement**: `AllSpice.CleanModularMonolith` → `Contoso.Erp`
   - File and folder names
   - Namespaces, class names, project references
   - Solution file references

2. **Custom symbol replacements**:
   - `{{ProjectName}}` → `Contoso.Erp` (used in config files, comments, Azure tags)
   - `{{ProjectNameLower}}` → `contoso.erp` (used in URLs, hostnames, realm names, service names)

This creates a new solution with:
- `Contoso.Erp.slnx`
- `Services/Contoso.Erp.Notifications`
- `Shared/Contoso.Erp.RealTime`
- `Contoso.Erp.ApiGateway`, `Contoso.Erp.AppHost`, and `Contoso.Erp.ServiceDefaults`

### Restore & Build

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

### Example Transformations

When you run `dotnet new allspice-modular -n Contoso.Erp`:

| Location | Original | Replaced With |
|----------|----------|---------------|
| **File Names** | `AllSpice.CleanModularMonolith.ApiGateway.csproj` | `Contoso.Erp.ApiGateway.csproj` |
| **Namespaces** | `namespace AllSpice.CleanModularMonolith.ApiGateway` | `namespace Contoso.Erp.ApiGateway` |
| **appsettings.json** | `"Application": "{{ProjectName}}.ApiGateway"` | `"Application": "Contoso.Erp.ApiGateway"` |
| **appsettings.json** | `"Realm": "{{ProjectNameLower}}"` | `"Realm": "contoso.erp"` |
| **AppHost.cs** | `"{{ProjectNameLower}}-apigateway"` | `"contoso.erp-apigateway"` |
| **launchSettings.json** | `"{{ProjectNameLower}}_mainwebsite.dev.localhost"` | `"contoso.erp_mainwebsite.dev.localhost"` |

### Troubleshooting

**Template Not Found:**
```bash
# List installed templates
dotnet new list

# Reinstall if needed
dotnet new uninstall .
dotnet new install .
```

**Replacements Not Working:**
1. Check that `.template.config/template.json` exists
2. Verify `sourceName` matches exactly: `AllSpice.CleanModularMonolith`
3. Ensure symbols are defined correctly
4. Check file encoding (should be UTF-8)

**Build Errors After Generation:**
1. Run `dotnet restore` first
2. Check that all project references were replaced correctly
3. Verify namespaces match project names

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

The repository also contains `samples/{{ProjectName}}.HR/` as an illustrative example. It is excluded from template output but kept in source control for documentation comparisons.

## Modules Included

| Module | Highlights |
| --- | --- |
| **Notifications** | Clean Architecture layers, FastEndpoints APIs, Quartz daily digest job, MassTransit consumer (`NotificationRequestedIntegrationEvent`), SignalR in-app channel, template rendering, user preferences. |
| **RealTime** | `AppHub` SignalR hub with automatic `user:{id}` groups, `IRealtimePublisher` abstraction for broadcasting payloads or notifications to users/groups. |
| **ServiceDefaults** | Shared OpenTelemetry logging/metrics, HTTP resilience, service discovery, Quartz hosting defaults. |
| **ApiGateway** | YARP reverse proxy, FastEndpoints registration, SignalR hub mapping, rate limiting, Redis output caching, Serilog, OpenTelemetry. |
| **AppHost** | Aspire orchestrator wiring PostgreSQL, Redis, Papercut SMTP, and environment parameters (Sinch credentials, CORS origins, Authentik placeholders). |

## Scheduler & Background Jobs

- Quartz is registered globally via `{{ProjectName}}.ServiceDefaults`.
- The template ships with `NotificationDailyDigestJob` as a sample (currently logs pending notifications older than 24 hours).
- Add new jobs in each module by calling `.AddQuartz(...)` inside that module's extension method.

## Identity & Authentication

- Keycloak acts as the identity provider. Configure separate clients for ERP (enterprise SSO via Entra ID through Keycloak OIDC) and MainWebsite (direct user registration in Keycloak) portals.
- `Shared/{{ProjectName}}.Identity.Abstractions` provides portal-aware JWT registration helpers (`AddIdentityPortals`), claims utilities, and module-role authorization requirements.
- `Services/{{ProjectName}}.Identity` owns module role assignments, Keycloak directory lookups/invitations via Admin API, and seeds baseline modules (HR, Finance, Events).
- AppHost wiring injects Keycloak authority/client IDs and API tokens via environment variables so the gateway and modules validate the correct issuer. For local development the Aspire AppHost spins up Keycloak (Postgres + Keycloak container); provide `keycloak-admin-user` and `keycloak-admin-password` via Aspire parameters or secrets before running. The Keycloak container image defaults to `quay.io/keycloak/keycloak:latest` and can be configured through `Keycloak:BaseUrl` and `Keycloak:Realm` in `{{ProjectName}}.AppHost/appsettings.json`. In production plan a dedicated Keycloak deployment and point the same settings at that instance.

## Roadmap

- Finish Keycloak integration (tokens, user provisioning, UI samples).
- Add persistence for in-app inbox (read/unread state) and admin endpoints.
- Publish reusable SignalR client helper (TypeScript/C#) for dashboards.
- Replace SMS stub with production gateway adapters (Sinch/Twilio) and rate limiting.
- Provide template packages via NuGet and the `dotnet new` short-name gallery.

## License

MIT

---

Built with love using .NET 10 RC, Clean Architecture, and modular monolith principles.
