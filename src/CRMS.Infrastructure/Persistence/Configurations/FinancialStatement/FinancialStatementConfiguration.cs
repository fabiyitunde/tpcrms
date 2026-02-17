using CRMS.Domain.Aggregates.FinancialStatement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.FinancialStatement;

public class FinancialStatementConfiguration : IEntityTypeConfiguration<Domain.Aggregates.FinancialStatement.FinancialStatement>
{
    public void Configure(EntityTypeBuilder<Domain.Aggregates.FinancialStatement.FinancialStatement> builder)
    {
        builder.ToTable("FinancialStatements");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.LoanApplicationId, x.FinancialYear }).IsUnique();

        builder.Property(x => x.YearType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.InputMethod)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(x => x.AuditorName)
            .HasMaxLength(200);

        builder.Property(x => x.AuditorFirm)
            .HasMaxLength(200);

        builder.Property(x => x.AuditOpinion)
            .HasMaxLength(500);

        builder.Property(x => x.OriginalFileName)
            .HasMaxLength(255);

        builder.Property(x => x.FilePath)
            .HasMaxLength(500);

        builder.Property(x => x.VerificationNotes)
            .HasMaxLength(1000);

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(1000);

        // One-to-one relationships
        builder.HasOne(x => x.BalanceSheet)
            .WithOne()
            .HasForeignKey<BalanceSheet>(x => x.FinancialStatementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.IncomeStatement)
            .WithOne()
            .HasForeignKey<IncomeStatement>(x => x.FinancialStatementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CashFlowStatement)
            .WithOne()
            .HasForeignKey<CashFlowStatement>(x => x.FinancialStatementId)
            .OnDelete(DeleteBehavior.Cascade);

        // FinancialRatios as owned type
        builder.OwnsOne(x => x.CalculatedRatios, r =>
        {
            r.Property(x => x.CurrentRatio).HasColumnName("Ratio_CurrentRatio").HasPrecision(10, 4);
            r.Property(x => x.QuickRatio).HasColumnName("Ratio_QuickRatio").HasPrecision(10, 4);
            r.Property(x => x.CashRatio).HasColumnName("Ratio_CashRatio").HasPrecision(10, 4);
            r.Property(x => x.DebtToEquityRatio).HasColumnName("Ratio_DebtToEquity").HasPrecision(10, 4);
            r.Property(x => x.DebtToAssetsRatio).HasColumnName("Ratio_DebtToAssets").HasPrecision(10, 4);
            r.Property(x => x.InterestCoverageRatio).HasColumnName("Ratio_InterestCoverage").HasPrecision(10, 4);
            r.Property(x => x.DebtServiceCoverageRatio).HasColumnName("Ratio_DSCR").HasPrecision(10, 4);
            r.Property(x => x.GrossMarginPercent).HasColumnName("Ratio_GrossMargin").HasPrecision(10, 4);
            r.Property(x => x.OperatingMarginPercent).HasColumnName("Ratio_OperatingMargin").HasPrecision(10, 4);
            r.Property(x => x.NetProfitMarginPercent).HasColumnName("Ratio_NetProfitMargin").HasPrecision(10, 4);
            r.Property(x => x.EBITDAMarginPercent).HasColumnName("Ratio_EBITDAMargin").HasPrecision(10, 4);
            r.Property(x => x.ReturnOnAssets).HasColumnName("Ratio_ROA").HasPrecision(10, 4);
            r.Property(x => x.ReturnOnEquity).HasColumnName("Ratio_ROE").HasPrecision(10, 4);
            r.Property(x => x.AssetTurnover).HasColumnName("Ratio_AssetTurnover").HasPrecision(10, 4);
            r.Property(x => x.InventoryTurnover).HasColumnName("Ratio_InventoryTurnover").HasPrecision(10, 4);
            r.Property(x => x.ReceivablesDays).HasColumnName("Ratio_ReceivablesDays").HasPrecision(10, 2);
            r.Property(x => x.PayablesDays).HasColumnName("Ratio_PayablesDays").HasPrecision(10, 2);
            r.Property(x => x.CashConversionCycle).HasColumnName("Ratio_CashConversionCycle").HasPrecision(10, 2);
            r.Property(x => x.WorkingCapital).HasColumnName("Ratio_WorkingCapital").HasPrecision(18, 2);
            r.Property(x => x.NetWorth).HasColumnName("Ratio_NetWorth").HasPrecision(18, 2);
            r.Property(x => x.TotalDebt).HasColumnName("Ratio_TotalDebt").HasPrecision(18, 2);
        });

        builder.Ignore(x => x.DomainEvents);
    }
}

