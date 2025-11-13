using System;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleAssignment;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="ModuleRoleAssignment"/> persistence, including auditing metadata.
/// </summary>
public sealed class ModuleRoleAssignmentConfiguration : IEntityTypeConfiguration<ModuleRoleAssignment>
{
    public void Configure(EntityTypeBuilder<ModuleRoleAssignment> builder)
    {
        builder.ToTable("ModuleRoleAssignments");

        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.ModuleKey)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(assignment => assignment.RoleKey)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(assignment => assignment.AssignedBy)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(assignment => assignment.AssignedUtc)
            .IsRequired();

        builder.Property(assignment => assignment.RevokedUtc);

        var userIdConverter = new ValueConverter<ExternalUserId, string>(
            userId => userId.Value,
            value => ExternalUserId.From(value!));

        var userIdComparer = new ValueComparer<ExternalUserId>(
            (left, right) => left.Value.Equals(right.Value, StringComparison.Ordinal),
            userId => userId.Value.GetHashCode(StringComparison.Ordinal),
            value => ExternalUserId.From(value.Value));

        var userIdProperty = builder.Property(assignment => assignment.UserId)
            .HasConversion(userIdConverter)
            .HasColumnName("UserObjectId")
            .HasMaxLength(128)
            .IsRequired();

        userIdProperty.Metadata.SetValueComparer(userIdComparer);

        builder.HasIndex(
                nameof(ModuleRoleAssignment.UserId),
                nameof(ModuleRoleAssignment.ModuleKey),
                nameof(ModuleRoleAssignment.RoleKey))
            .IsUnique()
            .HasDatabaseName("UX_ModuleRoleAssignment_UserModuleRole");

        builder.Property(assignment => assignment.CreatedOnUtc)
            .IsRequired();

        builder.Property(assignment => assignment.CreatedBy)
            .HasMaxLength(128);

        builder.Property(assignment => assignment.LastModifiedOnUtc);

        builder.Property(assignment => assignment.LastModifiedBy)
            .HasMaxLength(128);
    }
}


