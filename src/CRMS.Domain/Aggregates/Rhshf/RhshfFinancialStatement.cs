using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// One financial year of a FAC's financial statements on a RH-SHF case — captured for the Financial
/// Analysis tab (3-year history, per the bank's requirement). Stored flat (one row per year), mirroring
/// NampFinancialStatement rather than Corporate's separate BalanceSheet/IncomeStatement/CashFlow
/// entities: those are keyed to the shared FinancialStatements table and can't be reused here. Ratio
/// analysis reuses the shared FinancialRatios math, computed in memory from these fields at read time —
/// so the numbers match Corporate without duplicating the ratio engine.
///
/// All line items are nullable: a FAC may file a partial statement, and the analysis layer treats a
/// missing value as absent rather than zero. Carries Corporate's verify lifecycle (Draft → review →
/// Verified/Rejected) so an officer can sign off on what was entered before it feeds a decision.
/// </summary>
public class RhshfFinancialStatement : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    public int FinancialYear { get; private set; }
    public string? YearEndDate { get; private set; }
    public RhshfFinancialYearType YearType { get; private set; }
    public RhshfFinancialInputMethod InputMethod { get; private set; }
    public RhshfFinancialStatementStatus Status { get; private set; }
    public string Currency { get; private set; } = "NGN";

    // ── Income statement ─────────────────────────────────────────────────────
    public decimal? Revenue { get; private set; }
    public decimal? OtherOperatingIncome { get; private set; }
    public decimal? CostOfSales { get; private set; }
    public decimal? SellingExpenses { get; private set; }
    public decimal? AdministrativeExpenses { get; private set; }
    public decimal? DepreciationAmortization { get; private set; }
    public decimal? OtherOperatingExpenses { get; private set; }
    public decimal? InterestIncome { get; private set; }
    public decimal? InterestExpense { get; private set; }
    public decimal? OtherFinanceCosts { get; private set; }
    public decimal? IncomeTaxExpense { get; private set; }
    public decimal? DividendsDeclared { get; private set; }

    // ── Balance sheet — assets ───────────────────────────────────────────────
    public decimal? CashAndCashEquivalents { get; private set; }
    public decimal? TradeReceivables { get; private set; }
    public decimal? Inventory { get; private set; }
    public decimal? PrepaidExpenses { get; private set; }
    public decimal? OtherCurrentAssets { get; private set; }
    public decimal? PropertyPlantEquipment { get; private set; }
    public decimal? IntangibleAssets { get; private set; }
    public decimal? LongTermInvestments { get; private set; }
    public decimal? DeferredTaxAssets { get; private set; }
    public decimal? OtherNonCurrentAssets { get; private set; }

    // ── Balance sheet — liabilities ──────────────────────────────────────────
    public decimal? TradePayables { get; private set; }
    public decimal? ShortTermBorrowings { get; private set; }
    public decimal? CurrentPortionLongTermDebt { get; private set; }
    public decimal? AccruedExpenses { get; private set; }
    public decimal? TaxPayable { get; private set; }
    public decimal? OtherCurrentLiabilities { get; private set; }
    public decimal? LongTermDebt { get; private set; }
    public decimal? DeferredTaxLiabilities { get; private set; }
    public decimal? Provisions { get; private set; }
    public decimal? OtherNonCurrentLiabilities { get; private set; }

    // ── Balance sheet — equity ───────────────────────────────────────────────
    public decimal? ShareCapital { get; private set; }
    public decimal? SharePremium { get; private set; }
    public decimal? RetainedEarnings { get; private set; }
    public decimal? OtherReserves { get; private set; }

    // ── Cash flow ────────────────────────────────────────────────────────────
    public decimal? CfProfitBeforeTax { get; private set; }
    public decimal? CfDepreciationAmortization { get; private set; }
    public decimal? CfInterestExpenseAddBack { get; private set; }
    public decimal? CfChangesInWorkingCapital { get; private set; }
    public decimal? CfTaxPaid { get; private set; }
    public decimal? CfOtherOperatingAdjustments { get; private set; }
    public decimal? CfPurchaseOfPpe { get; private set; }
    public decimal? CfSaleOfPpe { get; private set; }
    public decimal? CfPurchaseOfInvestments { get; private set; }
    public decimal? CfSaleOfInvestments { get; private set; }
    public decimal? CfInterestReceived { get; private set; }
    public decimal? CfDividendsReceived { get; private set; }
    public decimal? CfOtherInvestingActivities { get; private set; }
    public decimal? CfProceedsFromBorrowings { get; private set; }
    public decimal? CfRepaymentOfBorrowings { get; private set; }
    public decimal? CfInterestPaid { get; private set; }
    public decimal? CfDividendsPaid { get; private set; }
    public decimal? CfProceedsFromShareIssue { get; private set; }
    public decimal? CfOtherFinancingActivities { get; private set; }
    public decimal? CfOpeningCashBalance { get; private set; }

    // ── Auditor ──────────────────────────────────────────────────────────────
    public string? AuditorName { get; private set; }
    public string? AuditorFirm { get; private set; }
    public string? AuditDate { get; private set; }
    public string? AuditOpinion { get; private set; }

    // ── Source document + review workflow ────────────────────────────────────
    public string? OriginalFileName { get; private set; }
    public string? FilePath { get; private set; }
    public Guid SubmittedByUserId { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public Guid? VerifiedByUserId { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public string? VerificationNotes { get; private set; }
    public string? RejectionReason { get; private set; }

    protected RhshfFinancialStatement() { }

    public static Result<RhshfFinancialStatement> Create(
        Guid rhshfCreditProfileId, int financialYear, string? yearEndDate,
        RhshfFinancialYearType yearType, RhshfFinancialInputMethod inputMethod, Guid submittedByUserId,
        string currency = "NGN", string? originalFileName = null, string? filePath = null)
    {
        if (rhshfCreditProfileId == Guid.Empty)
            return Result.Failure<RhshfFinancialStatement>("Credit profile is required.");
        if (financialYear < 2000 || financialYear > DateTime.UtcNow.Year + 1)
            return Result.Failure<RhshfFinancialStatement>("Invalid financial year.");

        return Result.Success(new RhshfFinancialStatement
        {
            RhshfCreditProfileId = rhshfCreditProfileId,
            FinancialYear = financialYear,
            YearEndDate = yearEndDate,
            YearType = yearType,
            InputMethod = inputMethod,
            Status = RhshfFinancialStatementStatus.Draft,
            Currency = string.IsNullOrWhiteSpace(currency) ? "NGN" : currency,
            OriginalFileName = originalFileName,
            FilePath = filePath,
            SubmittedByUserId = submittedByUserId,
            SubmittedAt = DateTime.UtcNow,
        });
    }

    /// <summary>Replaces all captured line items. Allowed only while Draft — a verified year is locked.</summary>
    public Result SetFigures(RhshfFinancialStatementFigures f)
    {
        if (Status is RhshfFinancialStatementStatus.Verified)
            return Result.Failure("A verified statement cannot be edited. Revert it to draft first.");

        Revenue = f.Revenue; OtherOperatingIncome = f.OtherOperatingIncome; CostOfSales = f.CostOfSales;
        SellingExpenses = f.SellingExpenses; AdministrativeExpenses = f.AdministrativeExpenses;
        DepreciationAmortization = f.DepreciationAmortization; OtherOperatingExpenses = f.OtherOperatingExpenses;
        InterestIncome = f.InterestIncome; InterestExpense = f.InterestExpense; OtherFinanceCosts = f.OtherFinanceCosts;
        IncomeTaxExpense = f.IncomeTaxExpense; DividendsDeclared = f.DividendsDeclared;

        CashAndCashEquivalents = f.CashAndCashEquivalents; TradeReceivables = f.TradeReceivables; Inventory = f.Inventory;
        PrepaidExpenses = f.PrepaidExpenses; OtherCurrentAssets = f.OtherCurrentAssets;
        PropertyPlantEquipment = f.PropertyPlantEquipment; IntangibleAssets = f.IntangibleAssets;
        LongTermInvestments = f.LongTermInvestments; DeferredTaxAssets = f.DeferredTaxAssets;
        OtherNonCurrentAssets = f.OtherNonCurrentAssets;

        TradePayables = f.TradePayables; ShortTermBorrowings = f.ShortTermBorrowings;
        CurrentPortionLongTermDebt = f.CurrentPortionLongTermDebt; AccruedExpenses = f.AccruedExpenses;
        TaxPayable = f.TaxPayable; OtherCurrentLiabilities = f.OtherCurrentLiabilities;
        LongTermDebt = f.LongTermDebt; DeferredTaxLiabilities = f.DeferredTaxLiabilities;
        Provisions = f.Provisions; OtherNonCurrentLiabilities = f.OtherNonCurrentLiabilities;

        ShareCapital = f.ShareCapital; SharePremium = f.SharePremium; RetainedEarnings = f.RetainedEarnings;
        OtherReserves = f.OtherReserves;

        CfProfitBeforeTax = f.CfProfitBeforeTax; CfDepreciationAmortization = f.CfDepreciationAmortization;
        CfInterestExpenseAddBack = f.CfInterestExpenseAddBack; CfChangesInWorkingCapital = f.CfChangesInWorkingCapital;
        CfTaxPaid = f.CfTaxPaid; CfOtherOperatingAdjustments = f.CfOtherOperatingAdjustments;
        CfPurchaseOfPpe = f.CfPurchaseOfPpe; CfSaleOfPpe = f.CfSaleOfPpe;
        CfPurchaseOfInvestments = f.CfPurchaseOfInvestments; CfSaleOfInvestments = f.CfSaleOfInvestments;
        CfInterestReceived = f.CfInterestReceived; CfDividendsReceived = f.CfDividendsReceived;
        CfOtherInvestingActivities = f.CfOtherInvestingActivities;
        CfProceedsFromBorrowings = f.CfProceedsFromBorrowings; CfRepaymentOfBorrowings = f.CfRepaymentOfBorrowings;
        CfInterestPaid = f.CfInterestPaid; CfDividendsPaid = f.CfDividendsPaid;
        CfProceedsFromShareIssue = f.CfProceedsFromShareIssue; CfOtherFinancingActivities = f.CfOtherFinancingActivities;
        CfOpeningCashBalance = f.CfOpeningCashBalance;

        AuditorName = f.AuditorName; AuditorFirm = f.AuditorFirm; AuditDate = f.AuditDate; AuditOpinion = f.AuditOpinion;
        return Result.Success();
    }

    public Result SetYearMeta(int financialYear, string? yearEndDate, RhshfFinancialYearType yearType, string currency)
    {
        if (Status is RhshfFinancialStatementStatus.Verified)
            return Result.Failure("A verified statement cannot be edited. Revert it to draft first.");
        if (financialYear < 2000 || financialYear > DateTime.UtcNow.Year + 1)
            return Result.Failure("Invalid financial year.");

        FinancialYear = financialYear;
        YearEndDate = yearEndDate;
        YearType = yearType;
        Currency = string.IsNullOrWhiteSpace(currency) ? Currency : currency;
        return Result.Success();
    }

    public Result Submit()
    {
        if (Status != RhshfFinancialStatementStatus.Draft)
            return Result.Failure("Only a draft statement can be submitted for review.");
        if (Revenue is null)
            return Result.Failure("Revenue is required before submitting a statement.");
        Status = RhshfFinancialStatementStatus.PendingReview;
        return Result.Success();
    }

    public Result Verify(Guid verifiedByUserId, string? notes)
    {
        if (Status is not (RhshfFinancialStatementStatus.PendingReview or RhshfFinancialStatementStatus.Draft))
            return Result.Failure("Only a draft or pending statement can be verified.");
        Status = RhshfFinancialStatementStatus.Verified;
        VerifiedByUserId = verifiedByUserId;
        VerifiedAt = DateTime.UtcNow;
        VerificationNotes = notes;
        RejectionReason = null;
        return Result.Success();
    }

    public Result Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("A rejection reason is required.");
        if (Status == RhshfFinancialStatementStatus.Verified)
            return Result.Failure("A verified statement cannot be rejected. Revert it to draft first.");
        Status = RhshfFinancialStatementStatus.Rejected;
        RejectionReason = reason;
        return Result.Success();
    }

    public Result RevertToDraft()
    {
        Status = RhshfFinancialStatementStatus.Draft;
        VerifiedByUserId = null;
        VerifiedAt = null;
        RejectionReason = null;
        return Result.Success();
    }
}

