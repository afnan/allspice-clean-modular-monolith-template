using AllSpice.CleanModularMonolith.Identity.Application.Contracts.External;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Invitation;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Invitations.Commands.InviteUser;

public sealed class InviteUserCommandHandler : IRequestHandler<InviteUserCommand, Result<Guid>>
{
    private readonly IExternalDirectoryClient _directoryClient;
    private readonly IUserRepository _userRepository;
    private readonly IInvitationRepository _invitationRepository;

    public InviteUserCommandHandler(
        IExternalDirectoryClient directoryClient,
        IUserRepository userRepository,
        IInvitationRepository invitationRepository)
    {
        _directoryClient = directoryClient;
        _userRepository = userRepository;
        _invitationRepository = invitationRepository;
    }

    public async ValueTask<Result<Guid>> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        // Check for existing pending invitation
        var existing = await _invitationRepository.GetPendingByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
        {
            return Result<Guid>.Conflict("A pending invitation already exists for this email.");
        }

        // Generate temporary password
        var tempPassword = GenerateTempPassword();

        // Create user in Keycloak with temp password
        var keycloakUserId = await _directoryClient.CreateUserWithTempPasswordAsync(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Email,
            tempPassword,
            cancellationToken);

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

        // Single commit — both User and Invitation share the same DbContext scope
        await _invitationRepository.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Created(invitation.Id);
    }

    private static string GenerateTempPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%";
        var random = Random.Shared;
        return new string(Enumerable.Range(0, 16).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}
