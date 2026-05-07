// Shared across all layers (Domain, Application, Infrastructure, Api)
global using System.Net.Http.Headers;
global using Ardalis.GuardClauses;
global using FluentValidation;
global using Mediator;
global using Microsoft.AspNetCore.Builder;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;

// Application layer contracts (safe for all layers to reference)
global using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
global using AllSpice.CleanModularMonolith.Identity.Application.Contracts.External;

// Infrastructure-only — used by Infrastructure and Api layers, not Domain
// NOTE: These are global for convenience since Domain/Application types don't conflict,
// but Domain layer files should not reference these namespaces directly.
global using Aspire.Npgsql.EntityFrameworkCore.PostgreSQL;
global using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
global using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
global using AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

// Type aliases
global using AppAssemblyReference = AllSpice.CleanModularMonolith.Identity.Application.AssemblyReference;

