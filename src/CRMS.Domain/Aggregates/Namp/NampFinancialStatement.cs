using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// One year of audited financials submitted via NAMP webhook, unpacked at Loan Officer recall.
/// Only present for MechanisationCompany applicants (empty array for individual categories).
/// </summary>
public class NampFinancialStatement : Entity
{
    public Guid NampApplicationId { get; private set; }

    // ── Metadata ───────────────────────────────────────────────────────────
    public int FinancialYear { get; private set; }
    public string? YearEndDate { get; private set; }
    public string FinancialYearType { get; private set; } = "Audited";
    public string InputMethod { get; private set; } = "ExcelUpload";
    public string Currency { get; private set; } = "NGN";

    // ── Income Statement ───────────────────────────────────────────────────
    public decimal? Revenue { get; private set; }
    public decimal? OtherOperatingIncome { get; private set; }
    public decimal? CostOfSales { get; private set; }
    public decimal? GrossProfit { get; private set; }
    public decimal? SellingExpenses { get; private set; }
    public decimal? AdministrativeExpenses { get; private set; }
    public decimal? DepreciationAmortization { get; private set; }
    public decimal? OtherOperatingExpenses { get; private set; }
    public decimal? OperatingExpenses { get; private set; }
    public decimal? Ebitda { get; private set; }
    public decimal? InterestIncome { get; private set; }
    public decimal? InterestExpense { get; private set; }
    public decimal? OtherFinanceCosts { get; private set; }
    public decimal? IncomeTaxExpense { get; private set; }
    public decimal? DividendsDeclared { get; private set; }
    public decimal? NetProfit { get; private set; }

    // ── Balance Sheet — Current Assets ─────────────────────────────────────
    public decimal? CashAndCashEquivalents { get; private set; }
    public decimal? TradeReceivables { get; private set; }
    public decimal? Inventory { get; private set; }
    public decimal? PrepaidExpenses { get; private set; }
    public decimal? OtherCurrentAssets { get; private set; }
    public decimal? TotalCurrentAssets { get; private set; }

    // ── Balance Sheet — Non-Current Assets ─────────────────────────────────
    public decimal? PropertyPlantEquipment { get; private set; }
    public decimal? IntangibleAssets { get; private set; }
    public decimal? LongTermInvestments { get; private set; }
    public decimal? DeferredTaxAssets { get; private set; }
    public decimal? OtherNonCurrentAssets { get; private set; }
    public decimal? TotalNonCurrentAssets { get; private set; }
    public decimal? TotalAssets { get; private set; }

    // ── Balance Sheet — Current Liabilities ────────────────────────────────
    public decimal? TradePayables { get; private set; }
    public decimal? ShortTermBorrowings { get; private set; }
    public decimal? CurrentPortionLongTermDebt { get; private set; }
    public decimal? AccruedExpenses { get; private set; }
    public decimal? TaxPayable { get; private set; }
    public decimal? OtherCurrentLiabilities { get; private set; }
    public decimal? TotalCurrentLiabilities { get; private set; }

    // ── Balance Sheet — Non-Current Liabilities ────────────────────────────
    public decimal? LongTermDebt { get; private set; }
    public decimal? DeferredTaxLiabilities { get; private set; }
    public decimal? Provisions { get; private set; }
    public decimal? OtherNonCurrentLiabilities { get; private set; }
    public decimal? TotalNonCurrentLiabilities { get; private set; }
    public decimal? TotalLiabilities { get; private set; }

    // ── Balance Sheet — Equity ─────────────────────────────────────────────
    public decimal? ShareCapital { get; private set; }
    public decimal? SharePremium { get; private set; }
    public decimal? RetainedEarnings { get; private set; }
    public decimal? OtherReserves { get; private set; }
    public decimal? TotalEquity { get; private set; }

    // ── Cash Flow — Operating Activities ───────────────────────────────────
    public decimal? CfProfitBeforeTax { get; private set; }
    public decimal? CfDepreciationAmortization { get; private set; }
    public decimal? CfInterestExpenseAddBack { get; private set; }
    public decimal? CfChangesInWorkingCapital { get; private set; }
    public decimal? CfTaxPaid { get; private set; }
    public decimal? CfOtherOperatingAdjustments { get; private set; }
    public decimal? NetCashFromOperating { get; private set; }

