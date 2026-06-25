using Ardalis.Result;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
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
    public void MapToResult_rethrows_the_original_exception_for_an_unsupported_response_type()
    {
        // For a response type that isn't an Ardalis Result (here PagedResult<T>, which derives from Result<T>
        // but isn't the Result<> generic definition and can't be built in an error state), the mapper has
        // nothing to map onto — it must re-throw the ORIGINAL exception so ErrorHandlingMiddleware renders it
        // with the right status, NOT mask it as a 500.
        var exception = new ValidationException([new ValidationFailure("PageSize", "out of range")]);

        var rethrown = Assert.Throws<ValidationException>(
            () => DomainExceptionResultMapper.MapToResult<PagedResult<int>>(exception));
        Assert.Same(exception, rethrown);
    }

    [Fact]
    public void MapToResult_rethrows_a_domain_exception_for_an_unsupported_response_type()
    {
        var exception = new NotFoundException("User", "missing-id");

        var rethrown = Assert.Throws<NotFoundException>(
            () => DomainExceptionResultMapper.MapToResult<int>(exception));
        Assert.Same(exception, rethrown);
    }
}
