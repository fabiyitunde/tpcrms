using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// Structured technical appraisal report prepared by the Agricultural Engineer.
/// Standalone entity (not a navigation on NampApplication) — one per NAMP application.
/// Individual applicants use the farm/site fields; AgroServiceCompany applicants use the
/// company operational fields. Shared fields (equipment suitability, viability, recommendation,
/// risks, mitigations, infrastructure notes, summary) apply to both categories.
/// </summary>
public class NampTechnicalAppraisalReport : Entity
{
    public Guid NampApplicationId { get; private set; }
    public Guid PreparedByUserId { get; private set; }
    public DateTime SavedAt { get; private set; }

    // ── Individual applicant — farm/site assessment ────────────────────────
    public string? FarmLocationDescription { get; private set; }
    public string? GpsCoordinates { get; private set; }
    public decimal? LandAreaAssessedHectares { get; private set; }
    public NampSoilConditionRating? SoilConditionRating { get; private set; }
    public string? WaterSourceAvailability { get; private set; }

    // ── AgroServiceCompany — operational assessment ────────────────────────
    public string? OperationalCoverageArea { get; private set; }
    public int? ExistingFleetSize { get; private set; }
    public bool? HasMaintenanceFacility { get; private set; }
    public int? TechnicalStaffCount { get; private set; }
    public int? YearsInOperation { get; private set; }
    public string? CurrentServiceContracts { get; private set; }
    public string? StorageFacilityDescription { get; private set; }

    // ── Shared ────────────────────────────────────────────────────────────
    public string ProposedEquipmentSuitability { get; private set; } = string.Empty;
    public string? InfrastructureNotes { get; private set; }
    public string? RisksIdentified { get; private set; }
    public string? RecommendedMitigations { get; private set; }
    public NampViabilityRating OverallViabilityRating { get; private set; }
    public NampTechnicalRecommendation EngineerRecommendation { get; private set; }
    public string? SummaryNotes { get; private set; }

    private NampTechnicalAppraisalReport() { }

    // ── Factory for individual (farmer) applicants ─────────────────────────
    public static Result<NampTechnicalAppraisalReport> CreateForIndividual(
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
                "A farm rated 'Not Viable' cannot be recommended as Pass.");

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

    // ── Factory for AgroServiceCompany applicants ─────────────────────────
    public static Result<NampTechnicalAppraisalReport> CreateForCompany(
        Guid nampApplicationId,
        Guid preparedByUserId,
        string operationalCoverageArea,
        int? existingFleetSize,
        bool? hasMaintenanceFacility,
        int? technicalStaffCount,
        int? yearsInOperation,
        string? currentServiceContracts,
        string? storageFacilityDescription,
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
                "A company rated 'Not Viable' cannot be recommended as Pass.");

        if (string.IsNullOrWhiteSpace(operationalCoverageArea))
            return Result.Failure<NampTechnicalAppraisalReport>("Operational coverage area is required.");
        if (string.IsNullOrWhiteSpace(proposedEquipmentSuitability))
            return Result.Failure<NampTechnicalAppraisalReport>("Proposed equipment suitability assessment is required.");

        return Result.Success(new NampTechnicalAppraisalReport
        {
            NampApplicationId = nampApplicationId,
            PreparedByUserId = preparedByUserId,
            SavedAt = DateTime.UtcNow,
            OperationalCoverageArea = operationalCoverageArea,
            ExistingFleetSize = existingFleetSize,
            HasMaintenanceFacility = hasMaintenanceFacility,
            TechnicalStaffCount = technicalStaffCount,
            YearsInOperation = yearsInOperation,
            CurrentServiceContracts = currentServiceContracts,
            StorageFacilityDescription = storageFacilityDescription,
            ProposedEquipmentSuitability = proposedEquipmentSuitability,
            InfrastructureNotes = infrastructureNotes,
            RisksIdentified = risksIdentified,
            RecommendedMitigations = recommendedMitigations,
            OverallViabilityRating = overallViabilityRating,
            EngineerRecommendation = engineerRecommendation,
            SummaryNotes = summaryNotes,
        });
    }

    // ── Update for individual applicants ──────────────────────────────────
    public Result UpdateForIndividual(
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
            return Result.Failure("A farm rated 'Not Viable' cannot be recommended as Pass.");

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
        // Clear company fields
        OperationalCoverageArea = null; ExistingFleetSize = null; HasMaintenanceFacility = null;
        TechnicalStaffCount = null; YearsInOperation = null; CurrentServiceContracts = null;
        StorageFacilityDescription = null;
        ProposedEquipmentSuitability = proposedEquipmentSuitability;
        InfrastructureNotes = infrastructureNotes;
        RisksIdentified = risksIdentified;
        RecommendedMitigations = recommendedMitigations;
        OverallViabilityRating = overallViabilityRating;
        EngineerRecommendation = engineerRecommendation;
        SummaryNotes = summaryNotes;
        return Result.Success();
    }

    // ── Update for AgroServiceCompany applicants ──────────────────────────
    public Result UpdateForCompany(
        Guid updatedByUserId,
        string operationalCoverageArea,
        int? existingFleetSize,
        bool? hasMaintenanceFacility,
        int? technicalStaffCount,
        int? yearsInOperation,
        string? currentServiceContracts,
        string? storageFacilityDescription,
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
            return Result.Failure("A company rated 'Not Viable' cannot be recommended as Pass.");

        if (string.IsNullOrWhiteSpace(operationalCoverageArea))
            return Result.Failure("Operational coverage area is required.");
        if (string.IsNullOrWhiteSpace(proposedEquipmentSuitability))
            return Result.Failure("Proposed equipment suitability assessment is required.");

        PreparedByUserId = updatedByUserId;
        SavedAt = DateTime.UtcNow;
        OperationalCoverageArea = operationalCoverageArea;
        ExistingFleetSize = existingFleetSize;
        HasMaintenanceFacility = hasMaintenanceFacility;
        TechnicalStaffCount = technicalStaffCount;
        YearsInOperation = yearsInOperation;
        CurrentServiceContracts = currentServiceContracts;
        StorageFacilityDescription = storageFacilityDescription;
        // Clear individual fields
        FarmLocationDescription = null; GpsCoordinates = null; LandAreaAssessedHectares = null;
        SoilConditionRating = null; WaterSourceAvailability = null;
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
