namespace CRMS.Application.FinancialAnalysis.DTOs;

public record FinancialStatementDto(
    Guid Id,
    Guid LoanApplicationId,
    int FinancialYear,
    DateTime YearEndDate,
    string YearType,
    string Status,
    string InputMethod,
    string Currency,
    string? AuditorName,
    string? AuditorFirm,
    DateTime? AuditDate,
    string? AuditOpinion,
    string? OriginalFileName,
    DateTime SubmittedAt,
    DateTime? VerifiedAt,
    BalanceSheetDto? BalanceSheet,
    IncomeStatementDto? IncomeStatement,
    CashFlowStatementDto? CashFlowStatement,
    FinancialRatiosDto? Ratios
);

public record FinancialStatementSummaryDto(
    Guid Id,
    int FinancialYear,
    string YearType,
    string Status,
    decimal? TotalAssets,
    decimal? TotalRevenue,
    decimal? NetProfit,
    string? OverallAssessment,
    DateTime SubmittedAt
);

public record BalanceSheetDto(
    // Current Assets
    decimal CashAndCashEquivalents,
    decimal TradeReceivables,
    decimal Inventory,
    decimal PrepaidExpenses,
    decimal OtherCurrentAssets,
    decimal TotalCurrentAssets,
    // Non-Current Assets
    decimal PropertyPlantEquipment,
    decimal IntangibleAssets,
    decimal LongTermInvestments,
    decimal DeferredTaxAssets,
    decimal OtherNonCurrentAssets,
    decimal TotalNonCurrentAssets,
    decimal TotalAssets,
    // Current Liabilities
    decimal TradePayables,
    decimal ShortTermBorrowings,
    decimal CurrentPortionLongTermDebt,
    decimal AccruedExpenses,
    decimal TaxPayable,
    decimal OtherCurrentLiabilities,
    decimal TotalCurrentLiabilities,
    // Non-Current Liabilities
    decimal LongTermDebt,
    decimal DeferredTaxLiabilities,
    decimal Provisions,
    decimal OtherNonCurrentLiabilities,
    decimal TotalNonCurrentLiabilities,
    decimal TotalLiabilities,
    // Equity
    decimal ShareCapital,
    decimal SharePremium,
    decimal RetainedEarnings,
    decimal OtherReserves,
    decimal TotalEquity,
    // Calculated
    decimal TotalDebt,
    decimal WorkingCapital,
    decimal NetWorth,
    bool IsBalanced
);

public record IncomeStatementDto(
    decimal Revenue,
    decimal OtherOperatingIncome,
    decimal TotalRevenue,
    decimal CostOfSales,
    decimal GrossProfit,
    decimal GrossMarginPercent,
    decimal SellingExpenses,
    decimal AdministrativeExpenses,
    decimal DepreciationAmortization,
    decimal OtherOperatingExpenses,
    decimal TotalOperatingExpenses,
    decimal OperatingProfit,
    decimal EBITDA,
    decimal EBITDAMarginPercent,
    decimal InterestIncome,
    decimal InterestExpense,
    decimal OtherFinanceCosts,
    decimal NetFinanceCost,
    decimal ProfitBeforeTax,
    decimal IncomeTaxExpense,
    decimal NetProfit,
    decimal NetProfitMarginPercent,
    decimal DividendsDeclared,
    decimal RetainedProfit,
    bool IsProfitable
);

public record CashFlowStatementDto(
    // Operating
    decimal ProfitBeforeTax,
    decimal DepreciationAmortization,
    decimal InterestExpenseAddBack,
    decimal ChangesInWorkingCapital,
    decimal TaxPaid,
    decimal OtherOperatingAdjustments,
    decimal NetCashFromOperations,
    // Investing
    decimal PurchaseOfPPE,
    decimal SaleOfPPE,
    decimal NetCashFromInvesting,
    // Financing
    decimal ProceedsFromBorrowings,
    decimal RepaymentOfBorrowings,
    decimal InterestPaid,
    decimal DividendsPaid,
    decimal NetCashFromFinancing,
    // Summary
    decimal NetChangeInCash,
    decimal OpeningCashBalance,
    decimal ClosingCashBalance,
    decimal FreeCashFlow,
    bool HasPositiveOperatingCashFlow
);

public record FinancialRatiosDto(
    // Liquidity
    decimal CurrentRatio,
    decimal QuickRatio,
    decimal CashRatio,
    string LiquidityAssessment,
    // Leverage
    decimal DebtToEquityRatio,
    decimal DebtToAssetsRatio,
    decimal InterestCoverageRatio,
    decimal DebtServiceCoverageRatio,
    string LeverageAssessment,
    // Profitability
    decimal GrossMarginPercent,
    decimal OperatingMarginPercent,
    decimal NetProfitMarginPercent,
    decimal EBITDAMarginPercent,
    decimal ReturnOnAssets,
    decimal ReturnOnEquity,
    string ProfitabilityAssessment,
    // Efficiency
    decimal AssetTurnover,
    decimal InventoryTurnover,
    decimal ReceivablesDays,
    decimal PayablesDays,
    decimal CashConversionCycle,
    // Summary
    decimal WorkingCapital,
    decimal NetWorth,
    decimal TotalDebt,
    string OverallAssessment
);

// Input DTOs
public record SubmitBalanceSheetRequest(
    decimal CashAndCashEquivalents,
    decimal TradeReceivables,
    decimal Inventory,
    decimal PrepaidExpenses,
    decimal OtherCurrentAssets,
    decimal PropertyPlantEquipment,
    decimal IntangibleAssets,
    decimal LongTermInvestments,
    decimal DeferredTaxAssets,
    decimal OtherNonCurrentAssets,
    decimal TradePayables,
    decimal ShortTermBorrowings,
    decimal CurrentPortionLongTermDebt,
    decimal AccruedExpenses,
    decimal TaxPayable,
    decimal OtherCurrentLiabilities,
    decimal LongTermDebt,
    decimal DeferredTaxLiabilities,
    decimal Provisions,
    decimal OtherNonCurrentLiabilities,
    decimal ShareCapital,
    decimal SharePremium,
    decimal RetainedEarnings,
    decimal OtherReserves
);

public record SubmitIncomeStatementRequest(
    decimal Revenue,
    decimal OtherOperatingIncome,
    decimal CostOfSales,
    decimal SellingExpenses,
    decimal AdministrativeExpenses,
    decimal DepreciationAmortization,
    decimal OtherOperatingExpenses,
    decimal InterestIncome,
    decimal InterestExpense,
    decimal OtherFinanceCosts,
    decimal IncomeTaxExpense,
    decimal DividendsDeclared = 0
);

public record SubmitCashFlowStatementRequest(
    decimal ProfitBeforeTax,
    decimal DepreciationAmortization,
    decimal InterestExpenseAddBack,
    decimal ChangesInWorkingCapital,
    decimal TaxPaid,
    decimal OtherOperatingAdjustments,
    decimal PurchaseOfPPE,
    decimal SaleOfPPE,
    decimal PurchaseOfInvestments,
    decimal SaleOfInvestments,
    decimal InterestReceived,
    decimal DividendsReceived,
    decimal OtherInvestingActivities,
    decimal ProceedsFromBorrowings,
    decimal RepaymentOfBorrowings,
    decimal InterestPaid,
    decimal DividendsPaid,
    decimal ProceedsFromShareIssue,
    decimal OtherFinancingActivities,
    decimal OpeningCashBalance
);