    // ── Cash Flow — Investing Activities ───────────────────────────────────
    public decimal? CfPurchaseOfPpe { get; private set; }
    public decimal? CfSaleOfPpe { get; private set; }
    public decimal? CfPurchaseOfInvestments { get; private set; }
    public decimal? CfSaleOfInvestments { get; private set; }
    public decimal? CfInterestReceived { get; private set; }
    public decimal? CfDividendsReceived { get; private set; }
    public decimal? CfOtherInvestingActivities { get; private set; }
    public decimal? NetCashFromInvesting { get; private set; }

    // ── Cash Flow — Financing Activities ───────────────────────────────────
    public decimal? CfProceedsFromBorrowings { get; private set; }
    public decimal? CfRepaymentOfBorrowings { get; private set; }
    public decimal? CfInterestPaid { get; private set; }
    public decimal? CfDividendsPaid { get; private set; }
    public decimal? CfProceedsFromShareIssue { get; private set; }
    public decimal? CfOtherFinancingActivities { get; private set; }
    public decimal? NetCashFromFinancing { get; private set; }
    public decimal? NetChangeInCash { get; private set; }
    public decimal? CfOpeningCashBalance { get; private set; }
    public decimal? ClosingCashBalance { get; private set; }

    // ── Audit ──────────────────────────────────────────────────────────────
    public string? AuditorName { get; private set; }
    public string? AuditorFirm { get; private set; }
    public string? AuditDate { get; private set; }
    public string? AuditOpinion { get; private set; }

    private NampFinancialStatement() { }

