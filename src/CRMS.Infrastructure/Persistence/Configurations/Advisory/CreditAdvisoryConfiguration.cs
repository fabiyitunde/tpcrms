using CRMS.Domain.Aggregates.Advisory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace CRMS.Infrastructure.Persistence.Configurations.Advisory;

public class CreditAdvisoryConfiguration : IEntityTypeConfiguration<CreditAdvisory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public void Configure(EntityTypeBuilder<CreditAdvisory> builder)
    {
        builder.ToTable("CreditAdvisories");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.LoanApplicationId);
        builder.HasIndex(x => new { x.LoanApplicationId, x.GeneratedAt });

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.OverallScore)
            .HasPrecision(5, 2);

        builder.Property(x => x.OverallRating)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Recommendation)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.RecommendedAmount)
            .HasPrecision(18, 2);

        builder.Property(x => x.RecommendedInterestRate)
            .HasPrecision(5, 2);

        builder.Property(x => x.MaxExposure)
            .HasPrecision(18, 2);

        builder.Property(x => x.ModelVersion)
            .HasMaxLength(50);

        builder.Property(x => x.ExecutiveSummary)
            .HasColumnType("text");

        builder.Property(x => x.StrengthsAnalysis)
            .HasColumnType("text");

        builder.Property(x => x.WeaknessesAnalysis)
            .HasColumnType("text");

        builder.Property(x => x.MitigatingFactors)
            .HasMaxLength(2000);

        builder.Property(x => x.KeyRisks)
            .HasMaxLength(2000);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1000);

        // Store List<Guid> as JSON
        var guidListConverter = new ValueConverter<List<Guid>, string>(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<List<Guid>>(v, JsonOptions) ?? new List<Guid>()
        );

        var guidListComparer = new ValueComparer<List<Guid>>(
            (c1, c2) => c1!.SequenceEqual(c2!),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList()
        );

        builder.Property(x => x.BureauReportIds)
            .HasConversion(guidListConverter)
            .Metadata.SetValueComparer(guidListComparer);

        builder.Property(x => x.FinancialStatementIds)
            .HasConversion(guidListConverter)
            .Metadata.SetValueComparer(guidListComparer);

        // Ignore navigation/computed properties - these are backed by private fields
        // which EF cannot map directly without complex configuration
        builder.Ignore(x => x.RiskScores);
        builder.Ignore(x => x.Conditions);
        builder.Ignore(x => x.Covenants);
        builder.Ignore(x => x.RedFlags);
        builder.Ignore(x => x.HasCriticalRedFlags);
        builder.Ignore(x => x.CreditHistoryScore);
        builder.Ignore(x => x.FinancialHealthScore);
        builder.Ignore(x => x.CashflowScore);
        builder.Ignore(x => x.DSCRScore);
        builder.Ignore(x => x.DomainEvents);
    }
}
