using System.Text.Json;
using AllSpice.CleanModularMonolith.ApiGateway.Middleware;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace AllSpice.CleanModularMonolith.ApiGateway.UnitTests.Middleware;

public class ErrorHandlingMiddlewareTests
{
    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static async Task<(int status, string contentType, string body)> InvokeWithAsync(Exception thrown)
    {
        RequestDelegate next = _ => throw thrown;
        var middleware = new ErrorHandlingMiddleware(next, NullLogger<ErrorHandlingMiddleware>.Instance, new FakeWebHostEnvironment());

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context.Response.StatusCode, context.Response.ContentType ?? string.Empty, body);
    }

    [Fact]
    public async Task ValidationException_maps_to_400_problem_json_with_errors()
    {
        var (status, contentType, body) = await InvokeWithAsync(
            new ValidationException([new ValidationFailure("Email", "Email is required")]));

        Assert.Equal(400, status);
        Assert.Equal("application/problem+json", contentType);

        using var doc = JsonDocument.Parse(body);
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        var extensions = doc.RootElement.GetProperty("extensions");
        Assert.True(extensions.TryGetProperty("errors", out var errors), "validation errors should be surfaced");
        Assert.True(errors.TryGetProperty("Email", out _), "the offending field should be present");
    }

    [Fact]
    public async Task NotFoundException_maps_to_404()
    {
        var (status, _, _) = await InvokeWithAsync(new NotFoundException("Entity", "missing-id"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Unknown_exception_maps_to_500()
    {
        var (status, _, _) = await InvokeWithAsync(new InvalidOperationException("boom"));
        Assert.Equal(500, status);
    }
}
