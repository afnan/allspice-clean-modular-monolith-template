using Ardalis.Result;
using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Invitations.Commands.InviteUser;

public sealed record InviteUserCommand(
    string Email,
    string FirstName,
    string LastName,
    string? Role,
    string InvitedByUserId) : IRequest<Result<Guid>>, ITransactional;
