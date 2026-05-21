using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// Structured technical appraisal report prepared by the Agricultural Engineer.
/// Standalone entity (not a navigation on NampApplication) — one per NAMP application.
/// </summary>
public class NampTechnicalAppraisalReport : Entity
{
    public Guid NampApplicationId { get; private set; }
    public Guid PreparedByUserId { get; private set; }
    public DateTime SavedAt { get; private set; }

    // Site Assessment
    public string FarmLocationDescription { get; private set; } = string.Empty;
    public string? GpsCoordinates { get; private set; }
    public decimal? LandAreaAssessedHectares { get; private set; }
    public NampSoilConditionRating SoilConditionRating { get; private set; }
    public string WaterSourceAvailability { get; private set; } = string.Empty;

    // Equipment & Infrastructure
    public string ProposedEquipmentSuitability { get; private set; } = string.Empty;
    public string? InfrastructureNotes { get; private set; }

    // Risk
    public string? RisksIdentified { get; private set; }
    public string? RecommendedMitigations { get; private set; }

    // Recommendation
    public NampViabilityRating OverallViabilityRating { get; private set; }
    public NampTechnicalRecommendation EngineerRecommendation { get; private set; }
    public string? SummaryNotes { get; private set; }

    private NampTechnicalAppraisalReport() { }

    public static Result<NampTechnicalAppraisalReport> Create(
        Guid nampApplicationId,
        Guid preparedByUserId,
        string farmLocationDescription,
        string? gpsCoordinates,
        decimal? landAreaAssessedHectares,
        NampSoilConditionRating soilConditionRating,
        string waterSourceAvailability,
        string proposedEquipmentSuitability,
        string? infrastructureNotes,
        string? risksIdentified,
        string? recommendedMitigations,
        NampViabilityRating overallViabilityRating,
        NampTechnicalRecommendation engineerRecommendation,
        string? summaryNotes)
    {
        if (overallViabilityRating == NampViabilityRating.NotViable &&
            engineerRecommendation == NampTechnicalRecommendation.Pass)
            return Result.Failure<NampTechnicalAppraisalReport>(
                "A farm rated 'Not Viable' cannot be recommended as Pass. Change the viability rating or set recommendation to Fail.");

        if (string.IsNullOrWhiteSpace(farmLocationDescription))
            return Result.Failure<NampTechnicalAppraisalReport>("Farm location description is required.");
        if (string.IsNullOrWhiteSpace(waterSourceAvailability))
            return Result.Failure<NampTechnicalAppraisalReport>("Water source availability is required.");
        if (string.IsNullOrWhiteSpace(proposedEquipmentSuitability))
            return Result.Failure<NampTechnicalAppraisalReport>("Proposed equipment suitability assessment is required.");

        return Result.Success(new NampTechnicalAppraisalReport
        {
            NampApplicationId = nampApplicationId,
            PreparedByUserId = preparedByUserId,
            SavedAt = DateTime.UtcNow,
            FarmLocationDescription = farmLocationDescription,
            GpsCoordinates = gpsCoordinates,
            LandAreaAssessedHectares = landAreaAssessedHectares,
            SoilConditionRating = soilConditionRating,
            WaterSourceAvailability = waterSourceAvailability,
            ProposedEquipmentSuitability = proposedEquipmentSuitability,
            InfrastructureNotes = infrastructureNotes,
            RisksIdentified = risksIdentified,
            RecommendedMitigations = recommendedMitigations,
            OverallViabilityRating = overallViabilityRating,
            EngineerRecommendation = engineerRecommendation,
            SummaryNotes = summaryNotes,
        });
    }

    public Result Update(
        Guid updatedByUserId,
        string farmLocationDescription,
        string? gpsCoordinates,
        decimal? landAreaAssessedHectares,
        NampSoilConditionRating soilConditionRating,
        string waterSourceAvailability,
        string proposedEquipmentSuitability,
        string? infrastructureNotes,
        string? risksIdentified,
        string? recommendedMitigations,
        NampViabilityRating overallViabilityRating,
        NampTechnicalRecommendation engineerRecommendation,
        string? summaryNotes)
    {
        if (overallViabilityRating == NampViabilityRating.NotViable &&
            engineerRecommendation == NampTechnicalRecommendation.Pass)
            return Result.Failure(
                "A farm rated 'Not Viable' cannot be recommended as Pass. Change the viability rating or set recommendation to Fail.");

        if (string.IsNullOrWhiteSpace(farmLocationDescription))
            return Result.Failure("Farm location description is required.");
        if (string.IsNullOrWhiteSpace(waterSourceAvailability))
            return Result.Failure("Water source availability is required.");
        if (string.IsNullOrWhiteSpace(proposedEquipmentSuitability))
            return Result.Failure("Proposed equipment suitability assessment is required.");

        PreparedByUserId = updatedByUserId;
        SavedAt = DateTime.UtcNow;
        FarmLocationDescription = farmLocationDescription;
        GpsCoordinates = gpsCoordinates;
        LandAreaAssessedHectares = landAreaAssessedHectares;
        SoilConditionRating = soilConditionRating;
        WaterSourceAvailability = waterSourceAvailability;
        ProposedEquipmentSuitability = proposedEquipmentSuitability;
        InfrastructureNotes = infrastructureNotes;
        RisksIdentified = risksIdentified;
        RecommendedMitigations = recommendedMitigations;
        OverallViabilityRating = overallViabilityRating;
        EngineerRecommendation = engineerRecommendation;
        SummaryNotes = summaryNotes;
        return Result.Success();
    }
}
