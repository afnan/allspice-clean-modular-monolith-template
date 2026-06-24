using Ardalis.Result;
using AllSpice.CleanModularMonolith.SharedKernel.Results;
using FluentValidation;
using FluentValidation.Results;

namespace AllSpice.CleanModularMonolith.SharedKernel.UnitTests.Results;

public class DomainExceptionResultMapperTests
{
    [Fact]
    public void MapToResult_maps_ValidationException_to_Invalid_with_field_errors()
    {
        var exception = new ValidationException(
        [
            new ValidationFailure("Email", "Email is required"),
            new ValidationFailure("Name", "Name is required"),
        ]);

        var result = DomainExceptionResultMapper.MapToResult<Result>(exception);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains(result.ValidationErrors, e => e.Identifier == "Email" && e.ErrorMessage == "Email is required");
        Assert.Contains(result.ValidationErrors, e => e.Identifier == "Name");
    }

    [Fact]
    public void MapToResult_maps_ValidationException_to_Invalid_for_typed_result()
    {
        var exception = new ValidationException([new ValidationFailure("Id", "bad")]);

        var result = DomainExceptionResultMapper.MapToResult<Result<Guid>>(exception);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains(result.ValidationErrors, e => e.Identifier == "Id");
    }

    [Fact]
    public void MapToResult_rejects_PagedResult_as_an_unsupported_response_type()
    {
        // Ardalis PagedResult<T> derives from Result<T> but is NOT the Result<> generic definition,
        // and the library exposes no way to build one in an error state. So it must never be used as a
        // mediator response type — doing so turns validation/domain failures into 500s. Paged queries
        // wrap their page in a plain Result<T> instead (e.g. ListUsersQuery -> Result<PagedList<UserDto>>).
        var exception = new ValidationException([new ValidationFailure("PageSize", "out of range")]);

        Assert.Throws<InvalidOperationException>(
            () => DomainExceptionResultMapper.MapToResult<PagedResult<int>>(exception));
    }
}
