using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

public class RhshfAdvisoryConfiguration : IEntityTypeConfiguration<RhshfAdvisory>
{
    public void Configure(EntityTypeBuilder<RhshfAdvisory> builder)
    {
        builder.ToTable("RhshfAdvisories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RhshfCreditProfileId).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.OverallScore).HasColumnType("decimal(6,2)");
        builder.Property(x => x.OverallRating).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Recommendation).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.RecommendedAmount).HasColumnType("decimal(18,2)");

        builder.Property(x => x.ExecutiveSummary).HasColumnType("longtext");
        builder.Property(x => x.StrengthsAnalysis).HasColumnType("longtext");
        builder.Property(x => x.WeaknessesAnalysis).HasColumnType("longtext");
        builder.Property(x => x.MitigatingFactors).HasColumnType("longtext");
        builder.Property(x => x.KeyRisks).HasColumnType("longtext");
        builder.Property(x => x.RiskScoresJson).HasColumnType("longtext");
        builder.Property(x => x.RedFlagsJson).HasColumnType("longtext");
        builder.Property(x => x.ConditionsJson).HasColumnType("longtext");
        builder.Property(x => x.CovenantsJson).HasColumnType("longtext");
        builder.Property(x => x.ErrorMessage).HasColumnType("longtext");

        builder.Property(x => x.ModelVersion).HasMaxLength(50);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.RhshfCreditProfileId);
    }
}
