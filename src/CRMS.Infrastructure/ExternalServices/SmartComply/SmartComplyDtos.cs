using System.Text.Json.Serialization;

namespace CRMS.Infrastructure.ExternalServices.SmartComply;

#region Base Response

public class SmartComplyResponse<T>
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("response_code")]
    public string? ResponseCode { get; set; }
    
    [JsonPropertyName("data")]
    public T? Data { get; set; }
}

#endregion

#region Individual Credit Report DTOs

public class IndividualCreditReportData
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("bvn")]
    public string? Bvn { get; set; }
    
    [JsonPropertyName("customerId")]
    public string? CustomerId { get; set; }
    
    [JsonPropertyName("businessId")]
    public string? BusinessId { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }
    
    [JsonPropertyName("gender")]
    public string? Gender { get; set; }
    
    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; set; }
    
    [JsonPropertyName("address")]
    public string? Address { get; set; }
    
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    
    [JsonPropertyName("score")]
    public CreditScoreData? Score { get; set; }
    
    [JsonPropertyName("searchedDate")]
    public DateTime? SearchedDate { get; set; }
}

public class CreditScoreData
{
    [JsonPropertyName("totalNoOfLoans")]
    public int TotalNoOfLoans { get; set; }
    
    [JsonPropertyName("totalNoOfInstitutions")]
    public int TotalNoOfInstitutions { get; set; }
    
    [JsonPropertyName("totalNoOfActiveLoans")]
    public int TotalNoOfActiveLoans { get; set; }
    
    [JsonPropertyName("totalNoOfClosedLoans")]
    public int TotalNoOfClosedLoans { get; set; }
    
    [JsonPropertyName("totalNoOfPerformingLoans")]
    public int TotalNoOfPerformingLoans { get; set; }
    
    [JsonPropertyName("totalNoOfDelinquentFacilities")]
    public int TotalNoOfDelinquentFacilities { get; set; }
    
    [JsonPropertyName("highestLoanAmount")]
    public decimal HighestLoanAmount { get; set; }
    
    [JsonPropertyName("totalMonthlyInstallment")]
    public decimal TotalMonthlyInstallment { get; set; }
    
    [JsonPropertyName("totalBorrowed")]
    public decimal TotalBorrowed { get; set; }
    
    [JsonPropertyName("totalOutstanding")]
    public decimal TotalOutstanding { get; set; }
    
    [JsonPropertyName("totalOverdue")]
    public decimal TotalOverdue { get; set; }
    
    [JsonPropertyName("maxNoOfDays")]
    public int MaxNoOfDays { get; set; }
    
    [JsonPropertyName("crcReportOrderNumber")]
    public string? CrcReportOrderNumber { get; set; }
    
    [JsonPropertyName("creditors")]
    public List<CreditorData>? Creditors { get; set; }
    
    [JsonPropertyName("creditEnquiries")]
    public List<CreditEnquiryData>? CreditEnquiries { get; set; }
    
    [JsonPropertyName("creditEnquiriesSummary")]
    public CreditEnquiriesSummary? CreditEnquiriesSummary { get; set; }
    
    [JsonPropertyName("loanPerformance")]
    public List<LoanPerformanceData>? LoanPerformance { get; set; }
    
    [JsonPropertyName("loanHistory")]
    public List<LoanHistoryData>? LoanHistory { get; set; }
}

public class CreditorData
{
    [JsonPropertyName("Subscriber_ID")]
    public string? SubscriberId { get; set; }
    
