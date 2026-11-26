using CRMS.Application.Namp.DTOs;
using CRMS.Application.Rhshf.Queries;

namespace CRMS.Application.CreditAppraisal;

// The stand-alone credit appraisal document, one per loan track. Each carries the case it belongs to
// and the appraisal exactly as saved; the generator turns it into a branded PDF.

/// <summary>The case an appraisal belongs to, as printed at the top of the document.</summary>
/// <param name="TrackLabel">"Corporate Loan", "NAMP Equipment Loan" or "RH-SHF Dry-Season Inputs".</param>
/// <param name="AmountLabel">What <paramref name="Amount"/> is: requested, approved, EOP value.</param>
public record AppraisalCaseInfo(
    string TrackLabel,
    string CaseNumber,
    string Borrower,
    string? Product,
    string AmountLabel,
    decimal? Amount,
    string Currency,
    int? TenorMonths,
    string Stage);

public record CorporateAppraisalDocument(
    AppraisalCaseInfo Case,
    decimal? Dscr,
    decimal? Leverage,
    decimal? CurrentRatio,
    decimal? Ltv,
    string? CapacityRating,
    string? Recommendation,
    string? Notes,
    string? MemoFileName,
    string? PreparedBy,
    DateTimeOffset? PreparedAt);

public record NampAppraisalDocument(
    AppraisalCaseInfo Case,
    NampFinancialAppraisalReportDto Report,
    string? DecisionNote,
    string? DecidedBy,
    DateTime? DecidedAt);

public record RhshfAppraisalDocument(
    AppraisalCaseInfo Case,
    RhshfFinancialAppraisalDto Appraisal);

/// <summary>Builds the branded credit appraisal PDF for each loan track.</summary>
public interface ICreditAppraisalPdfGenerator
{
    byte[] Corporate(CorporateAppraisalDocument document, string printedBy, DateTime printedAt);
    byte[] Namp(NampAppraisalDocument document, string printedBy, DateTime printedAt);
    byte[] Rhshf(RhshfAppraisalDocument document, string printedBy, DateTime printedAt);
}
