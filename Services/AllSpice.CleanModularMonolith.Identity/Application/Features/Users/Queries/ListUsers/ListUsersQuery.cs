using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Users.Queries.ListUsers;

public sealed record ListUsersQuery(int Page = 1, int PageSize = 20) : IRequest<Result<PaginationResult<UserDto>>>;
