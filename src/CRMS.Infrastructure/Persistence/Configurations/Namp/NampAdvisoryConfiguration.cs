using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampAdvisoryConfiguration : IEntityTypeConfiguration<NampAdvisory>
{
    public void Configure(EntityTypeBuilder<NampAdvisory> builder)
    {
        builder.ToTable("NampAdvisories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NampApplicationId)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.OverallScore)
            .HasColumnType("decimal(6,2)");

        builder.Property(x => x.OverallRating)
            .HasConversion<int>();

        builder.Property(x => x.Recommendation)
            .HasConversion<int>();

        builder.Property(x => x.RecommendedAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.RecommendedInterestRate)
            .HasColumnType("decimal(6,4)");

        builder.Property(x => x.ExecutiveSummary)
            .HasColumnType("longtext");

        builder.Property(x => x.StrengthsAnalysis)
            .HasColumnType("longtext");

        builder.Property(x => x.WeaknessesAnalysis)
            .HasColumnType("longtext");

        builder.Property(x => x.MitigatingFactors)
            .HasColumnType("longtext");

        builder.Property(x => x.KeyRisks)
            .HasColumnType("longtext");

        builder.Property(x => x.TechnicalViabilityRating)
            .HasMaxLength(50);

        builder.Property(x => x.TechnicalViabilityScore)
            .HasColumnType("decimal(6,2)");

        builder.Property(x => x.TechnicalViabilityCategoryWeight)
            .HasColumnType("decimal(8,2)");

        builder.Property(x => x.RiskScoresJson)
            .HasColumnType("longtext");

        builder.Property(x => x.RedFlagsJson)
            .HasColumnType("longtext");

        builder.Property(x => x.ConditionsJson)
            .HasColumnType("longtext");

        builder.Property(x => x.CovenantsJson)
            .HasColumnType("longtext");

        builder.Property(x => x.ModelVersion)
            .HasMaxLength(100);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(100);

        builder.Property(x => x.ModifiedBy)
            .HasMaxLength(100);

        builder.HasIndex(x => x.NampApplicationId).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}
