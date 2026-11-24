using CRMS.Application.CustomerCheck;

namespace CRMS.Application.CreditCheck;

/// <summary>What a member of staff fills in on the Credit Check page.</summary>
/// <param name="SubjectType"><c>INDIVIDUAL</c> or <c>COMPANY</c>.</param>
/// <param name="Bvn">The person's BVN, for an individual check.</param>
/// <param name="RcNumber">The registration number, for a company check.</param>
/// <param name="CompanyName">The registered name, for a company check.</param>
/// <param name="Nubans">Optional account numbers at the bank.</param>
/// <param name="Sections">The sections ticked; empty means all of them.</param>
/// <param name="Purpose">Why the check is being run. Required.</param>
/// <param name="CaseReference">The case it is for, if any.</param>
public record StaffCreditCheckRequest(
    string SubjectType,
    string? Bvn,
    string? RcNumber,
    string? CompanyName,
    List<string>? Nubans,
    List<string>? Sections,
    decimal? LoanAmount,
    int? TenorMonths,
    decimal? AnnualRevenue,
    string? LoanPurpose,
    bool RunAmlCheck,
    string? Purpose,
    string? CaseReference);

/// <summary>One line of the history list.</summary>
public record StaffCreditCheckSummary(
    Guid Id,
    string SubjectType,
    string SubjectLabel,
    string? SubjectName,
    string Status,
    string RequestedByName,
    Guid RequestedByUserId,
    string Purpose,
    string? CaseReference,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    decimal? AdvisoryScore,
    string? AdvisoryRating,
    string? AdvisoryRecommendation,
    int WarningCount);

public record StaffCreditCheckPage(int Page, int PageSize, int Total, List<StaffCreditCheckSummary> Items);

/// <summary>One check in full: its header, and once finished, the result of its kind.</summary>
public record StaffCreditCheckView(
    StaffCreditCheckSummary Summary,
    IReadOnlyList<string> Sections,
    IReadOnlyDictionary<string, string> Outcomes,
    DateTime? StartedAt,
    int DurationMs,
    int BilledCalls,
    string? BureauUsed,
    string? ErrorMessage,
    CustomerCheckResponse? Individual,
    CompanyCheckResponse? Company);

/// <summary>Builds the branded PDF of a finished check.</summary>
public interface ICreditCheckPdfGenerator
{
    byte[] Generate(StaffCreditCheckView view, DateTime printedAt, string printedBy);
}

/// <summary>Wakes the intranet's staff-check worker when a check is queued.</summary>
public interface IStaffCreditCheckSignal
{
    void Nudge();
}
