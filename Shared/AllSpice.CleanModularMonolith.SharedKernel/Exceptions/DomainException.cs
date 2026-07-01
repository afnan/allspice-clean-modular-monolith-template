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

            // Insert '_' only at the START of a new word, not inside an acronym run.
            // A new word begins at an uppercase char when either:
            //   - the previous char is lowercase (e.g. "Not" + "Found" → "not_found"), OR
            //   - the previous char is uppercase AND the next char is lowercase, i.e. the end of an
            //     acronym run transitioning into a word (e.g. "HTTP" + "Not" → "http_not").
            // This correctly converts "APIValidation" → "api_validation" (not "a_p_i_validation")
            // while keeping "BusinessRuleViolation" → "business_rule_violation" unchanged.
            if (char.IsUpper(c) && i > 0 &&
                (!char.IsUpper(typeName[i - 1]) ||
                 (i + 1 < typeName.Length && char.IsLower(typeName[i + 1]))))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
