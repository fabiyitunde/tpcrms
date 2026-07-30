using CRMS.Domain.Aggregates.LoanApplication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.LoanApplication;

public class SecurityPerfectionDocumentConfiguration : IEntityTypeConfiguration<SecurityPerfectionDocument>
{
    public void Configure(EntityTypeBuilder<SecurityPerfectionDocument> builder)
    {
        builder.ToTable("SecurityPerfectionDocuments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ApplicationId).IsRequired();
        builder.HasIndex(x => x.ApplicationId);

        builder.Property(x => x.Category)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CollateralId).IsRequired(false);

        builder.Property(x => x.CollateralDescription)
            .HasMaxLength(500);

        builder.Property(x => x.DocumentType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.FileName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.StoragePath)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasMaxLength(100);

        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.ModifiedBy).HasMaxLength(200);

        builder.HasOne<Domain.Aggregates.LoanApplication.LoanApplication>()
            .WithMany()
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
