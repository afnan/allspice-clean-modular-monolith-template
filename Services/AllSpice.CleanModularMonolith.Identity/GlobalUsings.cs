// Project-wide globals for the Identity module.
//
// Convention: Domain and Application files MUST NOT reference any
// AllSpice.CleanModularMonolith.Identity.Infrastructure.* type. The Infrastructure
// namespaces are listed below for the convenience of Infrastructure and Api files,
// but Clean Architecture rules apply at the layer level — code review and the
// directory structure (Domain/, Application/, Infrastructure/, Api/) enforce the
// boundary, not the compiler. If you find an Infrastructure type leaking into
// Domain/Application, that's a bug in the dependent file, not in this list.

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
global using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;

// Infrastructure-only — used by Infrastructure and Api layers, not Domain/Application.
// See convention comment at the top of this file.
global using Aspire.Npgsql.EntityFrameworkCore.PostgreSQL;
global using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
global using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
global using AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;
global using AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

// Type aliases
global using AppAssemblyReference = AllSpice.CleanModularMonolith.Identity.Application.AssemblyReference;
