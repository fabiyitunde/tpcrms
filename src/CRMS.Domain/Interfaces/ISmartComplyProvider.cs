using CRMS.Domain.Common;

namespace CRMS.Domain.Interfaces;

public interface ISmartComplyProvider
{
    #region Individual Credit Reports
    
    Task<Result<SmartComplyIndividualCreditReport>> GetFirstCentralSummaryAsync(string bvn, CancellationToken ct = default);
    Task<Result<SmartComplyIndividualCreditReport>> GetFirstCentralFullAsync(string bvn, CancellationToken ct = default);
    Task<Result<SmartComplyCreditScore>> GetFirstCentralScoreAsync(string bvn, CancellationToken ct = default);
    Task<Result<SmartComplyIndividualCreditReport>> GetCreditRegistrySummaryAsync(string bvn, CancellationToken ct = default);
    Task<Result<SmartComplyIndividualCreditReport>> GetCreditRegistryFullAsync(string bvn, CancellationToken ct = default);
    Task<Result<SmartComplyIndividualCreditReport>> GetCreditRegistryAdvancedAsync(string bvn, CancellationToken ct = default);
    Task<Result<SmartComplyCreditScore>> GetCRCScoreAsync(string bvn, CancellationToken ct = default);
    Task<Result<SmartComplyIndividualCreditReport>> GetCRCHistoryAsync(string bvn, CancellationToken ct = default);
    Task<Result<SmartComplyIndividualCreditReport>> GetCRCFullAsync(string bvn, CancellationToken ct = default);
    Task<Result<SmartComplyIndividualCreditReport>> GetCreditPremiumAsync(string bvn, CancellationToken ct = default);
    
    #endregion
    
    #region Business Credit Reports
    
    Task<Result<SmartComplyBusinessCreditReport>> GetCRCBusinessHistoryAsync(string rcNumber, CancellationToken ct = default);
    Task<Result<SmartComplyBusinessCreditReport>> GetFirstCentralBusinessAsync(string rcNumber, CancellationToken ct = default);
    Task<Result<SmartComplyBusinessCreditReport>> GetPremiumBusinessAsync(string rcNumber, CancellationToken ct = default);
    
    #endregion
    
    #region Loan Fraud Check
    
    Task<Result<SmartComplyLoanFraudResult>> CheckIndividualLoanFraudAsync(SmartComplyIndividualLoanRequest request, CancellationToken ct = default);
    Task<Result<SmartComplyLoanFraudResult>> CheckBusinessLoanFraudAsync(SmartComplyBusinessLoanRequest request, CancellationToken ct = default);
    
    #endregion
    
    #region KYC/Identity Verification
    
    Task<Result<SmartComplyBvnResult>> VerifyBvnAsync(string bvn, CancellationToken ct = default);
    Task<Result<SmartComplyBvnAdvancedResult>> VerifyBvnAdvancedAsync(string bvn, CancellationToken ct = default);
    Task<Result<SmartComplyNinResult>> VerifyNinAsync(string nin, CancellationToken ct = default);
    Task<Result<SmartComplyTinResult>> VerifyTinAsync(string tin, CancellationToken ct = default);
    Task<Result<SmartComplyCacResult>> VerifyCacAsync(string rcNumber, CancellationToken ct = default);
    Task<Result<SmartComplyCacResult>> VerifyCacAdvancedAsync(string rcNumber, CancellationToken ct = default);
    
    #endregion
}

#region Domain Result Records

public record SmartComplyIndividualCreditReport(
    string? Id,
    string? Bvn,
    string? Name,
    string? Phone,
    string? Gender,
    string? DateOfBirth,
    string? Address,
    string? Email,
    SmartComplyCreditSummary Summary,
    List<SmartComplyCreditor> Creditors,
    List<SmartComplyCreditEnquiry> CreditEnquiries,
    List<SmartComplyLoanPerformance> LoanPerformance,
    List<SmartComplyLoanHistory> LoanHistory,
    DateTime? SearchedDate
);

public record SmartComplyCreditSummary(
    int TotalNoOfLoans,
    int TotalNoOfInstitutions,
    int TotalNoOfActiveLoans,
    int TotalNoOfClosedLoans,
    int TotalNoOfPerformingLoans,
    int TotalNoOfDelinquentFacilities,
    decimal HighestLoanAmount,
    decimal TotalMonthlyInstallment,
    decimal TotalBorrowed,
    decimal TotalOutstanding,
    decimal TotalOverdue,
    int MaxNoOfDays,
    string? ReportOrderNumber
);

public record SmartComplyCreditor(
    string? SubscriberId,
    string? Name,
    string? Phone,
    string? Address
);

public record SmartComplyCreditEnquiry(
    string? LoanProvider,
    string? Reason,
    DateTime? Date,
    string? ContactPhone
);

public record SmartComplyLoanPerformance(
    string? LoanProvider,
    string? AccountNumber,
    decimal LoanAmount,
    decimal OutstandingBalance,
    string? Status,
    string? PerformanceStatus,
    decimal OverdueAmount,
    string? Type,
    string? LoanDuration,
    string? RepaymentFrequency,
    string? RepaymentBehavior,
    string? PaymentProfile,
    DateTime? DateAccountOpened,
    DateTime? LastUpdatedAt
);

