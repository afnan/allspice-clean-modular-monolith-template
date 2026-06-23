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
}
