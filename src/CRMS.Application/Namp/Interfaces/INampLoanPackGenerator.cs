using CRMS.Application.Namp.DTOs;

namespace CRMS.Application.Namp.Interfaces;

public interface INampLoanPackGenerator
{
    Task<byte[]> GenerateAsync(NampLoanPackData data, CancellationToken ct = default);
}

public record NampLoanPackData(
    // Identity
    string ApplicationNumber,
    string ApplicationReference,
    string Status,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? RatifiedAt,
    string? RatifiedByUserName,
    DateTime GeneratedAt,
    string GeneratedBy,
    string BankName,
    string BranchName,
    // Applicant
    string ApplicantName,
    string BoaAccountNumber,
    string? BoaAccountName,
    string ApplicantCategory,
    string? ApplicantPhone,
    string? ApplicantEmail,
    string? Nin,
    string? Bvn,
    string? DateOfBirth,
    string? StateOfResidence,
    string? LocalGovernmentArea,
    string? Occupation,
    string? EmployerName,
    string? EmploymentStatus,
    int? YearsOfExperience,
    int? NumberOfDependants,
    decimal? EstimatedMonthlyIncome,
    decimal? MonthlyLivingExpenses,
    decimal? EstimatedNetWorth,
    string? ExistingLoanObligations,
    string? CompanyName,
    string? RcNumber,
    string? IndustrySector,
    // CAC company profile (Agro-Service)
    string? CacStatus,
    string? CacEntityType,
    string? CacRegistrationDate,
    string? CacNatureOfBusiness,
    decimal? CacShareCapital,
    string? CacAddress,
    DateTime? CacFetchedAt,
    // Equipment & Finance
    string EquipmentDescription,
    decimal EquipmentValue,
    string? LoanPurpose,
    decimal? EquityPercent,
    decimal? EquityAmount,
    decimal? LoanAmount,
    int? RequestedTenorMonths,
    decimal? ApprovedInterestRate,
    // Committee
    string CommitteeTier,
    string? CommitteeDecision,
    int CommitteeApprovalVotes,
    int CommitteeRejectionVotes,
    int CommitteeAbstainVotes,
    int CommitteeMinimumApprovalVotes,
    int CommitteeRequiredVotes,
    DateTime? CommitteeDecisionAt,
    string? CommitteeDecisionNote,
    string? CommitteeConditions,
    List<NampLoanPackMemberVote> CommitteeMembers,
    // Appraisals
    NampFinancialAppraisalReportDto? FinancialAppraisal,
    // Collections
    List<NampDirectorDto> Directors,
    List<NampGuarantorDto> Guarantors,
    List<NampCollateralDto> Collaterals,
    List<NampBureauReportDto> BureauReports,
    List<NampFinancialStatementDto> FinancialStatements,
    List<NampLoanPackStatusEntry> StatusHistory,
    List<NampDocumentDto> Documents
);

public record NampLoanPackMemberVote(
    string UserName,
    string Role,
    bool IsChairperson,
    string? Vote,
    string? VoteComment,
    DateTime? VotedAt
);

public record NampLoanPackStatusEntry(
    string Status,
    DateTime ChangedAt,
    string UserName,
    string? Note
);
