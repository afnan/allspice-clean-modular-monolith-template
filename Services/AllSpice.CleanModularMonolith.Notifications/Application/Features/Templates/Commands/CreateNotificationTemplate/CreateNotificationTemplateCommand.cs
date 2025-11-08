using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Templates.Commands.CreateNotificationTemplate;

public sealed record CreateNotificationTemplateCommand(
    string Key,
    string SubjectTemplate,
    string BodyTemplate,
    bool IsHtml) : IRequest<Result<Guid>>;


