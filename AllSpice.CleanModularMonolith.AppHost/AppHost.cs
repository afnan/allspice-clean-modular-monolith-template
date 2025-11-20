using System.Net.Sockets;
using Azure.Provisioning.PostgreSql;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

static string GetParameter(IConfigurationSection section, string key, string defaultValue = "")
    => section[key] ?? defaultValue;

var parameters = builder.Configuration.GetSection("Parameters");

// Define parameters for PostgreSQL
var postgresUser = builder.AddParameter("postgres-user");
var postgresPassword = builder.AddParameter("postgres-password");

var postgresUserValue = builder.Configuration["Parameters:postgres-user"] ?? "postgres";
var postgresPasswordValue = builder.Configuration["Parameters:postgres-password"] ?? "pass!";

var sinchProjectId = GetParameter(parameters, "sinch-project-id");
var sinchApiKey = GetParameter(parameters, "sinch-api-key");
var sinchServicePlanId = GetParameter(parameters, "sinch-service-plan-id");
var sinchFromNumber = GetParameter(parameters, "sinch-from-number");
var keycloakAdminUser = GetParameter(parameters, "keycloak-admin-user");
var keycloakAdminPassword = GetParameter(parameters, "keycloak-admin-password");
var keycloakRealmValue = GetParameter(parameters, "keycloak-realm", "sadaqa");
var keycloakApiToken = GetParameter(parameters, "keycloak-api-token");
var keycloakErpClientSecret = GetParameter(parameters, "keycloak-erp-client-secret");
var keycloakMainWebsiteClientSecret = GetParameter(parameters, "keycloak-mainwebsite-client-secret");
var entraIdTenantId = GetParameter(parameters, "entra-id-tenant-id");
var entraIdClientId = GetParameter(parameters, "entra-id-client-id");
var entraIdClientSecret = GetParameter(parameters, "entra-id-client-secret");
var smtpUsername = GetParameter(parameters, "smtp-username");
var smtpPassword = GetParameter(parameters, "smtp-password");
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
var apiGateway = builder.AddProject<Projects.AllSpice_CleanModularMonolith_ApiGateway>("allspice-cleanmodular-apigateway")
    .WithReference(notificationsDatabase)
    .WithReference(identityDatabase)
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
    .WithEnvironment("Notifications__Sinch__ProjectId", sinchProjectId)
    .WithEnvironment("Notifications__Sinch__ApiKey", sinchApiKey)
    .WithEnvironment("Notifications__Sinch__Sms__ServicePlanId", sinchServicePlanId)
    .WithEnvironment("Notifications__Sinch__Sms__FromNumber", sinchFromNumber)
    .WaitFor(postgres);
#endregion

#region Portal Applications
// Pass the Keycloak endpoint reference and realm separately
// The endpoint reference will resolve at runtime, and the apps will construct the authority URL
// This is necessary because the OIDC handler creates its own HttpClient that doesn't use service discovery
var erpPortal = builder.AddProject<Projects.AllSpice_CleanModularMonolith_ErpPortal>("allspice-cleanmodularmonolith-erpportal")
    .WithEnvironment("Keycloak__BaseUrl", keycloakEndpoint)
    .WithEnvironment("Keycloak__Realm", keycloakRealm)
    .WithEnvironment("Keycloak__Portals__Erp__ClientId", builder.Configuration["Keycloak:Portals:Erp:ClientId"] ?? "")
    .WithEnvironment("Keycloak__Portals__Erp__ClientSecret", keycloakErpClientSecret)
    .WithEnvironment("Keycloak__Portals__Erp__CallbackPath", builder.Configuration["Keycloak:Portals:Erp:CallbackPath"] ?? "/signin-oidc")
    .WithEnvironment("Keycloak__Portals__Erp__SignedOutCallbackPath", builder.Configuration["Keycloak:Portals:Erp:SignedOutCallbackPath"] ?? "/signout-callback-oidc")
    .WithEnvironment("EntraId__Portals__Erp__TenantId", entraIdTenantId)
    .WithEnvironment("EntraId__Portals__Erp__ClientId", entraIdClientId)
    .WithEnvironment("EntraId__Portals__Erp__ClientSecret", entraIdClientSecret);

var mainWebsite = builder.AddProject<Projects.AllSpice_CleanModularMonolith_MainWebsite>("allspice-cleanmodularmonolith-mainwebsite")
  .WaitFor(apiGateway)
  .WaitFor(keycloak)
  .WaitFor(redis)
  .WaitFor(storage)
  .WaitFor(postgres)
    .WithEnvironment("Keycloak__BaseUrl", keycloakEndpoint)
    .WithEnvironment("Keycloak__Realm", keycloakRealm)
    .WithEnvironment("Keycloak__Portals__MainWebsite__ClientId", builder.Configuration["Keycloak:Portals:MainWebsite:ClientId"] ?? "")
    .WithEnvironment("Keycloak__Portals__MainWebsite__ClientSecret", keycloakMainWebsiteClientSecret)
    .WithEnvironment("Keycloak__Portals__MainWebsite__CallbackPath", builder.Configuration["Keycloak:Portals:MainWebsite:CallbackPath"] ?? "/signin-oidc")
    .WithEnvironment("Keycloak__Portals__MainWebsite__SignedOutCallbackPath", builder.Configuration["Keycloak:Portals:MainWebsite:SignedOutCallbackPath"] ?? "/signout-callback-oidc");
#endregion



builder.Build().Run();

