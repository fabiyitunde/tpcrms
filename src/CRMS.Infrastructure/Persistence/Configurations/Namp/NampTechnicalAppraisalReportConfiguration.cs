using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampTechnicalAppraisalReportConfiguration : IEntityTypeConfiguration<NampTechnicalAppraisalReport>
{
    public void Configure(EntityTypeBuilder<NampTechnicalAppraisalReport> builder)
    {
        builder.ToTable("NampTechnicalAppraisalReports");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.NampApplicationId).IsRequired();
        builder.Property(x => x.PreparedByUserId).IsRequired();
        builder.Property(x => x.SavedAt).IsRequired();

        // Individual applicant fields (nullable — not applicable to AgroServiceCompany)
        builder.Property(x => x.FarmLocationDescription).HasColumnType("text");
        builder.Property(x => x.GpsCoordinates).HasMaxLength(100);
        builder.Property(x => x.LandAreaAssessedHectares).HasColumnType("decimal(10,4)");
        builder.Property(x => x.SoilConditionRating).HasConversion<int>();
        builder.Property(x => x.WaterSourceAvailability).HasColumnType("text");

        // AgroServiceCompany fields (nullable — not applicable to individual applicants)
        builder.Property(x => x.OperationalCoverageArea).HasColumnType("text");
        builder.Property(x => x.CurrentServiceContracts).HasColumnType("text");
        builder.Property(x => x.StorageFacilityDescription).HasColumnType("text");

        builder.Property(x => x.ProposedEquipmentSuitability).IsRequired().HasColumnType("text");
        builder.Property(x => x.InfrastructureNotes).HasColumnType("text");
        builder.Property(x => x.RisksIdentified).HasColumnType("text");
        builder.Property(x => x.RecommendedMitigations).HasColumnType("text");
        builder.Property(x => x.OverallViabilityRating).IsRequired().HasConversion<int>();
        builder.Property(x => x.EngineerRecommendation).IsRequired().HasConversion<int>();
        builder.Property(x => x.SummaryNotes).HasColumnType("text");

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.NampApplicationId).IsUnique();
    }
}
