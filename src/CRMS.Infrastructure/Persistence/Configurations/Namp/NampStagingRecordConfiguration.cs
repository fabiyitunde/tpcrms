using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampStagingRecordConfiguration : IEntityTypeConfiguration<NampStagingRecord>
{
    public void Configure(EntityTypeBuilder<NampStagingRecord> builder)
    {
        builder.ToTable("NampStagingRecords");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ApplicationReference)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CrmsApplicationNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.RawPayload)
            .IsRequired()
            .HasColumnType("longtext");

        builder.Property(x => x.ApplicantName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.BoaAccountNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ApplicantPhone)
            .HasMaxLength(30);

        builder.Property(x => x.ApplicantEmail)
            .HasMaxLength(200);

        builder.Property(x => x.ApplicantCategory)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.EquipmentDescription)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.EquipmentValue)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.BranchResolutionNote)
            .HasMaxLength(500);

        builder.Property(x => x.ReceivedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(100);

        builder.Property(x => x.ModifiedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(x => x.ApplicationReference)
            .IsUnique();

        builder.HasIndex(x => x.IsRecalled);
        builder.HasIndex(x => x.ResolvedBranchId);
        builder.HasIndex(x => x.ReceivedAt);
    }
}
