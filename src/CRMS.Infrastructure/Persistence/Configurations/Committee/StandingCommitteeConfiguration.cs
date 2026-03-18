using CRMS.Domain.Aggregates.Committee;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Committee;

public class StandingCommitteeConfiguration : IEntityTypeConfiguration<StandingCommittee>
{
    public void Configure(EntityTypeBuilder<StandingCommittee> builder)
    {
        builder.ToTable("StandingCommittees");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.CommitteeType).IsUnique();
        builder.HasIndex(x => x.IsActive);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CommitteeType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.MinAmountThreshold)
            .HasPrecision(18, 2);

        builder.Property(x => x.MaxAmountThreshold)
            .HasPrecision(18, 2);

        builder.HasMany(x => x.Members)
            .WithOne()
            .HasForeignKey(x => x.StandingCommitteeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(x => x.DomainEvents);
    }
}

public class StandingCommitteeMemberConfiguration : IEntityTypeConfiguration<StandingCommitteeMember>
{
    public void Configure(EntityTypeBuilder<StandingCommitteeMember> builder)
    {
        builder.ToTable("StandingCommitteeMembers");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.StandingCommitteeId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.UserId);

        builder.Property(x => x.UserName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Role)
            .HasMaxLength(100)
            .IsRequired();
    }
}
