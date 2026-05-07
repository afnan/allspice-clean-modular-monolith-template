using System.Security.Cryptography;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.External;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Invitation;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using Ardalis.Result;
using Mediator;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Invitations.Commands.InviteUser;

public sealed class InviteUserCommandHandler : IRequestHandler<InviteUserCommand, Result<Guid>>
{
    private readonly IExternalDirectoryClient _directoryClient;
    private readonly IUserRepository _userRepository;
    private readonly IInvitationRepository _invitationRepository;
    private readonly ILogger<InviteUserCommandHandler> _logger;

    public InviteUserCommandHandler(
        IExternalDirectoryClient directoryClient,
        IUserRepository userRepository,
        IInvitationRepository invitationRepository,
        ILogger<InviteUserCommandHandler> logger)
    {
        _directoryClient = directoryClient;
        _userRepository = userRepository;
        _invitationRepository = invitationRepository;
        _logger = logger;
    }

    public async ValueTask<Result<Guid>> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        // Check for existing pending invitation
        var existing = await _invitationRepository.GetPendingByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
        {
            return Result<Guid>.Conflict("A pending invitation already exists for this email.");
        }

        var tempPassword = GenerateTempPassword();

        // Create user in Keycloak first (external side-effect)
        var keycloakUserId = await _directoryClient.CreateUserWithTempPasswordAsync(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Email,
            tempPassword,
            cancellationToken);

        try
        {
            // Assign role in Keycloak if specified
            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                await _directoryClient.AssignRealmRoleAsync(keycloakUserId, request.Role, cancellationToken);
            }

            // Create local user record
            var user = User.Create(
                ExternalUserId.From(keycloakUserId),
                request.Email,
                request.Email,
                request.FirstName,
                request.LastName);

            await _userRepository.AddAsync(user, cancellationToken);

            // Create invitation (fires InvitationCreatedDomainEvent)
            var invitation = Invitation.Create(
                request.Email,
                request.FirstName,
                request.LastName,
                request.Role ?? "User",
                keycloakUserId,
                user.Id,
                request.InvitedByUserId,
                tempPassword);

            await _invitationRepository.AddAsync(invitation, cancellationToken);

            // Do NOT call SaveChangesAsync here. The command implements ITransactional, so
            // SharedKernel's TransactionBehavior owns the unit-of-work boundary: it begins
            // the DB transaction before the handler runs, drains domain events after, then
            // calls SaveChanges + Commit. An explicit SaveChanges here is redundant and
            // would teach a pattern that's easy to extend incorrectly (post-commit work
            // would escape the transaction).
            return Result<Guid>.Created(invitation.Id);
        }
        catch (Exception ex)
        {
            // Compensate: remove the Keycloak user if local persistence fails
            _logger.LogWarning(ex, "Local persistence failed for invitation {Email}, compensating by deleting Keycloak user {KeycloakUserId}",
                request.Email, keycloakUserId);

            try
            {
                await _directoryClient.DeleteUserAsync(keycloakUserId, CancellationToken.None);
            }
            catch (Exception compensationEx)
            {
                _logger.LogError(compensationEx, "Failed to compensate Keycloak user deletion for {KeycloakUserId}. Manual cleanup required.",
                    keycloakUserId);
            }

            throw;
        }
    }

    private static string GenerateTempPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%";
        return RandomNumberGenerator.GetString(chars, 16);
    }
}
