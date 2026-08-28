using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

/// <summary>Own aggregate, own table — see RhshfCommitteeReview's own doc comment for why this is
/// not the generic CommitteeReview or NampCommitteeReview.</summary>
public class RhshfCommitteeReviewConfiguration : IEntityTypeConfiguration<RhshfCommitteeReview>
{
    public void Configure(EntityTypeBuilder<RhshfCommitteeReview> builder)
    {
        builder.ToTable("RhshfCommitteeReviews");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FinalDecision).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Notes).HasColumnType("longtext");

        builder.HasIndex(x => new { x.RhshfCreditProfileId, x.CycleNumber }).IsUnique();

        builder.HasMany(x => x.Votes)
            .WithOne()
            .HasForeignKey(x => x.RhshfCommitteeReviewId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RhshfCommitteeVoteConfiguration : IEntityTypeConfiguration<RhshfCommitteeVote>
{
    public void Configure(EntityTypeBuilder<RhshfCommitteeVote> builder)
    {
        builder.ToTable("RhshfCommitteeVotes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Vote).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Comment).HasColumnType("longtext");

        builder.HasIndex(x => new { x.RhshfCommitteeReviewId, x.UserId }).IsUnique();
    }
}
