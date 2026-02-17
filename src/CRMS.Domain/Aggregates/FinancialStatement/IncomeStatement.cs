using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.FinancialStatement;

/// <summary>
/// Income Statement (Profit & Loss Statement) data.
/// All amounts in the statement's currency (typically NGN).
/// </summary>
public class IncomeStatement : Entity
{
    public Guid FinancialStatementId { get; private set; }

    // Revenue
    public decimal Revenue { get; private set; }
    public decimal OtherOperatingIncome { get; private set; }
    public decimal TotalRevenue => Revenue + OtherOperatingIncome;

    // Cost of Sales
    public decimal CostOfSales { get; private set; }
    public decimal GrossProfit => TotalRevenue - CostOfSales;
    public decimal GrossMarginPercent => TotalRevenue != 0 ? (GrossProfit / TotalRevenue) * 100 : 0;

    // Operating Expenses
    public decimal SellingExpenses { get; private set; }
    public decimal AdministrativeExpenses { get; private set; }
    public decimal DepreciationAmortization { get; private set; }
    public decimal OtherOperatingExpenses { get; private set; }
    public decimal TotalOperatingExpenses => SellingExpenses + AdministrativeExpenses + 
        DepreciationAmortization + OtherOperatingExpenses;

    // Operating Profit (EBIT)
    public decimal OperatingProfit => GrossProfit - TotalOperatingExpenses;
    public decimal EBIT => OperatingProfit;
    public decimal EBITDA => OperatingProfit + DepreciationAmortization;
    public decimal EBITDAMarginPercent => TotalRevenue != 0 ? (EBITDA / TotalRevenue) * 100 : 0;

    // Finance Costs
    public decimal InterestIncome { get; private set; }
    public decimal InterestExpense { get; private set; }
    public decimal OtherFinanceCosts { get; private set; }
    public decimal NetFinanceCost => InterestExpense + OtherFinanceCosts - InterestIncome;

    // Profit Before Tax
    public decimal ProfitBeforeTax => OperatingProfit - NetFinanceCost;

    // Tax
    public decimal IncomeTaxExpense { get; private set; }

    // Net Profit
    public decimal NetProfit => ProfitBeforeTax - IncomeTaxExpense;
    public decimal NetProfitMarginPercent => TotalRevenue != 0 ? (NetProfit / TotalRevenue) * 100 : 0;

    // Dividends (if declared)
    public decimal DividendsDeclared { get; private set; }
    public decimal RetainedProfit => NetProfit - DividendsDeclared;

    private IncomeStatement() { }

    public static IncomeStatement Create(
        Guid financialStatementId,
        // Revenue
        decimal revenue,
        decimal otherOperatingIncome,
        // Cost of Sales
        decimal costOfSales,
        // Operating Expenses
        decimal sellingExpenses,
        decimal administrativeExpenses,
        decimal depreciationAmortization,
        decimal otherOperatingExpenses,
        // Finance
        decimal interestIncome,
        decimal interestExpense,
        decimal otherFinanceCosts,
        // Tax
        decimal incomeTaxExpense,
        // Dividends
        decimal dividendsDeclared = 0)
    {
        return new IncomeStatement
        {
            FinancialStatementId = financialStatementId,
            Revenue = revenue,
            OtherOperatingIncome = otherOperatingIncome,
            CostOfSales = costOfSales,
            SellingExpenses = sellingExpenses,
            AdministrativeExpenses = administrativeExpenses,
            DepreciationAmortization = depreciationAmortization,
            OtherOperatingExpenses = otherOperatingExpenses,
            InterestIncome = interestIncome,
            InterestExpense = interestExpense,
            OtherFinanceCosts = otherFinanceCosts,
            IncomeTaxExpense = incomeTaxExpense,
            DividendsDeclared = dividendsDeclared
        };
    }

    public bool IsProfitable => NetProfit > 0;
    public bool IsOperatingProfitable => OperatingProfit > 0;
}
