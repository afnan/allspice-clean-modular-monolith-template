using System.Net.Sockets;
using Azure.Provisioning.PostgreSql;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Reads a non-secret parameter with an optional default. Use only for non-sensitive values.
static string GetParameter(IConfigurationSection section, string key, string defaultValue = "")
    => section[key] ?? defaultValue;

// Reads a secret value. In Development, falls back to a dev-only default if missing so the
// template runs out-of-the-box. In any other environment, missing secrets throw — the AppHost
// refuses to launch with placeholder credentials in production.
string GetSecret(string key, string developmentDefault)
{
    var value = builder.Configuration[$"Parameters:{key}"];
    if (!string.IsNullOrEmpty(value))
    {
        return value;
    }

    if (builder.Environment.IsDevelopment())
    {
        return developmentDefault;
    }

    throw new InvalidOperationException(
        $"Required secret 'Parameters:{key}' is not configured. Set it via dotnet user-secrets, " +
        $"environment variable Parameters__{key.Replace('-', '_')}, or --parameter {key}=...");
}

var parameters = builder.Configuration.GetSection("Parameters");

// Define parameters for PostgreSQL — password is secret-flagged so the dashboard masks it.
var postgresUser = builder.AddParameter("postgres-user");
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

var postgresUserValue = GetParameter(parameters, "postgres-user", "postgres");
var postgresPasswordValue = GetSecret("postgres-password", developmentDefault: "pass!");

var keycloakAdminUser = GetParameter(parameters, "keycloak-admin-user", "admin");
var keycloakAdminPassword = GetSecret("keycloak-admin-password", developmentDefault: "admin");
var keycloakRealmValue = GetParameter(parameters, "keycloak-realm", "allspice-cleanmodularmonolith");
var keycloakApiToken = GetSecret("keycloak-api-token", developmentDefault: "");
var smtpUsername = GetParameter(parameters, "smtp-username");
var smtpPassword = GetSecret("smtp-password", developmentDefault: "");
var emailFromAddress = GetParameter(parameters, "email-from-address");
var emailReplyToAddress = GetParameter(parameters, "email-reply-to-address");

#region Local Email Container
// Papercut SMTP container for email testing
if (builder.Environment.IsDevelopment())
{
  var papercut = builder.AddContainer("papercut", "jijiechen/papercut", "latest")
  .WithEndpoint("smtp", e =>
  {
    e.TargetPort = 25;   // container port
    e.Port = 25;         // host port
    e.Protocol = ProtocolType.Tcp;
    e.UriScheme = "smtp";
  })
  .WithEndpoint("ui", e =>
  {
    e.TargetPort = 37408;
    e.Port = 37408;
    e.UriScheme = "http";
  });
}

#endregion

#region Storage
var storage = builder.AddAzureStorage("Storage");
if (builder.Environment.IsDevelopment())
{
  storage.RunAsEmulator();
}
var blobs = storage.AddBlobs("BlobConnection");
#endregion


#region Database Server Setup
var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
  .WithPasswordAuthentication(userName: postgresUser, password: postgresPassword)
  .ConfigureInfrastructure(infra =>
  {
    var flexibleServer = infra.GetProvisionableResources()
                              .OfType<PostgreSqlFlexibleServer>()
                              .Single();

    flexibleServer.Sku = new PostgreSqlFlexibleServerSku
    {
      Tier = PostgreSqlFlexibleServerSkuTier.Burstable,
      Name = "Standard_B2s" // 2 vCPUs, 4 GB RAM, good balance for small apps
    };

    flexibleServer.Tags.Add("Project", "AllSpice.CleanModularMonolith");
    flexibleServer.Tags.Add("Environment", "Production");
  }).RunAsContainer(container => container.WithPgAdmin().WithPgWeb().WithDataVolume());

#endregion

