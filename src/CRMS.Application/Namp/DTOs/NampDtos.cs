namespace CRMS.Application.Namp.DTOs;

public record NampStagingRecordSummaryDto(
    Guid Id,
    string ApplicationReference,
    string ApplicantName,
    string BoaAccountNumber,
    string ApplicantCategory,
    string EquipmentDescription,
    decimal EquipmentValue,
    Guid? ResolvedBranchId,
    bool IsRecalled,
    DateTime ReceivedAt
);

public record NampStagingRecordDto(
    Guid Id,
    string ApplicationReference,
    string CrmsApplicationNumber,
    string ApplicantName,
    string BoaAccountNumber,
    string? ApplicantPhone,
    string? ApplicantEmail,
    string ApplicantCategory,
    string EquipmentDescription,
    decimal EquipmentValue,
    Guid? ResolvedBranchId,
    Guid? ResolvedOfficeId,
    Guid? ResolvedLocationId,
    string? BranchResolutionNote,
    bool IsRecalled,
    DateTime? RecalledAt,
    Guid? RecalledByUserId,
    DateTime ReceivedAt
);

public record NampApplicationSummaryDto(
    Guid Id,
    string ApplicationNumber,
    string ApplicationReference,
    string Status,
    string ApplicantName,
    string BoaAccountNumber,
    string ApplicantCategory,
    decimal EquipmentValue,
    string CommitteeTier,
    Guid BranchId,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    Guid? CurrentCommitteeReviewId = null
);

public record NampApplicationDto(
    Guid Id,
    string ApplicationNumber,
    string ApplicationReference,
    string Status,
    Guid LoanProductId,
    string ApplicantName,
    string BoaAccountNumber,
    string? ApplicantPhone,
    string? ApplicantEmail,
    string ApplicantCategory,
    string EquipmentDescription,
    decimal EquipmentValue,
    string? EquipmentCartJson,
    Guid BranchId,
    Guid? OfficeId,
    Guid? LocationId,
    string CommitteeTier,
    Guid RecalledByUserId,
    DateTime RecalledAt,
    DateTime? SubmittedAt,
    // Extended applicant info
    string? Nin,
    string? Bvn,
    string? BoaAccountName,
    string? LoanPurpose,
    string? IndustrySector,
    int? RequestedTenorMonths,
    string? DateOfBirth,
    string? StateOfResidence,
    string? LocalGovernmentArea,
    string? CompanyName,
    string? RcNumber,
    // Individual credit profile
    string? Occupation,
    string? EmployerName,
    string? EmploymentStatus,
    int? YearsOfExperience,
    int? NumberOfDependants,
    decimal? EstimatedMonthlyIncome,
    string? OtherIncomeSource,
    decimal? OtherMonthlyIncome,
    decimal? MonthlyLivingExpenses,
    string? OwnedAssetsDescription,
    decimal? EstimatedNetWorth,
    string? ExistingLoanObligations,
    // Equity & loan amounts
    decimal? EquityPercent,
    decimal? CartTotal,
    decimal? EquityAmount,
    decimal? LoanAmount,
    // Financial Appraisal
    Guid? FinancialAppraisalByUserId,
    DateTime? FinancialAppraisalAt,
    string? FinancialAppraisalNote,
    // Committee
    Guid? CurrentCommitteeReviewId,
    DateTime? CommitteeDecisionAt,
    string? CommitteeDecisionNote,
    // Ratification
    Guid? RatifiedByUserId,
    DateTime? RatifiedAt,
    // Offer
    string? OfferLetterStoragePath,
    DateTime? OfferGeneratedAt,
    DateTime? OfferAcceptedAt,
    DateTime? OfferLapsedAt,
    // Legal Clearance
    Guid? LegalClearedByUserId,
    DateTime? LegalClearedAt,
    string? LegalClearanceNote,
    string? LegalDeclineNote,
    string? LegalReturnNote,
    // Pre-Deployment Verification
    Guid? PreDeploymentVerifiedByUserId,
    DateTime? PreDeploymentVerifiedAt,
    string? PreDeploymentNote,
    // Deployment
    Guid? DeployedByUserId,
    DateTime? DeployedAt,
    bool GpsActivated,
    string? DeploymentNote,
    // Fineract integration
    long? FineractClientId,
    int? FineractProductId,
    string? FineractProductName,
    decimal? FineractNominalInterestRate,
    decimal? ApprovedInterestRate,
    long? FineractLoanId,
    string? FineractLoanAccountNumber,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    List<NampDocumentDto> Documents,
    List<NampStatusHistoryDto> StatusHistory,
    List<NampGuarantorDto> Guarantors,
    List<NampCollateralDto> Collaterals,
    List<NampFinancialStatementDto> FinancialStatements,
    NampFinancialAppraisalReportDto? FinancialAppraisalReport,
    List<NampBureauReportDto> BureauReports,
    List<NampPreDeploymentChecklistItemDto> PreDeploymentChecklist,
    // CAC company profile (Agro-Service)
    string? CacStatus,
    string? CacEntityType,
    string? CacRegistrationDate,
    string? CacNatureOfBusiness,
    decimal? CacShareCapital,
    long? CacCompanyId,
    string? CacAddress,
    string? CacCity,
    string? CacState,
    DateTime? CacFetchedAt,
    List<NampDirectorDto> Directors
);

