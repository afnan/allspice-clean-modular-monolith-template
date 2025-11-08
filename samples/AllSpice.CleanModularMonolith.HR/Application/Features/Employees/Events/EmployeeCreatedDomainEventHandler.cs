using Ardalis.GuardClauses;
using AllSpice.CleanModularMonolith.HR.Domain.Events;
using AllSpice.CleanModularMonolith.Notifications.Contracts.Messaging;
using MassTransit;
using Mediator;

namespace AllSpice.CleanModularMonolith.HR.Application.Features.Employees.Events;

public sealed class EmployeeCreatedDomainEventHandler : INotificationHandler<EmployeeCreatedDomainEvent>
{
    private readonly IPublishEndpoint _publishEndpoint;

    public EmployeeCreatedDomainEventHandler(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async ValueTask Handle(EmployeeCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        Guard.Against.Null(notification);
        Guard.Against.NullOrWhiteSpace(notification.Email, nameof(notification.Email));

        var metadata = new Dictionary<string, string>
        {
            ["EmployeeId"] = notification.EmployeeId.ToString(),
            ["FirstName"] = notification.FirstName,
            ["LastName"] = notification.LastName
        };

        var integrationEvent = new NotificationRequestedIntegrationEvent(
            Guid.NewGuid(),
            "HR",
            notification.EmployeeId.ToString(),
            notification.Email,
            null,
            NotificationChannel.Email,
            $"Welcome to AllSpice.CleanModularMonolith, {notification.FirstName}!",
            "Hi {{FirstName}}, welcome aboard!",
            "hr.welcome",
            null,
            null,
            metadata);

        await _publishEndpoint.Publish(integrationEvent, cancellationToken);
    }
}


