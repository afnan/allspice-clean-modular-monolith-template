using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AllSpice.CleanModularMonolith.SharedKernel.Persistence;

/// <summary>
/// EF Core value converter that normalizes <see cref="DateTimeOffset"/> to UTC before writing to PostgreSQL.
/// Npgsql requires offset 0 (UTC) for <c>timestamptz</c> columns.
/// </summary>
public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public UtcDateTimeOffsetConverter()
        : base(
            v => v.ToUniversalTime(),
            v => v.ToUniversalTime())
    {
    }
}
