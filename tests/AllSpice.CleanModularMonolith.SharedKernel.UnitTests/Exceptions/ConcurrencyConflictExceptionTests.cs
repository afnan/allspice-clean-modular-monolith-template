using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using AllSpice.CleanModularMonolith.SharedKernel.Results;
using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.SharedKernel.UnitTests.Exceptions;

public class ConcurrencyConflictExceptionTests
{
    [Fact]
    public void Code_is_concurrency_conflict()
    {
        var ex = new ConcurrencyConflictException("Account 1 was modified by another request.");
        Assert.Equal("concurrency_conflict", ex.Code);
    }

    [Fact]
    public void Maps_to_a_Conflict_result()
    {
        var result = DomainExceptionResultMapper.MapToResult<Result>(new ConcurrencyConflictException("stale"));
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("stale", result.Errors);
    }

    [Fact]
    public void Maps_to_a_typed_Conflict_result()
    {
        var result = DomainExceptionResultMapper.MapToResult<Result<Guid>>(new ConcurrencyConflictException("stale"));
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }
}
