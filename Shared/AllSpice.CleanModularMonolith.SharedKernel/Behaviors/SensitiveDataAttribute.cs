namespace AllSpice.CleanModularMonolith.SharedKernel.Behaviors;

/// <summary>
/// Marks a command/query property as sensitive (passwords, tokens, secrets, PII) so the
/// <see cref="LoggingBehavior{TRequest,TResponse}"/> redacts its value instead of writing it to logs.
/// Apply to any property whose value must never appear in log output.
/// </summary>
/// <example><code>
/// public sealed record ResetPasswordCommand(string Email, [property: SensitiveData] string NewPassword);
/// </code></example>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class SensitiveDataAttribute : Attribute;
