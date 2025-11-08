using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.DTOs;
using Mediator;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Queries.GetUserNotifications;

public sealed record GetUserNotificationsQuery(string UserId) : IRequest<Result<IReadOnlyCollection<NotificationDto>>>;


