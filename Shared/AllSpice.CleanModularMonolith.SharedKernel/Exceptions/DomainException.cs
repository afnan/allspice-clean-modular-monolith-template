using System.Text;

namespace AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Stable, machine-readable error code surfaced to API clients (e.g. in the ProblemDetails <c>code</c>
    /// member). Defaults to a snake_case form of the exception type name with the <c>Exception</c> suffix
    /// removed — e.g. <c>NotFoundException</c> → <c>not_found</c>, <c>BusinessRuleViolationException</c> →
    /// <c>business_rule_violation</c>. Override for a more specific contract when needed.
    /// </summary>
    public virtual string Code => ToSnakeCase(GetType().Name);

    private static string ToSnakeCase(string typeName)
    {
        const string suffix = "Exception";
        if (typeName.EndsWith(suffix, StringComparison.Ordinal) && typeName.Length > suffix.Length)
        {
            typeName = typeName[..^suffix.Length];
        }

        var builder = new StringBuilder(typeName.Length + 8);
        for (var i = 0; i < typeName.Length; i++)
        {
            var c = typeName[i];
            if (char.IsUpper(c) && i > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
