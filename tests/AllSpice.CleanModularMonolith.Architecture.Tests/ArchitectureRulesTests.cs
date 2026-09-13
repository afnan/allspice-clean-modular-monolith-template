using System.Reflection;
using AllSpice.CleanModularMonolith.SharedKernel.Common;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using Mediator;
using NetArchTest.Rules;

namespace AllSpice.CleanModularMonolith.Architecture.Tests;

/// <summary>
/// Fitness functions that turn the golden rules in <c>AGENTS.md</c> into deterministic, fast-failing tests.
/// These are the guardrails an AI agent (or a human) hits at build/test time instead of only in code review:
/// domain purity, module isolation, and layer/naming conventions. When a rule legitimately changes, update
/// the rule here in the same change — the test IS the architecture contract.
/// </summary>
public class ArchitectureRulesTests
{
    private static readonly Assembly SharedKernel = typeof(Entity).Assembly;
    private static readonly Assembly Identity =
        typeof(AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User.User).Assembly;
    private static readonly Assembly IdentityAbstractions =
        typeof(AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization.ICurrentUserPermissions).Assembly;
    private static readonly Assembly Notifications =
        typeof(AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates.Notification).Assembly;
    private static readonly Assembly Ledger =
        typeof(AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates.Account).Assembly;
    private static readonly Assembly EventSourcing =
        typeof(AllSpice.CleanModularMonolith.EventSourcing.ModuleEventStoreExtensions).Assembly;

    private const string IdentityRoot = "AllSpice.CleanModularMonolith.Identity";
    private const string NotificationsRoot = "AllSpice.CleanModularMonolith.Notifications";
    private const string LedgerRoot = "AllSpice.CleanModularMonolith.Ledger";

    // Infrastructure concerns that must never leak into a Domain layer.

    private static Assembly ModuleAssembly(string moduleRoot) => moduleRoot switch
    {
        IdentityRoot => Identity,
        NotificationsRoot => Notifications,
        LedgerRoot => Ledger,
        _ => throw new ArgumentOutOfRangeException(nameof(moduleRoot), moduleRoot, null),
    };

