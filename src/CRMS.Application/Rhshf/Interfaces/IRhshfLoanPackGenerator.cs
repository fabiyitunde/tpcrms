using CRMS.Application.Rhshf.DTOs;
using CRMS.Application.Rhshf.Queries;

namespace CRMS.Application.Rhshf.Interfaces;

/// <summary>
/// Generates the RH-SHF Loan Pack — the consolidated case dossier an approving tier reviews and files,
/// mirroring NAMP's and corporate's loan packs but for RH-SHF's data (EOP input package, crop-economics
/// appraisal, per-subject bureau, collateral/guarantors, committee votes, ratification, disbursement,
/// AI advisory, and the audit trail). It composes the existing section DTOs rather than re-deriving
/// them, so the pack always matches what the Detail tabs show.
/// </summary>
public interface IRhshfLoanPackGenerator
{
    Task<byte[]> GenerateAsync(RhshfLoanPackData data, CancellationToken ct = default);
}

public record RhshfLoanPackData(
    string BankName,
    string GeneratedBy,
    DateTime GeneratedAt,
    RhshfCaseWorkspaceDto Workspace,
    RhshfDirectorsDto? Directors,
    IReadOnlyList<RhshfBureauReportDto> BureauReports,
    RhshfFinancialAppraisalDto? FinancialAppraisal,
    IReadOnlyList<RhshfCollateralDto> Collaterals,
    IReadOnlyList<RhshfGuarantorDto> Guarantors,
    RhshfCommitteeReviewDto? CommitteeReview,
    RhshfOfferDto? Offer,
    IReadOnlyList<RhshfDisbursementDto> Disbursements,
    RhshfAdvisoryDto? Advisory,
    IReadOnlyList<RhshfStatusHistoryDto> StatusHistory);
