using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.SharedKernel.Persistence;

/// <summary>
/// Applies <see cref="UtcDateTimeOffsetConverter"/> to all <see cref="DateTimeOffset"/> properties
/// across the entire model. Call from <c>ConfigureConventions</c> in each DbContext.
/// </summary>
public static class UtcDateTimeOffsetConvention
{
    public static void ApplyUtcDateTimeOffsetConversions(this ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<UtcDateTimeOffsetConverter>();

        configurationBuilder
            .Properties<DateTimeOffset?>()
            .HaveConversion<UtcDateTimeOffsetConverter>();
    }
}
