using Microsoft.Extensions.DependencyInjection;
using Wolverine.Persistence.Durability;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

try
{
  builder.AddGatewayServices();
  builder.RegisterGatewayModules();

  var app = builder.Build();

  app.MapDefaultEndpoints();

  // Ensure module databases
  await app.EnsureNotificationsModuleDatabaseAsync();
  await app.EnsureIdentityModuleDatabaseAsync();

  // Provision each Wolverine message store's schema. The main store (messagingdb) auto-builds, but the
  // enrolled ancillary module stores (identitydb/notificationsdb outbox tables) must be migrated explicitly.
  foreach (var messageStore in app.Services.GetServices<IMessageStore>())
  {
    await messageStore.Admin.MigrateAsync();
  }

  app.UseGatewayPipeline();
  app.MapHub<AppHub>("/hubs/app");
  app.MapGatewayReverseProxy();

  // OpenAPI/Swagger for development
  if (app.Environment.IsDevelopment())
  {
    app.MapOpenApi()
       .RequireAuthorization("allow-anonymous");
  }

  app.Run();
}
catch (Exception ex)
{
  Log.Fatal(ex, "Application terminated unexpectedly");
  throw;
}
finally
{
  Log.CloseAndFlush();
}


