using CRMS.Domain.Aggregates.Committee;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Committee;

public class CommitteeReviewConfiguration : IEntityTypeConfiguration<CommitteeReview>
{
    public void Configure(EntityTypeBuilder<CommitteeReview> builder)
    {
        builder.ToTable("CommitteeReviews");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.LoanApplicationId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CommitteeType);
        builder.HasIndex(x => new { x.Status, x.DeadlineAt });

        builder.Property(x => x.ApplicationNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CommitteeType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.FinalDecision)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.DecisionRationale)
            .HasMaxLength(2000);

        builder.Property(x => x.ApprovedAmount)
            .HasPrecision(18, 2);

        builder.Property(x => x.ApprovedInterestRate)
            .HasPrecision(8, 4);

        builder.Property(x => x.ApprovalConditions)
            .HasMaxLength(2000);

        builder.HasMany(x => x.Members)
            .WithOne()
            .HasForeignKey(x => x.CommitteeReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Comments)
            .WithOne()
            .HasForeignKey(x => x.CommitteeReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Documents)
            .WithOne()
            .HasForeignKey(x => x.CommitteeReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(x => x.DomainEvents);
    }
}

public class CommitteeMemberConfiguration : IEntityTypeConfiguration<CommitteeMember>
{
    public void Configure(EntityTypeBuilder<CommitteeMember> builder)
    {
        builder.ToTable("CommitteeMembers");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.CommitteeReviewId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.UserId);

        builder.Property(x => x.UserName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Role)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Vote)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.VoteComment)
            .HasMaxLength(1000);
    }
}

public class CommitteeCommentConfiguration : IEntityTypeConfiguration<CommitteeComment>
{
    public void Configure(EntityTypeBuilder<CommitteeComment> builder)
    {
        builder.ToTable("CommitteeComments");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.CommitteeReviewId);
        builder.HasIndex(x => x.UserId);

        builder.Property(x => x.Content)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.Visibility)
            .HasConversion<string>()
            .HasMaxLength(20);
    }
}

public class CommitteeDocumentConfiguration : IEntityTypeConfiguration<CommitteeDocument>
{
    public void Configure(EntityTypeBuilder<CommitteeDocument> builder)
    {
        builder.ToTable("CommitteeDocuments");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.CommitteeReviewId);

        builder.Property(x => x.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.FilePath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.Visibility)
            .HasConversion<string>()
            .HasMaxLength(20);
    }
}