    [JsonPropertyName("Name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("Phone")]
    public string? Phone { get; set; }
    
    [JsonPropertyName("Address")]
    public string? Address { get; set; }
}

public class CreditEnquiryData
{
    [JsonPropertyName("loanProvider")]
    public string? LoanProvider { get; set; }
    
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
    
    [JsonPropertyName("date")]
    public DateTime? Date { get; set; }
    
    [JsonPropertyName("contactPhone")]
    public string? ContactPhone { get; set; }
}

public class CreditEnquiriesSummary
{
    [JsonPropertyName("Last3MonthCount")]
    public string? Last3MonthCount { get; set; }
    
    [JsonPropertyName("Last12MonthCount")]
    public string? Last12MonthCount { get; set; }
    
    [JsonPropertyName("Last36MonthCount")]
    public string? Last36MonthCount { get; set; }
}

public class LoanPerformanceData
{
    [JsonPropertyName("loanProvider")]
    public string? LoanProvider { get; set; }
    
    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; set; }
    
    [JsonPropertyName("loanAmount")]
    public decimal LoanAmount { get; set; }
    
    [JsonPropertyName("outstandingBalance")]
    public decimal OutstandingBalance { get; set; }
    
    [JsonPropertyName("status")]
    public string? Status { get; set; }
    
    [JsonPropertyName("performanceStatus")]
    public string? PerformanceStatus { get; set; }
    
    [JsonPropertyName("overdueAmount")]
    public decimal OverdueAmount { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("loanDuration")]
    public string? LoanDuration { get; set; }
    
    [JsonPropertyName("repaymentFrequency")]
    public string? RepaymentFrequency { get; set; }
    
    [JsonPropertyName("repaymentBehavior")]
    public string? RepaymentBehavior { get; set; }
    
    [JsonPropertyName("paymentProfile")]
    public string? PaymentProfile { get; set; }
    
    [JsonPropertyName("dateAccountOpened")]
    public DateTime? DateAccountOpened { get; set; }
    
    [JsonPropertyName("lastUpdatedAt")]
    public DateTime? LastUpdatedAt { get; set; }
    
    [JsonPropertyName("loanCount")]
    public int LoanCount { get; set; }
    
    [JsonPropertyName("monthlyInstallmentAmt")]
    public decimal MonthlyInstallmentAmt { get; set; }
}

public class LoanHistoryData
{
    [JsonPropertyName("loanProvider")]
    public string? LoanProvider { get; set; }
    
    [JsonPropertyName("loanProviderAddress")]
    public string? LoanProviderAddress { get; set; }
    
    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("loanAmount")]
    public decimal LoanAmount { get; set; }
    
    [JsonPropertyName("installmentAmount")]
    public decimal? InstallmentAmount { get; set; }
    
    [JsonPropertyName("overdueAmount")]
    public decimal OverdueAmount { get; set; }
    
    [JsonPropertyName("lastPaymentDate")]
    public DateTime? LastPaymentDate { get; set; }
    
    [JsonPropertyName("loanDuration")]
    public string? LoanDuration { get; set; }
    
    [JsonPropertyName("dateReported")]
    public DateTime? DateReported { get; set; }
    
    [JsonPropertyName("disbursedDate")]
    public DateTime? DisbursedDate { get; set; }
    
    [JsonPropertyName("maturityDate")]
    public DateTime? MaturityDate { get; set; }
    
    [JsonPropertyName("performanceStatus")]
    public string? PerformanceStatus { get; set; }
    
    [JsonPropertyName("status")]
    public string? Status { get; set; }
    
    [JsonPropertyName("outstandingBalance")]
    public decimal OutstandingBalance { get; set; }
    
    [JsonPropertyName("collateral")]
    public string? Collateral { get; set; }
    
    [JsonPropertyName("collateralValue")]
    public decimal CollateralValue { get; set; }
    
    [JsonPropertyName("guarantor")]
    public string? Guarantor { get; set; }
    
    [JsonPropertyName("purpose")]
    public string? Purpose { get; set; }
    
    [JsonPropertyName("paymentHistory")]
    public string? PaymentHistory { get; set; }
    
    [JsonPropertyName("lastUpdatedAt")]
    public DateTime? LastUpdatedAt { get; set; }
}

#endregion

#region Business Credit Report DTOs