public class BalanceSheetConfiguration : IEntityTypeConfiguration<BalanceSheet>
{
    public void Configure(EntityTypeBuilder<BalanceSheet> builder)
    {
        builder.ToTable("BalanceSheets");

        builder.HasKey(x => x.Id);

        // Current Assets
        builder.Property(x => x.CashAndCashEquivalents).HasPrecision(18, 2);
        builder.Property(x => x.TradeReceivables).HasPrecision(18, 2);
        builder.Property(x => x.Inventory).HasPrecision(18, 2);
        builder.Property(x => x.PrepaidExpenses).HasPrecision(18, 2);
        builder.Property(x => x.OtherCurrentAssets).HasPrecision(18, 2);

        // Non-Current Assets
        builder.Property(x => x.PropertyPlantEquipment).HasPrecision(18, 2);
        builder.Property(x => x.IntangibleAssets).HasPrecision(18, 2);
        builder.Property(x => x.LongTermInvestments).HasPrecision(18, 2);
        builder.Property(x => x.DeferredTaxAssets).HasPrecision(18, 2);
        builder.Property(x => x.OtherNonCurrentAssets).HasPrecision(18, 2);

        // Current Liabilities
        builder.Property(x => x.TradePayables).HasPrecision(18, 2);
        builder.Property(x => x.ShortTermBorrowings).HasPrecision(18, 2);
        builder.Property(x => x.CurrentPortionLongTermDebt).HasPrecision(18, 2);
        builder.Property(x => x.AccruedExpenses).HasPrecision(18, 2);
        builder.Property(x => x.TaxPayable).HasPrecision(18, 2);
        builder.Property(x => x.OtherCurrentLiabilities).HasPrecision(18, 2);

        // Non-Current Liabilities
        builder.Property(x => x.LongTermDebt).HasPrecision(18, 2);
        builder.Property(x => x.DeferredTaxLiabilities).HasPrecision(18, 2);
        builder.Property(x => x.Provisions).HasPrecision(18, 2);
        builder.Property(x => x.OtherNonCurrentLiabilities).HasPrecision(18, 2);

        // Equity
        builder.Property(x => x.ShareCapital).HasPrecision(18, 2);
        builder.Property(x => x.SharePremium).HasPrecision(18, 2);
        builder.Property(x => x.RetainedEarnings).HasPrecision(18, 2);
        builder.Property(x => x.OtherReserves).HasPrecision(18, 2);

        // Ignore computed properties
        builder.Ignore(x => x.TotalCurrentAssets);
        builder.Ignore(x => x.TotalNonCurrentAssets);
        builder.Ignore(x => x.TotalAssets);
        builder.Ignore(x => x.TotalCurrentLiabilities);
        builder.Ignore(x => x.TotalNonCurrentLiabilities);
        builder.Ignore(x => x.TotalLiabilities);
        builder.Ignore(x => x.TotalEquity);
        builder.Ignore(x => x.TotalLiabilitiesAndEquity);
        builder.Ignore(x => x.TotalDebt);
        builder.Ignore(x => x.WorkingCapital);
        builder.Ignore(x => x.NetWorth);
    }
}