    public static NampFinancialStatement Create(
        Guid nampApplicationId,
        int financialYear,
        string? yearEndDate,
        string financialYearType,
        string inputMethod,
        string currency,
        // Income Statement
        decimal? revenue, decimal? otherOperatingIncome, decimal? costOfSales,
        decimal? grossProfit, decimal? sellingExpenses, decimal? administrativeExpenses,
        decimal? depreciationAmortization, decimal? otherOperatingExpenses, decimal? operatingExpenses,
        decimal? ebitda, decimal? interestIncome, decimal? interestExpense,
        decimal? otherFinanceCosts, decimal? incomeTaxExpense, decimal? dividendsDeclared,
        decimal? netProfit,
        // Balance Sheet — Current Assets
        decimal? cashAndCashEquivalents, decimal? tradeReceivables, decimal? inventory,
        decimal? prepaidExpenses, decimal? otherCurrentAssets, decimal? totalCurrentAssets,
        // Balance Sheet — Non-Current Assets
        decimal? propertyPlantEquipment, decimal? intangibleAssets, decimal? longTermInvestments,
        decimal? deferredTaxAssets, decimal? otherNonCurrentAssets, decimal? totalNonCurrentAssets,
        decimal? totalAssets,
        // Balance Sheet — Current Liabilities
        decimal? tradePayables, decimal? shortTermBorrowings, decimal? currentPortionLongTermDebt,
        decimal? accruedExpenses, decimal? taxPayable, decimal? otherCurrentLiabilities,
        decimal? totalCurrentLiabilities,
        // Balance Sheet — Non-Current Liabilities
        decimal? longTermDebt, decimal? deferredTaxLiabilities, decimal? provisions,
        decimal? otherNonCurrentLiabilities, decimal? totalNonCurrentLiabilities, decimal? totalLiabilities,
        // Balance Sheet — Equity
        decimal? shareCapital, decimal? sharePremium, decimal? retainedEarnings,
        decimal? otherReserves, decimal? totalEquity,
        // Cash Flow — Operating
        decimal? cfProfitBeforeTax, decimal? cfDepreciationAmortization, decimal? cfInterestExpenseAddBack,
        decimal? cfChangesInWorkingCapital, decimal? cfTaxPaid, decimal? cfOtherOperatingAdjustments,
        decimal? netCashFromOperating,
        // Cash Flow — Investing
        decimal? cfPurchaseOfPpe, decimal? cfSaleOfPpe, decimal? cfPurchaseOfInvestments,
        decimal? cfSaleOfInvestments, decimal? cfInterestReceived, decimal? cfDividendsReceived,
        decimal? cfOtherInvestingActivities, decimal? netCashFromInvesting,
        // Cash Flow — Financing
        decimal? cfProceedsFromBorrowings, decimal? cfRepaymentOfBorrowings, decimal? cfInterestPaid,
        decimal? cfDividendsPaid, decimal? cfProceedsFromShareIssue, decimal? cfOtherFinancingActivities,
        decimal? netCashFromFinancing, decimal? netChangeInCash, decimal? cfOpeningCashBalance,
        decimal? closingCashBalance,
        // Audit
        string? auditorName, string? auditorFirm, string? auditDate, string? auditOpinion)
    {
        return new NampFinancialStatement
        {
            NampApplicationId = nampApplicationId,
            FinancialYear = financialYear,
            YearEndDate = yearEndDate,
            FinancialYearType = financialYearType,
            InputMethod = inputMethod,
            Currency = currency,
            Revenue = revenue, OtherOperatingIncome = otherOperatingIncome, CostOfSales = costOfSales,
            GrossProfit = grossProfit, SellingExpenses = sellingExpenses, AdministrativeExpenses = administrativeExpenses,
            DepreciationAmortization = depreciationAmortization, OtherOperatingExpenses = otherOperatingExpenses,
            OperatingExpenses = operatingExpenses, Ebitda = ebitda, InterestIncome = interestIncome,
            InterestExpense = interestExpense, OtherFinanceCosts = otherFinanceCosts,
            IncomeTaxExpense = incomeTaxExpense, DividendsDeclared = dividendsDeclared, NetProfit = netProfit,
            CashAndCashEquivalents = cashAndCashEquivalents, TradeReceivables = tradeReceivables,
            Inventory = inventory, PrepaidExpenses = prepaidExpenses, OtherCurrentAssets = otherCurrentAssets,
            TotalCurrentAssets = totalCurrentAssets,
            PropertyPlantEquipment = propertyPlantEquipment, IntangibleAssets = intangibleAssets,
            LongTermInvestments = longTermInvestments, DeferredTaxAssets = deferredTaxAssets,
            OtherNonCurrentAssets = otherNonCurrentAssets, TotalNonCurrentAssets = totalNonCurrentAssets,
            TotalAssets = totalAssets,
            TradePayables = tradePayables, ShortTermBorrowings = shortTermBorrowings,
            CurrentPortionLongTermDebt = currentPortionLongTermDebt, AccruedExpenses = accruedExpenses,
            TaxPayable = taxPayable, OtherCurrentLiabilities = otherCurrentLiabilities,
            TotalCurrentLiabilities = totalCurrentLiabilities,
            LongTermDebt = longTermDebt, DeferredTaxLiabilities = deferredTaxLiabilities,
            Provisions = provisions, OtherNonCurrentLiabilities = otherNonCurrentLiabilities,
            TotalNonCurrentLiabilities = totalNonCurrentLiabilities, TotalLiabilities = totalLiabilities,
            ShareCapital = shareCapital, SharePremium = sharePremium, RetainedEarnings = retainedEarnings,
            OtherReserves = otherReserves, TotalEquity = totalEquity,
            CfProfitBeforeTax = cfProfitBeforeTax, CfDepreciationAmortization = cfDepreciationAmortization,
            CfInterestExpenseAddBack = cfInterestExpenseAddBack, CfChangesInWorkingCapital = cfChangesInWorkingCapital,
            CfTaxPaid = cfTaxPaid, CfOtherOperatingAdjustments = cfOtherOperatingAdjustments,
            NetCashFromOperating = netCashFromOperating,
            CfPurchaseOfPpe = cfPurchaseOfPpe, CfSaleOfPpe = cfSaleOfPpe,
            CfPurchaseOfInvestments = cfPurchaseOfInvestments, CfSaleOfInvestments = cfSaleOfInvestments,
            CfInterestReceived = cfInterestReceived, CfDividendsReceived = cfDividendsReceived,
            CfOtherInvestingActivities = cfOtherInvestingActivities, NetCashFromInvesting = netCashFromInvesting,
            CfProceedsFromBorrowings = cfProceedsFromBorrowings, CfRepaymentOfBorrowings = cfRepaymentOfBorrowings,
            CfInterestPaid = cfInterestPaid, CfDividendsPaid = cfDividendsPaid,
            CfProceedsFromShareIssue = cfProceedsFromShareIssue, CfOtherFinancingActivities = cfOtherFinancingActivities,
            NetCashFromFinancing = netCashFromFinancing, NetChangeInCash = netChangeInCash,
            CfOpeningCashBalance = cfOpeningCashBalance, ClosingCashBalance = closingCashBalance,
            AuditorName = auditorName, AuditorFirm = auditorFirm, AuditDate = auditDate,
            AuditOpinion = auditOpinion,
        };
    }
}
