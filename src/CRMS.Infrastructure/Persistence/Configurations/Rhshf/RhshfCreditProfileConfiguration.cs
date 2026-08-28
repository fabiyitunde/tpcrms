using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

public class RhshfCreditProfileConfiguration : IEntityTypeConfiguration<RhshfCreditProfile>
{
    public void Configure(EntityTypeBuilder<RhshfCreditProfile> builder)
    {
        builder.ToTable("RhshfCreditProfiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reference).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.Reference).IsUnique();

        builder.Property(x => x.SubmissionId).IsRequired();
        builder.HasIndex(x => x.SubmissionId).IsUnique();

        builder.Property(x => x.ProgrammeCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ProgrammeName).HasMaxLength(200);
        builder.Property(x => x.SessionCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SessionName).HasMaxLength(200);

        builder.Property(x => x.FacId).IsRequired();
        builder.Property(x => x.CompanyName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RcNumber).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Tin).IsRequired().HasMaxLength(50);
        builder.Property(x => x.BoaAccountNumber).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ContactEmail).HasMaxLength(200);
        builder.Property(x => x.ContactPhone).HasMaxLength(30);
        builder.Property(x => x.State).HasMaxLength(100);
        builder.Property(x => x.Lga).HasMaxLength(100);

        builder.Property(x => x.TotalEopValue).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(10);

        builder.Property(x => x.CallbackUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.CertifiedByAdmin).HasMaxLength(200);

        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.CurrentStage).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.DecisionOutcome).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.ApprovedAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.DecidedBy).HasMaxLength(200);

        builder.Property(x => x.RawSubmissionPayload).HasColumnType("longtext");

        builder.Property(x => x.BureauCheckOutcome).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.BureauTotalOutstanding).HasColumnType("decimal(18,2)");
        builder.Property(x => x.BureauTotalOverdue).HasColumnType("decimal(18,2)");
        builder.Property(x => x.BureauRawJson).HasColumnType("longtext");

        builder.Property(x => x.BranchResolutionNote).HasMaxLength(500);
        builder.Property(x => x.InternalStage).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.DecisionNotes).HasColumnType("longtext");

        builder.HasMany(x => x.EopLines)
            .WithOne()
            .HasForeignKey(x => x.RhshfCreditProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.IssuedTokens)
            .WithOne()
            .HasForeignKey(x => x.RhshfCreditProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.SupportingDocuments)
            .WithOne()
            .HasForeignKey(x => x.RhshfCreditProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Appraisals)
            .WithOne()
            .HasForeignKey(x => x.RhshfCreditProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.RiskReviews)
            .WithOne()
            .HasForeignKey(x => x.RhshfCreditProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Ratifications)
            .WithOne()
            .HasForeignKey(x => x.RhshfCreditProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RhshfRatificationConfiguration : IEntityTypeConfiguration<RhshfRatification>
{
    public void Configure(EntityTypeBuilder<RhshfRatification> builder)
    {
        builder.ToTable("RhshfRatifications");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Outcome).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ApprovedAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Notes).HasColumnType("longtext");
    }
}

public class RhshfAppraisalConfiguration : IEntityTypeConfiguration<RhshfAppraisal>
{
    public void Configure(EntityTypeBuilder<RhshfAppraisal> builder)
    {
        builder.ToTable("RhshfAppraisals");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Outcome).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Notes).HasColumnType("longtext");
    }
}

public class RhshfRiskReviewConfiguration : IEntityTypeConfiguration<RhshfRiskReview>
{
    public void Configure(EntityTypeBuilder<RhshfRiskReview> builder)
    {
        builder.ToTable("RhshfRiskReviews");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Outcome).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Notes).HasColumnType("longtext");
    }
}

public class RhshfSupportingDocumentConfiguration : IEntityTypeConfiguration<RhshfSupportingDocument>
{
    public void Configure(EntityTypeBuilder<RhshfSupportingDocument> builder)
    {
        builder.ToTable("RhshfSupportingDocuments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.StoragePath).IsRequired().HasMaxLength(500);
    }
}

public class RhshfIssuedTokenConfiguration : IEntityTypeConfiguration<RhshfIssuedToken>
{
    public void Configure(EntityTypeBuilder<RhshfIssuedToken> builder)
    {
        builder.ToTable("RhshfIssuedTokens");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Jti).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => x.Jti).IsUnique();
    }
}

public class RhshfEopLineConfiguration : IEntityTypeConfiguration<RhshfEopLine>
{
    public void Configure(EntityTypeBuilder<RhshfEopLine> builder)
    {
        builder.ToTable("RhshfEopLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Commodity).IsRequired().HasMaxLength(100);
        builder.Property(x => x.QuantityKg).HasColumnType("decimal(18,3)");
        builder.Property(x => x.UnitPricePerKg).HasColumnType("decimal(18,2)");
        builder.Property(x => x.LineValue).HasColumnType("decimal(18,2)");
    }
}