public class BusinessCreditReportData
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("business_reg_no")]
    public string? BusinessRegNo { get; set; }
    
    [JsonPropertyName("businessId")]
    public string? BusinessId { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }
    
    [JsonPropertyName("dateOfRegistration")]
    public string? DateOfRegistration { get; set; }
    
    [JsonPropertyName("address")]
    public string? Address { get; set; }
    
    [JsonPropertyName("website")]
    public string? Website { get; set; }
    
    [JsonPropertyName("taxIdentificationNumber")]
    public string? TaxIdentificationNumber { get; set; }
    
    [JsonPropertyName("noOfDirectors")]
    public int NoOfDirectors { get; set; }
    
    [JsonPropertyName("industry")]
    public string? Industry { get; set; }
    
    [JsonPropertyName("businessType")]
    public string? BusinessType { get; set; }
    
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    
    [JsonPropertyName("score")]
    public BusinessCreditScoreData? Score { get; set; }
    
    [JsonPropertyName("searchedDate")]
    public DateTime? SearchedDate { get; set; }
}

public class BusinessCreditScoreData
{
    [JsonPropertyName("totalNoOfLoans")]
    public int TotalNoOfLoans { get; set; }
    
    [JsonPropertyName("totalNoOfActiveLoans")]
    public int TotalNoOfActiveLoans { get; set; }
    
    [JsonPropertyName("totalNoOfClosedLoans")]
    public int TotalNoOfClosedLoans { get; set; }
    
    [JsonPropertyName("totalNoOfInstitutions")]
    public int TotalNoOfInstitutions { get; set; }
    
    [JsonPropertyName("totalOverdue")]
    public decimal TotalOverdue { get; set; }
    
    [JsonPropertyName("totalBorrowed")]
    public decimal TotalBorrowed { get; set; }
    
    [JsonPropertyName("highestLoanAmount")]
    public decimal HighestLoanAmount { get; set; }
    
    [JsonPropertyName("totalOutstanding")]
    public decimal TotalOutstanding { get; set; }
    
    [JsonPropertyName("totalMonthlyInstallment")]
    public decimal TotalMonthlyInstallment { get; set; }
    
    [JsonPropertyName("totalNoOfOverdueAccounts")]
    public int TotalNoOfOverdueAccounts { get; set; }
    
    [JsonPropertyName("totalNoOfPerformingLoans")]
    public int TotalNoOfPerformingLoans { get; set; }
    
    [JsonPropertyName("totalNoOfDelinquentFacilities")]
    public int TotalNoOfDelinquentFacilities { get; set; }
    
    [JsonPropertyName("lastReportedDate")]
    public string? LastReportedDate { get; set; }
    
    [JsonPropertyName("crcReportOrderNumber")]
    public string? CrcReportOrderNumber { get; set; }
    
    [JsonPropertyName("firstCentralEnquiryResultID")]
    public string? FirstCentralEnquiryResultID { get; set; }
    
    [JsonPropertyName("firstCentralEnquiryEngineID")]
    public string? FirstCentralEnquiryEngineID { get; set; }
    
    [JsonPropertyName("directors")]
    public object? Directors { get; set; }
    
    [JsonPropertyName("loanPerformance")]
    public object? LoanPerformance { get; set; }
    
    [JsonPropertyName("creditEnquiries")]
    public object? CreditEnquiries { get; set; }
    
    [JsonPropertyName("loanHistory")]
    public object? LoanHistory { get; set; }
}

#endregion

#region Loan Fraud Check DTOs

public class IndividualLoanFraudRequest
{
    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;
    
    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = string.Empty;
    
    [JsonPropertyName("other_name")]
    public string? OtherName { get; set; }
    
    [JsonPropertyName("date_of_birth")]
    public string DateOfBirth { get; set; } = string.Empty;
    
    [JsonPropertyName("gender")]
    public string Gender { get; set; } = string.Empty;
    
    [JsonPropertyName("country")]
    public string Country { get; set; } = "Nigeria";
    
    [JsonPropertyName("city")]
    public string? City { get; set; }
    
    [JsonPropertyName("current_address")]
    public string? CurrentAddress { get; set; }
    
    [JsonPropertyName("duration_of_stay")]
    public string? DurationOfStay { get; set; }
    
    [JsonPropertyName("identification_type")]
    public string? IdentificationType { get; set; }
    
    [JsonPropertyName("identification_number")]
    public string? IdentificationNumber { get; set; }
    
    [JsonPropertyName("bvn")]
    public string Bvn { get; set; } = string.Empty;
    
