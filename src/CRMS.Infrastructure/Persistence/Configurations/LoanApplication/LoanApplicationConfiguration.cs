using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LA = CRMS.Domain.Aggregates.LoanApplication;

namespace CRMS.Infrastructure.Persistence.Configurations.LoanApplication;

public class LoanApplicationConfiguration : IEntityTypeConfiguration<LA.LoanApplication>
{
    public void Configure(EntityTypeBuilder<LA.LoanApplication> builder)
    {
        builder.ToTable("LoanApplications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ApplicationNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.ApplicationNumber)
            .IsUnique();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(x => x.Status);

        builder.Property(x => x.ProductCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.AccountNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(x => x.AccountNumber);

        builder.Property(x => x.CustomerId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CustomerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.OwnsOne(x => x.RequestedAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("RequestedAmount")
                .HasPrecision(18, 2)
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("RequestedCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(x => x.InterestRatePerAnnum)
            .HasPrecision(5, 2);

        builder.Property(x => x.InterestRateType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Purpose)
            .HasMaxLength(500);

        builder.Property(x => x.RegistrationNumber)
            .HasMaxLength(50);

        builder.Property(x => x.IncorporationDate)
            .IsRequired(false);

        builder.OwnsOne(x => x.ApprovedAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("ApprovedAmount")
                .HasPrecision(18, 2);

            money.Property(m => m.Currency)
                .HasColumnName("ApprovedCurrency")
                .HasMaxLength(3);
        });

        builder.Property(x => x.ApprovedInterestRate)
            .HasPrecision(5, 2);

        builder.Property(x => x.CoreBankingLoanId)
            .HasMaxLength(50);

        builder.HasIndex(x => x.InitiatedByUserId);
        builder.HasIndex(x => x.BranchId);

        builder.HasMany(x => x.Documents)
            .WithOne()
            .HasForeignKey(x => x.LoanApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Parties)
            .WithOne()
            .HasForeignKey(x => x.LoanApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Comments)
            .WithOne()
            .HasForeignKey(x => x.LoanApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.StatusHistory)
            .WithOne()
            .HasForeignKey(x => x.LoanApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Concurrency token disabled for MySQL compatibility
        // RowVersion stored as BLOB, must have default value to avoid DBNull issues
        builder.Property(x => x.RowVersion)
            .HasColumnType("BLOB")
            .HasDefaultValue(new byte[] { 0 })
            .IsRequired(false);

        builder.Ignore(x => x.DomainEvents);
    }
}

public class LoanApplicationDocumentConfiguration : IEntityTypeConfiguration<LA.LoanApplicationDocument>
{
    public void Configure(EntityTypeBuilder<LA.LoanApplicationDocument> builder)
    {
        builder.ToTable("LoanApplicationDocuments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Category)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.FilePath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(500);
    }
}

public class LoanApplicationPartyConfiguration : IEntityTypeConfiguration<LA.LoanApplicationParty>
{
    public void Configure(EntityTypeBuilder<LA.LoanApplicationParty> builder)
    {
        builder.ToTable("LoanApplicationParties");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PartyType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.BVN)
            .HasMaxLength(11);

        builder.Property(x => x.Email)
            .HasMaxLength(200);

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(x => x.Designation)
            .HasMaxLength(100);

        builder.Property(x => x.ShareholdingPercent)
            .HasPrecision(5, 2);

        builder.Property(x => x.Address)
            .HasMaxLength(500);
    }
}

public class LoanApplicationCommentConfiguration : IEntityTypeConfiguration<LA.LoanApplicationComment>
{
    public void Configure(EntityTypeBuilder<LA.LoanApplicationComment> builder)
    {
        builder.ToTable("LoanApplicationComments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasMaxLength(50);
    }
}

public class LoanApplicationStatusHistoryConfiguration : IEntityTypeConfiguration<LA.LoanApplicationStatusHistory>
{
    public void Configure(EntityTypeBuilder<LA.LoanApplicationStatusHistory> builder)
    {
        builder.ToTable("LoanApplicationStatusHistory");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Comment)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.LoanApplicationId, x.ChangedAt });
    }
}
