using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampFinancialAppraisalReportConfiguration : IEntityTypeConfiguration<NampFinancialAppraisalReport>
{
    public void Configure(EntityTypeBuilder<NampFinancialAppraisalReport> builder)
    {
        builder.ToTable("NampFinancialAppraisalReports");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.NampApplicationId).IsRequired();
        builder.Property(x => x.PreparedByUserId).IsRequired();
        builder.Property(x => x.SavedAt).IsRequired();

        builder.Property(x => x.MonthlyDisposableIncome).HasColumnType("decimal(18,2)");
        builder.Property(x => x.DebtServiceCoverageRatio).HasColumnType("decimal(10,4)");
        builder.Property(x => x.LoanToValueRatio).HasColumnType("decimal(10,4)");
        builder.Property(x => x.RepaymentCapacityRating).IsRequired().HasConversion<int>();
        builder.Property(x => x.EquityAssessmentNote).HasColumnType("text");
        builder.Property(x => x.CreditBureauSummary).HasColumnType("text");
        builder.Property(x => x.CreditOfficerRecommendation).IsRequired().HasConversion<int>();
        builder.Property(x => x.SummaryNotes).HasColumnType("text");

        builder.Property(x => x.RepaymentSource).IsRequired().HasConversion<int>().HasDefaultValue(NampRepaymentSource.PrimaryIncome);
        builder.Property(x => x.ProjectedMonthlyRentalRevenue).HasColumnType("decimal(18,2)");
        builder.Property(x => x.UtilisationRateAssumption).HasColumnType("decimal(5,2)");
        builder.Property(x => x.DemandEvidenceNote).HasColumnType("text");

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.NampApplicationId).IsUnique();
    }
}
