using System;
using System.IO.Compression;
using System.Linq;
using System.Threading.RateLimiting;
using FastEndpoints;
using AllSpice.CleanModularMonolith.ApiGateway.RealTime;
using AllSpice.CleanModularMonolith.RealTime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery.Yarp;
using Microsoft.IdentityModel.Tokens;

namespace AllSpice.CleanModularMonolith.ApiGateway.Extensions;

/// <summary>
/// Provides extension methods that encapsulate all service registrations required by the gateway.
/// </summary>
public static class GatewayServiceCollectionExtensions
{
    /// <summary>
    /// Adds the gateway's infrastructure services (compression, caching, rate limiting, CORS, auth, YARP, etc.).
    /// </summary>
    /// <param name="builder">The web application builder that is being configured.</param>
    public static void AddGatewayServices(this WebApplicationBuilder builder)
    {
        builder.AddServiceDefaults();

        builder.Services.AddOpenApi();
        builder.Services.AddFastEndpoints();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<IRealtimePublisher, RealtimePublisher>();

        builder.Services.ConfigureResponseCompressionDefaults();
        builder.ConfigureOutputCaching();
        builder.Services.ConfigureRateLimiting();
        builder.ConfigureCorsPolicies();
        builder.ConfigureAuthentication();
        builder.ConfigureAuthorization();

        builder.Services
            .AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
            .AddServiceDiscoveryDestinationResolver();
    }

    /// <summary>
    /// Configures output caching, wiring up Redis if Aspire provides a connection string.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    private static void ConfigureOutputCaching(this WebApplicationBuilder builder)
    {
        var redisConnectionString = builder.Configuration.GetConnectionString("redis");

        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            var redisConfig = redisConnectionString.StartsWith("redis://", StringComparison.OrdinalIgnoreCase)
                ? redisConnectionString.Substring(8)
                : redisConnectionString;

            builder.Services.AddStackExchangeRedisOutputCache(options =>
            {
                options.Configuration = redisConfig;
                options.InstanceName = "AllSpice.CleanModularMonolith_Gateway";
            });
        }

        builder.Services.AddOutputCache(options =>
        {
            options.AddBasePolicy(policy => policy.Expire(TimeSpan.FromMinutes(5)));
            options.AddPolicy("Cache5Min", policy => policy.Expire(TimeSpan.FromMinutes(5)));
            options.AddPolicy("Cache1Hour", policy => policy.Expire(TimeSpan.FromHours(1)));
        });
    }

    /// <summary>
    /// Enables Brotli and Gzip compression with tuned defaults for JSON payloads.
    /// </summary>
    /// <param name="services">The service collection into which the compression components are registered.</param>
    private static IServiceCollection ConfigureResponseCompressionDefaults(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                new[] { "application/json", "application/problem+json" });
        });

        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });

        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });

        return services;
    }

    /// <summary>
    /// Sets up global and named rate limiting policies along with rejection messaging.
    /// </summary>
    /// <param name="services">The service collection.</param>
    private static IServiceCollection ConfigureRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var identifier = context.User.Identity?.IsAuthenticated == true
                    ? context.User.Identity!.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous"
                    : context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: identifier,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 10
                    });
            });

            options.AddFixedWindowLimiter("api", limiterOptions =>
            {
                limiterOptions.PermitLimit = 200;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 10;
            });

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers["Retry-After"] = "60";

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    await context.HttpContext.Response.WriteAsync(
                        $"Rate limit exceeded. Please try again after {retryAfter.TotalSeconds} seconds.",
                        cancellationToken: token);
                }
                else
                {
                    await context.HttpContext.Response.WriteAsync(
                        "Rate limit exceeded. Please try again later.",
                        cancellationToken: token);
                }
            };
        });

        return services;
    }

    /// <summary>
    /// Configures CORS policies for web and mobile clients, honoring Aspire-provided overrides.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    private static void ConfigureCorsPolicies(this WebApplicationBuilder builder)
    {
        var webOrigin = builder.Configuration["Cors:WebOrigin"]
            ?? Environment.GetEnvironmentVariable("CORS__WEBORIGIN")
            ?? Environment.GetEnvironmentVariable("Cors__WebOrigin")
            ?? "https://localhost:7001";

        var mobileOrigin = builder.Configuration["Cors:MobileOrigin"]
            ?? Environment.GetEnvironmentVariable("CORS__MOBILEORIGIN")
            ?? Environment.GetEnvironmentVariable("Cors__MobileOrigin")
            ?? "https://localhost:7002";

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("WebClient", policy =>
            {
                policy.WithOrigins(webOrigin)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });

            options.AddPolicy("MobileClient", policy =>
            {
                policy.WithOrigins(mobileOrigin)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });
    }

    /// <summary>
    /// Configures JWT bearer authentication using Entra settings supplied via configuration or Aspire.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    private static void ConfigureAuthentication(this WebApplicationBuilder builder)
    {
        var entraAuthority = builder.Configuration["EntraExternalID:Authority"]
            ?? Environment.GetEnvironmentVariable("ENTRAEXTERNALID__AUTHORITY")
            ?? string.Empty;

        var entraAudience = builder.Configuration["EntraExternalID:Audience"]
            ?? Environment.GetEnvironmentVariable("ENTRAEXTERNALID__AUDIENCE")
            ?? string.Empty;

        if (!string.IsNullOrEmpty(entraAuthority) && !string.IsNullOrEmpty(entraAudience))
        {
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = entraAuthority;
                    options.Audience = entraAudience;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true
                    };
                });
        }
    }

    /// <summary>
    /// Establishes authorization policies, including the authenticated fallback and an allow-anonymous policy.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    private static void ConfigureAuthorization(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy("authenticated", policy =>
            {
                policy.RequireAuthenticatedUser();
            });

            options.AddPolicy("allow-anonymous", policy =>
            {
                policy.RequireAssertion(_ => true);
            });
        });
    }
}


