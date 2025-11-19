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
var keycloakAuthority = builder.Configuration["Keycloak:Portals:Erp:Authority"] ?? string.Empty;
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