/// <summary>Carrier for the full set of line items — keeps Create/SetFigures from needing a 70-argument
/// signature. All nullable; absent means not reported.</summary>
public record RhshfFinancialStatementFigures(
    decimal? Revenue, decimal? OtherOperatingIncome, decimal? CostOfSales, decimal? SellingExpenses,
    decimal? AdministrativeExpenses, decimal? DepreciationAmortization, decimal? OtherOperatingExpenses,
    decimal? InterestIncome, decimal? InterestExpense, decimal? OtherFinanceCosts, decimal? IncomeTaxExpense,
    decimal? DividendsDeclared,
    decimal? CashAndCashEquivalents, decimal? TradeReceivables, decimal? Inventory, decimal? PrepaidExpenses,
    decimal? OtherCurrentAssets, decimal? PropertyPlantEquipment, decimal? IntangibleAssets,
    decimal? LongTermInvestments, decimal? DeferredTaxAssets, decimal? OtherNonCurrentAssets,
    decimal? TradePayables, decimal? ShortTermBorrowings, decimal? CurrentPortionLongTermDebt,
    decimal? AccruedExpenses, decimal? TaxPayable, decimal? OtherCurrentLiabilities, decimal? LongTermDebt,
    decimal? DeferredTaxLiabilities, decimal? Provisions, decimal? OtherNonCurrentLiabilities,
    decimal? ShareCapital, decimal? SharePremium, decimal? RetainedEarnings, decimal? OtherReserves,
    decimal? CfProfitBeforeTax, decimal? CfDepreciationAmortization, decimal? CfInterestExpenseAddBack,
    decimal? CfChangesInWorkingCapital, decimal? CfTaxPaid, decimal? CfOtherOperatingAdjustments,
    decimal? CfPurchaseOfPpe, decimal? CfSaleOfPpe, decimal? CfPurchaseOfInvestments, decimal? CfSaleOfInvestments,
    decimal? CfInterestReceived, decimal? CfDividendsReceived, decimal? CfOtherInvestingActivities,
    decimal? CfProceedsFromBorrowings, decimal? CfRepaymentOfBorrowings, decimal? CfInterestPaid,
    decimal? CfDividendsPaid, decimal? CfProceedsFromShareIssue, decimal? CfOtherFinancingActivities,
    decimal? CfOpeningCashBalance,
    string? AuditorName, string? AuditorFirm, string? AuditDate, string? AuditOpinion);
