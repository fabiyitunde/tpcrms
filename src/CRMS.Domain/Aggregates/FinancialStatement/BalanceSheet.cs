using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.FinancialStatement;

/// <summary>
/// Balance Sheet (Statement of Financial Position) data.
/// All amounts in the statement's currency (typically NGN).
/// </summary>
public class BalanceSheet : Entity
{
    public Guid FinancialStatementId { get; private set; }

    // ASSETS
    // Current Assets
    public decimal CashAndCashEquivalents { get; private set; }
    public decimal TradeReceivables { get; private set; }
    public decimal Inventory { get; private set; }
    public decimal PrepaidExpenses { get; private set; }
    public decimal OtherCurrentAssets { get; private set; }
    public decimal TotalCurrentAssets => CashAndCashEquivalents + TradeReceivables + 
        Inventory + PrepaidExpenses + OtherCurrentAssets;

    // Non-Current Assets
    public decimal PropertyPlantEquipment { get; private set; }
    public decimal IntangibleAssets { get; private set; }
    public decimal LongTermInvestments { get; private set; }
    public decimal DeferredTaxAssets { get; private set; }
    public decimal OtherNonCurrentAssets { get; private set; }
    public decimal TotalNonCurrentAssets => PropertyPlantEquipment + IntangibleAssets + 
        LongTermInvestments + DeferredTaxAssets + OtherNonCurrentAssets;

    public decimal TotalAssets => TotalCurrentAssets + TotalNonCurrentAssets;

    // LIABILITIES
    // Current Liabilities
    public decimal TradePayables { get; private set; }
    public decimal ShortTermBorrowings { get; private set; }
    public decimal CurrentPortionLongTermDebt { get; private set; }
    public decimal AccruedExpenses { get; private set; }
    public decimal TaxPayable { get; private set; }
    public decimal OtherCurrentLiabilities { get; private set; }
    public decimal TotalCurrentLiabilities => TradePayables + ShortTermBorrowings + 
        CurrentPortionLongTermDebt + AccruedExpenses + TaxPayable + OtherCurrentLiabilities;

    // Non-Current Liabilities
    public decimal LongTermDebt { get; private set; }
    public decimal DeferredTaxLiabilities { get; private set; }
    public decimal Provisions { get; private set; }
    public decimal OtherNonCurrentLiabilities { get; private set; }
    public decimal TotalNonCurrentLiabilities => LongTermDebt + DeferredTaxLiabilities + 
        Provisions + OtherNonCurrentLiabilities;

    public decimal TotalLiabilities => TotalCurrentLiabilities + TotalNonCurrentLiabilities;

    // EQUITY
    public decimal ShareCapital { get; private set; }
    public decimal SharePremium { get; private set; }
    public decimal RetainedEarnings { get; private set; }
    public decimal OtherReserves { get; private set; }
    public decimal TotalEquity => ShareCapital + SharePremium + RetainedEarnings + OtherReserves;

    // Calculated
    public decimal TotalLiabilitiesAndEquity => TotalLiabilities + TotalEquity;
    public decimal TotalDebt => ShortTermBorrowings + CurrentPortionLongTermDebt + LongTermDebt;
    public decimal WorkingCapital => TotalCurrentAssets - TotalCurrentLiabilities;
    public decimal NetWorth => TotalEquity;

    private BalanceSheet() { }

    public static BalanceSheet Create(
        Guid financialStatementId,
        // Current Assets
        decimal cashAndCashEquivalents,
        decimal tradeReceivables,
        decimal inventory,
        decimal prepaidExpenses,
        decimal otherCurrentAssets,
        // Non-Current Assets
        decimal propertyPlantEquipment,
        decimal intangibleAssets,
        decimal longTermInvestments,
        decimal deferredTaxAssets,
        decimal otherNonCurrentAssets,
        // Current Liabilities
        decimal tradePayables,
        decimal shortTermBorrowings,
        decimal currentPortionLongTermDebt,
        decimal accruedExpenses,
        decimal taxPayable,
        decimal otherCurrentLiabilities,
        // Non-Current Liabilities
        decimal longTermDebt,
        decimal deferredTaxLiabilities,
        decimal provisions,
        decimal otherNonCurrentLiabilities,
        // Equity
        decimal shareCapital,
        decimal sharePremium,
        decimal retainedEarnings,
        decimal otherReserves)
    {
        return new BalanceSheet
        {
            FinancialStatementId = financialStatementId,
            // Current Assets
            CashAndCashEquivalents = cashAndCashEquivalents,
            TradeReceivables = tradeReceivables,
            Inventory = inventory,
            PrepaidExpenses = prepaidExpenses,
            OtherCurrentAssets = otherCurrentAssets,
            // Non-Current Assets
            PropertyPlantEquipment = propertyPlantEquipment,
            IntangibleAssets = intangibleAssets,
            LongTermInvestments = longTermInvestments,
            DeferredTaxAssets = deferredTaxAssets,
            OtherNonCurrentAssets = otherNonCurrentAssets,
            // Current Liabilities
            TradePayables = tradePayables,
            ShortTermBorrowings = shortTermBorrowings,
            CurrentPortionLongTermDebt = currentPortionLongTermDebt,
            AccruedExpenses = accruedExpenses,
            TaxPayable = taxPayable,
            OtherCurrentLiabilities = otherCurrentLiabilities,
            // Non-Current Liabilities
            LongTermDebt = longTermDebt,
            DeferredTaxLiabilities = deferredTaxLiabilities,
            Provisions = provisions,
            OtherNonCurrentLiabilities = otherNonCurrentLiabilities,
            // Equity
            ShareCapital = shareCapital,
            SharePremium = sharePremium,
            RetainedEarnings = retainedEarnings,
            OtherReserves = otherReserves
        };
    }

    /// <summary>
    /// Validates that Total Assets = Total Liabilities + Total Equity (within tolerance)
    /// </summary>
    public bool IsBalanced(decimal tolerance = 1.0m)
    {
        return Math.Abs(TotalAssets - TotalLiabilitiesAndEquity) <= tolerance;
    }
}
