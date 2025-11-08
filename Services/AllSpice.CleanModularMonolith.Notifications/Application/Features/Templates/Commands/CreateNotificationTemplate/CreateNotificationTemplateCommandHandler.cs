using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using Mediator;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Templates.Commands.CreateNotificationTemplate;

public sealed class CreateNotificationTemplateCommandHandler : IRequestHandler<CreateNotificationTemplateCommand, Result<Guid>>
{
    private readonly INotificationTemplateRepository _templateRepository;

    public CreateNotificationTemplateCommandHandler(INotificationTemplateRepository templateRepository)
    {
        _templateRepository = templateRepository;
    }

    public async ValueTask<Result<Guid>> Handle(CreateNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var existing = await _templateRepository.GetByKeyAsync(request.Key, cancellationToken);
            if (existing is not null)
            {
                return Result.Error("Template key already exists.");
            }

            var template = NotificationTemplate.Create(request.Key, request.SubjectTemplate, request.BodyTemplate, request.IsHtml);
            await _templateRepository.AddAsync(template, cancellationToken);
            return Result.Success(template.Id);
        }
        catch (Exception ex)
        {
            return Result.Error(ex.Message);
        }
    }
}


