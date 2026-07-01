using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Commands.DeletePermission;

public sealed record DeletePermissionCommand(Guid Id)
    : IRequest<Result>, ITransactional;
