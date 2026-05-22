using CRMS.Application.Common;
using CRMS.Application.Namp.DTOs;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Commands;

public record SaveNampTechnicalAppraisalReportCommand(
    Guid NampApplicationId,
    Guid UserId,
    string ApplicantCategory,
    // Individual fields
    string? FarmLocationDescription,
    string? GpsCoordinates,
    decimal? LandAreaAssessedHectares,
    string? SoilConditionRating,
    string? WaterSourceAvailability,
    // AgroServiceCompany fields
    string? OperationalCoverageArea,
    int? ExistingFleetSize,
    bool? HasMaintenanceFacility,
    int? TechnicalStaffCount,
    int? YearsInOperation,
    string? CurrentServiceContracts,
    string? StorageFacilityDescription,
    // Shared fields
    string ProposedEquipmentSuitability,
    string? InfrastructureNotes,
    string? RisksIdentified,
    string? RecommendedMitigations,
    string OverallViabilityRating,
    string EngineerRecommendation,
    string? SummaryNotes
) : IRequest<ApplicationResult<NampTechnicalAppraisalReportDto>>;

public class SaveNampTechnicalAppraisalReportHandler
    : IRequestHandler<SaveNampTechnicalAppraisalReportCommand, ApplicationResult<NampTechnicalAppraisalReportDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public SaveNampTechnicalAppraisalReportHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampTechnicalAppraisalReportDto>> Handle(
        SaveNampTechnicalAppraisalReportCommand request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<NampViabilityRating>(request.OverallViabilityRating, ignoreCase: true, out var viability))
            return ApplicationResult<NampTechnicalAppraisalReportDto>.Failure($"Unknown viability rating: '{request.OverallViabilityRating}'.");
        if (!Enum.TryParse<NampTechnicalRecommendation>(request.EngineerRecommendation, ignoreCase: true, out var recommendation))
            return ApplicationResult<NampTechnicalAppraisalReportDto>.Failure($"Unknown recommendation: '{request.EngineerRecommendation}'.");

        bool isCompany = request.ApplicantCategory == "AgroServiceCompany";

        var existing = await _repo.GetTechnicalAppraisalReportAsync(request.NampApplicationId, ct);

        if (existing is null)
        {
            var createResult = isCompany
                ? NampTechnicalAppraisalReport.CreateForCompany(
                    request.NampApplicationId, request.UserId,
                    request.OperationalCoverageArea ?? string.Empty,
                    request.ExistingFleetSize, request.HasMaintenanceFacility,
                    request.TechnicalStaffCount, request.YearsInOperation,
                    request.CurrentServiceContracts, request.StorageFacilityDescription,
                    request.ProposedEquipmentSuitability,
                    request.InfrastructureNotes, request.RisksIdentified, request.RecommendedMitigations,
                    viability, recommendation, request.SummaryNotes)
                : CreateForIndividual(request, viability, recommendation);

            if (createResult.IsFailure)
                return ApplicationResult<NampTechnicalAppraisalReportDto>.Failure(createResult.Error);

            await _repo.AddTechnicalAppraisalReportAsync(createResult.Value, ct);
            await _uow.SaveChangesAsync(ct);
            return ApplicationResult<NampTechnicalAppraisalReportDto>.Success(MapToDto(createResult.Value));
        }
        else
        {
            var updateResult = isCompany
                ? existing.UpdateForCompany(
                    request.UserId,
                    request.OperationalCoverageArea ?? string.Empty,
                    request.ExistingFleetSize, request.HasMaintenanceFacility,
                    request.TechnicalStaffCount, request.YearsInOperation,
                    request.CurrentServiceContracts, request.StorageFacilityDescription,
                    request.ProposedEquipmentSuitability,
                    request.InfrastructureNotes, request.RisksIdentified, request.RecommendedMitigations,
                    viability, recommendation, request.SummaryNotes)
                : UpdateForIndividual(existing, request, viability, recommendation);

            if (updateResult.IsFailure)
                return ApplicationResult<NampTechnicalAppraisalReportDto>.Failure(updateResult.Error);

            await _uow.SaveChangesAsync(ct);
            return ApplicationResult<NampTechnicalAppraisalReportDto>.Success(MapToDto(existing));
        }
    }

    private static Result<NampTechnicalAppraisalReport> CreateForIndividual(
        SaveNampTechnicalAppraisalReportCommand request,
        NampViabilityRating viability,
        NampTechnicalRecommendation recommendation)
    {
        if (!Enum.TryParse<NampSoilConditionRating>(request.SoilConditionRating ?? "", ignoreCase: true, out var soil))
            return Result.Failure<NampTechnicalAppraisalReport>($"Unknown soil condition rating: '{request.SoilConditionRating}'.");

        return NampTechnicalAppraisalReport.CreateForIndividual(
            request.NampApplicationId, request.UserId,
            request.FarmLocationDescription ?? string.Empty,
            request.GpsCoordinates, request.LandAreaAssessedHectares,
            soil, request.WaterSourceAvailability ?? string.Empty,
            request.ProposedEquipmentSuitability,
            request.InfrastructureNotes, request.RisksIdentified, request.RecommendedMitigations,
            viability, recommendation, request.SummaryNotes);
    }

    private static Result UpdateForIndividual(
        NampTechnicalAppraisalReport existing,
        SaveNampTechnicalAppraisalReportCommand request,
        NampViabilityRating viability,
        NampTechnicalRecommendation recommendation)
    {
        if (!Enum.TryParse<NampSoilConditionRating>(request.SoilConditionRating ?? "", ignoreCase: true, out var soil))
            return Result.Failure($"Unknown soil condition rating: '{request.SoilConditionRating}'.");

        return existing.UpdateForIndividual(
            request.UserId,
            request.FarmLocationDescription ?? string.Empty,
            request.GpsCoordinates, request.LandAreaAssessedHectares,
            soil, request.WaterSourceAvailability ?? string.Empty,
            request.ProposedEquipmentSuitability,
            request.InfrastructureNotes, request.RisksIdentified, request.RecommendedMitigations,
            viability, recommendation, request.SummaryNotes);
    }

    internal static NampTechnicalAppraisalReportDto MapToDto(NampTechnicalAppraisalReport r) => new(
        r.Id, r.NampApplicationId, r.PreparedByUserId, r.SavedAt,
        r.FarmLocationDescription, r.GpsCoordinates, r.LandAreaAssessedHectares,
        r.SoilConditionRating?.ToString(), r.WaterSourceAvailability,
        r.OperationalCoverageArea, r.ExistingFleetSize, r.HasMaintenanceFacility,
        r.TechnicalStaffCount, r.YearsInOperation, r.CurrentServiceContracts, r.StorageFacilityDescription,
        r.ProposedEquipmentSuitability, r.InfrastructureNotes,
        r.RisksIdentified, r.RecommendedMitigations,
        r.OverallViabilityRating.ToString(), r.EngineerRecommendation.ToString(), r.SummaryNotes);
}