public record SmartComplyLoanHistory(
    string? LoanProvider,
    string? LoanProviderAddress,
    string? AccountNumber,
    string? Type,
    decimal LoanAmount,
    decimal? InstallmentAmount,
    decimal OverdueAmount,
    DateTime? LastPaymentDate,
    string? LoanDuration,
    DateTime? DisbursedDate,
    DateTime? MaturityDate,
    string? PerformanceStatus,
    string? Status,
    decimal OutstandingBalance,
    string? Collateral,
    decimal CollateralValue,
    string? PaymentHistory,
    DateTime? LastUpdatedAt
);

public record SmartComplyCreditScore(
    int Score,
    string? Grade,
    string? Provider,
    DateTime? GeneratedDate
);

public record SmartComplyBusinessCreditReport(
    string? Id,
    string? BusinessRegNo,
    string? Name,
    string? Phone,
    string? DateOfRegistration,
    string? Address,
    string? Website,
    string? TaxIdentificationNumber,
    int NoOfDirectors,
    string? Industry,
    string? BusinessType,
    string? Email,
    SmartComplyBusinessCreditSummary Summary,
    DateTime? SearchedDate
);

public record SmartComplyBusinessCreditSummary(
    int TotalNoOfLoans,
    int TotalNoOfActiveLoans,
    int TotalNoOfClosedLoans,
    int TotalNoOfInstitutions,
    decimal TotalOverdue,
    decimal TotalBorrowed,
    decimal HighestLoanAmount,
    decimal TotalOutstanding,
    decimal TotalMonthlyInstallment,
    int TotalNoOfOverdueAccounts,
    int TotalNoOfPerformingLoans,
    int TotalNoOfDelinquentFacilities,
    string? LastReportedDate,
    string? ReportOrderNumber
);

public record SmartComplyIndividualLoanRequest(
    string FirstName,
    string LastName,
    string? OtherName,
    string DateOfBirth,
    string Gender,
    string Country,
    string? City,
    string? CurrentAddress,
    string Bvn,
    string? PhoneNumber,
    string? EmailAddress,
    string? EmploymentType,
    string? JobRole,
    string? EmployerName,
    decimal AnnualIncome,
    string? BankName,
    string? AccountNumber,
    decimal LoanAmountRequested,
    string? PurposeOfLoan,
    string LoanRepaymentDurationType,
    int LoanRepaymentDurationValue,
    bool CollateralRequired,
    decimal? CollateralValue,
    bool RunAmlCheck
);

public record SmartComplyBusinessLoanRequest(
    string BusinessName,
    string? BusinessAddress,
    string RcNumber,
    string? City,
    string Country,
    string? PhoneNumber,
    string? EmailAddress,
    decimal AnnualRevenue,
    string? BankName,
    string? AccountNumber,
    decimal LoanAmountRequested,
    string? PurposeOfLoan,
    string LoanRepaymentDurationType,
    int LoanRepaymentDurationValue,
    bool CollateralRequired,
    decimal? CollateralValue,
    bool RunAmlCheck
);

public record SmartComplyLoanFraudResult(
    int Id,
    string? ApplicantName,
    string? ApplicantIdentifier,
    bool IsIndividual,
    bool IsBusiness,
    int FraudRiskScore,
    string? Recommendation,
    string? Status,
    SmartComplyFinancialAnalysis? FinancialAnalysis,
    SmartComplyLoanFraudHistory? History,
    DateTime? DateCreated
);

public record SmartComplyFinancialAnalysis(
    SmartComplyRiskItem? IncomeStability,
    SmartComplyRiskItem? RepaymentDuration,
    SmartComplyRiskItem? CollateralCoverage,
    SmartComplyRiskItem? DebtServiceability,
    SmartComplyRiskItem? DebtToIncomeRatio
);

public record SmartComplyRiskItem(
    int RiskScore,
    string? Observation
);

public record SmartComplyLoanFraudHistory(
    int TotalNoOfLoans,
    int TotalNoOfInstitutions,
    int TotalNoOfActiveLoans,
    int TotalNoOfClosedLoans,
    int TotalNoOfPerformingLoans,
    int TotalNoOfDelinquentFacilities,
    decimal HighestLoanAmount,
    decimal TotalBorrowed,
    decimal TotalOutstanding,
    decimal TotalOverdue
);

public record SmartComplyBvnResult(
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? DateOfBirth,
    string? PhoneNumber
);

public record SmartComplyBvnAdvancedResult(
    string? Bvn,
    string? Image,
    string? Title,
    string? Gender,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? DateOfBirth,
    string? PhoneNumber1,
    string? PhoneNumber2,
    string? MaritalStatus,
    string? StateOfOrigin,
    string? LgaOfOrigin,
    string? StateOfResidence,
    string? LgaOfResidence,
    string? ResidentialAddress,
    string? EnrollmentBank,
    string? EnrollmentBranch,
    string? RegistrationDate,
    string? LevelOfAccount,
    string? WatchListed
);

public record SmartComplyNinResult(
    string? Nin,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? DateOfBirth,
    string? Gender,
    string? PhoneNumber,
    string? Photo
);

public record SmartComplyTinResult(
    string? TaxIdentificationNumber,
    string? TaxpayerName,
    string? CacRegNumber,
    string? TaxOffice,
    string? PhoneNumber,
    string? Email
);

public record SmartComplyCacResult(
    string? CompanyName,
    string? RcNumber,
    string? CompanyType,
    string? RegistrationDate,
    string? Address,
    string? City,
    string? State,
    string? Email,
    string? Status,
    string? NatureOfBusiness,
    decimal? ShareCapital,
    List<SmartComplyCacDirector> Directors
);

public record SmartComplyCacDirector(
    string? Name,
    string? Designation,
    string? DateOfAppointment
);

#endregion
