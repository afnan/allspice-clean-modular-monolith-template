using AllSpice.CleanModularMonolith.ApiContracts.Common;
using AllSpice.CleanModularMonolith.ApiContracts.Identity.Responses;
using AllSpice.CleanModularMonolith.Identity.Application.Features.Users.Queries.ListUsers;
using AllSpice.CleanModularMonolith.Identity.Application.Mappers;
using AllSpice.CleanModularMonolith.Web;
using Ardalis.Result;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.Users;

public sealed class ListUsersEndpoint : EndpointWithoutRequest<Results<Ok<PagedResponse<UserResponse>>, ProblemHttpResult>>
{
    private readonly IMediator _mediator;

    public ListUsersEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/identity/users");
        Roles("Identity.Admin");
        Tags("Identity");
        Summary(summary =>
        {
            summary.Summary = "Lists active users with optional pagination.";
        });
    }

    public override async Task<Results<Ok<PagedResponse<UserResponse>>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var page = Query<int?>("page") ?? 1;
        var pageSize = Query<int?>("pageSize") ?? 20;

        var result = await _mediator.Send(new ListUsersQuery(page, pageSize), ct);

        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok(new PagedResponse<UserResponse>(
                result.Value.Select(UserMapper.ToResponse).ToList(),
                (int)result.PagedInfo.PageNumber,
                (int)result.PagedInfo.PageSize,
                (int)result.PagedInfo.TotalRecords,
                (int)result.PagedInfo.TotalPages)),
            _ => result.ToProblem()
        };
    }
}
