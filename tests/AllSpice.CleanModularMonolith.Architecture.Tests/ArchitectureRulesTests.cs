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
    private static readonly Assembly Notifications =
        typeof(AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates.Notification).Assembly;

    private const string IdentityRoot = "AllSpice.CleanModularMonolith.Identity";
    private const string NotificationsRoot = "AllSpice.CleanModularMonolith.Notifications";

    // Infrastructure concerns that must never leak into a Domain layer.
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
    ];

    [Theory]
    [InlineData(IdentityRoot)]
    [InlineData(NotificationsRoot)]
    public void Domain_layer_has_no_infrastructure_dependencies(string moduleRoot)
    {
        var assembly = moduleRoot == IdentityRoot ? Identity : Notifications;

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
    public void Mediator_handlers_live_in_the_Application_layer(string moduleRoot)
    {
        var assembly = moduleRoot == IdentityRoot ? Identity : Notifications;

        var result = Types.InAssembly(assembly)
            .That().ImplementInterface(typeof(IRequestHandler<,>))
            .Should().ResideInNamespaceStartingWith($"{moduleRoot}.Application")
            .GetResult();

        AssertSuccess(result, "Mediator request handlers belong in the Application layer");
    }

    [Theory]
    [InlineData(IdentityRoot)]
    [InlineData(NotificationsRoot)]
    public void Aggregate_roots_live_in_the_Domain_layer(string moduleRoot)
    {
        var assembly = moduleRoot == IdentityRoot ? Identity : Notifications;

        var result = Types.InAssembly(assembly)
            .That().ImplementInterface(typeof(IAggregateRoot))
            .Should().ResideInNamespaceStartingWith($"{moduleRoot}.Domain")
            .GetResult();

        AssertSuccess(result, "Aggregate roots belong in the Domain layer");
    }

    [Fact]
    public void Domain_events_are_sealed()
    {
        foreach (var assembly in new[] { SharedKernel, Identity, Notifications })
        {
            var result = Types.InAssembly(assembly)
                .That().Inherit(typeof(DomainEventBase))
                .Should().BeSealed()
                .GetResult();

            AssertSuccess(result, $"Domain events in {assembly.GetName().Name} must be sealed");
        }
    }

    private static void AssertSuccess(TestResult result, string because)
    {
        Assert.True(
            result.IsSuccessful,
            $"{because}. Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
