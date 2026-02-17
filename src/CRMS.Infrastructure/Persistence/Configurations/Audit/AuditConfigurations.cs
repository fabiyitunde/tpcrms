using CRMS.Domain.Aggregates.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Audit;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(x => x.Id);

        // Indexes for common queries
        builder.HasIndex(x => x.Timestamp);
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.Action);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.LoanApplicationId);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => new { x.Category, x.Timestamp });
        builder.HasIndex(x => new { x.Action, x.Timestamp });
        builder.HasIndex(x => x.IsSuccess);

        builder.Property(x => x.Action)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.UserName)
            .HasMaxLength(200);

        builder.Property(x => x.UserRole)
            .HasMaxLength(100);

        builder.Property(x => x.IpAddress)
            .HasMaxLength(45);  // IPv6 max length

        builder.Property(x => x.UserAgent)
            .HasMaxLength(500);

        builder.Property(x => x.EntityType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.EntityReference)
            .HasMaxLength(100);

        builder.Property(x => x.LoanApplicationNumber)
            .HasMaxLength(50);

        builder.Property(x => x.OldValues)
            .HasColumnType("json");

        builder.Property(x => x.NewValues)
            .HasColumnType("json");

        builder.Property(x => x.AdditionalData)
            .HasColumnType("json");

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1000);
    }
}

public class DataAccessLogConfiguration : IEntityTypeConfiguration<DataAccessLog>
{
    public void Configure(EntityTypeBuilder<DataAccessLog> builder)
    {
        builder.ToTable("DataAccessLogs");

        builder.HasKey(x => x.Id);

        // Indexes
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.DataType);
        builder.HasIndex(x => x.AccessedAt);
        builder.HasIndex(x => x.LoanApplicationId);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => new { x.DataType, x.AccessedAt });

        builder.Property(x => x.UserName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.UserRole)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DataType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.EntityReference)
            .HasMaxLength(100);

        builder.Property(x => x.LoanApplicationNumber)
            .HasMaxLength(50);

        builder.Property(x => x.AccessType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.AccessReason)
            .HasMaxLength(500);

        builder.Property(x => x.IpAddress)
            .HasMaxLength(45);
    }
}