public class IncomeStatementConfiguration : IEntityTypeConfiguration<IncomeStatement>
{
    public void Configure(EntityTypeBuilder<IncomeStatement> builder)
    {
        builder.ToTable("IncomeStatements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Revenue).HasPrecision(18, 2);
        builder.Property(x => x.OtherOperatingIncome).HasPrecision(18, 2);
        builder.Property(x => x.CostOfSales).HasPrecision(18, 2);
        builder.Property(x => x.SellingExpenses).HasPrecision(18, 2);
        builder.Property(x => x.AdministrativeExpenses).HasPrecision(18, 2);
        builder.Property(x => x.DepreciationAmortization).HasPrecision(18, 2);
        builder.Property(x => x.OtherOperatingExpenses).HasPrecision(18, 2);
        builder.Property(x => x.InterestIncome).HasPrecision(18, 2);
        builder.Property(x => x.InterestExpense).HasPrecision(18, 2);
        builder.Property(x => x.OtherFinanceCosts).HasPrecision(18, 2);
        builder.Property(x => x.IncomeTaxExpense).HasPrecision(18, 2);
        builder.Property(x => x.DividendsDeclared).HasPrecision(18, 2);

        // Ignore computed properties
        builder.Ignore(x => x.TotalRevenue);
        builder.Ignore(x => x.GrossProfit);
        builder.Ignore(x => x.GrossMarginPercent);
        builder.Ignore(x => x.TotalOperatingExpenses);
        builder.Ignore(x => x.OperatingProfit);
        builder.Ignore(x => x.EBIT);
        builder.Ignore(x => x.EBITDA);
        builder.Ignore(x => x.EBITDAMarginPercent);
        builder.Ignore(x => x.NetFinanceCost);
        builder.Ignore(x => x.ProfitBeforeTax);
        builder.Ignore(x => x.NetProfit);
        builder.Ignore(x => x.NetProfitMarginPercent);
        builder.Ignore(x => x.RetainedProfit);
        builder.Ignore(x => x.IsProfitable);
        builder.Ignore(x => x.IsOperatingProfitable);
    }
}

public class CashFlowStatementConfiguration : IEntityTypeConfiguration<CashFlowStatement>
{
    public void Configure(EntityTypeBuilder<CashFlowStatement> builder)
    {
        builder.ToTable("CashFlowStatements");

        builder.HasKey(x => x.Id);

        // Operating
        builder.Property(x => x.ProfitBeforeTax).HasPrecision(18, 2);
        builder.Property(x => x.DepreciationAmortization).HasPrecision(18, 2);
        builder.Property(x => x.InterestExpenseAddBack).HasPrecision(18, 2);
        builder.Property(x => x.ChangesInWorkingCapital).HasPrecision(18, 2);
        builder.Property(x => x.TaxPaid).HasPrecision(18, 2);
        builder.Property(x => x.OtherOperatingAdjustments).HasPrecision(18, 2);

        // Investing
        builder.Property(x => x.PurchaseOfPPE).HasPrecision(18, 2);
        builder.Property(x => x.SaleOfPPE).HasPrecision(18, 2);
        builder.Property(x => x.PurchaseOfInvestments).HasPrecision(18, 2);
        builder.Property(x => x.SaleOfInvestments).HasPrecision(18, 2);
        builder.Property(x => x.InterestReceived).HasPrecision(18, 2);
        builder.Property(x => x.DividendsReceived).HasPrecision(18, 2);
        builder.Property(x => x.OtherInvestingActivities).HasPrecision(18, 2);

        // Financing
        builder.Property(x => x.ProceedsFromBorrowings).HasPrecision(18, 2);
        builder.Property(x => x.RepaymentOfBorrowings).HasPrecision(18, 2);
        builder.Property(x => x.InterestPaid).HasPrecision(18, 2);
        builder.Property(x => x.DividendsPaid).HasPrecision(18, 2);
        builder.Property(x => x.ProceedsFromShareIssue).HasPrecision(18, 2);
        builder.Property(x => x.OtherFinancingActivities).HasPrecision(18, 2);
        builder.Property(x => x.OpeningCashBalance).HasPrecision(18, 2);

        // Ignore computed
        builder.Ignore(x => x.NetCashFromOperations);
        builder.Ignore(x => x.NetCashFromInvesting);
        builder.Ignore(x => x.NetCashFromFinancing);
        builder.Ignore(x => x.NetChangeInCash);
        builder.Ignore(x => x.ClosingCashBalance);
        builder.Ignore(x => x.FreeCashFlow);
        builder.Ignore(x => x.FreeCashFlowToFirm);
        builder.Ignore(x => x.HasPositiveOperatingCashFlow);
        builder.Ignore(x => x.HasPositiveFreeCashFlow);
    }
}
