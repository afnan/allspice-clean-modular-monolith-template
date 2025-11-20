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


