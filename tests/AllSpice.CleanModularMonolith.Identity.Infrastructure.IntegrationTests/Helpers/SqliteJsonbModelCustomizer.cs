using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests.Helpers;

/// <summary>
/// Lets the Npgsql-typed module DbContexts run against in-memory SQLite for fast tests:
/// rewrites PostgreSQL <c>jsonb</c> columns to <c>text</c>, adds a <see cref="JsonDocument"/> converter,
/// and maps <see cref="DateTimeOffset"/> to UtcTicks (long) so <c>ORDER BY</c> translates on SQLite
/// (which cannot natively order <c>DateTimeOffset</c>). Register via
/// <c>options.ReplaceService&lt;IModelCustomizer, SqliteJsonbModelCustomizer&gt;()</c>.
/// </summary>
internal sealed class SqliteJsonbModelCustomizer : RelationalModelCustomizer
{
    private static readonly ValueConverter<JsonDocument?, string?> JsonDocumentConverter = new(
        v => v == null ? null : v.RootElement.GetRawText(),
        v => v == null ? null : JsonDocument.Parse(v, default));

    // SQLite cannot ORDER BY DateTimeOffset. Store as UtcTicks (long) so ordering is chronological.
    // Round-trip returns the same instant with a UTC offset (the original local offset is not preserved —
    // DateTimeOffset.Equals compares instants, so this is transparent to typical Assert.Equal usage).
    private static readonly ValueConverter<DateTimeOffset, long> DateTimeOffsetConverter = new(
        v => v.UtcTicks,
        v => new DateTimeOffset(v, TimeSpan.Zero));

    private static readonly ValueConverter<DateTimeOffset?, long?> NullableDateTimeOffsetConverter = new(
        v => v == null ? (long?)null : v.Value.UtcTicks,
        v => v == null ? (DateTimeOffset?)null : new DateTimeOffset(v.Value, TimeSpan.Zero));

    public SqliteJsonbModelCustomizer(ModelCustomizerDependencies dependencies)
        : base(dependencies)
    {
    }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Npgsql's UseXminAsConcurrencyToken (see AuthzMapVersionConfiguration) maps PostgreSQL's
            // system 'xmin' column (type 'xid', store-generated) as an optimistic-concurrency token.
            // SQLite has no such system column, and a store-generated NOT NULL column breaks inserts,
            // so drop the shadow property here. The concurrency guarantee is a Postgres runtime concern
            // not exercised by these SQLite-backed tests.
            if (entityType.FindProperty("xmin") is { } xminProperty && xminProperty.IsShadowProperty())
            {
                entityType.RemoveProperty(xminProperty.Name);
            }

            foreach (var property in entityType.GetProperties())
            {
                if (string.Equals(property.GetColumnType(), "jsonb", StringComparison.OrdinalIgnoreCase))
                {
                    property.SetColumnType("text");
                }

                if (property.ClrType == typeof(JsonDocument))
                {
                    property.SetValueConverter(JsonDocumentConverter);
                }

                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(DateTimeOffsetConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(NullableDateTimeOffsetConverter);
                }
            }
        }
    }
}
