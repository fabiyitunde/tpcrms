using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>Per-subject bureau reports for a case — the company plus every checked director.
/// Replaces reading the flat summary fields off the profile.</summary>
public record GetRhshfBureauReportsQuery(string Reference) : IRequest<ApplicationResult<List<RhshfBureauReportDto>>>;

public record RhshfBureauReportDto(
    Guid Id,
    string SubjectName,
    /// <summary>"RhshfCompany" or "RhshfDirector" — drives the tab's sectioning.</summary>
    string SubjectType,
    string? Bvn,
    string Status,
    int? CreditScore,
    string? ScoreGrade,
    int DelinquentFacilities,
    int ActiveLoans,
    int TotalAccounts,
    decimal TotalOutstanding,
    decimal TotalOverdue,
    int MaxDelinquencyDays,
    bool HasLegalActions,
    int? FraudRiskScore,
    string? FraudRecommendation,
    string? ErrorMessage,
    string? RawResponseJson,
    DateTime CreatedAt);

public class GetRhshfBureauReportsHandler
    : IRequestHandler<GetRhshfBureauReportsQuery, ApplicationResult<List<RhshfBureauReportDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IBureauReportRepository _bureauRepo;

    public GetRhshfBureauReportsHandler(IRhshfCreditProfileRepository repo, IBureauReportRepository bureauRepo)
    {
        _repo = repo;
        _bureauRepo = bureauRepo;
    }

    public async Task<ApplicationResult<List<RhshfBureauReportDto>>> Handle(
        GetRhshfBureauReportsQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<List<RhshfBureauReportDto>>.Failure("Case not found.");

        var reports = await _bureauRepo.GetByRhshfCreditProfileIdAsync(profile.Id, ct);

        var dtos = reports
            .OrderBy(r => r.SubjectType == Domain.Enums.SubjectType.Business ? 0 : 1)
            .ThenBy(r => r.SubjectName)
            .Select(r => new RhshfBureauReportDto(
                r.Id,
                r.SubjectName,
                r.PartyType ?? (r.SubjectType == Domain.Enums.SubjectType.Business ? "RhshfCompany" : "RhshfDirector"),
                r.BVN,
                r.Status.ToString(),
                r.CreditScore,
                r.ScoreGrade,
                r.DelinquentFacilities,
                r.ActiveLoans,
                r.TotalAccounts,
                r.TotalOutstandingBalance,
                r.TotalOverdue,
                r.MaxDelinquencyDays,
                r.HasLegalActions,
                r.FraudRiskScore,
                r.FraudRecommendation,
                r.ErrorMessage,
                r.RawResponseJson,
                r.CreatedAt))
            .ToList();

        return ApplicationResult<List<RhshfBureauReportDto>>.Success(dtos);
    }
}
