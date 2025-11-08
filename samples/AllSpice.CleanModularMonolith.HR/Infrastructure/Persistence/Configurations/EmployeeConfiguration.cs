using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AllSpice.CleanModularMonolith.HR.Domain.Aggregates;

namespace AllSpice.CleanModularMonolith.HR.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(e => e.Email)
            .IsUnique();

        builder.Property(e => e.HireDate)
            .IsRequired();

        builder.Property(e => e.CreatedOnUtc)
            .IsRequired();

        builder.Property(e => e.LastModifiedOnUtc);
    }
}


