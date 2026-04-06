# AllSpice Clean Modular Monolith

A production-ready .NET 10 modular monolith template (`dotnet new allspice-modular`) using Clean Architecture, CQRS, and event-driven patterns. Ships with full Keycloak integration, multi-provider email delivery, PuppeteerSharp PDF generation, and a complete Identity + Notifications module stack.

## Features

- **API Gateway with YARP** for routing, Redis output caching, and JWT validation
- **Identity module** with full Keycloak Admin API integration — user provisioning, role management, invitation flow, client credentials token caching
- **Notifications module** with Resend/SendGrid/MailKit fallback chain, HTML email templates (embedded resources), SignalR in-app delivery, Quartz daily digest
- **PuppeteerSharp PDF library** — headless Chromium, A4 output, reusable theme CSS, header/footer page-frame
- **Realtime hub** sharing SignalR infrastructure across modules with automatic user groups
- **Wolverine messaging** with PostgreSQL durable outbox for reliable event-driven cross-module communication
- **Quartz.NET scheduling** with per-module jobs (Keycloak user sync, notification digest)
- **Aspire AppHost** to spin up PostgreSQL, Redis, Keycloak, and Papercut SMTP in one command
- **Central package management** with .NET 10, Clean Architecture patterns powered by Ardalis libraries
- **FastEndpoints** with explicit assembly discovery (not controllers)
- **Serilog + OpenTelemetry** logging and tracing

## Getting Started

### Install the Template

```bash
dotnet new install .
```

### Create a New Project

```bash
dotnet new allspice-modular -n Contoso.Erp
cd Contoso.Erp
dotnet restore Contoso.Erp.slnx
dotnet build Contoso.Erp.slnx
```

### Run with Aspire

```bash
dotnet run --project Contoso.Erp.AppHost/Contoso.Erp.AppHost.csproj
```

Spins up PostgreSQL, Redis, Keycloak, and Papercut SMTP (dev only).

## Project Layout

```
{{ProjectName}}/
|- {{ProjectName}}.ApiGateway/           -- Sole runnable host, YARP, FastEndpoints, SignalR
|- {{ProjectName}}.AppHost/              -- Aspire orchestrator (Postgres, Redis, Keycloak, Papercut)
|- {{ProjectName}}.ServiceDefaults/      -- OpenTelemetry, resilience, service discovery, Quartz hosting
|- Services/{{ProjectName}}.Identity/    -- User/Invitation aggregates, Keycloak sync, RBAC
|- Services/{{ProjectName}}.Notifications/ -- Multi-channel delivery, templates, preferences
|- Shared/{{ProjectName}}.SharedKernel/  -- Base entities, domain events, EfRepository, pipeline behaviors
|- Shared/{{ProjectName}}.Pdf/           -- PuppeteerSharp PDF generation, theme CSS, footer builder
|- Shared/{{ProjectName}}.RealTime/      -- SignalR hub, IRealtimePublisher
|- Shared/{{ProjectName}}.Notifications.Contracts/ -- Integration event DTOs
|- Shared/{{ProjectName}}.Identity.Abstractions/   -- Portal-aware JWT, claims, module-role auth
|- Shared/{{ProjectName}}.Web/           -- Ardalis.Result HTTP extensions
\- Directory.Packages.props              -- Central NuGet version management
```

## Modules

| Module | Highlights |
| --- | --- |
| **Identity** | User + Invitation aggregates, `KeycloakTokenProvider` (client credentials flow), `KeycloakDirectoryClient` (full Admin REST API), `KeycloakUserSyncJob`, module role assignments/templates, health checks |
| **Notifications** | Email (Resend/SendGrid/MailKit), InApp (SignalR), HTML templates (embedded resources + DB seeding), `NotificationContentBuilder` with `{{token}}` replacement, Quartz daily digest, Wolverine consumer |
| **ApiGateway** | FastEndpoints (explicit assembly discovery), YARP reverse proxy, SignalR hub mapping, Redis output caching, centralized Wolverine durable outbox registration |
| **AppHost** | Aspire orchestrator: PostgreSQL, Redis, Keycloak (dev + prod modes), Papercut SMTP |

## Identity & Authentication

- **Keycloak** acts as the identity provider with client credentials flow for server-to-server API calls
- `KeycloakTokenProvider` caches tokens with SemaphoreSlim thread safety, refreshes 5 minutes before expiry
- `KeycloakTokenHandler` (DelegatingHandler) auto-injects Bearer tokens into HTTP clients
- `KeycloakDirectoryClient` provides full Admin REST API: create users, manage roles, reset passwords, paginated user listing
- `KeycloakUserSyncJob` (Quartz) periodically syncs Keycloak users to local User table
- Invitation flow: creates Keycloak user with temp password (with compensating delete on local failure), local User + Invitation records, fires domain event that publishes notification integration event via durable outbox

## Email Delivery

Provider fallback chain via `EmailSenderDispatcher`:
- **Development:** Always MailKit (Papercut SMTP container via Aspire)
- **Production:** Resend -> SendGrid -> MailKit

HTML email templates stored as embedded resources, loaded by `EmailTemplateLoader`, merged with `_Layout.html` layout, and seeded/synced to database on startup. Templates: `invitation-created`, `registration-welcome`, `role-assigned`, `role-revoked`, `password-reset`, `profile-updated`.

## Configuration

Key environment variables (set via Aspire AppHost or appsettings):

| Setting | Description |
| --- | --- |
| `Identity:Keycloak:ServiceName` | Aspire service discovery name for Keycloak |
| `Identity:Keycloak:Realm` | Keycloak realm name |
| `Identity:Keycloak:ClientId` | OAuth client ID for client credentials flow |
| `Identity:Keycloak:ClientSecret` | OAuth client secret |
| `Identity:Keycloak:ApiToken` | Static admin token (fallback when ClientId not set) |
| `Notifications:Resend:ApiKey` | Resend API key |
| `Notifications:Resend:FromAddress` | Resend from address |
| `Notifications:SendGrid:ApiKey` | SendGrid API key |
| `Notifications:SendGrid:FromAddress` | SendGrid from address |
| `Notifications:Smtp:Host` | SMTP host (MailKit) |
| `Notifications:Smtp:Port` | SMTP port |

## License

MIT

---

Built with .NET 10, Clean Architecture, and modular monolith principles.