    private static readonly string[] InfrastructureDependencies =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Npgsql",
        "FastEndpoints",
        "Wolverine",
        "Quartz",
        "MailKit",
        "Azure.Storage",
        "Marten",
        "JasperFx",
    ];

    [Theory]
    [InlineData(IdentityRoot)]
    [InlineData(NotificationsRoot)]
    [InlineData(LedgerRoot)]
    public void Domain_layer_has_no_infrastructure_dependencies(string moduleRoot)
    {
        var assembly = ModuleAssembly(moduleRoot);

        var result = Types.InAssembly(assembly)
            .That().ResideInNamespaceStartingWith($"{moduleRoot}.Domain")
            .ShouldNot().HaveDependencyOnAny(InfrastructureDependencies)
            .GetResult();

        AssertSuccess(result, $"{moduleRoot}.Domain must not depend on infrastructure");
    }

    [Fact]
    public void Identity_module_does_not_depend_on_Notifications_internals()
    {
        var result = Types.InAssembly(Identity)
            .ShouldNot()
            .HaveDependencyOnAny(
                $"{NotificationsRoot}.Application",
                $"{NotificationsRoot}.Infrastructure",
                $"{NotificationsRoot}.Domain")
            .GetResult();

        // Cross-module contact is allowed only through the *.Contracts integration-event library.
        AssertSuccess(result, "Identity must reach Notifications only via Notifications.Contracts");
    }

    [Fact]
    public void Notifications_module_does_not_depend_on_Identity_internals()
    {
        var result = Types.InAssembly(Notifications)
            .ShouldNot()
            .HaveDependencyOnAny(
                $"{IdentityRoot}.Application",
                $"{IdentityRoot}.Infrastructure",
                $"{IdentityRoot}.Domain")
            .GetResult();

        AssertSuccess(result, "Notifications must reach Identity only via shared abstractions/contracts");
    }

    [Theory]
    [InlineData(IdentityRoot)]
    [InlineData(NotificationsRoot)]
    [InlineData(LedgerRoot)]
    public void Mediator_handlers_live_in_the_Application_layer(string moduleRoot)
    {
        var assembly = ModuleAssembly(moduleRoot);

        var result = Types.InAssembly(assembly)
            .That().ImplementInterface(typeof(IRequestHandler<,>))
            .Should().ResideInNamespaceStartingWith($"{moduleRoot}.Application")
            .GetResult();

        AssertSuccess(result, "Mediator request handlers belong in the Application layer");
    }

    [Theory]
    [InlineData(IdentityRoot)]
    [InlineData(NotificationsRoot)]
    [InlineData(LedgerRoot)]
    public void Aggregate_roots_live_in_the_Domain_layer(string moduleRoot)
    {
        var assembly = ModuleAssembly(moduleRoot);

        var result = Types.InAssembly(assembly)
            .That().ImplementInterface(typeof(IAggregateRoot))
            .Should().ResideInNamespaceStartingWith($"{moduleRoot}.Domain")
            .GetResult();

        AssertSuccess(result, "Aggregate roots belong in the Domain layer");
    }

    [Fact]
    public void Domain_events_are_sealed()
    {
        foreach (var assembly in new[] { SharedKernel, Identity, Notifications, Ledger })
        {
            // Persisted stream events are a permanent contract; sealing prevents accidental subclass drift.
            var result = Types.InAssembly(assembly)
                .That().ImplementInterface(typeof(IDomainEvent))
                .And().AreNotAbstract()
                .Should().BeSealed()
                .GetResult();

            AssertSuccess(result, $"Domain events in {assembly.GetName().Name} must be sealed");
        }
    }

    [Theory]
    [InlineData(IdentityRoot)]
    [InlineData(NotificationsRoot)]
    [InlineData(LedgerRoot)]
    public void Marten_is_referenced_only_from_the_Infrastructure_layer(string moduleRoot)
    {
        // Event sourcing is an INFRASTRUCTURE choice (ADR-0009). Domain aggregates use SharedKernel's
        // EventSourcedAggregate; Application uses IEventSourcedRepository. Only Infrastructure may see Marten.
        var result = Types.InAssembly(ModuleAssembly(moduleRoot))
            .That().DoNotResideInNamespaceStartingWith($"{moduleRoot}.Infrastructure")
            .ShouldNot().HaveDependencyOnAny("Marten", "JasperFx", "Weasel")
            .GetResult();

        AssertSuccess(result, $"Only {moduleRoot}.Infrastructure may depend on Marten");
    }

    [Fact]
    public void SharedKernel_has_no_dependency_on_Marten()
    {
        var result = Types.InAssembly(SharedKernel)
            .ShouldNot().HaveDependencyOnAny("Marten", "JasperFx", "Weasel")
            .GetResult();

        AssertSuccess(result, "SharedKernel must stay store-agnostic; Marten lives in the EventSourcing project");
    }

    [Fact]
    public void Ledger_module_does_not_depend_on_other_module_internals()
    {
        var result = Types.InAssembly(Ledger)
            .ShouldNot()
            .HaveDependencyOnAny(
                $"{IdentityRoot}.Application", $"{IdentityRoot}.Infrastructure", $"{IdentityRoot}.Domain",
                $"{NotificationsRoot}.Application", $"{NotificationsRoot}.Infrastructure", $"{NotificationsRoot}.Domain")
            .GetResult();

        AssertSuccess(result, "Ledger may reach other modules only via *.Contracts / Identity.Abstractions");
    }

    [Fact]
    public void Other_modules_do_not_depend_on_Ledger()
    {
        foreach (var assembly in new[] { Identity, Notifications })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot().HaveDependencyOnAny(LedgerRoot)
                .GetResult();

            AssertSuccess(result, $"{assembly.GetName().Name} must not depend on the Ledger reference module");
        }
    }

    [Theory]
    [InlineData(LedgerRoot)]
    public void Event_sourced_aggregates_live_in_the_Domain_layer(string moduleRoot)
    {
        var result = Types.InAssembly(ModuleAssembly(moduleRoot))
            .That().Inherit(typeof(AllSpice.CleanModularMonolith.SharedKernel.EventSourcing.EventSourcedAggregate))
            .Should().ResideInNamespaceStartingWith($"{moduleRoot}.Domain")
            .GetResult();

        AssertSuccess(result, "Event-sourced aggregates belong in the Domain layer");
    }

    [Fact]
    public void Identity_Abstractions_has_no_dependency_on_Identity_Infrastructure()
    {
        // Identity.Abstractions is the contract library consumed by every module and the gateway.
        // It must never import implementation details from the Identity module's Infrastructure layer,
        // keeping the abstraction boundary clean (see ADR-0008).
        var result = Types.InAssembly(IdentityAbstractions)
            .ShouldNot()
            .HaveDependencyOnAny($"{IdentityRoot}.Infrastructure")
            .GetResult();

        AssertSuccess(result, "Identity.Abstractions must not depend on Identity.Infrastructure");
    }

    [Fact]
    public void Identity_Domain_has_no_dependency_on_Identity_Abstractions()
    {
        var result = Types.InAssembly(Identity)
            .That().ResideInNamespaceStartingWith($"{IdentityRoot}.Domain")
            .ShouldNot().HaveDependencyOnAny($"{IdentityRoot}.Abstractions")
            .GetResult();
        AssertSuccess(result, "Identity.Domain must not depend on Identity.Abstractions");
    }

    private static void AssertSuccess(TestResult result, string because)
    {
        Assert.True(
            result.IsSuccessful,
            $"{because}. Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
