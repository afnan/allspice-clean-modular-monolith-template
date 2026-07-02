using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using Mediator;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Templates.Commands.CreateNotificationTemplate;

public sealed class CreateNotificationTemplateCommandHandler(
    INotificationTemplateRepository templateRepository,
    TimeProvider timeProvider)
    : IRequestHandler<CreateNotificationTemplateCommand, Result<Guid>>
{
    private readonly INotificationTemplateRepository _templateRepository = templateRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async ValueTask<Result<Guid>> Handle(CreateNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        // No blanket try/catch: it leaked internal exception text via Result.Error(ex.Message) and mislabelled
        // transient infrastructure faults as permanent. Invalid input is thrown by ValidationBehavior and domain
        // guard violations are mapped by DomainExceptionBehavior; genuine infrastructure faults must propagate so
        // the pipeline can classify them. Only the genuine domain rule (unique key) is an explicit Result here.
        var existing = await _templateRepository.GetByKeyAsync(request.Key, cancellationToken);
        if (existing is not null)
        {
            return Result.Conflict("Template key already exists.");
        }

        var template = NotificationTemplate.Create(request.Key, request.SubjectTemplate, request.BodyTemplate, request.IsHtml, _timeProvider.GetUtcNow());
        await _templateRepository.AddAsync(template, cancellationToken);
        return Result.Success(template.Id);
    }
}


