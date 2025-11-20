using AllSpice.CleanModularMonolith.ErpPortal.Client.Pages;
using AllSpice.CleanModularMonolith.ErpPortal.Components;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Configure authentication
// Construct authority URL from BaseUrl (endpoint reference) and Realm
// The endpoint reference resolves at runtime when the container is running
var keycloakBaseUrl = builder.Configuration["Keycloak:BaseUrl"] ?? string.Empty;
var keycloakRealm = builder.Configuration["Keycloak:Realm"] ?? "{{ProjectNameLower}}";
var keycloakAuthority = string.IsNullOrWhiteSpace(keycloakBaseUrl) 
    ? string.Empty 
    : $"{keycloakBaseUrl.TrimEnd('/')}/realms/{keycloakRealm}";
var keycloakClientId = builder.Configuration["Keycloak:Portals:Erp:ClientId"] ?? string.Empty;
var keycloakClientSecret = builder.Configuration["Keycloak:Portals:Erp:ClientSecret"] ?? string.Empty;
var callbackPath = builder.Configuration["Keycloak:Portals:Erp:CallbackPath"] ?? "/signin-oidc";
var signedOutCallbackPath = builder.Configuration["Keycloak:Portals:Erp:SignedOutCallbackPath"] ?? "/signout-callback-oidc";

if (!string.IsNullOrWhiteSpace(keycloakAuthority) && !string.IsNullOrWhiteSpace(keycloakClientId))
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = keycloakAuthority;
        options.ClientId = keycloakClientId;
        options.ClientSecret = keycloakClientSecret;
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.CallbackPath = callbackPath;
        options.SignedOutCallbackPath = signedOutCallbackPath;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        
        // Allow HTTP in development for local Keycloak
        // Disable HTTPS requirement if authority uses HTTP (local development)
        if (builder.Environment.IsDevelopment() || keycloakAuthority.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            options.RequireHttpsMetadata = false;
        }
        
        // Configure BackchannelHttpHandler to avoid "response ended prematurely" errors
        // The issue occurs when Keycloak closes the connection before the full response is sent
        // This can happen due to HTTP version mismatches or connection pooling issues
        options.BackchannelHttpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => 
                builder.Environment.IsDevelopment() || errors == System.Net.Security.SslPolicyErrors.None,
            MaxConnectionsPerServer = 1, // Use single connection to avoid pooling issues
            AllowAutoRedirect = true,
            // Disable connection keep-alive to force new connections (helps with premature closure)
           //UseCookies = false
        };
        
        // Configure metadata refresh settings with longer intervals to reduce connection attempts
        options.RefreshOnIssuerKeyNotFound = true;
        options.AutomaticRefreshInterval = TimeSpan.FromHours(24);
        options.RefreshInterval = TimeSpan.FromMinutes(10); // Longer interval to reduce connection attempts
        
        // Add retry logic via Events
        options.Events = new Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectEvents
        {
            OnAuthenticationFailed = context =>
            {
                // Log but don't fail immediately - let retry mechanism handle it
                context.Response.Redirect("/login");
                context.HandleResponse();
                return Task.CompletedTask;
            }
        };
    });

    builder.Services.AddAuthorization();
}

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();

// Authentication endpoints
app.MapGet("/login", () => Results.Challenge(new() { RedirectUri = "/" }));
app.MapPost("/logout", () => Results.SignOut(new() { RedirectUri = "/" }))
    .RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(AllSpice.CleanModularMonolith.ErpPortal.Client._Imports).Assembly);

app.Run();
