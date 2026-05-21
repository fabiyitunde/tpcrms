using CRMS.Application.Common;
using CRMS.Application.Namp.DTOs;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Commands;

public record SaveNampTechnicalAppraisalReportCommand(
    Guid NampApplicationId,
    Guid UserId,
    string FarmLocationDescription,
    string? GpsCoordinates,
    decimal? LandAreaAssessedHectares,
    string SoilConditionRating,
    string WaterSourceAvailability,
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
        if (!Enum.TryParse<NampSoilConditionRating>(request.SoilConditionRating, ignoreCase: true, out var soil))
            return ApplicationResult<NampTechnicalAppraisalReportDto>.Failure($"Unknown soil condition rating: '{request.SoilConditionRating}'.");
        if (!Enum.TryParse<NampViabilityRating>(request.OverallViabilityRating, ignoreCase: true, out var viability))
            return ApplicationResult<NampTechnicalAppraisalReportDto>.Failure($"Unknown viability rating: '{request.OverallViabilityRating}'.");
        if (!Enum.TryParse<NampTechnicalRecommendation>(request.EngineerRecommendation, ignoreCase: true, out var recommendation))
            return ApplicationResult<NampTechnicalAppraisalReportDto>.Failure($"Unknown recommendation: '{request.EngineerRecommendation}'.");

        var existing = await _repo.GetTechnicalAppraisalReportAsync(request.NampApplicationId, ct);

        if (existing is null)
        {
            var createResult = NampTechnicalAppraisalReport.Create(
                request.NampApplicationId, request.UserId,
                request.FarmLocationDescription, request.GpsCoordinates, request.LandAreaAssessedHectares,
                soil, request.WaterSourceAvailability, request.ProposedEquipmentSuitability,
                request.InfrastructureNotes, request.RisksIdentified, request.RecommendedMitigations,
                viability, recommendation, request.SummaryNotes);

            if (createResult.IsFailure)
                return ApplicationResult<NampTechnicalAppraisalReportDto>.Failure(createResult.Error);

            await _repo.AddTechnicalAppraisalReportAsync(createResult.Value, ct);
            await _uow.SaveChangesAsync(ct);
            return ApplicationResult<NampTechnicalAppraisalReportDto>.Success(MapToDto(createResult.Value));
        }
        else
        {
            var updateResult = existing.Update(
                request.UserId,
                request.FarmLocationDescription, request.GpsCoordinates, request.LandAreaAssessedHectares,
                soil, request.WaterSourceAvailability, request.ProposedEquipmentSuitability,
                request.InfrastructureNotes, request.RisksIdentified, request.RecommendedMitigations,
                viability, recommendation, request.SummaryNotes);

            if (updateResult.IsFailure)
                return ApplicationResult<NampTechnicalAppraisalReportDto>.Failure(updateResult.Error);

            await _uow.SaveChangesAsync(ct);
            return ApplicationResult<NampTechnicalAppraisalReportDto>.Success(MapToDto(existing));
        }
    }

    internal static NampTechnicalAppraisalReportDto MapToDto(NampTechnicalAppraisalReport r) => new(
        r.Id, r.NampApplicationId, r.PreparedByUserId, r.SavedAt,
        r.FarmLocationDescription, r.GpsCoordinates, r.LandAreaAssessedHectares,
        r.SoilConditionRating.ToString(), r.WaterSourceAvailability,
        r.ProposedEquipmentSuitability, r.InfrastructureNotes,
        r.RisksIdentified, r.RecommendedMitigations,
        r.OverallViabilityRating.ToString(), r.EngineerRecommendation.ToString(), r.SummaryNotes);
}
