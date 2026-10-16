using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

/// <summary>Append-only record of each financial appraisal pass — see
/// NampFinancialAppraisalRecord for why the single fields on the aggregate are not enough.</summary>
public class NampFinancialAppraisalRecordConfiguration : IEntityTypeConfiguration<NampFinancialAppraisalRecord>
{
    public void Configure(EntityTypeBuilder<NampFinancialAppraisalRecord> builder)
    {
        builder.ToTable("NampFinancialAppraisalRecords");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.NampApplicationId).IsRequired();
        builder.Property(x => x.PassNumber).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.NampApplicationId);
        builder.HasIndex(x => new { x.NampApplicationId, x.PassNumber });
    }
}

public class NampRiskReviewRecordConfiguration : IEntityTypeConfiguration<NampRiskReviewRecord>
{
    public void Configure(EntityTypeBuilder<NampRiskReviewRecord> builder)
    {
        builder.ToTable("NampRiskReviewRecords");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.NampApplicationId).IsRequired();
        builder.Property(x => x.PassNumber).IsRequired();

        // By name, like every other enum in this schema — an int would make the column meaningless
        // the moment an outcome is inserted into the middle of the enum.
        builder.Property(x => x.Outcome).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.NampApplicationId);
        builder.HasIndex(x => new { x.NampApplicationId, x.PassNumber });
    }
}
