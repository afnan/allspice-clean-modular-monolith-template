namespace AllSpice.CleanModularMonolith.SharedKernel.Common;

/// <summary>
/// String helpers shared across modules and gateway middleware.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Returns the first <paramref name="maxLength"/> characters of <paramref name="value"/>,
    /// or the whole string if it's already shorter. Null returns empty string. Used to bound
    /// log messages and HTTP response payloads that could otherwise leak large or sensitive
    /// content (tokens echoed in error bodies, full exception strings, etc).
    /// </summary>
    public static string Truncate(this string? value, int maxLength)
    {
        // Clamp non-positive lengths to empty rather than throwing: this helper guards
        // log/response payloads and is often called from catch blocks, so it must never
        // be the thing that throws (a negative maxLength would otherwise fault value[..maxLength]).
        if (string.IsNullOrEmpty(value) || maxLength <= 0)
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    /// <summary>
    /// Escapes SQL <c>LIKE</c>/<c>ILIKE</c> wildcard meta-characters (<c>\</c>, <c>%</c>, <c>_</c>)
    /// so a value can be used as a literal pattern. Pair with the 3-argument
    /// <c>EF.Functions.ILike(column, pattern, "\\")</c> overload. Without this, characters such as
    /// <c>_</c> (legal in an email local-part) are treated as wildcards and break exact matching.
    /// </summary>
    public static string EscapeLikePattern(this string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }
}