#region Database Creation
var notificationsDatabase = postgres.AddDatabase("notificationsdb");
var identityDatabase = postgres.AddDatabase("identitydb");
var keycloakDb = postgres.AddDatabase("keycloakdb");
var messagingDatabase = postgres.AddDatabase("messagingdb");
#endregion

#region Redis Cache
var redis = builder.AddContainer("redis", "redis", "latest")
    .WithEndpoint("tcp", e =>
    {
      e.TargetPort = 6379;
      e.Port = 6379;
      e.Protocol = ProtocolType.Tcp;
      e.UriScheme = "redis";
    });
var redisEndpoint = redis.GetEndpoint("tcp");
#endregion

#region Keycloak Identity Provider
// Development mode: Uses start-dev with HTTP only, relaxed hostname settings
// Production mode: Use start with HTTPS, proper hostname, and optimized build
const string keycloakDbHost = "postgres";
const string keycloakDbPort = "5432";
// Use parameter for Keycloak realm (allows override via command line or environment)
var keycloakRealm = keycloakRealmValue;

IResourceBuilder<IResourceWithEndpoints> keycloak;

if (builder.Environment.IsDevelopment())
{
  keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "latest")
      .WithArgs("start-dev")
      .WithReference(keycloakDb)
      .WithReference(postgres)
      .WithEndpoint("http", e =>
      {
        e.TargetPort = 8080;
        e.Port = 8080;
        e.UriScheme = "http";
      })
      .WaitFor(postgres)
      .WithEnvironment("KC_DB", "postgres")
      .WithEnvironment("KC_DB_URL_HOST", keycloakDbHost)
      .WithEnvironment("KC_DB_URL_PORT", keycloakDbPort)
      .WithEnvironment("KC_DB_URL_DATABASE", "keycloakdb")
      .WithEnvironment("KC_DB_USERNAME", postgresUserValue)
      .WithEnvironment("KC_DB_PASSWORD", postgresPasswordValue)
      // Use v2 hostname options to avoid deprecation warnings
      .WithEnvironment("KC_HOSTNAME_STRICT", "false")
      .WithEnvironment("KC_HOSTNAME_STRICT_BACKEND", "false")
      .WithEnvironment("KC_HTTP_ENABLED", "true")
      .WithEnvironment("KC_HTTPS_ENABLED", "false")
      .WithEnvironment("KC_HEALTH_ENABLED", "true")
      .WithEnvironment("KC_METRICS_ENABLED", "true")
      .WithEnvironment("KEYCLOAK_ADMIN", keycloakAdminUser ?? "admin")
      .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", keycloakAdminPassword ?? "admin");
}
else
{
  // Production mode: use optimized start with HTTPS support
  var keycloakImage = builder.Configuration["Keycloak:Image"] ?? "quay.io/keycloak/keycloak";
  var keycloakTag = builder.Configuration["Keycloak:Tag"] ?? "latest";
  var keycloakHostname = builder.Configuration["Keycloak:Hostname"] ?? "";
  var proxyMode = builder.Configuration["Keycloak:Proxy"] ?? "edge";

  var keycloakBuilder = builder.AddContainer("keycloak", keycloakImage, keycloakTag)
      .WithArgs("start", "--optimized")
      .WithReference(keycloakDb)
      .WithReference(postgres)
      .WithEndpoint("http", e =>
      {
        e.TargetPort = 8080;
        e.UriScheme = "http";
      })
      .WithEndpoint("https", e =>
      {
        e.TargetPort = 8443;
        e.UriScheme = "https";
      })
      .WaitFor(postgres)
      .WithEnvironment("KC_DB", "postgres")
      .WithEnvironment("KC_DB_URL_HOST", keycloakDbHost)
      .WithEnvironment("KC_DB_URL_PORT", keycloakDbPort)
      .WithEnvironment("KC_DB_URL_DATABASE", "keycloakdb")
      .WithEnvironment("KC_DB_USERNAME", postgresUserValue)
      .WithEnvironment("KC_DB_PASSWORD", postgresPasswordValue)
      .WithEnvironment("KC_HOSTNAME_STRICT", builder.Configuration["Keycloak:HostnameStrict"] ?? "true")
      .WithEnvironment("KC_HOSTNAME_STRICT_BACKEND", builder.Configuration["Keycloak:HostnameStrictBackend"] ?? "false")
      .WithEnvironment("KC_HTTP_ENABLED", builder.Configuration["Keycloak:HttpEnabled"] ?? "true")
      .WithEnvironment("KC_HTTPS_ENABLED", builder.Configuration["Keycloak:HttpsEnabled"] ?? "true")
      .WithEnvironment("KC_HTTPS_PORT", builder.Configuration["Keycloak:HttpsPort"] ?? "8443")
      .WithEnvironment("KC_HEALTH_ENABLED", "true")
      .WithEnvironment("KC_METRICS_ENABLED", "true")
      .WithEnvironment("KC_PROXY", proxyMode)
      .WithEnvironment("KEYCLOAK_ADMIN", keycloakAdminUser ?? "admin")
      .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", keycloakAdminPassword ?? "admin");

  if (!string.IsNullOrWhiteSpace(keycloakHostname))
  {
    keycloakBuilder.WithEnvironment("KC_HOSTNAME", keycloakHostname);
  }

  keycloak = keycloakBuilder;
}

