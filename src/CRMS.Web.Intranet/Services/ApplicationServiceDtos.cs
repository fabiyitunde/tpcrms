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
    public class EnhancedReportingData
    {
        public int ApplicationsReceived { get; set; }
        public int ApplicationsGrowth { get; set; }
        public int Approved { get; set; }
        public int ApprovalRate { get; set; }
        public decimal AvgProcessingDays { get; set; }
        public int ProcessingImprovement { get; set; }
        public decimal DisbursedAmount { get; set; }
        public int DisbursementGrowth { get; set; }
        public int Rejected { get; set; }
        public int InReview { get; set; }
        public List<FunnelStageData> FunnelStages { get; set; } = new();
        public List<ProductPortfolioData> PortfolioByProduct { get; set; } = new();
        public int SlaCompliance { get; set; }
        public int WithinSla { get; set; }
        public int BreachedSla { get; set; }
    }
    public class FunnelStageData
    {
        public string Stage { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Percentage { get; set; }
    }
    public class ProductPortfolioData
    {
        public string ProductName { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }
    public class CommitteeReviewSummary
    {
        public Guid ReviewId { get; set; }

        public Guid ApplicationId { get; set; }

        public string ApplicationNumber { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        public decimal RequestedAmount { get; set; }

        public decimal Amount { get; set; }

        public string CommitteeType { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime CirculatedAt { get; set; }

        public DateTime DueDate { get; set; }

        public bool HasVoted { get; set; }

        public string? MyVote { get; set; }

        public int VotesCast { get; set; }

        public int TotalMembers { get; set; }
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
    public class OverdueWorkflowItem
    {
        public Guid ApplicationId { get; set; }

        public string ApplicationNumber { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        public string Stage { get; set; } = string.Empty;

        public string AssignedTo { get; set; } = string.Empty;

        public DateTime SLABreachedAt { get; set; }
    }
    public class LoanPackResult
    {
        public Guid LoanPackId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string? StoragePath { get; set; }
    }

    public class OfferLetterResult
    {
        public Guid OfferLetterId { get; set; }

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
    public class UploadCollateralDocumentRequest
    {
        public Guid CollateralId { get; set; }

        public string DocumentType { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public Stream FileContent { get; set; } = Stream.Null;

        public long FileSize { get; set; }

        public string ContentType { get; set; } = string.Empty;

        public string? Description { get; set; }
    }

    public class CollateralDocumentResult
    {
        public Guid Id { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string DocumentType { get; set; } = string.Empty;
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

        public List<CollateralDocumentInfo> Documents { get; set; } = [];
    }

    public class CollateralDocumentInfo
    {
        public Guid Id { get; set; }

        public string DocumentType { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        public bool IsVerified { get; set; }

        public DateTime UploadedAt { get; set; }
    }

    public class PerformanceReportData
    {
        public decimal AvgProcessingTimeDays { get; set; }
        public decimal PrevAvgProcessingTimeDays { get; set; }
        public decimal SlaComplianceRate { get; set; }
        public decimal PrevSlaComplianceRate { get; set; }
        public int ApplicationsProcessed { get; set; }
        public int PrevApplicationsProcessed { get; set; }
        public int SlaBreaches { get; set; }
        public List<StagePerformanceData> StagePerformance { get; set; } = [];
        public List<PerformerData> TopPerformers { get; set; } = [];
        public List<TeamPerformanceData> TeamPerformance { get; set; } = [];
    }

    public class StagePerformanceData
    {
        public string Name { get; set; } = string.Empty;
        public decimal TargetHours { get; set; }
        public decimal ActualHours { get; set; }
    }

    public class PerformerData
    {
        public string Name { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public int ProcessedCount { get; set; }
        public decimal AvgHours { get; set; }
        public int SlaCompliance { get; set; }
    }

    public class TeamPerformanceData
    {
        public string Name { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public int TotalProcessed { get; set; }
        public decimal AvgHours { get; set; }
        public int SlaCompliance { get; set; }
        public bool TrendUp { get; set; }
    }

    public class CommitteeReportData
    {
        public int TotalReviews { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public decimal ApprovalRate { get; set; }
        public decimal AvgReviewDays { get; set; }
        public List<CommitteeTypeData> ByCommitteeType { get; set; } = [];
        public List<CommitteeMemberData> MemberStats { get; set; } = [];
    }

    public class CommitteeTypeData
    {
        public string Name { get; set; } = string.Empty;
        public int Reviews { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public decimal ApprovalRate { get; set; }
        public decimal AvgReviewDays { get; set; }
    }

    public class CommitteeMemberData
    {
        public string Name { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public int VotesCast { get; set; }
        public int ApproveVotes { get; set; }
        public int RejectVotes { get; set; }
        public decimal ParticipationRate { get; set; }
    }


}
