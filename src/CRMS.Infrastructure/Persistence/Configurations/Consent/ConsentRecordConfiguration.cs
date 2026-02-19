using CRMS.Domain.Aggregates.Consent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Consent;

public class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("ConsentRecords");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.LoanApplicationId);
        builder.HasIndex(x => x.BVN);
        builder.HasIndex(x => new { x.BVN, x.ConsentType, x.Status });

        builder.Property(x => x.ConsentType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.SubjectName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.BVN)
            .HasMaxLength(11);

        builder.Property(x => x.NIN)
            .HasMaxLength(11);

        builder.Property(x => x.Email)
            .HasMaxLength(100);

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(x => x.Purpose)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.ConsentText)
            .HasColumnType("TEXT")
            .IsRequired();

        builder.Property(x => x.ConsentVersion)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CaptureMethod)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.SignatureData)
            .HasColumnType("TEXT");

        builder.Property(x => x.IpAddress)
            .HasMaxLength(45);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(500);

        builder.Property(x => x.CapturedByUserName)
            .HasMaxLength(200);

        builder.Property(x => x.WitnessName)
            .HasMaxLength(200);

        builder.Property(x => x.RevocationReason)
            .HasMaxLength(1000);

        builder.Ignore(x => x.DomainEvents);
    }
}
