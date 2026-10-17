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
    NampAdvisoryDto? Advisory,
    // Collections
    List<NampDirectorDto> Directors,
    List<NampGuarantorDto> Guarantors,
    List<NampCollateralDto> Collaterals,
    List<NampBureauReportDto> BureauReports,
    List<NampFinancialStatementDto> FinancialStatements,
    List<NampLoanPackStatusEntry> StatusHistory,
    List<NampDocumentDto> Documents,
    // Revision trail
    /// <summary>Which trip through appraisal → risk → committee the case is on. 1 means it was never
    /// returned, and the revision sections below are then empty.</summary>
    int ReviewPassNumber = 1,
    /// <summary>Every appraisal and risk pass, oldest first. Without these the pack would show only
    /// the latest opinion at each stage, so a reader could not tell a revised case from a clean one
    /// — which is the whole reason a committee return is recorded.</summary>
    List<NampLoanPackStagePass>? StagePasses = null,
    /// <summary>Committee rounds closed by a return for rework, with the votes that were cast. Kept
    /// so the pack shows that a vote happened and was superseded, rather than quietly presenting the
    /// final round as the only one.</summary>
    List<NampLoanPackCommitteeRound>? SupersededRounds = null
);

/// <summary>One recorded pass of financial appraisal or risk review.</summary>
public record NampLoanPackStagePass(
    int PassNumber,
    string Stage,
    string Outcome,
    DateTime At,
    string UserName,
    string? Note
);

/// <summary>A committee round that was closed and superseded.</summary>
public record NampLoanPackCommitteeRound(
    string CommitteeType,
    string Status,
    string? FinalDecision,
    DateTime CirculatedAt,
    DateTime? ClosedAt,
    string? ClosureRationale,
    int ApprovalVotes,
    int RejectionVotes,
    int AbstainVotes,
    int PendingVotes,
    List<NampLoanPackMemberVote> Members
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