    [JsonPropertyName("phone_number")]
    public string? PhoneNumber { get; set; }
    
    [JsonPropertyName("email_address")]
    public string? EmailAddress { get; set; }
    
    [JsonPropertyName("employment_type")]
    public string? EmploymentType { get; set; }
    
    [JsonPropertyName("job_role")]
    public string? JobRole { get; set; }
    
    [JsonPropertyName("employer_name")]
    public string? EmployerName { get; set; }
    
    [JsonPropertyName("employer_address")]
    public string? EmployerAddress { get; set; }
    
    [JsonPropertyName("annual_income")]
    public decimal AnnualIncome { get; set; }
    
    [JsonPropertyName("employment_duration")]
    public string? EmploymentDuration { get; set; }
    
    [JsonPropertyName("bank_name")]
    public string? BankName { get; set; }
    
    [JsonPropertyName("account_number")]
    public string? AccountNumber { get; set; }
    
    [JsonPropertyName("account_name")]
    public string? AccountName { get; set; }
    
    [JsonPropertyName("loan_amount_requested")]
    public decimal LoanAmountRequested { get; set; }
    
    [JsonPropertyName("purpose_of_loan")]
    public string? PurposeOfLoan { get; set; }
    
    [JsonPropertyName("loan_repayment_duration_type")]
    public string LoanRepaymentDurationType { get; set; } = "months";
    
    [JsonPropertyName("loan_repayment_duration_value")]
    public int LoanRepaymentDurationValue { get; set; }
    
    [JsonPropertyName("collateral_required")]
    public bool CollateralRequired { get; set; }
    
    [JsonPropertyName("collateral")]
    public string? Collateral { get; set; }
    
    [JsonPropertyName("collateral_value")]
    public decimal? CollateralValue { get; set; }
    
    [JsonPropertyName("is_individual")]
    public bool IsIndividual { get; set; } = true;
    
    [JsonPropertyName("run_aml_check")]
    public bool RunAmlCheck { get; set; }
}

public class BusinessLoanFraudRequest
{
    [JsonPropertyName("business_name")]
    public string BusinessName { get; set; } = string.Empty;
    
    [JsonPropertyName("business_address")]
    public string? BusinessAddress { get; set; }
    
    [JsonPropertyName("rc_number")]
    public string RcNumber { get; set; } = string.Empty;
    
    [JsonPropertyName("city")]
    public string? City { get; set; }
    
    [JsonPropertyName("country")]
    public string Country { get; set; } = "Nigeria";
    
    [JsonPropertyName("phone_number")]
    public string? PhoneNumber { get; set; }
    
    [JsonPropertyName("email_address")]
    public string? EmailAddress { get; set; }
    
    [JsonPropertyName("identification_type")]
    public string? IdentificationType { get; set; }
    
    [JsonPropertyName("identification_number")]
    public string? IdentificationNumber { get; set; }
    
    [JsonPropertyName("annual_revenue")]
    public decimal AnnualRevenue { get; set; }
    
    [JsonPropertyName("bank_name")]
    public string? BankName { get; set; }
    
    [JsonPropertyName("account_number")]
    public string? AccountNumber { get; set; }
    
    [JsonPropertyName("account_name")]
    public string? AccountName { get; set; }
    
    [JsonPropertyName("loan_amount_requested")]
    public decimal LoanAmountRequested { get; set; }
    
    [JsonPropertyName("purpose_of_loan")]
    public string? PurposeOfLoan { get; set; }
    
    [JsonPropertyName("loan_repayment_duration_type")]
    public string LoanRepaymentDurationType { get; set; } = "months";
    
    [JsonPropertyName("loan_repayment_duration_value")]
    public int LoanRepaymentDurationValue { get; set; }
    
    [JsonPropertyName("collateral_required")]
    public bool CollateralRequired { get; set; }
    
    [JsonPropertyName("collateral")]
    public string? Collateral { get; set; }
    
    [JsonPropertyName("collateral_value")]
    public decimal? CollateralValue { get; set; }
    
    [JsonPropertyName("is_business")]
    public bool IsBusiness { get; set; } = true;
    
