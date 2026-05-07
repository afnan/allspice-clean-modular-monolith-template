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
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
