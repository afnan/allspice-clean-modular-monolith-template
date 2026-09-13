// Project-wide globals for the Ledger module — the template's REFERENCE event-sourced module (ADR-0009).
//
// Convention: Domain and Application files MUST NOT reference any
// AllSpice.CleanModularMonolith.Ledger.Infrastructure.* type, and only Infrastructure may `using Marten`
// (enforced by Architecture.Tests). The Infrastructure namespaces are listed below for the convenience of
// Infrastructure and Api files.

global using Ardalis.GuardClauses;
global using FluentValidation;
global using Mediator;
global using Microsoft.AspNetCore.Builder;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;

// Application layer contracts (safe for all layers to reference)
// AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence is added in Task 8 — until the
// Application layer exists, that namespace has zero types anywhere and the global using fails with CS0234
// (unlike a namespace with at least one type elsewhere, a namespace that doesn't exist at all can't be
// resolved). Same deferral as the Infrastructure lines below.
global using AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;

// Type aliases
global using AppAssemblyReference = AllSpice.CleanModularMonolith.Ledger.Application.AssemblyReference;
