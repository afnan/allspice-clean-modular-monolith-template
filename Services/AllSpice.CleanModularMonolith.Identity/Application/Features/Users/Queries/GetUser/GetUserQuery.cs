using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Users.Queries.GetUser;

public sealed record GetUserQuery(string ExternalId) : IRequest<Result<UserDto>>;
