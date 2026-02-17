using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.FinancialStatement;

/// <summary>
/// Aggregate root for financial statements attached to a loan application.
/// Contains Balance Sheet, Income Statement, and Cash Flow Statement for a specific year.
/// </summary>
public class FinancialStatement : AggregateRoot
{
    public Guid LoanApplicationId { get; private set; }
    public int FinancialYear { get; private set; }
    public DateTime YearEndDate { get; private set; }
    public FinancialYearType YearType { get; private set; }
    public FinancialStatementStatus Status { get; private set; }
    public InputMethod InputMethod { get; private set; }
    public string Currency { get; private set; } = "NGN";

    // Audit information
    public string? AuditorName { get; private set; }
    public string? AuditorFirm { get; private set; }
    public DateTime? AuditDate { get; private set; }
    public string? AuditOpinion { get; private set; }

    // Source document
    public string? OriginalFileName { get; private set; }
    public string? FilePath { get; private set; }

    // Timestamps
    public Guid SubmittedByUserId { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public Guid? VerifiedByUserId { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public string? VerificationNotes { get; private set; }
    public string? RejectionReason { get; private set; }

    // Child entities
    public BalanceSheet? BalanceSheet { get; private set; }
    public IncomeStatement? IncomeStatement { get; private set; }
    public CashFlowStatement? CashFlowStatement { get; private set; }

    // Calculated ratios (computed from BS + IS + CF)
    public FinancialRatios? CalculatedRatios { get; private set; }

    private FinancialStatement() { }

    public static Result<FinancialStatement> Create(
        Guid loanApplicationId,
        int financialYear,
        DateTime yearEndDate,
        FinancialYearType yearType,
        InputMethod inputMethod,
        Guid submittedByUserId,
        string currency = "NGN",
        string? auditorName = null,
        string? auditorFirm = null,
        DateTime? auditDate = null,
        string? auditOpinion = null,
        string? originalFileName = null,
        string? filePath = null)
    {
        if (loanApplicationId == Guid.Empty)
            return Result.Failure<FinancialStatement>("Loan application ID is required");

        if (financialYear < 2000 || financialYear > DateTime.Now.Year + 5)
            return Result.Failure<FinancialStatement>("Invalid financial year");

        return Result.Success<FinancialStatement>(new FinancialStatement
        {
            LoanApplicationId = loanApplicationId,
            FinancialYear = financialYear,
            YearEndDate = yearEndDate,
            YearType = yearType,
            InputMethod = inputMethod,
            SubmittedByUserId = submittedByUserId,
            Currency = currency,
            AuditorName = auditorName,
            AuditorFirm = auditorFirm,
            AuditDate = auditDate,
            AuditOpinion = auditOpinion,
            OriginalFileName = originalFileName,
            FilePath = filePath,
            Status = FinancialStatementStatus.Draft,
            SubmittedAt = DateTime.UtcNow
        });
    }

    public Result SetBalanceSheet(BalanceSheet balanceSheet)
    {
        if (Status != FinancialStatementStatus.Draft)
            return Result.Failure("Cannot modify a non-draft statement");

        BalanceSheet = balanceSheet;
        RecalculateRatios();
        return Result.Success();
    }

    public Result SetIncomeStatement(IncomeStatement incomeStatement)
    {
        if (Status != FinancialStatementStatus.Draft)
            return Result.Failure("Cannot modify a non-draft statement");

        IncomeStatement = incomeStatement;
        RecalculateRatios();
        return Result.Success();
    }

    public Result SetCashFlowStatement(CashFlowStatement cashFlowStatement)
    {
        if (Status != FinancialStatementStatus.Draft)
            return Result.Failure("Cannot modify a non-draft statement");

        CashFlowStatement = cashFlowStatement;
        RecalculateRatios();
        return Result.Success();
    }

    public Result Submit()
    {
        if (Status != FinancialStatementStatus.Draft)
            return Result.Failure("Only draft statements can be submitted");

        if (BalanceSheet == null)
            return Result.Failure("Balance sheet is required");

        if (IncomeStatement == null)
            return Result.Failure("Income statement is required");

        if (!BalanceSheet.IsBalanced())
            return Result.Failure("Balance sheet does not balance (Assets != Liabilities + Equity)");

        Status = FinancialStatementStatus.PendingReview;
        return Result.Success();
    }

    public Result Verify(Guid verifiedByUserId, string? notes = null)
    {
        if (Status != FinancialStatementStatus.PendingReview)
            return Result.Failure("Only pending statements can be verified");

        Status = FinancialStatementStatus.Verified;
        VerifiedByUserId = verifiedByUserId;
        VerifiedAt = DateTime.UtcNow;
        VerificationNotes = notes;
        return Result.Success();
    }

    public Result Reject(string reason)
    {
        if (Status != FinancialStatementStatus.PendingReview)
            return Result.Failure("Only pending statements can be rejected");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Rejection reason is required");

        Status = FinancialStatementStatus.Rejected;
        RejectionReason = reason;
        return Result.Success();
    }

    public Result RevertToDraft()
    {
        if (Status == FinancialStatementStatus.Verified)
            return Result.Failure("Verified statements cannot be reverted");

        Status = FinancialStatementStatus.Draft;
        RejectionReason = null;
        return Result.Success();
    }

    private void RecalculateRatios()
    {
        if (BalanceSheet != null && IncomeStatement != null)
        {
            CalculatedRatios = FinancialRatios.Calculate(BalanceSheet, IncomeStatement, CashFlowStatement);
        }
    }

    public bool IsComplete => BalanceSheet != null && IncomeStatement != null;
    public bool HasCashFlow => CashFlowStatement != null;
}
