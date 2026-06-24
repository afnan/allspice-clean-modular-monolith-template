using AllSpice.CleanModularMonolith.Web;
using Ardalis.Result;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.ApiGateway.UnitTests.Web;

/// <summary>
/// D2 regression: the de-duplicated <c>ExecuteFailureAsync</c> (shared by the <c>Result</c> and
/// <c>Result&lt;T&gt;</c> overloads) must keep mapping each <see cref="ResultStatus"/> to the same HTTP code.
/// </summary>
public class ResultExecuteFailureTests
{
    private static DefaultHttpContext NewContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    private static async Task<int> StatusFor(Result result)
    {
        var context = NewContext();
        await result.ExecuteFailureAsync(context);
        return context.Response.StatusCode;
    }

    private static async Task<int> StatusFor<T>(Result<T> result)
    {
        var context = NewContext();
        await result.ExecuteFailureAsync(context);
        return context.Response.StatusCode;
    }

    [Theory]
    [InlineData(ResultStatus.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ResultStatus.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ResultStatus.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ResultStatus.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ResultStatus.Error, StatusCodes.Status400BadRequest)]
    public async Task Maps_status_to_http_code_for_untyped_and_typed_results(ResultStatus status, int expected)
    {
        Result untyped = status switch
        {
            ResultStatus.NotFound => Result.NotFound(),
            ResultStatus.Conflict => Result.Conflict(),
            ResultStatus.Forbidden => Result.Forbidden(),
            ResultStatus.Unauthorized => Result.Unauthorized(),
            _ => Result.Error("boom")
        };

        Result<int> typed = status switch
        {
            ResultStatus.NotFound => Result<int>.NotFound(),
            ResultStatus.Conflict => Result<int>.Conflict(),
            ResultStatus.Forbidden => Result<int>.Forbidden(),
            ResultStatus.Unauthorized => Result<int>.Unauthorized(),
            _ => Result<int>.Error("boom")
        };

        Assert.Equal(expected, await StatusFor(untyped));
        Assert.Equal(expected, await StatusFor(typed));
    }

    [Fact]
    public async Task Maps_invalid_to_400_for_both_overloads()
    {
        var error = new ValidationError { Identifier = "Name", ErrorMessage = "required" };

        Assert.Equal(StatusCodes.Status400BadRequest, await StatusFor(Result.Invalid(error)));
        Assert.Equal(StatusCodes.Status400BadRequest, await StatusFor(Result<int>.Invalid(error)));
    }
}
