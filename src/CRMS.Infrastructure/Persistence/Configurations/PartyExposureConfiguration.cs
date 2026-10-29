using CRMS.Domain.Aggregates.PartyExposure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations;

public class PartyExposureLookupLogConfiguration : IEntityTypeConfiguration<PartyExposureLookupLog>
{
    public void Configure(EntityTypeBuilder<PartyExposureLookupLog> builder)
    {
        builder.ToTable("PartyExposureLookupLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RequestId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Purpose).IsRequired().HasMaxLength(50);
        builder.Property(x => x.RequestedByUserId).HasMaxLength(100);
        builder.Property(x => x.RequestedByRole).HasMaxLength(100);
        builder.Property(x => x.SubjectRef).HasMaxLength(200);
        // HMAC-SHA256 hex — 64 characters.
        builder.Property(x => x.BvnHash).HasMaxLength(64);
        builder.Property(x => x.NinHash).HasMaxLength(64);
        builder.Property(x => x.NubanHashes).HasMaxLength(1000);
        builder.Property(x => x.MatchStatus).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Error).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.RequestId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.BatchId);
        // "Who has looked this person up?" is answered by hashing the identifier and searching here.
        builder.HasIndex(x => x.BvnHash);

        builder.Ignore(x => x.DomainEvents);
    }
}

public class PartyExposureBatchConfiguration : IEntityTypeConfiguration<PartyExposureBatch>
{
    public void Configure(EntityTypeBuilder<PartyExposureBatch> builder)
    {
        builder.ToTable("PartyExposureBatches");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RequestId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Purpose).IsRequired().HasMaxLength(50);
        builder.Property(x => x.RequestedByUserId).HasMaxLength(100);
        builder.Property(x => x.RequestedByRole).HasMaxLength(100);
        builder.Property(x => x.CallbackUrl).HasMaxLength(500);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);
        builder.Property(x => x.ResultsJson).HasColumnType("longtext");
        builder.Property(x => x.Failure).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.RequestId);
        builder.HasIndex(x => x.Status);

        builder.Ignore(x => x.DomainEvents);
    }
}
