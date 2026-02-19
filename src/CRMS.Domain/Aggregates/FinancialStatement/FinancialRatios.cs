using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.FinancialStatement;

/// <summary>
/// Calculated financial ratios from Balance Sheet and Income Statement.
/// Used for credit assessment and AI advisory engine input.
/// </summary>
public class FinancialRatios : ValueObject
{
    // Liquidity Ratios
    public decimal CurrentRatio { get; private set; }
    public decimal QuickRatio { get; private set; }
    public decimal CashRatio { get; private set; }

    // Leverage Ratios
    public decimal DebtToEquityRatio { get; private set; }
    public decimal DebtToAssetsRatio { get; private set; }
    public decimal InterestCoverageRatio { get; private set; }
    public decimal DebtServiceCoverageRatio { get; private set; }

    // Profitability Ratios
    public decimal GrossMarginPercent { get; private set; }
    public decimal OperatingMarginPercent { get; private set; }
    public decimal NetProfitMarginPercent { get; private set; }
    public decimal EBITDAMarginPercent { get; private set; }
    public decimal ReturnOnAssets { get; private set; }
    public decimal ReturnOnEquity { get; private set; }

    // Efficiency Ratios
    public decimal AssetTurnover { get; private set; }
    public decimal InventoryTurnover { get; private set; }
    public decimal ReceivablesDays { get; private set; }
    public decimal PayablesDays { get; private set; }
    public decimal CashConversionCycle { get; private set; }

    // Valuation/Other
    public decimal WorkingCapital { get; private set; }
    public decimal NetWorth { get; private set; }
    public decimal TotalDebt { get; private set; }

    // Assessment Flags
    public bool IsLiquidityHealthy => CurrentRatio >= 1.0m && QuickRatio >= 0.8m;
    public bool IsLeverageHealthy => DebtToEquityRatio <= 2.0m && InterestCoverageRatio >= 2.0m;
    public bool IsProfitable => NetProfitMarginPercent > 0;
    public bool HasStrongCashGeneration => DebtServiceCoverageRatio >= 1.25m;
    
    /// <summary>
    /// Indicates if DSCR was calculated without cash flow statement data.
    /// When true, DSCR uses estimated principal from balance sheet debt and should be treated with caution.
    /// </summary>
    public bool IsDSCREstimated { get; private set; }

    private FinancialRatios() { }

