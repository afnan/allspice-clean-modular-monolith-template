using AllSpice.CleanModularMonolith.MainWebsite.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Configure authentication for public client (no client secret)
// Construct authority URL from BaseUrl (endpoint reference) and Realm
// The endpoint reference resolves at runtime when the container is running
var keycloakBaseUrl = builder.Configuration["Keycloak:BaseUrl"] ?? string.Empty;
var keycloakRealm = builder.Configuration["Keycloak:Realm"] ?? "{{ProjectNameLower}}";
var keycloakAuthority = string.IsNullOrWhiteSpace(keycloakBaseUrl) 
    ? string.Empty 
    : $"{keycloakBaseUrl.TrimEnd('/')}/realms/{keycloakRealm}";
var keycloakClientId = builder.Configuration["Keycloak:Portals:MainWebsite:ClientId"] ?? string.Empty;
var callbackPath = builder.Configuration["Keycloak:Portals:MainWebsite:CallbackPath"] ?? "/signin-oidc";
// Use root path for post-logout redirect to avoid Keycloak configuration issues
var signedOutCallbackPath = builder.Configuration["Keycloak:Portals:MainWebsite:SignedOutCallbackPath"] ?? "/";

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
        // Public client - no ClientSecret needed
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.CallbackPath = callbackPath;
        options.SignedOutCallbackPath = signedOutCallbackPath;
        // Set the redirect URI after signout callback - this tells OIDC where to go after processing the callback
        options.SignedOutRedirectUri = "/";
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        
        // Map Keycloak claims to ASP.NET Core identity claims
        // Keycloak typically uses "preferred_username" or "name" for the username
        // Map multiple possible claim names to ensure we get the username
        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
        options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "given_name");
        options.ClaimActions.MapJsonKey(ClaimTypes.Surname, "family_name");
        
        // Try preferred_username first, then name, then email as fallback
        options.ClaimActions.MapUniqueJsonKey(ClaimTypes.Name, "preferred_username");
        options.ClaimActions.MapUniqueJsonKey(ClaimTypes.Name, "name");
        
        // Set the name claim type so User.Identity.Name works
        // This tells ASP.NET Core which claim to use for User.Identity.Name
        options.TokenValidationParameters.NameClaimType = ClaimTypes.Name;
        
        // IMPORTANT: Configure redirect URI in Keycloak client
        // The redirect URI is automatically constructed from the current request URL + CallbackPath
        // You MUST add the exact redirect URI(s) to your Keycloak client's "Valid Redirect URIs"
        // 
        // Steps to fix "Invalid parameter: redirect_uri" error:
        // 1. Check the Aspire dashboard for the MainWebsite URL (or check app logs)
        // 2. The redirect URI format is: {your-app-url}/signin-oidc
        // 3. In Keycloak Admin Console: Clients > {{ProjectNameLower}}-public > Settings
        // 4. Add redirect URI(s) to "Valid redirect URIs", e.g.:
        //    - http://{{ProjectNameLower}}_mainwebsite.dev.localhost:5222/signin-oidc
        //    - https://{{ProjectNameLower}}_mainwebsite.dev.localhost:7285/signin-oidc
        //    - Or use wildcard for dev: http://*:*/signin-oidc (NOT recommended for production!)
        // For development with dynamic ports, you may need to use wildcards in Keycloak
        // or configure specific redirect URIs for each port
        
        options.Events = new Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                // Log detailed information about the redirect
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var redirectUri = context.ProtocolMessage.RedirectUri;
                var authorizationEndpoint = context.ProtocolMessage.IssuerAddress;
                var authority = context.Options.Authority;
                var request = context.HttpContext.Request;
                var currentUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
                
                logger.LogInformation("OIDC Redirect URI: {RedirectUri}", redirectUri);
                logger.LogInformation("OIDC Authorization Endpoint: {AuthorizationEndpoint}", authorizationEndpoint);
                logger.LogInformation("OIDC Authority: {Authority}", authority);
                logger.LogInformation("Current Request URL: {CurrentUrl}", currentUrl);
                logger.LogInformation("Callback Path: {CallbackPath}", context.Options.CallbackPath);
                logger.LogInformation("Full Redirect URI should be: {FullRedirectUri}", $"{currentUrl}{context.Options.CallbackPath}");
                
                // IMPORTANT: Add this redirect URI to Keycloak client's "Valid redirect URIs"
                // Use wildcard for dynamic ports: https://{{ProjectNameLower}}_mainwebsite.dev.localhost:*/signin-oidc
                // OR disable PAR (Pushed Authorization Requests) in Keycloak client settings
                
                // Ensure the redirect happens as a full page navigation, not a fetch request
                // This is important for Blazor apps where navigation might be intercepted
                context.ProtocolMessage.ResponseMode = Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseMode.Query;
                
                return Task.CompletedTask;
            },
            OnRedirectToIdentityProviderForSignOut = context =>
            {
                // Log post-logout redirect information
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var request = context.HttpContext.Request;
                var currentUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
                
                // Use the signout callback path, which will then redirect to home
                var finalPostLogoutUri = $"{currentUrl}{context.Options.SignedOutCallbackPath}";
                context.ProtocolMessage.PostLogoutRedirectUri = finalPostLogoutUri;
                
                logger.LogInformation("OIDC Post-Logout Redirect URI: {PostLogoutRedirectUri}", finalPostLogoutUri);
                logger.LogInformation("Current Request URL: {CurrentUrl}", currentUrl);
                logger.LogInformation("IMPORTANT: Add this URI to Keycloak 'Valid post logout redirect URIs': {Uri}", finalPostLogoutUri);
                
                return Task.CompletedTask;
            },
            OnSignedOutCallbackRedirect = async context =>
            {
                // After Keycloak redirects back to the signout callback, redirect to home
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var request = context.HttpContext.Request;
                logger.LogInformation("OIDC signout callback received at {Path}, redirecting to home", request.Path);
                
                // Ensure we're signed out from cookie authentication
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                
                // Redirect to home
                context.Response.Redirect("/");
                context.HandleResponse();
            },
            OnRemoteFailure = context =>
            {
                // Log authentication failures to help debug redirect URI issues
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var request = context.HttpContext.Request;
                
                logger.LogError(context.Failure, 
                    "OIDC authentication failed. Error: {Error}", 
                    context.Failure?.Message);
                
                logger.LogError("Request URL: {RequestUrl}, Method: {Method}, Query: {Query}", 
                    $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}",
                    request.Method,
                    request.QueryString);
                
                // If this is a logout failure, redirect to home page
                // This handles the case where Keycloak rejects the post-logout redirect URI
                if (request.Path.Value?.Contains("/signout", StringComparison.OrdinalIgnoreCase) == true || 
                    request.QueryString.ToString().Contains("logout", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Logout failure detected. Redirecting to home page.");
                    context.Response.Redirect("/");
                    context.HandleResponse();
                }
                
                return Task.CompletedTask;
            }
        };
        
        // Allow HTTP in development for local Keycloak
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
            UseCookies = false
        };
        
        // Configure metadata refresh settings with longer intervals to reduce connection attempts
        options.RefreshOnIssuerKeyNotFound = true;
        options.AutomaticRefreshInterval = TimeSpan.FromHours(24);
        options.RefreshInterval = TimeSpan.FromMinutes(10); // Longer interval to reduce connection attempts
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
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();

