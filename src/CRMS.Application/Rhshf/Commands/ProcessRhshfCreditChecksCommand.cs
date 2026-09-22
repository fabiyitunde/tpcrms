using System.Text.Json;
using CRMS.Application.Common;
using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Runs per-subject credit bureau checks for a RH-SHF case: the FAC company (by RC number) plus
/// every director carrying a BVN. Replaces the original single flat company-level check that lived
/// on RhshfCreditProfile.
///
/// Mirrors ProcessNampCreditChecksCommand's structure (dedupe by BVN, idempotent on completed
/// reports, ForceRefresh deletes first) with one deliberate departure: NAMP fabricates a
/// creditScore of 600 with grade "DERIVED" when the score lookup fails. That invents data the
/// bureau never returned, and "DERIVED" isn't even handled by NAMP's own grade-badge switch so it
/// renders as a failure. Here an unavailable score stays null and the UI shows "N/A".
/// </summary>
public record ProcessRhshfCreditChecksCommand(string Reference, Guid SystemUserId, bool ForceRefresh = false)
    : IRequest<ApplicationResult<RhshfCreditCheckBatchDto>>;

public record RhshfCreditCheckBatchDto(int Requested, int Successful, int Failed, int Skipped);

public class ProcessRhshfCreditChecksHandler
    : IRequestHandler<ProcessRhshfCreditChecksCommand, ApplicationResult<RhshfCreditCheckBatchDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IBureauReportRepository _bureauRepo;
    private readonly ISmartComplyProvider _smartComply;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ProcessRhshfCreditChecksHandler> _logger;

    public ProcessRhshfCreditChecksHandler(
        IRhshfCreditProfileRepository repo, IBureauReportRepository bureauRepo,
        ISmartComplyProvider smartComply, IUnitOfWork uow, ILogger<ProcessRhshfCreditChecksHandler> logger)
    {
        _repo = repo;
        _bureauRepo = bureauRepo;
        _smartComply = smartComply;
        _uow = uow;
        _logger = logger;
    }

    public async Task<ApplicationResult<RhshfCreditCheckBatchDto>> Handle(
        ProcessRhshfCreditChecksCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfCreditCheckBatchDto>.Failure("Case not found.");

        var existing = await _bureauRepo.GetByRhshfCreditProfileIdAsync(profile.Id, ct);

        if (request.ForceRefresh)
        {
            foreach (var report in existing)
                _bureauRepo.Delete(report);
            await _uow.SaveChangesAsync(ct);
            existing = [];
        }

        // Subject list: every director with a BVN, deduped (a person listed twice keeps one check).
        var subjects = profile.GetDirectorsWithBvn()
            .Select(d => (Name: d.FullName, Bvn: d.Bvn!.Trim(), PartyId: (Guid?)d.Id))
            .GroupBy(s => s.Bvn, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var hasBusinessCheck = !string.IsNullOrWhiteSpace(profile.RcNumber);
        if (subjects.Count == 0 && !hasBusinessCheck)
            return ApplicationResult<RhshfCreditCheckBatchDto>.Success(new RhshfCreditCheckBatchDto(0, 0, 0, 0));

        int successful = 0, failed = 0, skipped = 0;

        // ── Directors (individual, CRC by BVN) ────────────────────────────
        foreach (var (name, bvn, partyId) in subjects)
        {
            // Idempotent: a completed report for this BVN already satisfies the requirement.
            if (existing.Any(r => string.Equals(r.BVN, bvn, StringComparison.OrdinalIgnoreCase)
                                  && r.Status == BureauReportStatus.Completed))
            {
                skipped++;
                continue;
            }

            try
            {
                var reportResult = BureauReport.Create(
                    CreditBureauProvider.CRC, SubjectType.Individual, name, bvn, request.SystemUserId,
                    loanApplicationId: null, nampApplicationId: null,
                    partyId: partyId, partyType: "RhshfDirector", rhshfCreditProfileId: profile.Id);

                if (reportResult.IsFailure) { failed++; continue; }

                var bureauReport = reportResult.Value;
                bureauReport.MarkProcessing();
                await _bureauRepo.AddAsync(bureauReport, ct);
                await _uow.SaveChangesAsync(ct);

                var checkResult = await _smartComply.GetCRCFullAsync(bvn, ct);
                if (checkResult.IsSuccess)
                {
                    var report = checkResult.Value;
                    var summary = report.Summary;

                    // Score is best-effort. If the bureau has no score for this BVN we record null
                    // rather than inventing one — see this class's doc comment.
                    int? creditScore = null;
                    string? scoreGrade = null;
                    var scoreResult = await _smartComply.GetCRCScoreAsync(bvn, ct);
                    if (scoreResult.IsSuccess && scoreResult.Value.Score > 0)
                    {
                        creditScore = scoreResult.Value.Score;
                        scoreGrade = scoreResult.Value.Grade ?? GetScoreGrade(scoreResult.Value.Score);
                    }

                    bureauReport.CompleteWithData(
                        report.Id ?? bvn, creditScore, scoreGrade,
                        report.SearchedDate ?? DateTime.UtcNow,
                        JsonSerializer.Serialize(report), null,
                        summary.TotalNoOfLoans, summary.TotalNoOfActiveLoans, summary.TotalNoOfPerformingLoans,
                        summary.TotalNoOfDelinquentFacilities, summary.TotalNoOfClosedLoans,
                        summary.TotalOutstanding, summary.TotalOverdue, summary.HighestLoanAmount,
                        summary.MaxNoOfDays, false);
                    successful++;
                }
                else
                {
                    MarkOutcome(bureauReport, checkResult.Error);
                    failed++;
                }

                await _uow.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running RH-SHF credit check for {Name} ({BVN})", name, bvn);
                failed++;
            }
        }

        // ── Company (business, CRC by RC number) ──────────────────────────
        if (hasBusinessCheck)
        {
            var rcNumber = profile.RcNumber.Trim();
            var alreadyDone = existing.Any(r => r.SubjectType == SubjectType.Business
                                                && r.Status == BureauReportStatus.Completed);
            if (alreadyDone)
            {
                skipped++;
            }
            else
            {
                try
                {
                    var reportResult = BureauReport.Create(
                        CreditBureauProvider.CRC, SubjectType.Business, profile.CompanyName, null, request.SystemUserId,
                        loanApplicationId: null, nampApplicationId: null, taxId: rcNumber,
                        partyId: null, partyType: "RhshfCompany", rhshfCreditProfileId: profile.Id);

                    if (reportResult.IsFailure)
                    {
                        failed++;
                    }
                    else
                    {
                        var bureauReport = reportResult.Value;
                        bureauReport.MarkProcessing();
                        await _bureauRepo.AddAsync(bureauReport, ct);
                        await _uow.SaveChangesAsync(ct);

                        var checkResult = await _smartComply.GetCRCBusinessHistoryAsync(rcNumber, ct);
                        if (checkResult.IsSuccess)
                        {
                            var report = checkResult.Value;
                            var summary = report.Summary;
                            bureauReport.CompleteWithData(
                                report.Id ?? rcNumber,
                                null, null, // business CRC returns no individual score/grade
                                report.SearchedDate ?? DateTime.UtcNow,
                                JsonSerializer.Serialize(report), null,
                                summary.TotalNoOfLoans, summary.TotalNoOfActiveLoans, summary.TotalNoOfPerformingLoans,
                                summary.TotalNoOfDelinquentFacilities, summary.TotalNoOfClosedLoans,
                                summary.TotalOutstanding, summary.TotalOverdue, summary.HighestLoanAmount,
                                0, false);

                            // Keep the profile's flat summary fields in step — the queue and the
                            // advisory still read them, so they must not drift from the report rows.
                            profile.RecordBureauCheck(
                                outcome: summary.TotalNoOfDelinquentFacilities > 0
                                    ? RhshfBureauOutcome.Flagged : RhshfBureauOutcome.Cleared,
                                totalLoans: summary.TotalNoOfLoans,
                                activeLoans: summary.TotalNoOfActiveLoans,
                                delinquentFacilities: summary.TotalNoOfDelinquentFacilities,
                                totalOutstanding: summary.TotalOutstanding,
                                totalOverdue: summary.TotalOverdue,
                                rawJson: JsonSerializer.Serialize(report));
                            successful++;
                        }
                        else
                        {
                            MarkOutcome(bureauReport, checkResult.Error);
                            profile.RecordBureauCheckFailure();
                            failed++;
                        }

                        await _uow.SaveChangesAsync(ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running RH-SHF business credit check for {Rc}", rcNumber);
                    failed++;
                }
            }
        }

        return ApplicationResult<RhshfCreditCheckBatchDto>.Success(
            new RhshfCreditCheckBatchDto(subjects.Count + (hasBusinessCheck ? 1 : 0), successful, failed, skipped));
    }

    /// <summary>"Not found" means the bureau has no file for this subject — a legitimate, displayable
    /// outcome, distinct from a call that actually failed.</summary>
    private static void MarkOutcome(BureauReport report, string? error)
    {
        if (error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            report.MarkNotFound();
        else
            report.MarkFailed(error ?? "Unknown error");
    }

    /// <summary>Single source of truth for score banding, shared with the UI's score-circle colours.
    /// NAMP duplicates this logic in its Razor file with different thresholds — the two disagree.</summary>
    public static string GetScoreGrade(int score) => score switch
    {
        >= 750 => "A+",
        >= 700 => "A",
        >= 650 => "B",
        >= 600 => "C",
        >= 550 => "D",
        _ => "E"
    };
}