    public static FinancialRatios Calculate(
        BalanceSheet bs, 
        IncomeStatement inc, 
        CashFlowStatement? cf = null)
    {
        var ratios = new FinancialRatios();

        // Liquidity Ratios
        ratios.CurrentRatio = SafeDivide(bs.TotalCurrentAssets, bs.TotalCurrentLiabilities);
        ratios.QuickRatio = SafeDivide(bs.TotalCurrentAssets - bs.Inventory, bs.TotalCurrentLiabilities);
        ratios.CashRatio = SafeDivide(bs.CashAndCashEquivalents, bs.TotalCurrentLiabilities);

        // Leverage Ratios
        ratios.DebtToEquityRatio = SafeDivide(bs.TotalDebt, bs.TotalEquity);
        ratios.DebtToAssetsRatio = SafeDivide(bs.TotalDebt, bs.TotalAssets);
        ratios.InterestCoverageRatio = SafeDivide(inc.EBIT, inc.InterestExpense);
        
        // DSCR = (Net Operating Income) / (Total Debt Service)
        // Simplified: EBITDA / (Interest + Principal Payments)
        // When cash flow statement is absent, estimate principal as TotalDebt / 5 (5-year amortization assumption)
        decimal principalPayments;
        if (cf != null)
        {
            principalPayments = cf.RepaymentOfBorrowings;
            ratios.IsDSCREstimated = false;
        }
        else
        {
            // Conservative estimate: assume 5-year amortization of total debt
            principalPayments = bs.TotalDebt / 5;
            ratios.IsDSCREstimated = true;
        }
        var annualDebtService = inc.InterestExpense + principalPayments;
        ratios.DebtServiceCoverageRatio = SafeDivide(inc.EBITDA, annualDebtService);

        // Profitability Ratios
        ratios.GrossMarginPercent = inc.GrossMarginPercent;
        ratios.OperatingMarginPercent = SafeDivide(inc.OperatingProfit, inc.TotalRevenue) * 100;
        ratios.NetProfitMarginPercent = inc.NetProfitMarginPercent;
        ratios.EBITDAMarginPercent = inc.EBITDAMarginPercent;
        ratios.ReturnOnAssets = SafeDivide(inc.NetProfit, bs.TotalAssets) * 100;
        ratios.ReturnOnEquity = SafeDivide(inc.NetProfit, bs.TotalEquity) * 100;

        // Efficiency Ratios
        ratios.AssetTurnover = SafeDivide(inc.TotalRevenue, bs.TotalAssets);
        ratios.InventoryTurnover = SafeDivide(inc.CostOfSales, bs.Inventory);
        ratios.ReceivablesDays = SafeDivide(bs.TradeReceivables * 365, inc.TotalRevenue);
        ratios.PayablesDays = SafeDivide(bs.TradePayables * 365, inc.CostOfSales);
        
        var inventoryDays = SafeDivide(bs.Inventory * 365, inc.CostOfSales);
        ratios.CashConversionCycle = ratios.ReceivablesDays + inventoryDays - ratios.PayablesDays;

        // Absolute Values
        ratios.WorkingCapital = bs.WorkingCapital;
        ratios.NetWorth = bs.NetWorth;
        ratios.TotalDebt = bs.TotalDebt;

        return ratios;
    }

    private static decimal SafeDivide(decimal numerator, decimal denominator)
    {
        if (denominator == 0) return 0;
        return Math.Round(numerator / denominator, 4);
    }

    public string GetLiquidityAssessment()
    {
        if (CurrentRatio >= 2.0m && QuickRatio >= 1.5m) return "Excellent";
        if (CurrentRatio >= 1.5m && QuickRatio >= 1.0m) return "Good";
        if (CurrentRatio >= 1.0m && QuickRatio >= 0.8m) return "Adequate";
        if (CurrentRatio >= 0.8m) return "Weak";
        return "Critical";
    }

    public string GetLeverageAssessment()
    {
        if (DebtToEquityRatio <= 0.5m && InterestCoverageRatio >= 5.0m) return "Excellent";
        if (DebtToEquityRatio <= 1.0m && InterestCoverageRatio >= 3.0m) return "Good";
        if (DebtToEquityRatio <= 2.0m && InterestCoverageRatio >= 2.0m) return "Adequate";
        if (DebtToEquityRatio <= 3.0m && InterestCoverageRatio >= 1.5m) return "Weak";
        return "Critical";
    }

    public string GetProfitabilityAssessment()
    {
        if (NetProfitMarginPercent >= 15 && ReturnOnEquity >= 20) return "Excellent";
        if (NetProfitMarginPercent >= 10 && ReturnOnEquity >= 15) return "Good";
        if (NetProfitMarginPercent >= 5 && ReturnOnEquity >= 10) return "Adequate";
        if (NetProfitMarginPercent > 0) return "Weak";
        return "Loss-Making";
    }

    public string GetOverallAssessment()
    {
        var scores = new[] 
        { 
            GetLiquidityAssessment(), 
            GetLeverageAssessment(), 
            GetProfitabilityAssessment() 
        };

        var excellent = scores.Count(s => s == "Excellent");
        var good = scores.Count(s => s == "Good");
        var critical = scores.Count(s => s == "Critical" || s == "Loss-Making");

        if (excellent >= 2) return "Strong";
        if (critical >= 2) return "High Risk";
        if (good >= 2 || excellent >= 1) return "Acceptable";
        return "Needs Review";
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CurrentRatio;
        yield return DebtToEquityRatio;
        yield return NetProfitMarginPercent;
        yield return ReturnOnEquity;
    }
}
