using CRMS.Domain.Aggregates.CreditBureau;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.CreditBureau;

public class BureauReportConfiguration : IEntityTypeConfiguration<BureauReport>
{
    public void Configure(EntityTypeBuilder<BureauReport> builder)
    {
        builder.ToTable("BureauReports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.SubjectType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(x => x.Status);

        builder.Property(x => x.BVN)
            .HasMaxLength(11);

        builder.HasIndex(x => x.BVN);

        builder.Property(x => x.RegistryId)
            .HasMaxLength(50);

        builder.Property(x => x.TaxId)
            .HasMaxLength(50);

        builder.Property(x => x.SubjectName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ScoreGrade)
            .HasMaxLength(10);

        builder.Property(x => x.RawResponseJson)
            .HasColumnType("LONGTEXT");

        builder.Property(x => x.PdfReportBase64)
            .HasColumnType("LONGTEXT");

        builder.Property(x => x.TotalOutstandingBalance)
            .HasPrecision(18, 2);

        builder.Property(x => x.TotalCreditLimit)
            .HasPrecision(18, 2);

        builder.Property(x => x.RequestReference)
            .HasMaxLength(50);

        builder.HasIndex(x => x.RequestReference);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.LoanApplicationId);
        builder.HasIndex(x => x.RequestedByUserId);

        builder.HasMany(x => x.Accounts)
            .WithOne()
            .HasForeignKey(x => x.BureauReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ScoreFactors)
            .WithOne()
            .HasForeignKey(x => x.BureauReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(x => x.DomainEvents);
    }
}

public class BureauAccountConfiguration : IEntityTypeConfiguration<BureauAccount>
{
    public void Configure(EntityTypeBuilder<BureauAccount> builder)
    {
        builder.ToTable("BureauAccounts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AccountNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CreditorName)
            .HasMaxLength(200);

        builder.Property(x => x.AccountType)
            .HasMaxLength(50);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.DelinquencyLevel)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.CreditLimit)
            .HasPrecision(18, 2);

        builder.Property(x => x.Balance)
            .HasPrecision(18, 2);

        builder.Property(x => x.MinimumPayment)
            .HasPrecision(18, 2);

        builder.Property(x => x.LastPaymentAmount)
            .HasPrecision(18, 2);

        builder.Property(x => x.PaymentProfile)
            .HasMaxLength(100);

        builder.Property(x => x.LegalStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Currency)
            .HasMaxLength(3);
    }
}

public class BureauScoreFactorConfiguration : IEntityTypeConfiguration<BureauScoreFactor>
{
    public void Configure(EntityTypeBuilder<BureauScoreFactor> builder)
    {
        builder.ToTable("BureauScoreFactors");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FactorCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.FactorDescription)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Impact)
            .HasMaxLength(50)
            .IsRequired();
    }
}
