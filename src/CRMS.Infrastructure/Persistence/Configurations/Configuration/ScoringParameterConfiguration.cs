using CRMS.Domain.Aggregates.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Configuration;

public class ScoringParameterConfiguration : IEntityTypeConfiguration<ScoringParameter>
{
    public void Configure(EntityTypeBuilder<ScoringParameter> builder)
    {
        builder.ToTable("ScoringParameters");

        builder.HasKey(x => x.Id);

        // Unique constraint on Category + ParameterKey
        builder.HasIndex(x => new { x.Category, x.ParameterKey }).IsUnique();
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.ChangeStatus);

        builder.Property(x => x.Category)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ParameterKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.DataType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.CurrentValue)
            .HasPrecision(18, 6);

        builder.Property(x => x.MinValue)
            .HasPrecision(18, 6);

        builder.Property(x => x.MaxValue)
            .HasPrecision(18, 6);

        builder.Property(x => x.PendingValue)
            .HasPrecision(18, 6);

        builder.Property(x => x.PendingChangeReason)
            .HasMaxLength(500);

        builder.Property(x => x.ChangeStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.ApprovalNotes)
            .HasMaxLength(500);

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(500);

        builder.Ignore(x => x.DomainEvents);
    }
}

public class ScoringParameterHistoryConfiguration : IEntityTypeConfiguration<ScoringParameterHistory>
{
    public void Configure(EntityTypeBuilder<ScoringParameterHistory> builder)
    {
        builder.ToTable("ScoringParameterHistory");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.ScoringParameterId);
        builder.HasIndex(x => x.ApprovedAt);
        builder.HasIndex(x => new { x.Category, x.ParameterKey });

        builder.Property(x => x.Category)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ParameterKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PreviousValue)
            .HasPrecision(18, 6);

        builder.Property(x => x.NewValue)
            .HasPrecision(18, 6);

        builder.Property(x => x.ChangeType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ChangeReason)
            .HasMaxLength(500);

        builder.Property(x => x.ApprovalNotes)
            .HasMaxLength(500);
    }
}
