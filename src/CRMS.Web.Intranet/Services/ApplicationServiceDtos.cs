namespace CRMS.Web.Intranet.Services
{
    public class BalanceSheetInputDto
    {
        public decimal CashAndCashEquivalents { get; set; }
        public decimal TradeReceivables { get; set; }
        public decimal Inventory { get; set; }
        public decimal PrepaidExpenses { get; set; }
        public decimal OtherCurrentAssets { get; set; }
        public decimal PropertyPlantEquipment { get; set; }
        public decimal IntangibleAssets { get; set; }
        public decimal LongTermInvestments { get; set; }
        public decimal DeferredTaxAssets { get; set; }
        public decimal OtherNonCurrentAssets { get; set; }
        public decimal TradePayables { get; set; }
        public decimal ShortTermBorrowings { get; set; }
        public decimal CurrentPortionLongTermDebt { get; set; }
        public decimal AccruedExpenses { get; set; }
        public decimal TaxPayable { get; set; }
        public decimal OtherCurrentLiabilities { get; set; }
        public decimal LongTermDebt { get; set; }
        public decimal DeferredTaxLiabilities { get; set; }
        public decimal Provisions { get; set; }
        public decimal OtherNonCurrentLiabilities { get; set; }
        public decimal ShareCapital { get; set; }
        public decimal SharePremium { get; set; }
        public decimal RetainedEarnings { get; set; }
        public decimal OtherReserves { get; set; }
    }

    public class IncomeStatementInputDto
    {
        public decimal Revenue { get; set; }
        public decimal OtherOperatingIncome { get; set; }
        public decimal CostOfSales { get; set; }
        public decimal SellingExpenses { get; set; }
        public decimal AdministrativeExpenses { get; set; }
        public decimal DepreciationAmortization { get; set; }
        public decimal OtherOperatingExpenses { get; set; }
        public decimal InterestIncome { get; set; }
        public decimal InterestExpense { get; set; }
        public decimal OtherFinanceCosts { get; set; }
        public decimal IncomeTaxExpense { get; set; }
        public decimal DividendsDeclared { get; set; }
    }

    public class CashFlowInputDto
    {
        public decimal ProfitBeforeTax { get; set; }
        public decimal DepreciationAmortization { get; set; }
        public decimal InterestExpenseAddBack { get; set; }
        public decimal ChangesInWorkingCapital { get; set; }
        public decimal TaxPaid { get; set; }
        public decimal OtherOperatingAdjustments { get; set; }
        public decimal PurchaseOfPPE { get; set; }
        public decimal SaleOfPPE { get; set; }
        public decimal PurchaseOfInvestments { get; set; }
        public decimal SaleOfInvestments { get; set; }
        public decimal InterestReceived { get; set; }
        public decimal DividendsReceived { get; set; }
        public decimal ProceedsFromBorrowings { get; set; }
        public decimal RepaymentOfBorrowings { get; set; }
        public decimal InterestPaid { get; set; }
        public decimal DividendsPaid { get; set; }
        public decimal ProceedsFromShareIssue { get; set; }
        public decimal OpeningCashBalance { get; set; }
    }

    public class FinancialStatementDetailDto
    {
        public Guid Id { get; set; }

        public int Year { get; set; }

        public DateTime YearEndDate { get; set; }

        public string YearType { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public BalanceSheetDetailDto? BalanceSheet { get; set; }

        public IncomeStatementDetailDto? IncomeStatement { get; set; }

        public CashFlowDetailDto? CashFlow { get; set; }
    }
    public class BalanceSheetDetailDto
    {
        public decimal CashAndCashEquivalents { get; set; }

        public decimal TradeReceivables { get; set; }

        public decimal Inventory { get; set; }

        public decimal PrepaidExpenses { get; set; }

        public decimal OtherCurrentAssets { get; set; }

        public decimal PropertyPlantEquipment { get; set; }

        public decimal IntangibleAssets { get; set; }

        public decimal LongTermInvestments { get; set; }

        public decimal DeferredTaxAssets { get; set; }

        public decimal OtherNonCurrentAssets { get; set; }

        public decimal TradePayables { get; set; }

        public decimal ShortTermBorrowings { get; set; }

        public decimal CurrentPortionLongTermDebt { get; set; }

        public decimal AccruedExpenses { get; set; }

        public decimal TaxPayable { get; set; }

        public decimal OtherCurrentLiabilities { get; set; }

        public decimal LongTermDebt { get; set; }

        public decimal DeferredTaxLiabilities { get; set; }

        public decimal Provisions { get; set; }

        public decimal OtherNonCurrentLiabilities { get; set; }

        public decimal ShareCapital { get; set; }

        public decimal SharePremium { get; set; }

        public decimal RetainedEarnings { get; set; }

        public decimal OtherReserves { get; set; }
    }
    public class CashFlowDetailDto
    {
        public decimal ProfitBeforeTax { get; set; }

        public decimal DepreciationAmortization { get; set; }

        public decimal InterestExpenseAddBack { get; set; }

        public decimal ChangesInWorkingCapital { get; set; }

        public decimal TaxPaid { get; set; }

        public decimal OtherOperatingAdjustments { get; set; }

        public decimal PurchaseOfPPE { get; set; }

        public decimal SaleOfPPE { get; set; }

        public decimal PurchaseOfInvestments { get; set; }

        public decimal SaleOfInvestments { get; set; }

        public decimal InterestReceived { get; set; }

        public decimal DividendsReceived { get; set; }

        public decimal ProceedsFromBorrowings { get; set; }

        public decimal RepaymentOfBorrowings { get; set; }

        public decimal InterestPaid { get; set; }

        public decimal DividendsPaid { get; set; }

        public decimal ProceedsFromShareIssue { get; set; }

        public decimal OpeningCashBalance { get; set; }
    }
    public class IncomeStatementDetailDto
    {
        public decimal Revenue { get; set; }

        public decimal OtherOperatingIncome { get; set; }

        public decimal CostOfSales { get; set; }

        public decimal SellingExpenses { get; set; }

        public decimal AdministrativeExpenses { get; set; }

        public decimal DepreciationAmortization { get; set; }

        public decimal OtherOperatingExpenses { get; set; }

        public decimal InterestIncome { get; set; }

        public decimal InterestExpense { get; set; }

        public decimal OtherFinanceCosts { get; set; }

        public decimal IncomeTaxExpense { get; set; }

        public decimal DividendsDeclared { get; set; }
    }
    public class CreateFinancialStatementFromExcelRequest
    {
        public int Year { get; set; }

        public string YearType { get; set; } = "Audited";

        public BalanceSheetData BalanceSheet { get; set; } = new BalanceSheetData();

        public IncomeStatementData IncomeStatement { get; set; } = new IncomeStatementData();

        public CashFlowData? CashFlow { get; set; }
    }
    public class WorkflowInstanceInfo
    {
        public Guid Id { get; set; }

        public string CurrentStatus { get; set; } = string.Empty;
    }
    public class DocumentResult
    {
        public Guid Id { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }
    public class UploadDocumentRequest
    {
        public Guid ApplicationId { get; set; }

        public string Category { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public Stream FileContent { get; set; } = Stream.Null;

        public long FileSize { get; set; }

        public string ContentType { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
    public class GuarantorResult
    {
        public Guid Id { get; set; }

        public string Reference { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }
    public class AddGuarantorRequest
    {
        public Guid LoanApplicationId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string? BVN { get; set; }

        public string GuaranteeType { get; set; } = "Limited";

        public string? Relationship { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string? Address { get; set; }

        public decimal? GuaranteeLimit { get; set; }

        public bool IsDirector { get; set; }

        public bool IsShareholder { get; set; }

        public decimal? ShareholdingPercentage { get; set; }

        public decimal? DeclaredNetWorth { get; set; }

        public string? Occupation { get; set; }

        public string? EmployerName { get; set; }

        public decimal? MonthlyIncome { get; set; }
    }
    public class AddCollateralRequest
    {
        public Guid LoanApplicationId { get; set; }

        public string Type { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string? AssetIdentifier { get; set; }

        public string? Location { get; set; }

        public string? OwnerName { get; set; }

        public string? OwnershipType { get; set; }
    }
    public class CollateralResult
    {
        public Guid Id { get; set; }

        public string Reference { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }
    public class UserSummary
    {
        public Guid Id { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public string FullName => FirstName + " " + LastName;

        public string Initials => $"{FirstName?.FirstOrDefault()}{LastName?.FirstOrDefault()}";
    }
    public class AuditLogSummary
    {
        public Guid Id { get; set; }

        public DateTime Timestamp { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public string EntityType { get; set; } = string.Empty;

        public string EntityId { get; set; } = string.Empty;

        public string Details { get; set; } = string.Empty;

        public string IpAddress { get; set; } = string.Empty;
    }
    public class ReportingMetrics
    {
        public int ApplicationsReceived { get; set; }

        public int Approved { get; set; }

        public int ApprovalRate { get; set; }

        public decimal AvgProcessingDays { get; set; }

        public decimal DisbursedAmount { get; set; }
    }
    public class CommitteeReviewSummary
    {
        public Guid ReviewId { get; set; }

        public Guid ApplicationId { get; set; }

        public string ApplicationNumber { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        public decimal RequestedAmount { get; set; }

        public string CommitteeType { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime CirculatedAt { get; set; }

        public DateTime DueDate { get; set; }

        public bool HasVoted { get; set; }

        public string? MyVote { get; set; }
    }
    public class WorkflowQueueItem
    {
        public Guid ApplicationId { get; set; }

        public string ApplicationNumber { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        public string Stage { get; set; } = string.Empty;

        public bool IsOverdue { get; set; }

        public string? AssignedTo { get; set; }
    }
    public class WorkflowQueueSummary
    {
        public string Stage { get; set; } = string.Empty;

        public int Count { get; set; }

        public int OverdueCount { get; set; }
    }
    public class LoanPackResult
    {
        public Guid LoanPackId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string? StoragePath { get; set; }
    }
    public class UpdateGuarantorRequest
    {
        public string FullName { get; set; } = string.Empty;

        public string? BVN { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string? Address { get; set; }

        public string? Relationship { get; set; }

        public string GuaranteeType { get; set; } = "Unlimited";

        public bool IsDirector { get; set; }

        public bool IsShareholder { get; set; }

        public decimal? ShareholdingPercentage { get; set; }

        public string? Occupation { get; set; }

        public string? EmployerName { get; set; }

        public decimal? MonthlyIncome { get; set; }

        public decimal? DeclaredNetWorth { get; set; }

        public decimal? GuaranteeLimit { get; set; }
    }
    public class GuarantorDetailDto
    {
        public Guid Id { get; set; }

        public string GuarantorReference { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string GuaranteeType { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string? BVN { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string? Address { get; set; }

        public string? RelationshipToApplicant { get; set; }

        public bool IsDirector { get; set; }

        public bool IsShareholder { get; set; }

        public decimal? ShareholdingPercentage { get; set; }

        public string? Occupation { get; set; }

        public string? EmployerName { get; set; }

        public decimal? MonthlyIncome { get; set; }

        public decimal? DeclaredNetWorth { get; set; }

        public decimal? VerifiedNetWorth { get; set; }

        public decimal? GuaranteeLimit { get; set; }

        public bool IsUnlimited { get; set; }

        public DateTime? GuaranteeStartDate { get; set; }

        public DateTime? GuaranteeEndDate { get; set; }

        public int? CreditScore { get; set; }

        public string? CreditScoreGrade { get; set; }

        public DateTime? CreditCheckDate { get; set; }

        public bool HasCreditIssues { get; set; }

        public string? CreditIssuesSummary { get; set; }

        public int ExistingGuaranteeCount { get; set; }

        public decimal? TotalExistingGuarantees { get; set; }

        public bool HasSignedGuaranteeAgreement { get; set; }

        public DateTime? AgreementSignedDate { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public string? RejectionReason { get; set; }
    }
    public class UpdateCollateralRequest
    {
        public string Type { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string? AssetIdentifier { get; set; }

        public string? Location { get; set; }

        public string? OwnerName { get; set; }

        public string? OwnershipType { get; set; }
    }
    public class CollateralDetailDto
    {
        public Guid Id { get; set; }

        public string CollateralReference { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string PerfectionStatus { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string? AssetIdentifier { get; set; }

        public string? Location { get; set; }

        public string? OwnerName { get; set; }

        public string? OwnershipType { get; set; }

        public decimal? MarketValue { get; set; }

        public decimal? ForcedSaleValue { get; set; }

        public decimal? AcceptableValue { get; set; }

        public decimal? HaircutPercentage { get; set; }

        public string? Currency { get; set; }

        public DateTime? LastValuationDate { get; set; }

        public string? LienType { get; set; }

        public string? LienReference { get; set; }

        public DateTime? LienRegistrationDate { get; set; }

        public bool IsInsured { get; set; }

        public string? InsurancePolicyNumber { get; set; }

        public decimal? InsuredValue { get; set; }

        public DateTime? InsuranceExpiryDate { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public string? RejectionReason { get; set; }
    }


}
