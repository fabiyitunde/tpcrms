using CRMS.Application.Common;
using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CRMS.Application.CreditBureau.Commands;

public record ProcessNampCreditChecksCommand(
    Guid NampApplicationId,
    Guid SystemUserId,
    bool ForceRefresh = false
) : IRequest<ApplicationResult<NampCreditCheckBatchResultDto>>;

public record NampCreditCheckBatchResultDto(
    Guid NampApplicationId,
    int TotalChecks,
    int Successful,
    int Failed,
    List<IndividualCreditCheckResultDto> Results
);

public class ProcessNampCreditChecksHandler
    : IRequestHandler<ProcessNampCreditChecksCommand, ApplicationResult<NampCreditCheckBatchResultDto>>
{
    private readonly INampApplicationRepository _nampRepo;
    private readonly IBureauReportRepository _bureauRepo;
    private readonly ISmartComplyProvider _smartComply;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ProcessNampCreditChecksHandler> _logger;

    public ProcessNampCreditChecksHandler(
        INampApplicationRepository nampRepo,
        IBureauReportRepository bureauRepo,
        ISmartComplyProvider smartComply,
        IUnitOfWork uow,
        ILogger<ProcessNampCreditChecksHandler> logger)
    {
        _nampRepo = nampRepo;
        _bureauRepo = bureauRepo;
        _smartComply = smartComply;
        _uow = uow;
        _logger = logger;
    }

    public async Task<ApplicationResult<NampCreditCheckBatchResultDto>> Handle(
        ProcessNampCreditChecksCommand request, CancellationToken ct = default)
    {
        var app = await _nampRepo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null)
            return ApplicationResult<NampCreditCheckBatchResultDto>.Failure("NAMP application not found.");

        if (app.Status != NampApplicationStatus.FinancialAppraisal && app.Status != NampApplicationStatus.Submitted)
            return ApplicationResult<NampCreditCheckBatchResultDto>.Failure(
                $"NAMP application must be in Submitted or FinancialAppraisal status for credit checks. Current: {app.Status}");

        // Build subject list: applicant + guarantors (each with a BVN)
        var subjects = new List<(string Name, string Bvn, Guid? GuarantorId)>();
        if (!string.IsNullOrWhiteSpace(app.Bvn))
            subjects.Add((app.ApplicantName, app.Bvn, null));

        foreach (var g in app.Guarantors.Where(g => !string.IsNullOrWhiteSpace(g.Bvn)))
            subjects.Add((g.FullName, g.Bvn!, g.Id));

        if (subjects.Count == 0)
        {
            _logger.LogWarning("NAMP application {Id} has no BVNs to check (applicant or guarantors).", request.NampApplicationId);
            return ApplicationResult<NampCreditCheckBatchResultDto>.Success(
                new NampCreditCheckBatchResultDto(request.NampApplicationId, 0, 0, 0, []));
        }

        // Idempotency: load existing reports
        var existingReports = await _bureauRepo.GetByNampApplicationIdAsync(request.NampApplicationId, ct);

        var results = new List<IndividualCreditCheckResultDto>();
        int successful = 0, failed = 0;

        foreach (var (name, bvn, guarantorId) in subjects)
        {
            // Skip if already completed and not forcing refresh
            if (!request.ForceRefresh && existingReports.Any(r =>
                r.BVN == bvn &&
                r.Status == BureauReportStatus.Completed &&
                r.NampApplicationId == request.NampApplicationId))
            {
                var existing = existingReports.First(r => r.BVN == bvn && r.NampApplicationId == request.NampApplicationId);
                results.Add(new IndividualCreditCheckResultDto(
                    guarantorId ?? Guid.Empty, name, guarantorId.HasValue ? "Guarantor" : "Applicant",
                    bvn, true, existing.Id, existing.CreditScore, existing.ScoreGrade,
                    existing.DelinquentFacilities > 0, existing.FraudRiskScore, existing.FraudRecommendation, null));
                successful++;
                continue;
            }

            try
            {
                var reportResult = BureauReport.Create(
                    CreditBureauProvider.CRC,
                    SubjectType.Individual,
                    name,
                    bvn,
                    request.SystemUserId,
                    loanApplicationId: null,
                    nampApplicationId: request.NampApplicationId,
                    partyId: guarantorId,
                    partyType: guarantorId.HasValue ? "NampGuarantor" : "NampApplicant");

                if (reportResult.IsFailure)
                {
                    results.Add(new IndividualCreditCheckResultDto(
                        guarantorId ?? Guid.Empty, name, guarantorId.HasValue ? "Guarantor" : "Applicant",
                        bvn, false, null, null, null, false, null, null, reportResult.Error));
                    failed++;
                    continue;
                }

                var bureauReport = reportResult.Value;
                bureauReport.MarkProcessing();
                await _bureauRepo.AddAsync(bureauReport, ct);
                await _uow.SaveChangesAsync(ct);

                // Run the credit check via SmartComply (CRC Full)
                var checkResult = await _smartComply.GetCRCFullAsync(bvn, ct);
                if (checkResult.IsSuccess)
                {
                    var report = checkResult.Value;
                    var summary = report.Summary;

                    // Fetch bureau score
                    int creditScore;
                    string scoreGrade;
                    var scoreResult = await _smartComply.GetCRCScoreAsync(bvn, ct);
                    if (scoreResult.IsSuccess && scoreResult.Value.Score > 0)
                    {
                        creditScore = scoreResult.Value.Score;
                        scoreGrade = scoreResult.Value.Grade ?? GetScoreGrade(creditScore);
                    }
                    else
                    {
                        creditScore = 600; // default derived
                        scoreGrade = "DERIVED";
                    }

                    bureauReport.CompleteWithData(
                        report.Id ?? bvn,
                        creditScore,
                        scoreGrade,
                        report.SearchedDate ?? DateTime.UtcNow,
                        JsonSerializer.Serialize(report),
                        null,
                        summary.TotalNoOfLoans,
                        summary.TotalNoOfActiveLoans,
                        summary.TotalNoOfPerformingLoans,
                        summary.TotalNoOfDelinquentFacilities,
                        summary.TotalNoOfClosedLoans,
                        summary.TotalOutstanding,
                        summary.TotalOverdue,
                        summary.HighestLoanAmount,
                        summary.MaxNoOfDays,
                        false);

                    successful++;
                    results.Add(new IndividualCreditCheckResultDto(
                        guarantorId ?? Guid.Empty, name, guarantorId.HasValue ? "Guarantor" : "Applicant",
                        bvn, true, bureauReport.Id, bureauReport.CreditScore, bureauReport.ScoreGrade,
                        bureauReport.DelinquentFacilities > 0, bureauReport.FraudRiskScore, bureauReport.FraudRecommendation, null));
                }
                else
                {
                    var isNotFound = checkResult.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true;
                    if (isNotFound)
                        bureauReport.MarkNotFound();
                    else
                        bureauReport.MarkFailed(checkResult.Error ?? "Unknown error");
                    failed++;
                    results.Add(new IndividualCreditCheckResultDto(
                        guarantorId ?? Guid.Empty, name, guarantorId.HasValue ? "Guarantor" : "Applicant",
                        bvn, false, bureauReport.Id, null, null, false, null, null, checkResult.Error));
                }

                await _uow.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running credit check for NAMP subject {Name} ({BVN})", name, bvn);
                results.Add(new IndividualCreditCheckResultDto(
                    guarantorId ?? Guid.Empty, name, guarantorId.HasValue ? "Guarantor" : "Applicant",
                    bvn, false, null, null, null, false, null, null, ex.Message));
                failed++;
            }
        }

        return ApplicationResult<NampCreditCheckBatchResultDto>.Success(
            new NampCreditCheckBatchResultDto(request.NampApplicationId, subjects.Count, successful, failed, results));
    }

    private static string GetScoreGrade(int score) => score switch
    {
        >= 750 => "A+",
        >= 700 => "A",
        >= 650 => "B",
        >= 600 => "C",
        >= 550 => "D",
        _ => "E"
    };
}
