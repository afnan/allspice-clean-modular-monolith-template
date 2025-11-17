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
var authentikSecretKey = GetParameter(parameters, "authentik-secret-key");
var authentikBootstrapPassword = GetParameter(parameters, "authentik-bootstrap-password");

//var authentikClientSecret = builder.AddParameter("identity-authentik-client-secret");


var authentikImage = builder.Configuration["Authentik:Image"] ?? "ghcr.io/goauthentik/server";
var authentikTag = builder.Configuration["Authentik:Tag"] ?? "2025.10.1";

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
var authentikDb = postgres.AddDatabase("authentikdb");
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
  const string authentikDbHost = "postgres";
  const string authentikDbPort = "5432";

  var authentikServer = builder.AddContainer("authentik-server", authentikImage, authentikTag)
      .WithArgs("server")
      .WithReference(authentikDb)
      .WithReference(postgres)
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
      .WaitFor(postgres)
      .WithEnvironment("AUTHENTIK_HOST", "localhost:9443")
      .WithEnvironment("AUTHENTIK_SECRET_KEY", authentikSecretKey)
      .WithEnvironment("AUTHENTIK_BOOTSTRAP_PASSWORD", authentikBootstrapPassword)
      .WithEnvironment("AUTHENTIK_BOOTSTRAP_EMAIL", builder.Configuration["Authentik:Bootstrap:Email"] ?? "admin@example.com")
      .WithEnvironment("AUTHENTIK_BOOTSTRAP_USERNAME", builder.Configuration["Authentik:Bootstrap:Username"] ?? "admin")
      .WithEnvironment("AUTHENTIK_POSTGRESQL__HOST", authentikDbHost)
      .WithEnvironment("AUTHENTIK_POSTGRESQL__PORT", authentikDbPort)
      .WithEnvironment("AUTHENTIK_POSTGRESQL__USER", postgresUserValue)
      .WithEnvironment("AUTHENTIK_POSTGRESQL__PASSWORD", postgresPasswordValue)
      .WithEnvironment("AUTHENTIK_POSTGRESQL__NAME", "authentikdb")
      .WithEnvironment("AUTHENTIK_REDIS__HOST", "redis")
      .WithEnvironment("AUTHENTIK_REDIS__PORT", "6379")
      .WithEnvironment("AUTHENTIK_DISABLE_UPDATE_CHECK", "true")
      .WithEnvironment("AUTHENTIK_LOGGING__LEVEL", builder.Configuration["Authentik:Logging:Level"] ?? "INFO")
      ;

  var authentikWorker = builder.AddContainer("authentik-worker", authentikImage, authentikTag)
      .WithArgs("worker")
      .WithReference(authentikDb)
      .WithReference(postgres)
      .WaitFor(postgres)
      .WithEnvironment("AUTHENTIK_HOST", "localhost:9443")
      .WithEnvironment("AUTHENTIK_SECRET_KEY", authentikSecretKey)
      .WithEnvironment("AUTHENTIK_BOOTSTRAP_PASSWORD", authentikBootstrapPassword)
      .WithEnvironment("AUTHENTIK_BOOTSTRAP_EMAIL", builder.Configuration["Authentik:Bootstrap:Email"] ?? "admin@example.com")
      .WithEnvironment("AUTHENTIK_BOOTSTRAP_USERNAME", builder.Configuration["Authentik:Bootstrap:Username"] ?? "admin")
      .WithEnvironment("AUTHENTIK_POSTGRESQL__HOST", authentikDbHost)
      .WithEnvironment("AUTHENTIK_POSTGRESQL__PORT", authentikDbPort)
      .WithEnvironment("AUTHENTIK_POSTGRESQL__USER", postgresUserValue)
      .WithEnvironment("AUTHENTIK_POSTGRESQL__PASSWORD", postgresPasswordValue)
      .WithEnvironment("AUTHENTIK_POSTGRESQL__NAME", "authentikdb")
      .WithEnvironment("AUTHENTIK_REDIS__HOST", "redis")
      .WithEnvironment("AUTHENTIK_REDIS__PORT", "6379")
      .WithEnvironment("AUTHENTIK_DISABLE_UPDATE_CHECK", "true")
      .WithEnvironment("AUTHENTIK_LOGGING__LEVEL", builder.Configuration["Authentik:Logging:Level"] ?? "INFO")
      .WithBindMount("/var/run/docker.sock", "/var/run/docker.sock");
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
    .WithEnvironment("Notifications__Sinch__Sms__FromNumber", sinchFromNumber)
    .WaitFor(postgres);
#endregion


builder.AddProject<Projects.AllSpice_CleanModularMonolith_ErpPortal>("allspice-cleanmodularmonolith-erpportal");



builder.Build().Run();

