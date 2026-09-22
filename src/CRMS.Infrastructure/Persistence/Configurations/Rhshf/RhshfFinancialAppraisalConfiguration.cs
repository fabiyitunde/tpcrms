using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

public class RhshfFarmPlanConfiguration : IEntityTypeConfiguration<RhshfFarmPlan>
{
    public void Configure(EntityTypeBuilder<RhshfFarmPlan> builder)
    {
        builder.ToTable("RhshfFarmPlans");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Crop).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Hectares).HasColumnType("decimal(18,4)");
        builder.Property(x => x.ExpectedYieldKgPerHectare).HasColumnType("decimal(18,3)");
        builder.Property(x => x.ExpectedPricePerKg).HasColumnType("decimal(18,4)");

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // ExpectedRevenue / ExpectedOutputKg are derived properties, not columns.
        builder.Ignore(x => x.ExpectedRevenue);
        builder.Ignore(x => x.ExpectedOutputKg);

        builder.HasIndex(x => new { x.RhshfCreditProfileId, x.CycleNumber });
    }
}

public class RhshfFinancialAppraisalReportConfiguration : IEntityTypeConfiguration<RhshfFinancialAppraisalReport>
{
    public void Configure(EntityTypeBuilder<RhshfFinancialAppraisalReport> builder)
    {
        builder.ToTable("RhshfFinancialAppraisalReports");
        builder.HasKey(x => x.Id);

        // Captured assumptions
        builder.Property(x => x.OwnProductionCost).HasColumnType("decimal(18,2)");
        builder.Property(x => x.HarvestAndLogisticsCost).HasColumnType("decimal(18,2)");
        builder.Property(x => x.InterestRatePercent).HasColumnType("decimal(10,4)");
        builder.Property(x => x.AssumptionBasisNote).HasColumnType("longtext");

        // Computed money figures
        foreach (var money in new[]
        {
            nameof(RhshfFinancialAppraisalReport.GrossRevenue),
            nameof(RhshfFinancialAppraisalReport.FinancedInputCost),
            nameof(RhshfFinancialAppraisalReport.OwnCashCosts),
            nameof(RhshfFinancialAppraisalReport.InterestCharge),
            nameof(RhshfFinancialAppraisalReport.TotalCost),
            nameof(RhshfFinancialAppraisalReport.AmountDueAtHarvest),
            nameof(RhshfFinancialAppraisalReport.CashAvailableForDebtService),
            nameof(RhshfFinancialAppraisalReport.GrossMargin),
            nameof(RhshfFinancialAppraisalReport.NetReturnToFarmer),
            nameof(RhshfFinancialAppraisalReport.NetPresentValue),
        })
        {
            builder.Property(money).HasColumnType("decimal(18,2)");
        }

        builder.Property(x => x.GrossMarginPercent).HasColumnType("decimal(10,4)");
        builder.Property(x => x.Dscr).HasColumnType("decimal(10,4)");
        builder.Property(x => x.Irr).HasColumnType("decimal(12,6)");

        builder.Property(x => x.TotalHectares).HasColumnType("decimal(18,4)");
        builder.Property(x => x.BlendedYieldKgPerHectare).HasColumnType("decimal(18,3)");
        builder.Property(x => x.BlendedPricePerKg).HasColumnType("decimal(18,4)");
        builder.Property(x => x.BreakEvenYieldKgPerHectare).HasColumnType("decimal(18,3)");
        builder.Property(x => x.BreakEvenPricePerKg).HasColumnType("decimal(18,4)");
        builder.Property(x => x.YieldHeadroomPercent).HasColumnType("decimal(10,4)");
        builder.Property(x => x.PriceHeadroomPercent).HasColumnType("decimal(10,4)");

        // Threshold snapshot
        builder.Property(x => x.ThresholdMinDscr).HasColumnType("decimal(10,4)");
        builder.Property(x => x.ThresholdMinGrossMarginPercent).HasColumnType("decimal(10,4)");
        builder.Property(x => x.ThresholdHurdleRatePercent).HasColumnType("decimal(10,4)");
        builder.Property(x => x.ThresholdMinYieldHeadroomPercent).HasColumnType("decimal(10,4)");
        builder.Property(x => x.ThresholdMinPriceHeadroomPercent).HasColumnType("decimal(10,4)");

        builder.Property(x => x.RepaymentCapacityRating).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreditOfficerRecommendation).IsRequired().HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.SummaryNotes).HasColumnType("longtext");
        builder.Property(x => x.OverrideJustification).HasColumnType("longtext");

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // Computed aggregates over the gate flags — not columns.
        builder.Ignore(x => x.AllGatesPass);
        builder.Ignore(x => x.GatesPassed);

        // One report per cycle (not one per case — cases round-trip to the FAC and re-appraise).
        builder.HasIndex(x => new { x.RhshfCreditProfileId, x.CycleNumber }).IsUnique();
    }
}

public class RhshfAppraisalThresholdsConfiguration : IEntityTypeConfiguration<RhshfAppraisalThresholds>
{
    public void Configure(EntityTypeBuilder<RhshfAppraisalThresholds> builder)
    {
        builder.ToTable("RhshfAppraisalThresholds");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MinDscr).HasColumnType("decimal(10,4)");
        builder.Property(x => x.MinGrossMarginPercent).HasColumnType("decimal(10,4)");
        builder.Property(x => x.HurdleRatePercent).HasColumnType("decimal(10,4)");
        builder.Property(x => x.MinYieldHeadroomPercent).HasColumnType("decimal(10,4)");
        builder.Property(x => x.MinPriceHeadroomPercent).HasColumnType("decimal(10,4)");

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.IsActive);
    }
}
