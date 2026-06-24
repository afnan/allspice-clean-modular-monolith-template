using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.SharedKernel.UnitTests.Behaviors;

public sealed class PerformanceBehaviorTests
{
    private sealed record Request : Mediator.IRequest<string>;

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Levels.Add(logLevel);

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private static async Task<List<LogLevel>> RunWithThreshold(long thresholdMs)
    {
        var logger = new CapturingLogger<PerformanceBehavior<Request, string>>();
        var options = Options.Create(new PerformanceBehaviorOptions { SlowRequestThresholdMs = thresholdMs });
        var behavior = new PerformanceBehavior<Request, string>(logger, options);

        await behavior.Handle(new Request(), (_, _) => ValueTask.FromResult("ok"), CancellationToken.None);

        return logger.Levels;
    }

    [Fact]
    public async Task Logs_warning_when_request_meets_or_exceeds_threshold()
    {
        // Threshold 0 → any elapsed (always >= 0) is treated as slow.
        var levels = await RunWithThreshold(0);

        Assert.Contains(LogLevel.Warning, levels);
        Assert.DoesNotContain(LogLevel.Trace, levels);
    }

    [Fact]
    public async Task Logs_trace_when_request_is_under_threshold()
    {
        // Threshold effectively unreachable → request is fast → trace, no warning.
        var levels = await RunWithThreshold(long.MaxValue);

        Assert.Contains(LogLevel.Trace, levels);
        Assert.DoesNotContain(LogLevel.Warning, levels);
    }
}