public record NampDirectorDto(
    Guid Id,
    long? CacDirectorId,
    bool SourcedFromCac,
    string FullName,
    string? Gender,
    string? DateOfBirth,
    string? Nationality,
    string? Occupation,
    string? Email,
    string? PhoneNumber,
    string? Address,
    bool IsChairman,
    string? DateOfAppointment,
    string? AffiliateType,
    string? TypeOfShares,
    long? NumSharesAllotted,
    decimal? ShareholdingPercent,
    string? Bvn,
    string? IdentityNumber,
    bool BvnVerified
);

public record NampBureauReportDto(
    Guid Id,
    string SubjectName,
    string SubjectType,
    string Bvn,
    string Status,
    int? CreditScore,
    string? ScoreGrade,
    int? DelinquentFacilities,
    decimal? TotalOutstanding,
    decimal? TotalOverdue,
    string? FraudRecommendation,
    string? ErrorMessage,
    DateTime CreatedAt
);

public record NampDocumentDto(
    Guid Id,
    string Stage,
    string Category,
    string FileName,
    string ContentType,
    long FileSize,
    string StoragePath,
    string? Description,
    DateTime UploadedAt,
    Guid UploadedByUserId
);

public record NampStatusHistoryDto(
    Guid Id,
    string Status,
    DateTime ChangedAt,
    Guid ChangedByUserId,
    string? Note
);

public record NampRoutingConfigDto(
    Guid Id,
    string ApplicantCategory,
    string CommitteeTier,
    decimal MinEquipmentValue,
    decimal MaxEquipmentValue,
    int Priority,
    bool IsActive,
    DateTime CreatedAt
);

public record NampWorkflowConfigDto(
    Guid Id,
    string Status,
    string DisplayName,
    string Description,
    string AssignedRole,
    int SlaHours,
    int SortOrder,
    bool IsTerminal
);

public record NampGuarantorDto(
    Guid Id,
    string FullName,
    string? Bvn,
    string? Nin,
    string? DateOfBirth,
    string? Gender,
    string? CompanyName,
    string? CompanyRegistrationNumber,
    string? Email,
    string? Phone,
    string? Address,
    string GuarantorType,
    string GuaranteeType,
    string? RelationshipToApplicant,
    bool IsDirector,
    bool IsShareholder,
    decimal? ShareholdingPercentage,
    decimal? DeclaredNetWorth,
    string? Occupation,
    string? EmployerName,
    decimal? MonthlyIncome,
    decimal? GuaranteeLimit,
    bool IsUnlimited
);

