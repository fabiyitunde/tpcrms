using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.FinancialStatement;

/// <summary>
/// Cash Flow Statement data.
/// All amounts in the statement's currency (typically NGN).
/// </summary>
public class CashFlowStatement : Entity
{
    public Guid FinancialStatementId { get; private set; }

    // Cash Flow from Operating Activities
    public decimal ProfitBeforeTax { get; private set; }
    public decimal DepreciationAmortization { get; private set; }
    public decimal InterestExpenseAddBack { get; private set; }
    public decimal ChangesInWorkingCapital { get; private set; }
    public decimal TaxPaid { get; private set; }
    public decimal OtherOperatingAdjustments { get; private set; }
    public decimal NetCashFromOperations => ProfitBeforeTax + DepreciationAmortization + 
        InterestExpenseAddBack + ChangesInWorkingCapital - TaxPaid + OtherOperatingAdjustments;

    // Cash Flow from Investing Activities
    public decimal PurchaseOfPPE { get; private set; }
    public decimal SaleOfPPE { get; private set; }
    public decimal PurchaseOfInvestments { get; private set; }
    public decimal SaleOfInvestments { get; private set; }
    public decimal InterestReceived { get; private set; }
    public decimal DividendsReceived { get; private set; }
    public decimal OtherInvestingActivities { get; private set; }
    public decimal NetCashFromInvesting => SaleOfPPE - PurchaseOfPPE + SaleOfInvestments - 
        PurchaseOfInvestments + InterestReceived + DividendsReceived + OtherInvestingActivities;

    // Cash Flow from Financing Activities
    public decimal ProceedsFromBorrowings { get; private set; }
    public decimal RepaymentOfBorrowings { get; private set; }
    public decimal InterestPaid { get; private set; }
    public decimal DividendsPaid { get; private set; }
    public decimal ProceedsFromShareIssue { get; private set; }
    public decimal OtherFinancingActivities { get; private set; }
    public decimal NetCashFromFinancing => ProceedsFromBorrowings - RepaymentOfBorrowings - 
        InterestPaid - DividendsPaid + ProceedsFromShareIssue + OtherFinancingActivities;

    // Net Change
    public decimal NetChangeInCash => NetCashFromOperations + NetCashFromInvesting + NetCashFromFinancing;
    public decimal OpeningCashBalance { get; private set; }
    public decimal ClosingCashBalance => OpeningCashBalance + NetChangeInCash;

    // Free Cash Flow
    public decimal FreeCashFlow => NetCashFromOperations - PurchaseOfPPE;
    public decimal FreeCashFlowToFirm => NetCashFromOperations + InterestPaid - PurchaseOfPPE;

    private CashFlowStatement() { }

    public static CashFlowStatement Create(
        Guid financialStatementId,
        // Operating
        decimal profitBeforeTax,
        decimal depreciationAmortization,
        decimal interestExpenseAddBack,
        decimal changesInWorkingCapital,
        decimal taxPaid,
        decimal otherOperatingAdjustments,
        // Investing
        decimal purchaseOfPPE,
        decimal saleOfPPE,
        decimal purchaseOfInvestments,
        decimal saleOfInvestments,
        decimal interestReceived,
        decimal dividendsReceived,
        decimal otherInvestingActivities,
        // Financing
        decimal proceedsFromBorrowings,
        decimal repaymentOfBorrowings,
        decimal interestPaid,
        decimal dividendsPaid,
        decimal proceedsFromShareIssue,
        decimal otherFinancingActivities,
        // Opening Balance
        decimal openingCashBalance)
    {
        return new CashFlowStatement
        {
            FinancialStatementId = financialStatementId,
            ProfitBeforeTax = profitBeforeTax,
            DepreciationAmortization = depreciationAmortization,
            InterestExpenseAddBack = interestExpenseAddBack,
            ChangesInWorkingCapital = changesInWorkingCapital,
            TaxPaid = taxPaid,
            OtherOperatingAdjustments = otherOperatingAdjustments,
            PurchaseOfPPE = purchaseOfPPE,
            SaleOfPPE = saleOfPPE,
            PurchaseOfInvestments = purchaseOfInvestments,
            SaleOfInvestments = saleOfInvestments,
            InterestReceived = interestReceived,
            DividendsReceived = dividendsReceived,
            OtherInvestingActivities = otherInvestingActivities,
            ProceedsFromBorrowings = proceedsFromBorrowings,
            RepaymentOfBorrowings = repaymentOfBorrowings,
            InterestPaid = interestPaid,
            DividendsPaid = dividendsPaid,
            ProceedsFromShareIssue = proceedsFromShareIssue,
            OtherFinancingActivities = otherFinancingActivities,
            OpeningCashBalance = openingCashBalance
        };
    }

    public bool HasPositiveOperatingCashFlow => NetCashFromOperations > 0;
    public bool HasPositiveFreeCashFlow => FreeCashFlow > 0;
}
