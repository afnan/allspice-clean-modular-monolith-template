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

var sinchProjectId = GetParameter(parameters, "sinch-project-id");
var sinchApiKey = GetParameter(parameters, "sinch-api-key");
var sinchServicePlanId = GetParameter(parameters, "sinch-service-plan-id");
var sinchFromNumber = GetParameter(parameters, "sinch-from-number");
var authentikSecretKey = GetParameter(parameters, "authentik-secret-key");
var authentikBootstrapPassword = GetParameter(parameters, "authentik-bootstrap-password");
var authentikDbPassword = GetParameter(parameters, "authentik-db-password");

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

#region Authentik Identity Provider (Local Dev)
if (builder.Environment.IsDevelopment())
{
  var authentikDb = builder.AddContainer("authentik-db", "postgres", "16-alpine")
      .WithEndpoint("postgres", e =>
      {
        e.TargetPort = 5432;
        e.Port = 5433;
        e.Protocol = ProtocolType.Tcp;
        e.UriScheme = "postgres";
      })
      .WithEnvironment("POSTGRES_DB", "authentik")
      .WithEnvironment("POSTGRES_USER", "authentik")
        .WithEnvironment("POSTGRES_PASSWORD", authentikDbPassword)
      .WithVolume("authentik-db-data", "/var/lib/postgresql/data");

  builder.AddContainer("authentik", "ghcr.io/goauthentik/server", "2024.6.2")
      .WithEndpoint("http", e =>
      {
        e.TargetPort = 9000;
        e.Port = 9000;
        e.UriScheme = "http";
      })
      .WithEndpoint("https", e =>
      {
        e.TargetPort = 9443;
        e.Port = 9443;
        e.UriScheme = "https";
      })
      .WithEnvironment("AUTHENTIK_SECRET_KEY", authentikSecretKey)
      .WithEnvironment("AUTHENTIK_BOOTSTRAP_PASSWORD", authentikBootstrapPassword)
      .WithEnvironment("AUTHENTIK_BOOTSTRAP_EMAIL", builder.Configuration["Authentik:Bootstrap:Email"] ?? "admin@example.com")
      .WithEnvironment("AUTHENTIK_BOOTSTRAP_USERNAME", builder.Configuration["Authentik:Bootstrap:Username"] ?? "admin")
      .WithEnvironment("AUTHENTIK_POSTGRESQL__HOST", "authentik-db")
      .WithEnvironment("AUTHENTIK_POSTGRESQL__PORT", "5432")
      .WithEnvironment("AUTHENTIK_POSTGRESQL__USER", "authentik")
      .WithEnvironment("AUTHENTIK_POSTGRESQL__PASSWORD", authentikDbPassword)
      .WithEnvironment("AUTHENTIK_POSTGRESQL__NAME", "authentik")
      .WithEnvironment("AUTHENTIK_REDIS__HOST", "redis")
      .WithEnvironment("AUTHENTIK_REDIS__PORT", "6379")
      .WithEnvironment("AUTHENTIK_DISABLE_UPDATE_CHECK", "true")
      .WithEnvironment("AUTHENTIK_LOGGING__LEVEL", builder.Configuration["Authentik:Logging:Level"] ?? "INFO");
}
#endregion

#region API Gateway
var apiGateway = builder.AddProject<Projects.AllSpice_CleanModularMonolith_ApiGateway>("allspice-cleanmodular-apigateway")
    .WithReference(notificationsDatabase)
    .WithReference(identityDatabase)
    .WithEnvironment("ConnectionStrings__redis", redisEndpoint)
    .WithEnvironment("Cors__WebOrigin", builder.Configuration["Cors:WebOrigin"] ?? "https://localhost:7001")
    .WithEnvironment("Cors__MobileOrigin", builder.Configuration["Cors:MobileOrigin"] ?? "https://localhost:7002")
    .WithEnvironment("Authentik__Portals__Erp__Authority", builder.Configuration["Authentik:Portals:Erp:Authority"] ?? "")
    .WithEnvironment("Authentik__Portals__Erp__Audience", builder.Configuration["Authentik:Portals:Erp:Audience"] ?? "")
    .WithEnvironment("Authentik__Portals__Public__Authority", builder.Configuration["Authentik:Portals:Public:Authority"] ?? "")
    .WithEnvironment("Authentik__Portals__Public__Audience", builder.Configuration["Authentik:Portals:Public:Audience"] ?? "")
    .WithEnvironment("Identity__Authentik__BaseUrl", builder.Configuration["Identity:Authentik:BaseUrl"] ?? "")
    .WithEnvironment("Identity__Authentik__ApiToken", builder.Configuration["Identity:Authentik:ApiToken"] ?? "")
    .WithEnvironment("Identity__Authentik__UserLookupTemplate", builder.Configuration["Identity:Authentik:UserLookupTemplate"] ?? "/api/v3/core/users/{0}/")
    .WithEnvironment("Identity__Authentik__InvitationEndpoint", builder.Configuration["Identity:Authentik:InvitationEndpoint"] ?? "")
    .WithEnvironment("Identity__Authentik__AllowUntrustedCertificates", builder.Configuration["Identity:Authentik:AllowUntrustedCertificates"] ?? "false")
    .WithEnvironment("Notifications__Sinch__ProjectId", sinchProjectId)
    .WithEnvironment("Notifications__Sinch__ApiKey", sinchApiKey)
    .WithEnvironment("Notifications__Sinch__Sms__ServicePlanId", sinchServicePlanId)
    .WithEnvironment("Notifications__Sinch__Sms__FromNumber", sinchFromNumber);
#endregion


builder.Build().Run();