public record NampCollateralDto(
    Guid Id,
    string CollateralType,
    string Description,
    string? AssetIdentifier,
    string? Location,
    string? OwnerName,
    string? OwnershipType,
    decimal? MarketValue,
    decimal? ForcedSaleValue,
    string? LienType,
    string? LienReference,
    string? LienRegistrationAuthority,
    bool IsInsured,
    string? InsurancePolicyNumber,
    string? InsuranceCompany,
    decimal? InsuredValue,
    string? InsuranceExpiryDate
);

public record NampFinancialStatementDto(
    Guid Id,
    int FinancialYear,
    string? YearEndDate,
    string FinancialYearType,
    string Currency,
    decimal? Revenue,
    decimal? GrossProfit,
    decimal? Ebitda,
    decimal? NetProfit,
    decimal? TotalAssets,
    decimal? TotalLiabilities,
    decimal? TotalEquity,
    decimal? NetCashFromOperating,
    string? AuditorName,
    string? AuditorFirm,
    string? AuditOpinion
);

public record NampCommitteeMemberViewDto(
    Guid UserId,
    string UserName,
    string Role,
    bool IsChairperson,
    DateTime AssignedAt,
    string? Vote,
    DateTime? VotedAt,
    string? VoteComment
);

public record NampCommitteeReviewDto(
    Guid Id,
    string CommitteeType,
    string Status,
    DateTime CirculatedAt,
    DateTime? DeadlineAt,
    int RequiredVotes,
    int MinimumApprovalVotes,
    int ApprovalVotes,
    int RejectionVotes,
    int AbstainVotes,
    int PendingVotes,
    bool HasQuorum,
    bool HasMajorityApproval,
    bool IsOverdue,
    List<NampCommitteeMemberViewDto> Members
);

public record NampFinancialAppraisalReportDto(
    Guid Id,
    Guid NampApplicationId,
    Guid PreparedByUserId,
    DateTime SavedAt,
    decimal? MonthlyDisposableIncome,
    decimal? DebtServiceCoverageRatio,
    decimal? LoanToValueRatio,
    string RepaymentCapacityRating,
    string? EquityAssessmentNote,
    string? CreditBureauSummary,
    string CreditOfficerRecommendation,
    string? SummaryNotes,
    string RepaymentSource,
    decimal? ProjectedMonthlyRentalRevenue,
    decimal? UtilisationRateAssumption,
    string? DemandEvidenceNote
);

public record NampPreDeploymentChecklistTemplateDto(
    Guid Id,
    string Title,
    string? Description,
    bool RequiresDocumentUpload,
    string? DocumentCategory,
    bool IsMandatory,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAt
);

public record NampPreDeploymentChecklistItemDto(
    Guid Id,
    Guid TemplateItemId,
    string Title,
    string? Description,
    bool RequiresDocumentUpload,
    string? DocumentCategory,
    bool IsMandatory,
    int SortOrder,
    bool? IsConfirmed,
    Guid? ConfirmedByUserId,
    DateTime? ConfirmedAt,
    string? Notes
);

// ── NAMP Advisory ──────────────────────────────────────────────────────────

public record NampAdvisoryDto(
    Guid Id,
    Guid NampApplicationId,
    string Status,
    decimal OverallScore,
    string OverallRating,
    string Recommendation,
    decimal? RecommendedAmount,
    int? RecommendedTenorMonths,
    decimal? RecommendedInterestRate,
    string? ExecutiveSummary,
    string? StrengthsAnalysis,
    string? WeaknessesAnalysis,
    string? MitigatingFactors,
    string? KeyRisks,
    string? TechnicalViabilityRating,
    decimal? TechnicalViabilityScore,
    bool HasCriticalRedFlags,
    string ModelVersion,
    DateTime GeneratedAt,
    string? ErrorMessage,
    // Deserialized collections (populated by the query handler from JSON fields)
    List<NampAdvisoryRiskScoreDto> RiskScores,
    List<string> RedFlags,
    List<string> Conditions,
    List<string> Covenants
);

public record NampAdvisoryRiskScoreDto(
    string Category,
    decimal Score,
    decimal Weight,
    decimal WeightedScore,
    string Rating,
    string Rationale,
    List<string> RedFlags,
    List<string> PositiveIndicators
);

