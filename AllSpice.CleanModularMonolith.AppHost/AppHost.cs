using System.Net.Sockets;
using Azure.Provisioning.PostgreSql;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Define parameters for PostgreSQL and admin details
var postgresUser = builder.AddParameter("postgres-user");
var postgresPassword = builder.AddParameter("postgres-password");

var sinchProjectId = builder.AddParameter("sinch-project-id");
var sinchApiKey = builder.AddParameter("sinch-api-key");
var sinchServicePlanId = builder.AddParameter("sinch-service-plan-id");
var sinchFromNumber = builder.AddParameter("sinch-from-number");

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

#region API Gateway
var apiGateway = builder.AddProject<Projects.AllSpice_CleanModularMonolith_ApiGateway>("allspice-cleanmodular-apigateway")
    .WithReference(notificationsDatabase)
    .WithEnvironment("ConnectionStrings__redis", redisEndpoint)
    .WithEnvironment("Cors__WebOrigin", builder.Configuration["Cors:WebOrigin"] ?? "https://localhost:7001")
    .WithEnvironment("Cors__MobileOrigin", builder.Configuration["Cors:MobileOrigin"] ?? "https://localhost:7002")
    .WithEnvironment("EntraExternalID__Authority", builder.Configuration["EntraExternalID:Authority"] ?? "")
    .WithEnvironment("EntraExternalID__Audience", builder.Configuration["EntraExternalID:Audience"] ?? "")
    .WithEnvironment("Notifications__Sinch__ProjectId", sinchProjectId)
    .WithEnvironment("Notifications__Sinch__ApiKey", sinchApiKey)
    .WithEnvironment("Notifications__Sinch__Sms__ServicePlanId", sinchServicePlanId)
    .WithEnvironment("Notifications__Sinch__Sms__FromNumber", sinchFromNumber);
#endregion


builder.Build().Run();