    [JsonPropertyName("run_aml_check")]
    public bool RunAmlCheck { get; set; }
}

public class LoanFraudCheckData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }
    
    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }
    
    [JsonPropertyName("other_name")]
    public string? OtherName { get; set; }
    
    [JsonPropertyName("business_name")]
    public string? BusinessName { get; set; }
    
    [JsonPropertyName("date_of_birth")]
    public string? DateOfBirth { get; set; }
    
    [JsonPropertyName("gender")]
    public string? Gender { get; set; }
    
    [JsonPropertyName("country")]
    public string? Country { get; set; }
    
    [JsonPropertyName("city")]
    public string? City { get; set; }
    
    [JsonPropertyName("current_address")]
    public string? CurrentAddress { get; set; }
    
    [JsonPropertyName("business_address")]
    public string? BusinessAddress { get; set; }
    
    [JsonPropertyName("bvn")]
    public string? Bvn { get; set; }
    
    [JsonPropertyName("rc_number")]
    public string? RcNumber { get; set; }
    
    [JsonPropertyName("annual_income")]
    public string? AnnualIncome { get; set; }
    
    [JsonPropertyName("annual_revenue")]
    public string? AnnualRevenue { get; set; }
    
    [JsonPropertyName("loan_amount_requested")]
    public string? LoanAmountRequested { get; set; }
    
    [JsonPropertyName("fraud_risk_score")]
    public int FraudRiskScore { get; set; }
    
    [JsonPropertyName("recommendation")]
    public string? Recommendation { get; set; }
    
    [JsonPropertyName("key_financial_analysis")]
    public KeyFinancialAnalysis? KeyFinancialAnalysis { get; set; }
    
    [JsonPropertyName("history")]
    public LoanFraudHistory? History { get; set; }
    
    [JsonPropertyName("is_individual")]
    public bool IsIndividual { get; set; }
    
    [JsonPropertyName("is_business")]
    public bool IsBusiness { get; set; }
    
    [JsonPropertyName("status")]
    public string? Status { get; set; }
    
    [JsonPropertyName("run_aml_check")]
    public bool RunAmlCheck { get; set; }
    
    [JsonPropertyName("aml_data")]
    public object? AmlData { get; set; }
    
    [JsonPropertyName("credit_report_data")]
    public object? CreditReportData { get; set; }
    
    [JsonPropertyName("date_created")]
    public DateTime? DateCreated { get; set; }
    
    [JsonPropertyName("date_updated")]
    public DateTime? DateUpdated { get; set; }
}

public class KeyFinancialAnalysis
{
    [JsonPropertyName("income_stability")]
    public IncomeStabilityAnalysis? IncomeStability { get; set; }
    
    [JsonPropertyName("repayment_duration")]
    public RepaymentDurationAnalysis? RepaymentDuration { get; set; }
    
    [JsonPropertyName("collateral_coverage")]
    public CollateralCoverageAnalysis? CollateralCoverage { get; set; }
    
    [JsonPropertyName("debt_serviceability")]
    public DebtServiceabilityAnalysis? DebtServiceability { get; set; }
    
    [JsonPropertyName("debt_to_income_ratio")]
    public DebtToIncomeRatioAnalysis? DebtToIncomeRatio { get; set; }
}

public class IncomeStabilityAnalysis
{
    [JsonPropertyName("risk_score")]
    public int RiskScore { get; set; }
    
    [JsonPropertyName("data_source")]
    public string? DataSource { get; set; }
    
    [JsonPropertyName("observation")]
    public string? Observation { get; set; }
    
    [JsonPropertyName("monthly_income")]
    public string? MonthlyIncome { get; set; }
    
    [JsonPropertyName("monthly_expenses")]
    public string? MonthlyExpenses { get; set; }
    
    [JsonPropertyName("disposable_income")]
    public string? DisposableIncome { get; set; }
}

public class RepaymentDurationAnalysis
{
    [JsonPropertyName("risk_score")]
    public int RiskScore { get; set; }
    
    [JsonPropertyName("observation")]
    public string? Observation { get; set; }
    
