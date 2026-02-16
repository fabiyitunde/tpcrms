using CRMS.Domain.Aggregates.StatementAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.StatementAnalysis;

public class BankStatementConfiguration : IEntityTypeConfiguration<BankStatement>
{
    public void Configure(EntityTypeBuilder<BankStatement> builder)
    {
        builder.ToTable("BankStatements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AccountNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(x => x.AccountNumber);

        builder.Property(x => x.AccountName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.BankName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(x => x.OpeningBalance)
            .HasPrecision(18, 2);

        builder.Property(x => x.ClosingBalance)
            .HasPrecision(18, 2);

        builder.Property(x => x.Format)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Source)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.AnalysisStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(x => x.AnalysisStatus);

        builder.Property(x => x.OriginalFileName)
            .HasMaxLength(255);

        builder.Property(x => x.FilePath)
            .HasMaxLength(500);

        builder.HasIndex(x => x.LoanApplicationId);

        builder.HasMany(x => x.Transactions)
            .WithOne()
            .HasForeignKey(x => x.BankStatementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsOne(x => x.CashflowSummary, cs =>
        {
            cs.Property(p => p.PeriodMonths).HasColumnName("CS_PeriodMonths");
            cs.Property(p => p.PeriodStart).HasColumnName("CS_PeriodStart");
            cs.Property(p => p.PeriodEnd).HasColumnName("CS_PeriodEnd");
            cs.Property(p => p.TotalCredits).HasColumnName("CS_TotalCredits").HasPrecision(18, 2);
            cs.Property(p => p.TotalDebits).HasColumnName("CS_TotalDebits").HasPrecision(18, 2);
            cs.Property(p => p.NetCashflow).HasColumnName("CS_NetCashflow").HasPrecision(18, 2);
            cs.Property(p => p.TotalTransactionCount).HasColumnName("CS_TotalTransactionCount");
            cs.Property(p => p.CreditCount).HasColumnName("CS_CreditCount");
            cs.Property(p => p.DebitCount).HasColumnName("CS_DebitCount");
            cs.Property(p => p.AverageMonthlyCredits).HasColumnName("CS_AvgMonthlyCredits").HasPrecision(18, 2);
            cs.Property(p => p.AverageMonthlyDebits).HasColumnName("CS_AvgMonthlyDebits").HasPrecision(18, 2);
            cs.Property(p => p.AverageMonthlyBalance).HasColumnName("CS_AvgMonthlyBalance").HasPrecision(18, 2);
            cs.Property(p => p.AverageTransactionSize).HasColumnName("CS_AvgTransactionSize").HasPrecision(18, 2);
            cs.Property(p => p.DetectedMonthlySalary).HasColumnName("CS_DetectedSalary").HasPrecision(18, 2);
            cs.Property(p => p.HasRegularSalary).HasColumnName("CS_HasRegularSalary");
            cs.Property(p => p.SalaryPayDay).HasColumnName("CS_SalaryPayDay");
            cs.Property(p => p.SalarySource).HasColumnName("CS_SalarySource").HasMaxLength(100);
            cs.Property(p => p.TotalMonthlyObligations).HasColumnName("CS_MonthlyObligations").HasPrecision(18, 2);
            cs.Property(p => p.DetectedLoanRepayments).HasColumnName("CS_LoanRepayments").HasPrecision(18, 2);
            cs.Property(p => p.DetectedRentPayments).HasColumnName("CS_RentPayments").HasPrecision(18, 2);
            cs.Property(p => p.DetectedUtilityPayments).HasColumnName("CS_UtilityPayments").HasPrecision(18, 2);
            cs.Property(p => p.GamblingTransactionsTotal).HasColumnName("CS_GamblingTotal").HasPrecision(18, 2);
            cs.Property(p => p.GamblingTransactionCount).HasColumnName("CS_GamblingCount");
            cs.Property(p => p.BouncedTransactionCount).HasColumnName("CS_BouncedCount");
            cs.Property(p => p.DaysWithNegativeBalance).HasColumnName("CS_DaysNegative");
            cs.Property(p => p.LowestBalance).HasColumnName("CS_LowestBalance").HasPrecision(18, 2);
            cs.Property(p => p.HighestBalance).HasColumnName("CS_HighestBalance").HasPrecision(18, 2);
            cs.Property(p => p.BalanceVolatility).HasColumnName("CS_BalanceVolatility").HasPrecision(10, 4);
            cs.Property(p => p.IncomeVolatility).HasColumnName("CS_IncomeVolatility").HasPrecision(10, 4);
            cs.Property(p => p.CreditToDebitRatio).HasColumnName("CS_CreditDebitRatio").HasPrecision(10, 4);
            cs.Property(p => p.DebtServiceCoverageRatio).HasColumnName("CS_DSCR").HasPrecision(10, 4);
            cs.Property(p => p.DisposableIncomeRatio).HasColumnName("CS_DisposableRatio").HasPrecision(10, 4);
        });

        builder.Ignore(x => x.DomainEvents);
    }
}

public class StatementTransactionConfiguration : IEntityTypeConfiguration<StatementTransaction>
{
    public void Configure(EntityTypeBuilder<StatementTransaction> builder)
    {
        builder.ToTable("StatementTransactions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.NormalizedDescription)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2);

        builder.Property(x => x.RunningBalance)
            .HasPrecision(18, 2);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(x => x.Reference)
            .HasMaxLength(100);

        builder.Property(x => x.Category)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.CategoryConfidence)
            .HasPrecision(5, 4);

        builder.Property(x => x.RecurringPattern)
            .HasMaxLength(50);

        builder.HasIndex(x => new { x.BankStatementId, x.Date });
        builder.HasIndex(x => x.Category);
    }
}
