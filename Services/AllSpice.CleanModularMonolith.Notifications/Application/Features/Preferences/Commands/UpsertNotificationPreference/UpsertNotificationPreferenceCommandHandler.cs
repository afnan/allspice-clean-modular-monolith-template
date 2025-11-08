using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using Mediator;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Preferences.Commands.UpsertNotificationPreference;

public sealed class UpsertNotificationPreferenceCommandHandler : IRequestHandler<UpsertNotificationPreferenceCommand, Result>
{
    private readonly INotificationPreferenceRepository _preferenceRepository;

    public UpsertNotificationPreferenceCommandHandler(INotificationPreferenceRepository preferenceRepository)
    {
        _preferenceRepository = preferenceRepository;
    }

    public async ValueTask<Result> Handle(UpsertNotificationPreferenceCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var existing = await _preferenceRepository.GetByUserAndChannelAsync(request.UserId, request.Channel, cancellationToken);

            if (existing is null)
            {
                var preference = NotificationPreference.Create(request.UserId, request.Channel, request.IsEnabled);
                await _preferenceRepository.AddAsync(preference, cancellationToken);
            }
            else
            {
                existing.Update(request.IsEnabled);
                await _preferenceRepository.UpdateAsync(existing, cancellationToken);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Error(ex.Message);
        }
    }
}