// Get the Keycloak endpoint reference for OIDC configuration
// The endpoint reference will resolve at runtime when the container is running
var keycloakEndpoint = keycloak.GetEndpoint("http");

#endregion

#region API Gateway
var apiGateway = builder.AddProject<Projects.AllSpice_CleanModularMonolith_ApiGateway>("allspice-cleanmodularmonolith-apigateway")
    .WithReference(notificationsDatabase)
    .WithReference(identityDatabase)
    .WithReference(messagingDatabase)
    .WithEnvironment("ConnectionStrings__redis", redisEndpoint)
    .WithEnvironment("Cors__WebOrigin", builder.Configuration["Cors:WebOrigin"] ?? "https://localhost:7001")
    .WithEnvironment("Cors__MobileOrigin", builder.Configuration["Cors:MobileOrigin"] ?? "https://localhost:7002")
    .WithEnvironment("Keycloak__BaseUrl", keycloakEndpoint)
    .WithEnvironment("Keycloak__Realm", keycloakRealm)
    .WithEnvironment("Keycloak__Portals__Erp__ClientId", builder.Configuration["Keycloak:Portals:Erp:ClientId"] ?? "")
    .WithEnvironment("Keycloak__Portals__MainWebsite__ClientId", builder.Configuration["Keycloak:Portals:MainWebsite:ClientId"] ?? "")
    .WithEnvironment("Identity__Keycloak__ServiceName", "keycloak")
    .WithEnvironment("Identity__Keycloak__Realm", keycloakRealm)
    .WithEnvironment("Identity__Keycloak__ApiToken", keycloakApiToken)
    .WithEnvironment("Identity__Keycloak__UserLookupTemplate", builder.Configuration["Identity:Keycloak:UserLookupTemplate"] ?? "/admin/realms/{realm}/users/{0}")
    .WithEnvironment("Identity__Keycloak__InvitationEndpoint", builder.Configuration["Identity:Keycloak:InvitationEndpoint"] ?? "")
    .WithEnvironment("Identity__Keycloak__AllowUntrustedCertificates", builder.Configuration["Identity:Keycloak:AllowUntrustedCertificates"] ?? "false")
    .WithEnvironment("Identity__Keycloak__ClientId", builder.Configuration["Identity:Keycloak:ClientId"] ?? "")
    .WithEnvironment("Identity__Keycloak__ClientSecret", builder.Configuration["Identity:Keycloak:ClientSecret"] ?? "")
    .WithEnvironment("Notifications__Smtp__Host", "localhost")
    .WithEnvironment("Notifications__Smtp__Port", "25")
    .WaitFor(postgres);
#endregion


builder.Build().Run();

