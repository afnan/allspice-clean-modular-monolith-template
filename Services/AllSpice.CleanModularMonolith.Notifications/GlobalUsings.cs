// Project-wide globals for the Notifications module.
//
// Convention: Domain and Application files MUST NOT reference any
// AllSpice.CleanModularMonolith.Notifications.Infrastructure.* type. The Infrastructure
// namespaces are listed below for the convenience of Infrastructure and Api files,
// but Clean Architecture rules apply at the layer level — code review and the
// directory structure (Domain/, Application/, Infrastructure/, Api/) enforce the
// boundary, not the compiler. If you find an Infrastructure type leaking into
// Domain/Application, that's a bug in the dependent file, not in this list.

global using Ardalis.GuardClauses;
global using FluentValidation;
global using Mediator;
global using Microsoft.AspNetCore.Builder;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Quartz;
global using Wolverine;

// Application layer contracts (safe for all layers to reference)
global using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
global using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
global using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services.Channels;
global using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;

// Infrastructure-only — used by Infrastructure and Api layers, not Domain/Application.
// See convention comment at the top of this file.
global using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Jobs;
global using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Messaging.Consumers;
global using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;
global using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
global using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Repositories;
global using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;
global using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Channels;
global using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Email;

// Type aliases
global using AppAssemblyReference = AllSpice.CleanModularMonolith.Notifications.Application.AssemblyReference;
