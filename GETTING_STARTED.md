# Getting Started

This guide walks you through setting up your new project after scaffolding from the template.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Aspire containers)
- A code editor (VS Code, Rider, or Visual Studio)

## 1. Scaffold Your Project

```bash
dotnet new install .
dotnet new allspice-modular -n YourProject
cd YourProject
```

This replaces all namespaces, file names, and configuration values with your project name.

## 2. Run with Aspire (Quickest Start)

```bash
dotnet run --project YourProject.AppHost
```

This automatically provisions:
- **PostgreSQL** — databases for Identity and Notifications modules
- **Redis** — output caching and distributed cache
- **Keycloak** — identity provider (admin UI at `http://localhost:8080`)
- **Papercut SMTP** — email testing (UI at `http://localhost:37408`)

The API Gateway runs at `https://localhost:7113` (or `http://localhost:5120`).

> **First run** will download container images (~5 min). Subsequent runs are instant.

## 3. Configure Keycloak (Required for Authentication)

After Aspire starts Keycloak, you need to create a realm and clients:

### a. Access Keycloak Admin Console

Open `http://localhost:8080` and log in with:
- **Username:** `admin`
- **Password:** `admin`

### b. Create a Realm

1. Click the realm dropdown (top-left) → **Create Realm**
2. Set **Realm name** to your project name in lowercase (e.g., `yourproject`)
3. Click **Create**

### c. Create the ERP Portal Client

1. Go to **Clients** → **Create client**
2. **Client ID:** `erp-portal`
3. **Client type:** OpenID Connect
4. **Valid redirect URIs:** `https://localhost:7113/*`
5. **Web origins:** `https://localhost:7113`
6. Enable **Client authentication** (for confidential client)
7. Save and note the **Client secret** from the **Credentials** tab

### d. Create the Service Account Client (for Admin API)

1. Go to **Clients** → **Create client**
2. **Client ID:** `yourproject-admin`
3. **Client type:** OpenID Connect
4. Enable **Client authentication** and **Service accounts roles**
5. Disable **Standard flow** and **Direct access grants**
6. Save, go to **Credentials** tab, copy the **Client secret**
7. Go to **Service accounts roles** tab → **Assign role** → Filter by `realm-management` → assign `manage-users` and `manage-realm`

### e. Update Configuration

In `YourProject.AppHost/appsettings.json`, update:

```json
{
  "Parameters": {
    "keycloak-realm": "yourproject"
  },
  "Identity": {
    "Keycloak": {
      "ClientId": "yourproject-admin",
      "ClientSecret": "<paste-service-account-secret>"
    }
  }
}
```

## 4. Configuration Reference

### Required for Authentication

| Setting | Location | Description |
|---------|----------|-------------|
| `Keycloak:Realm` | AppHost appsettings | Realm name (lowercase project name) |
| `Keycloak:Portals:Erp:ClientId` | AppHost appsettings | ERP portal OAuth client ID |
| `Identity:Keycloak:ClientId` | AppHost appsettings | Service account client ID for Admin API |
| `Identity:Keycloak:ClientSecret` | AppHost appsettings | Service account client secret |

### Optional — Email Providers (Production)

In development, all emails go to Papercut SMTP (no config needed). For production, configure one or both:

| Setting | Description |
|---------|-------------|
| `Notifications:Resend:ApiKey` | [Resend](https://resend.com) API key (primary provider) |
| `Notifications:Resend:FromAddress` | Verified sender email for Resend |
| `Notifications:SendGrid:ApiKey` | [SendGrid](https://sendgrid.com) API key (fallback provider) |
| `Notifications:SendGrid:FromAddress` | Verified sender email for SendGrid |

The email fallback chain is: **Resend** → **SendGrid** → **MailKit** (SMTP).

### Optional — CORS Origins

| Setting | Default | Description |
|---------|---------|-------------|
| `Cors:WebOrigin` | `http://localhost:5173` | Frontend web app URL |
| `Cors:MobileOrigin` | `http://localhost:5174` | Mobile app URL |

## 5. Verify Everything Works

```bash
# Build
dotnet build YourProject.slnx

# Run tests
dotnet test YourProject.slnx

# Run with Aspire
dotnet run --project YourProject.AppHost
```

Then:
1. Open the Aspire dashboard (URL shown in terminal output)
2. Verify all services are healthy (green)
3. Test the API at `https://localhost:7113/swagger`
4. Check Papercut for test emails at `http://localhost:37408`

## 6. Add a New Module

1. Create the Clean Architecture folders:
   ```
   Services/YourProject.NewModule/
     Domain/          -- Aggregates, ValueObjects, Events
     Application/     -- Features (Commands/Queries), Contracts, DTOs
     Infrastructure/  -- Persistence, Services, Extensions
     Api/             -- FastEndpoints
   ```

2. Create the module extension:
   ```csharp
   // Infrastructure/Extensions/NewModuleExtensions.cs
   public static class NewModuleExtensions
   {
       public static IHostApplicationBuilder AddNewModuleServices(
           this IHostApplicationBuilder builder, ILogger logger) { ... }

       public static async Task<WebApplication> EnsureNewModuleDatabaseAsync(
           this WebApplication app) { ... }
   }
   ```

3. Register in the gateway:
   - `GatewayModuleRegistrationExtensions.cs` → add `builder.AddNewModuleServices(logger);`
   - `Program.cs` → add `await app.EnsureNewModuleDatabaseAsync();`
   - `GatewayServiceCollectionExtensions.cs` → add assembly to FastEndpoints discovery

4. Add a database in `AppHost.cs`:
   ```csharp
   var newModuleDb = postgres.AddDatabase("newmoduledb");
   // Add .WithReference(newModuleDb) to apiGateway
   ```

## Architecture Overview

```
ApiGateway (sole host)
├── FastEndpoints (API)
├── SignalR (/hubs/app)
├── YARP (reverse proxy)
├── Wolverine (in-memory messaging)
└── Modules register services via extension methods

Identity Module
├── Keycloak integration (user sync, roles, invitations)
├── Client credentials token caching
└── Module role authorization

Notifications Module
├── Email: Resend → SendGrid → MailKit fallback
├── InApp: SignalR with external ID resolution
├── HTML templates (embedded resources)
└── Background dispatcher with retry/backoff
```

## Troubleshooting

**Keycloak container not starting:**
- Ensure Docker Desktop is running
- Check Aspire dashboard for container logs
- Verify ports 8080 and 5432 are not in use

**Auth returning 401:**
- Verify realm exists in Keycloak
- Check `ClientId` matches the Keycloak client
- Ensure the JWT `iss` claim matches `Authority` in config

**Emails not arriving in Papercut:**
- Verify Papercut container is running (port 37408)
- Check `Notifications:Smtp:Host` is `localhost` and `Port` is `25`
- View Aspire dashboard for dispatcher logs

**Database errors on startup:**
- Databases auto-create via `EnsureCreatedAsync` with retry (5 attempts)
- Check PostgreSQL container is healthy in Aspire dashboard
- Verify connection string resolution in logs
