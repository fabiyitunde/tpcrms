using CRMS.Domain.Aggregates.Guarantor;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Guarantor;

public class GuarantorConfiguration : IEntityTypeConfiguration<Domain.Aggregates.Guarantor.Guarantor>
{
    public void Configure(EntityTypeBuilder<Domain.Aggregates.Guarantor.Guarantor> builder)
    {
        builder.ToTable("Guarantors");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GuarantorReference)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(x => x.GuarantorReference).IsUnique();
        builder.HasIndex(x => x.LoanApplicationId);
        builder.HasIndex(x => x.BVN);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(x => x.Status);

        builder.Property(x => x.GuaranteeType)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Identity
        builder.Property(x => x.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.BVN)
            .HasMaxLength(11);

        builder.Property(x => x.NIN)
            .HasMaxLength(11);

        builder.Property(x => x.Gender)
            .HasMaxLength(10);

        builder.Property(x => x.CompanyName)
            .HasMaxLength(200);

        builder.Property(x => x.RegistrationNumber)
            .HasMaxLength(50);

        builder.Property(x => x.TaxId)
            .HasMaxLength(50);

        // Contact
        builder.Property(x => x.Email)
            .HasMaxLength(100);

        builder.Property(x => x.Phone)
            .HasMaxLength(20);

        builder.Property(x => x.Address)
            .HasMaxLength(500);

        // Relationship
        builder.Property(x => x.RelationshipToApplicant)
            .HasMaxLength(50);

        builder.Property(x => x.ShareholdingPercentage)
            .HasPrecision(5, 2);

        // Financial
        builder.OwnsOne(x => x.DeclaredNetWorth, nw =>
        {
            nw.Property(m => m.Amount).HasColumnName("DeclaredNetWorth").HasPrecision(18, 2);
            nw.Property(m => m.Currency).HasColumnName("DeclaredNetWorthCurrency").HasMaxLength(3);
        });

        builder.OwnsOne(x => x.VerifiedNetWorth, nw =>
        {
            nw.Property(m => m.Amount).HasColumnName("VerifiedNetWorth").HasPrecision(18, 2);
            nw.Property(m => m.Currency).HasColumnName("VerifiedNetWorthCurrency").HasMaxLength(3);
        });

        builder.OwnsOne(x => x.MonthlyIncome, mi =>
        {
            mi.Property(m => m.Amount).HasColumnName("MonthlyIncome").HasPrecision(18, 2);
            mi.Property(m => m.Currency).HasColumnName("MonthlyIncomeCurrency").HasMaxLength(3);
        });

        builder.OwnsOne(x => x.GuaranteeLimit, gl =>
        {
            gl.Property(m => m.Amount).HasColumnName("GuaranteeLimit").HasPrecision(18, 2);
            gl.Property(m => m.Currency).HasColumnName("GuaranteeLimitCurrency").HasMaxLength(3);
        });

        builder.OwnsOne(x => x.TotalExistingGuarantees, teg =>
        {
            teg.Property(m => m.Amount).HasColumnName("TotalExistingGuarantees").HasPrecision(18, 2);
            teg.Property(m => m.Currency).HasColumnName("TotalExistingGuaranteesCurrency").HasMaxLength(3);
        });

        builder.Property(x => x.Occupation)
            .HasMaxLength(100);

        builder.Property(x => x.EmployerName)
            .HasMaxLength(200);

        // Credit check
        builder.Property(x => x.CreditScoreGrade)
            .HasMaxLength(10);

        builder.Property(x => x.CreditIssuesSummary)
            .HasMaxLength(1000);

        // Documents
        builder.Property(x => x.AgreementDocumentPath)
            .HasMaxLength(500);

        // Audit
        builder.Property(x => x.RejectionReason)
            .HasMaxLength(1000);

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        // Relationships
        builder.HasMany(x => x.Documents)
            .WithOne()
            .HasForeignKey(x => x.GuarantorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(x => x.DomainEvents);
    }
}

public class GuarantorDocumentConfiguration : IEntityTypeConfiguration<GuarantorDocument>
{
    public void Configure(EntityTypeBuilder<GuarantorDocument> builder)
    {
        builder.ToTable("GuarantorDocuments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DocumentType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.StoragePath)
            .HasMaxLength(500);

        builder.Property(x => x.ContentType)
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(500);
    }
}
