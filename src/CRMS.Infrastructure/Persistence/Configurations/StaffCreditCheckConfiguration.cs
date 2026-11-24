using CRMS.Domain.Aggregates.Access;
using CRMS.Domain.Aggregates.CreditCheck;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations;

public class StaffCreditCheckConfiguration : IEntityTypeConfiguration<StaffCreditCheck>
{
    public void Configure(EntityTypeBuilder<StaffCreditCheck> builder)
    {
        builder.ToTable("StaffCreditChecks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubjectType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.SubjectHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SubjectLabel).IsRequired().HasMaxLength(250);
        builder.Property(x => x.SubjectName).HasMaxLength(200);
        builder.Property(x => x.RequestedByName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Purpose).IsRequired().HasMaxLength(500);
        builder.Property(x => x.CaseReference).HasMaxLength(100);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);
        builder.Property(x => x.SectionsRequested).IsRequired().HasMaxLength(250);
        builder.Property(x => x.RequestCiphertext).IsRequired().HasColumnType("longtext");
        builder.Property(x => x.ResultCiphertext).HasColumnType("longtext");
        builder.Property(x => x.SectionOutcomes).HasMaxLength(500);
        builder.Property(x => x.BureauUsed).HasMaxLength(50);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.AdvisoryScore).HasPrecision(5, 2);
        builder.Property(x => x.AdvisoryRating).HasMaxLength(50);
        builder.Property(x => x.AdvisoryRecommendation).HasMaxLength(50);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // The claim query (runnable checks, oldest first), a user's history, everyone's history,
        // and earlier checks on the same subject.
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.RequestedByUserId, x.CreatedAt });
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.SubjectHash);

        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.SectionList);
        builder.Ignore(x => x.OutcomeMap);
        builder.Ignore(x => x.IsOpen);
    }
}

public class FeatureAccessRuleConfiguration : IEntityTypeConfiguration<FeatureAccessRule>
{
    public void Configure(EntityTypeBuilder<FeatureAccessRule> builder)
    {
        builder.ToTable("FeatureAccessRules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FeatureKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.AllowedRoles).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.FeatureKey).IsUnique();

        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.RoleList);
    }
}