    [JsonPropertyName("repayment_duration")]
    public string? RepaymentDuration { get; set; }
}

public class CollateralCoverageAnalysis
{
    [JsonPropertyName("risk_score")]
    public int RiskScore { get; set; }
    
    [JsonPropertyName("loan_amount")]
    public string? LoanAmount { get; set; }
    
    [JsonPropertyName("observation")]
    public string? Observation { get; set; }
    
    [JsonPropertyName("collateral_value")]
    public string? CollateralValue { get; set; }
    
    [JsonPropertyName("loan_to_collateral_ratio")]
    public string? LoanToCollateralRatio { get; set; }
}

public class DebtServiceabilityAnalysis
{
    [JsonPropertyName("risk_score")]
    public int RiskScore { get; set; }
    
    [JsonPropertyName("loan_amount")]
    public string? LoanAmount { get; set; }
    
    [JsonPropertyName("observation")]
    public string? Observation { get; set; }
    
    [JsonPropertyName("total_repayment")]
    public string? TotalRepayment { get; set; }
}

public class DebtToIncomeRatioAnalysis
{
    [JsonPropertyName("risk_score")]
    public int RiskScore { get; set; }
    
    [JsonPropertyName("observation")]
    public string? Observation { get; set; }
    
    [JsonPropertyName("debt_service_ratio")]
    public string? DebtServiceRatio { get; set; }
}

public class LoanFraudHistory
{
    [JsonPropertyName("totalNoOfLoans")]
    public int TotalNoOfLoans { get; set; }
    
    [JsonPropertyName("totalNoOfInstitutions")]
    public int TotalNoOfInstitutions { get; set; }
    
    [JsonPropertyName("totalNoOfActiveLoans")]
    public int TotalNoOfActiveLoans { get; set; }
    
    [JsonPropertyName("totalNoOfClosedLoans")]
    public int TotalNoOfClosedLoans { get; set; }
    
    [JsonPropertyName("totalNoOfPerformingLoans")]
    public int TotalNoOfPerformingLoans { get; set; }
    
    [JsonPropertyName("totalNoOfDelinquentFacilities")]
    public int TotalNoOfDelinquentFacilities { get; set; }
    
    [JsonPropertyName("highestLoanAmount")]
    public decimal HighestLoanAmount { get; set; }
    
    [JsonPropertyName("totalMonthlyInstallment")]
    public decimal TotalMonthlyInstallment { get; set; }
    
    [JsonPropertyName("totalBorrowed")]
    public decimal TotalBorrowed { get; set; }
    
    [JsonPropertyName("totalOutstanding")]
    public decimal TotalOutstanding { get; set; }
    
    [JsonPropertyName("totalOverdue")]
    public decimal TotalOverdue { get; set; }
}

#endregion

#region KYC/Identity Verification DTOs

public class BvnVerificationRequest
{
    [JsonPropertyName("bvn")]
    public string Bvn { get; set; } = string.Empty;
}

public class BvnVerificationData
{
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
    
    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }
    
    [JsonPropertyName("middleName")]
    public string? MiddleName { get; set; }
    
    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; set; }
    
    [JsonPropertyName("phoneNumber1")]
    public string? PhoneNumber1 { get; set; }
}

public class BvnAdvancedData
{
    [JsonPropertyName("bvn")]
    public string? Bvn { get; set; }
    
