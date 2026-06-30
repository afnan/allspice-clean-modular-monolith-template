using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Commands.CreatePermission;

public sealed record CreatePermissionCommand(string Key, string Description)
    : IRequest<Result>, ITransactional;