// Authentication endpoints
// Use Challenge to redirect to Keycloak for authentication
app.MapGet("/login", async (HttpContext context) =>
{
    // Always challenge with OpenIdConnect scheme, even if already authenticated
    // This ensures users can re-authenticate or switch accounts
    // The challenge will redirect to Keycloak's authorization endpoint
    await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, new()
    {
        RedirectUri = "/"
    });
})
    .AllowAnonymous(); // Allow unauthenticated access to login endpoint
app.MapMethods("/logout", new[] { "GET", "POST" }, async (HttpContext context) =>
{
    // Sign out from Cookie scheme first
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    
    // Then sign out from OpenIdConnect scheme
    // This will redirect to Keycloak's end session endpoint, then back to our signout callback
    var request = context.Request;
    var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
    var signedOutCallbackPath = configuration["Keycloak:Portals:MainWebsite:SignedOutCallbackPath"] ?? "/signout-callback-oidc";
    var postLogoutRedirectUri = $"{request.Scheme}://{request.Host}{request.PathBase}{signedOutCallbackPath}";
    
    try
    {
        await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new()
        {
            RedirectUri = postLogoutRedirectUri
        });
    }
    catch (Exception ex)
    {
        // If logout fails (e.g., Keycloak rejects redirect URI), just redirect to home
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Logout failed, redirecting to home page");
        context.Response.Redirect("/");
    }
})
    .AllowAnonymous(); // Allow logout even if not authenticated

// Handle signout callback - this route will be called after OIDC middleware processes the callback
// The OIDC middleware handles the callback first, but if it doesn't redirect, this will catch it
app.MapGet("/signout-callback-oidc", async (HttpContext context) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Signout callback route handler called, redirecting to home");
    
    // Ensure we're signed out from cookie authentication
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    
    // Redirect to home
    context.Response.Redirect("/");
})
    .AllowAnonymous();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(AllSpice.CleanModularMonolith.MainWebsite.Client._Imports).Assembly);

app.Run();