    [JsonPropertyName("image")]
    public string? Image { get; set; }
    
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    
    [JsonPropertyName("gender")]
    public string? Gender { get; set; }
    
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
    
    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }
    
    [JsonPropertyName("middleName")]
    public string? MiddleName { get; set; }
    
    [JsonPropertyName("nameOnCard")]
    public string? NameOnCard { get; set; }
    
    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; set; }
    
    [JsonPropertyName("lgaOfOrigin")]
    public string? LgaOfOrigin { get; set; }
    
    [JsonPropertyName("watchListed")]
    public string? WatchListed { get; set; }
    
    [JsonPropertyName("phoneNumber1")]
    public string? PhoneNumber1 { get; set; }
    
    [JsonPropertyName("phoneNumber2")]
    public string? PhoneNumber2 { get; set; }
    
    [JsonPropertyName("maritalStatus")]
    public string? MaritalStatus { get; set; }
    
    [JsonPropertyName("stateOfOrigin")]
    public string? StateOfOrigin { get; set; }
    
    [JsonPropertyName("enrollmentBank")]
    public string? EnrollmentBank { get; set; }
    
    [JsonPropertyName("levelOfAccount")]
    public string? LevelOfAccount { get; set; }
    
    [JsonPropertyName("lgaOfResidence")]
    public string? LgaOfResidence { get; set; }
    
    [JsonPropertyName("enrollmentBranch")]
    public string? EnrollmentBranch { get; set; }
    
    [JsonPropertyName("registrationDate")]
    public string? RegistrationDate { get; set; }
    
    [JsonPropertyName("stateOfResidence")]
    public string? StateOfResidence { get; set; }
    
    [JsonPropertyName("residentialAddress")]
    public string? ResidentialAddress { get; set; }
}

public class TinVerificationRequest
{
    [JsonPropertyName("tax_identification_number")]
    public string TaxIdentificationNumber { get; set; } = string.Empty;
}

public class TinVerificationData
{
    [JsonPropertyName("search")]
    public string? Search { get; set; }
    
    [JsonPropertyName("taxpayer_name")]
    public string? TaxpayerName { get; set; }
    
    [JsonPropertyName("cac_reg_number")]
    public string? CacRegNumber { get; set; }
    
    [JsonPropertyName("firstin")]
    public string? Firstin { get; set; }
    
    [JsonPropertyName("jittin")]
    public string? Jittin { get; set; }
    
    [JsonPropertyName("tax_office")]
    public string? TaxOffice { get; set; }
    
    [JsonPropertyName("phone_number")]
    public string? PhoneNumber { get; set; }
    
    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

public class CacVerificationRequest
{
    [JsonPropertyName("rc_number")]
    public string RcNumber { get; set; } = string.Empty;
}

public class CacVerificationData
{
    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }
    
    [JsonPropertyName("rc_number")]
    public string? RcNumber { get; set; }
    
    [JsonPropertyName("company_type")]
    public string? CompanyType { get; set; }
    
    [JsonPropertyName("registration_date")]
    public string? RegistrationDate { get; set; }
    
    [JsonPropertyName("address")]
    public string? Address { get; set; }
    
    [JsonPropertyName("city")]
    public string? City { get; set; }
    
    [JsonPropertyName("state")]
    public string? State { get; set; }
    
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    
    [JsonPropertyName("status")]
    public string? Status { get; set; }
    
    [JsonPropertyName("nature_of_business")]
    public string? NatureOfBusiness { get; set; }
    
    [JsonPropertyName("share_capital")]
    public decimal? ShareCapital { get; set; }
    
    [JsonPropertyName("directors")]
    public List<CacDirectorData>? Directors { get; set; }
}

public class CacDirectorData
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("designation")]
    public string? Designation { get; set; }
    
    [JsonPropertyName("date_of_appointment")]
    public string? DateOfAppointment { get; set; }
}

public class NinVerificationRequest
{
    [JsonPropertyName("nin")]
    public string Nin { get; set; } = string.Empty;
}

public class NinVerificationData
{
    [JsonPropertyName("nin")]
    public string? Nin { get; set; }
    
    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }
    
    [JsonPropertyName("middleName")]
    public string? MiddleName { get; set; }
    
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
    
    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; set; }
    
    [JsonPropertyName("gender")]
    public string? Gender { get; set; }
    
    [JsonPropertyName("telephoneNumber")]
    public string? TelephoneNumber { get; set; }
    
    [JsonPropertyName("photo")]
    public string? Photo { get; set; }
}

#endregion

#region Credit Score Only Response

public class CreditScoreOnlyData
{
    [JsonPropertyName("score")]
    public int Score { get; set; }
    
    [JsonPropertyName("grade")]
    public string? Grade { get; set; }
    
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }
    
    [JsonPropertyName("generatedDate")]
    public DateTime? GeneratedDate { get; set; }
}

#endregion
