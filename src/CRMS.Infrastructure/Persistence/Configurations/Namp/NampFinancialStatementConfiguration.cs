using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampFinancialStatementConfiguration : IEntityTypeConfiguration<NampFinancialStatement>
{
    public void Configure(EntityTypeBuilder<NampFinancialStatement> builder)
    {
        builder.ToTable("NampFinancialStatements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NampApplicationId).IsRequired();
        builder.Property(x => x.FinancialYear).IsRequired();
        builder.Property(x => x.YearEndDate).HasMaxLength(20);
        builder.Property(x => x.FinancialYearType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.InputMethod).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(10);

        // Income Statement
        foreach (var col in new[]
        {
            nameof(NampFinancialStatement.Revenue), nameof(NampFinancialStatement.OtherOperatingIncome),
            nameof(NampFinancialStatement.CostOfSales), nameof(NampFinancialStatement.GrossProfit),
            nameof(NampFinancialStatement.SellingExpenses), nameof(NampFinancialStatement.AdministrativeExpenses),
            nameof(NampFinancialStatement.DepreciationAmortization), nameof(NampFinancialStatement.OtherOperatingExpenses),
            nameof(NampFinancialStatement.OperatingExpenses), nameof(NampFinancialStatement.Ebitda),
            nameof(NampFinancialStatement.InterestIncome), nameof(NampFinancialStatement.InterestExpense),
            nameof(NampFinancialStatement.OtherFinanceCosts), nameof(NampFinancialStatement.IncomeTaxExpense),
            nameof(NampFinancialStatement.DividendsDeclared), nameof(NampFinancialStatement.NetProfit),
            // Balance Sheet
            nameof(NampFinancialStatement.CashAndCashEquivalents), nameof(NampFinancialStatement.TradeReceivables),
            nameof(NampFinancialStatement.Inventory), nameof(NampFinancialStatement.PrepaidExpenses),
            nameof(NampFinancialStatement.OtherCurrentAssets), nameof(NampFinancialStatement.TotalCurrentAssets),
            nameof(NampFinancialStatement.PropertyPlantEquipment), nameof(NampFinancialStatement.IntangibleAssets),
            nameof(NampFinancialStatement.LongTermInvestments), nameof(NampFinancialStatement.DeferredTaxAssets),
            nameof(NampFinancialStatement.OtherNonCurrentAssets), nameof(NampFinancialStatement.TotalNonCurrentAssets),
            nameof(NampFinancialStatement.TotalAssets),
            nameof(NampFinancialStatement.TradePayables), nameof(NampFinancialStatement.ShortTermBorrowings),
            nameof(NampFinancialStatement.CurrentPortionLongTermDebt), nameof(NampFinancialStatement.AccruedExpenses),
            nameof(NampFinancialStatement.TaxPayable), nameof(NampFinancialStatement.OtherCurrentLiabilities),
            nameof(NampFinancialStatement.TotalCurrentLiabilities),
            nameof(NampFinancialStatement.LongTermDebt), nameof(NampFinancialStatement.DeferredTaxLiabilities),
            nameof(NampFinancialStatement.Provisions), nameof(NampFinancialStatement.OtherNonCurrentLiabilities),
            nameof(NampFinancialStatement.TotalNonCurrentLiabilities), nameof(NampFinancialStatement.TotalLiabilities),
            nameof(NampFinancialStatement.ShareCapital), nameof(NampFinancialStatement.SharePremium),
            nameof(NampFinancialStatement.RetainedEarnings), nameof(NampFinancialStatement.OtherReserves),
            nameof(NampFinancialStatement.TotalEquity),
            // Cash Flow
            nameof(NampFinancialStatement.CfProfitBeforeTax), nameof(NampFinancialStatement.CfDepreciationAmortization),
            nameof(NampFinancialStatement.CfInterestExpenseAddBack), nameof(NampFinancialStatement.CfChangesInWorkingCapital),
            nameof(NampFinancialStatement.CfTaxPaid), nameof(NampFinancialStatement.CfOtherOperatingAdjustments),
            nameof(NampFinancialStatement.NetCashFromOperating),
            nameof(NampFinancialStatement.CfPurchaseOfPpe), nameof(NampFinancialStatement.CfSaleOfPpe),
            nameof(NampFinancialStatement.CfPurchaseOfInvestments), nameof(NampFinancialStatement.CfSaleOfInvestments),
            nameof(NampFinancialStatement.CfInterestReceived), nameof(NampFinancialStatement.CfDividendsReceived),
            nameof(NampFinancialStatement.CfOtherInvestingActivities), nameof(NampFinancialStatement.NetCashFromInvesting),
            nameof(NampFinancialStatement.CfProceedsFromBorrowings), nameof(NampFinancialStatement.CfRepaymentOfBorrowings),
            nameof(NampFinancialStatement.CfInterestPaid), nameof(NampFinancialStatement.CfDividendsPaid),
            nameof(NampFinancialStatement.CfProceedsFromShareIssue), nameof(NampFinancialStatement.CfOtherFinancingActivities),
            nameof(NampFinancialStatement.NetCashFromFinancing), nameof(NampFinancialStatement.NetChangeInCash),
            nameof(NampFinancialStatement.CfOpeningCashBalance), nameof(NampFinancialStatement.ClosingCashBalance),
        })
        {
            builder.Property(col).HasColumnType("decimal(18,2)");
        }

        builder.Property(x => x.AuditorName).HasMaxLength(300);
        builder.Property(x => x.AuditorFirm).HasMaxLength(300);
        builder.Property(x => x.AuditDate).HasMaxLength(20);
        builder.Property(x => x.AuditOpinion).HasMaxLength(100);

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.NampApplicationId);
        builder.HasIndex(x => new { x.NampApplicationId, x.FinancialYear }).IsUnique();
    }
}
