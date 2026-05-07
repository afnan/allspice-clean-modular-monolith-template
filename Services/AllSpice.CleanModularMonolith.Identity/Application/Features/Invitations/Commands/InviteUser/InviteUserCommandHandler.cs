using System.Security.Cryptography;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.External;
using AllSpice.CleanModularMonolith.Identity.Application.Specifications.Invitations;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Invitation;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Ardalis.Result;
using Mediator;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Invitations.Commands.InviteUser;

public sealed class InviteUserCommandHandler : IRequestHandler<InviteUserCommand, Result<Guid>>
{
    private readonly IExternalDirectoryClient _directoryClient;
    private readonly IRepository<User> _users;
    private readonly IRepository<Invitation> _invitations;
    private readonly ILogger<InviteUserCommandHandler> _logger;

    public InviteUserCommandHandler(
        IExternalDirectoryClient directoryClient,
        IRepository<User> users,
        IRepository<Invitation> invitations,
        ILogger<InviteUserCommandHandler> logger)
    {
        _directoryClient = directoryClient;
        _users = users;
        _invitations = invitations;
        _logger = logger;
    }

    public async ValueTask<Result<Guid>> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        var existing = await _invitations.FirstOrDefaultAsync(
            new PendingInvitationByEmailSpec(request.Email),
            cancellationToken);

        if (existing is not null)
        {
            return Result<Guid>.Conflict("A pending invitation already exists for this email.");
        }

        var tempPassword = GenerateTempPassword();

        var keycloakUserId = await _directoryClient.CreateUserWithTempPasswordAsync(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Email,
            tempPassword,
            cancellationToken);

        try
        {
            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                await _directoryClient.AssignRealmRoleAsync(keycloakUserId, request.Role, cancellationToken);
            }

            var user = User.Create(
                ExternalUserId.From(keycloakUserId),
                request.Email,
                request.Email,
                request.FirstName,
                request.LastName);

            await _users.AddAsync(user, cancellationToken);

            var invitation = Invitation.Create(
                request.Email,
                request.FirstName,
                request.LastName,
                request.Role ?? "User",
                keycloakUserId,
                user.Id,
                request.InvitedByUserId,
                tempPassword);

            await _invitations.AddAsync(invitation, cancellationToken);
            await _invitations.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Created(invitation.Id);
        }
        catch (Exception ex)
        {
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
