using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using Mediator;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Preferences.Commands.UpsertNotificationPreference;

public sealed class UpsertNotificationPreferenceCommandHandler(
    INotificationPreferenceRepository preferenceRepository,
    TimeProvider timeProvider)
    : IRequestHandler<UpsertNotificationPreferenceCommand, Result>
{
    private readonly INotificationPreferenceRepository _preferenceRepository = preferenceRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async ValueTask<Result> Handle(UpsertNotificationPreferenceCommand request, CancellationToken cancellationToken)
    {
        var existing = await _preferenceRepository.GetByUserAndChannelAsync(request.UserId, request.Channel, cancellationToken);

        if (existing is null)
        {
            var preference = NotificationPreference.Create(request.UserId, request.Channel, request.IsEnabled, _timeProvider.GetUtcNow());
            await _preferenceRepository.AddAsync(preference, cancellationToken);
        }
        else
        {
            existing.Update(request.IsEnabled, _timeProvider.GetUtcNow());
            await _preferenceRepository.UpdateAsync(existing, cancellationToken);
        }

        return Result.Success();
    }
}


