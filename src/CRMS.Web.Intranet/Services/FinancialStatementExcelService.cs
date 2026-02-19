using ClosedXML.Excel;
using CRMS.Web.Intranet.Models;

namespace CRMS.Web.Intranet.Services;

/// <summary>
/// Service for generating, parsing, and validating Financial Statement Excel templates.
/// Provides shared validation logic for both Excel uploads and manual UI entry.
/// </summary>
public class FinancialStatementExcelService
{
    private const string CURRENCY = "NGN";
    private const decimal BALANCE_TOLERANCE = 1m; // Allow for rounding differences

    #region Validation Methods (Shared between Excel upload and UI entry)

    /// <summary>
    /// Validates that a Balance Sheet balances (Assets = Liabilities + Equity)
    /// </summary>
    public static (bool IsValid, string? Error) ValidateBalanceSheet(BalanceSheetData bs, int? year = null)
    {
        var totalAssets = bs.TotalCurrentAssets + bs.TotalNonCurrentAssets;
        var totalLiabilitiesEquity = bs.TotalCurrentLiabilities + bs.TotalNonCurrentLiabilities + bs.TotalEquity;
        
        if (Math.Abs(totalAssets - totalLiabilitiesEquity) > BALANCE_TOLERANCE)
        {
            var yearStr = year.HasValue ? $" for {year}" : "";
            return (false, $"Balance Sheet{yearStr} does not balance. " +
                $"Total Assets: {totalAssets:N0}, Total Liabilities + Equity: {totalLiabilitiesEquity:N0}. " +
                $"Difference: {Math.Abs(totalAssets - totalLiabilitiesEquity):N0}");
        }
        
        return (true, null);
    }

    /// <summary>
    /// Validates that Cash Flow closing balance matches Balance Sheet cash
    /// </summary>
    public static (bool IsValid, string? Error) ValidateCashFlowToBalanceSheet(
        CashFlowData cf, BalanceSheetData bs, int? year = null)
    {
        var closingCash = cf.OpeningCashBalance + CalculateNetCashChange(cf);
        var bsCash = bs.CashAndCashEquivalents;
        
        if (Math.Abs(closingCash - bsCash) > BALANCE_TOLERANCE)
        {
            var yearStr = year.HasValue ? $" for {year}" : "";
            return (false, $"Cash Flow closing balance{yearStr} ({closingCash:N0}) " +
                $"does not match Balance Sheet cash ({bsCash:N0})");
        }
        
        return (true, null);
    }

    /// <summary>
    /// Validates that prior year closing cash equals current year opening cash
    /// </summary>
    public static (bool IsValid, string? Error) ValidateCashFlowContinuity(
        CashFlowData priorYearCf, CashFlowData currentYearCf, int priorYear, int currentYear)
    {
        var priorClosing = priorYearCf.OpeningCashBalance + CalculateNetCashChange(priorYearCf);
        var currentOpening = currentYearCf.OpeningCashBalance;
        
        if (Math.Abs(priorClosing - currentOpening) > BALANCE_TOLERANCE)
        {
            return (false, $"Cash Flow continuity error: {priorYear} closing cash ({priorClosing:N0}) " +
                $"does not equal {currentYear} opening cash ({currentOpening:N0})");
        }
        
        return (true, null);
    }

    /// <summary>
    /// Calculates net cash change from Cash Flow components
    /// </summary>
    public static decimal CalculateNetCashChange(CashFlowData cf)
    {
        var operating = cf.ProfitBeforeTax + cf.DepreciationAmortization + cf.InterestExpenseAddBack 
            + cf.ChangesInWorkingCapital - cf.TaxPaid + cf.OtherOperatingAdjustments;
        
        var investing = -cf.PurchaseOfPPE + cf.SaleOfPPE - cf.PurchaseOfInvestments + cf.SaleOfInvestments 
            + cf.InterestReceived + cf.DividendsReceived + cf.OtherInvestingActivities;
        
        var financing = cf.ProceedsFromBorrowings - cf.RepaymentOfBorrowings - cf.InterestPaid 
            - cf.DividendsPaid + cf.ProceedsFromShareIssue + cf.OtherFinancingActivities;
        
        return operating + investing + financing;
    }

    /// <summary>
    /// Comprehensive validation of a complete financial statement set
    /// </summary>
    public static (bool IsValid, List<string> Errors) ValidateFinancialStatements(
        List<FinancialStatementUploadData> statements)
    {
        var errors = new List<string>();
        
        // Sort by year
        var sorted = statements.OrderBy(s => s.Year).ToList();
        
        for (int i = 0; i < sorted.Count; i++)
        {
            var stmt = sorted[i];
            
            // Validate Balance Sheet balances
            var bsResult = ValidateBalanceSheet(stmt.BalanceSheet, stmt.Year);
            if (!bsResult.IsValid)
                errors.Add(bsResult.Error!);
            
            // Validate Cash Flow to Balance Sheet (if Cash Flow provided)
            if (stmt.CashFlow != null)
            {
                var cfBsResult = ValidateCashFlowToBalanceSheet(stmt.CashFlow, stmt.BalanceSheet, stmt.Year);
                if (!cfBsResult.IsValid)
                    errors.Add(cfBsResult.Error!);
                
                // Validate Cash Flow continuity with prior year
                if (i > 0 && sorted[i - 1].CashFlow != null)
                {
                    var continuityResult = ValidateCashFlowContinuity(
                        sorted[i - 1].CashFlow!, stmt.CashFlow, sorted[i - 1].Year, stmt.Year);
                    if (!continuityResult.IsValid)
                        errors.Add(continuityResult.Error!);
                }
            }
        }
        
        return (errors.Count == 0, errors);
    }

    #endregion

    /// <summary>
    /// Generates a blank Excel template for financial statement entry
    /// </summary>
    public byte[] GenerateBlankTemplate()
    {
        using var workbook = new XLWorkbook();
        
        CreateInstructionsSheet(workbook);
        CreateBalanceSheetTemplate(workbook);
        CreateIncomeStatementTemplate(workbook);
        CreateCashFlowTemplate(workbook);
        
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Generates an Excel template with sample data for reference
    /// </summary>
    public byte[] GenerateSampleTemplate()
    {
        using var workbook = new XLWorkbook();
        
        CreateInstructionsSheet(workbook);
        CreateBalanceSheetWithSampleData(workbook);
        CreateIncomeStatementWithSampleData(workbook);
        CreateCashFlowWithSampleData(workbook);
        
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Parses uploaded Excel file and extracts financial statement data
    /// </summary>
    public (bool Success, string? Error, List<FinancialStatementUploadData>? Data) ParseUploadedFile(Stream fileStream)
    {
        try
        {
            using var workbook = new XLWorkbook(fileStream);
            var statements = new List<FinancialStatementUploadData>();

            // Parse Balance Sheet
            var bsSheet = workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("Balance", StringComparison.OrdinalIgnoreCase));
            if (bsSheet == null)
                return (false, "Balance Sheet worksheet not found", null);

            // Parse Income Statement
            var isSheet = workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("Income", StringComparison.OrdinalIgnoreCase) || w.Name.Contains("P&L", StringComparison.OrdinalIgnoreCase));
            if (isSheet == null)
                return (false, "Income Statement worksheet not found", null);

            // Parse Cash Flow (optional)
            var cfSheet = workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("Cash", StringComparison.OrdinalIgnoreCase));

            // Get years from Balance Sheet header (columns C, D, E = Year 1, 2, 3)
            var years = new List<int>();
            for (int col = 3; col <= 5; col++)
            {
                var yearCell = bsSheet.Cell(3, col).GetString();
                if (int.TryParse(yearCell, out var year))
                    years.Add(year);
            }

            if (years.Count == 0)
                return (false, "No valid years found in Balance Sheet header (row 3, columns C-E)", null);

            // Parse each year
            for (int i = 0; i < years.Count; i++)
            {
                var col = 3 + i; // C=3, D=4, E=5
                var data = new FinancialStatementUploadData
                {
                    Year = years[i],
                    YearType = bsSheet.Cell(4, col).GetString() ?? "Audited",
                    BalanceSheet = ParseBalanceSheet(bsSheet, col),
                    IncomeStatement = ParseIncomeStatement(isSheet, col),
                    CashFlow = cfSheet != null ? ParseCashFlow(cfSheet, col) : null
                };
                
                statements.Add(data);
            }

            // Use shared validation logic
            var (isValid, errors) = ValidateFinancialStatements(statements);
            if (!isValid)
            {
                return (false, string.Join("; ", errors), null);
            }

            return (true, null, statements);
        }
        catch (Exception ex)
        {
            return (false, $"Error parsing Excel file: {ex.Message}", null);
        }
    }

    #region Template Creation

    private void CreateInstructionsSheet(IXLWorkbook workbook)
    {
        var ws = workbook.Worksheets.Add("Instructions");
        
        ws.Cell("A1").Value = "CRMS Financial Statement Template";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 16;
        
        ws.Cell("A3").Value = "Instructions:";
        ws.Cell("A3").Style.Font.Bold = true;
        
        ws.Cell("A4").Value = "1. Fill in the Balance Sheet, Income Statement, and Cash Flow worksheets";
        ws.Cell("A5").Value = "2. Enter amounts in Nigerian Naira (NGN) without currency symbols";
        ws.Cell("A6").Value = "3. Use negative numbers for expenses and outflows where appropriate";
        ws.Cell("A7").Value = "4. Ensure Balance Sheet balances: Total Assets = Total Liabilities + Total Equity";
        ws.Cell("A8").Value = "5. You can enter up to 3 years of data (columns C, D, E)";
        ws.Cell("A9").Value = "6. Set Year Type in row 4: Audited, ManagementAccounts, or Projected";
        
        ws.Cell("A11").Value = "Year Types:";
        ws.Cell("A11").Style.Font.Bold = true;
        ws.Cell("A12").Value = "- Audited: Externally audited by registered auditor";
        ws.Cell("A13").Value = "- ManagementAccounts: Internally prepared management accounts";
        ws.Cell("A14").Value = "- Projected: Future projections/forecasts";
        
        ws.Cell("A16").Value = "Minimum Requirements (by business age):";
        ws.Cell("A16").Style.Font.Bold = true;
        ws.Cell("A17").Value = "- Startup (< 1 year): 3 years Projected";
        ws.Cell("A18").Value = "- 1 year old: 1 Actual + 2 Projected";
        ws.Cell("A19").Value = "- 2 years old: 2 Actuals (1 Audited) + 1 Projected";
        ws.Cell("A20").Value = "- 3+ years old: 3 years Audited";
        
        ws.Columns().AdjustToContents();
    }

    private void CreateBalanceSheetTemplate(IXLWorkbook workbook)
    {
        var ws = workbook.Worksheets.Add("Balance Sheet");
        
        // Header
        ws.Cell("A1").Value = "STATEMENT OF FINANCIAL POSITION (BALANCE SHEET)";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Range("A1:E1").Merge();
        
        ws.Cell("A2").Value = $"All amounts in {CURRENCY} '000";
        ws.Range("A2:E2").Merge();
        
        // Year headers
        ws.Cell("B3").Value = "Line Item";
        ws.Cell("C3").Value = "Year 1";
        ws.Cell("D3").Value = "Year 2";
        ws.Cell("E3").Value = "Year 3";
        
        ws.Cell("B4").Value = "Year Type";
        ws.Cell("C4").Value = "Audited";
        ws.Cell("D4").Value = "Audited";
        ws.Cell("E4").Value = "Audited";
        
        ws.Range("B3:E4").Style.Font.Bold = true;
        ws.Range("B3:E4").Style.Fill.BackgroundColor = XLColor.LightGray;
        
        int row = 6;
        
        // ASSETS
        ws.Cell($"A{row}").Value = "ASSETS";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        row++;
        
        // Current Assets
        ws.Cell($"A{row}").Value = "Current Assets";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        ws.Cell($"A{row}").Style.Font.Italic = true;
        row++;
        
        AddInputRow(ws, ref row, "Cash and Cash Equivalents");
        AddInputRow(ws, ref row, "Trade Receivables");
        AddInputRow(ws, ref row, "Inventory");
        AddInputRow(ws, ref row, "Prepaid Expenses");
        AddInputRow(ws, ref row, "Other Current Assets");
        AddFormulaRow(ws, ref row, "Total Current Assets", row - 5, row - 1);
        
        row++;
        
        // Non-Current Assets
        ws.Cell($"A{row}").Value = "Non-Current Assets";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        ws.Cell($"A{row}").Style.Font.Italic = true;
        row++;
        
        AddInputRow(ws, ref row, "Property, Plant & Equipment");
        AddInputRow(ws, ref row, "Intangible Assets");
        AddInputRow(ws, ref row, "Long-term Investments");
        AddInputRow(ws, ref row, "Deferred Tax Assets");
        AddInputRow(ws, ref row, "Other Non-Current Assets");
        int totalNcaRow = row; // Capture row BEFORE AddFormulaRow increments it
        AddFormulaRow(ws, ref row, "Total Non-Current Assets", row - 5, row - 1);
        
        row++;
        int totalCaRow = 13;        // Row 13 = Total Current Assets
        int totalAssetsRow = row;
        ws.Cell($"A{row}").Value = "TOTAL ASSETS";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        ws.Cell($"C{row}").FormulaA1 = $"=C{totalCaRow}+C{totalNcaRow}";
        ws.Cell($"D{row}").FormulaA1 = $"=D{totalCaRow}+D{totalNcaRow}";
        ws.Cell($"E{row}").FormulaA1 = $"=E{totalCaRow}+E{totalNcaRow}";
        ws.Range($"C{row}:E{row}").Style.Fill.BackgroundColor = XLColor.LightBlue;
        row++;
        
        row += 2;
        
        // LIABILITIES
        ws.Cell($"A{row}").Value = "LIABILITIES";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        row++;
        
        // Current Liabilities
        ws.Cell($"A{row}").Value = "Current Liabilities";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        ws.Cell($"A{row}").Style.Font.Italic = true;
        row++;
        
        int clStart = row;
        AddInputRow(ws, ref row, "Trade Payables");
        AddInputRow(ws, ref row, "Short-term Borrowings");
        AddInputRow(ws, ref row, "Current Portion of Long-term Debt");
        AddInputRow(ws, ref row, "Accrued Expenses");
        AddInputRow(ws, ref row, "Tax Payable");
        AddInputRow(ws, ref row, "Other Current Liabilities");
        AddFormulaRow(ws, ref row, "Total Current Liabilities", clStart, row - 1);
        
        row++;
        
        // Non-Current Liabilities
        ws.Cell($"A{row}").Value = "Non-Current Liabilities";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        ws.Cell($"A{row}").Style.Font.Italic = true;
        row++;
        
        int nclStart = row;
        AddInputRow(ws, ref row, "Long-term Debt");
        AddInputRow(ws, ref row, "Deferred Tax Liabilities");
        AddInputRow(ws, ref row, "Provisions");
        AddInputRow(ws, ref row, "Other Non-Current Liabilities");
        AddFormulaRow(ws, ref row, "Total Non-Current Liabilities", nclStart, row - 1);
        
        row++;
        int totalLiabilitiesRow = row;
        ws.Cell($"A{row}").Value = "TOTAL LIABILITIES";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        ws.Cell($"C{row}").FormulaA1 = $"=C{clStart + 6}+C{nclStart + 4}";
        ws.Cell($"D{row}").FormulaA1 = $"=D{clStart + 6}+D{nclStart + 4}";
        ws.Cell($"E{row}").FormulaA1 = $"=E{clStart + 6}+E{nclStart + 4}";
        row++;
        
        row += 2;
        
        // EQUITY
        ws.Cell($"A{row}").Value = "EQUITY";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        row++;
        
        int eqStart = row;
        AddInputRow(ws, ref row, "Share Capital");
        AddInputRow(ws, ref row, "Share Premium");
        AddInputRow(ws, ref row, "Retained Earnings");
        AddInputRow(ws, ref row, "Other Reserves");
        int totalEquityRow = row; // Capture row BEFORE AddFormulaRow increments it
        AddFormulaRow(ws, ref row, "TOTAL EQUITY", eqStart, row - 1, isBold: true);
        
        row += 2;
        
        // Total Liabilities + Equity
        int totalLiabEquityRow = row;
        ws.Cell($"A{row}").Value = "TOTAL LIABILITIES + EQUITY";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        ws.Cell($"C{row}").FormulaA1 = $"=C{totalLiabilitiesRow}+C{totalEquityRow}";
        ws.Cell($"D{row}").FormulaA1 = $"=D{totalLiabilitiesRow}+D{totalEquityRow}";
        ws.Cell($"E{row}").FormulaA1 = $"=E{totalLiabilitiesRow}+E{totalEquityRow}";
        ws.Range($"A{row}:E{row}").Style.Font.Bold = true;
        ws.Range($"C{row}:E{row}").Style.Fill.BackgroundColor = XLColor.LightYellow;
        
        row += 2;
        
        // Balance Check
        ws.Cell($"A{row}").Value = "Balance Check (should be 0)";
        ws.Cell($"C{row}").FormulaA1 = $"=C{totalAssetsRow}-C{totalLiabEquityRow}";
        ws.Cell($"D{row}").FormulaA1 = $"=D{totalAssetsRow}-D{totalLiabEquityRow}";
        ws.Cell($"E{row}").FormulaA1 = $"=E{totalAssetsRow}-E{totalLiabEquityRow}";
        
        // Add conditional formatting for balance check
        var checkRange = ws.Range($"C{row}:E{row}");
        checkRange.AddConditionalFormat().WhenNotEquals("0").Fill.SetBackgroundColor(XLColor.Red);
        checkRange.AddConditionalFormat().WhenEquals("0").Fill.SetBackgroundColor(XLColor.LightGreen);
        
        // Format columns
        ws.Column("A").Width = 35;
        ws.Column("B").Width = 5;
        ws.Columns("C", "E").Width = 15;
        ws.Range("C6:E100").Style.NumberFormat.Format = "#,##0";
        
        // Note: Data validation dropdowns removed to avoid Excel compatibility issues
        // Year type values are pre-filled and can be manually changed if needed
    }

    private void CreateIncomeStatementTemplate(IXLWorkbook workbook)
    {
        var ws = workbook.Worksheets.Add("Income Statement");
        
        // Header
        ws.Cell("A1").Value = "INCOME STATEMENT (PROFIT & LOSS)";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Range("A1:E1").Merge();
        
        ws.Cell("A2").Value = $"All amounts in {CURRENCY} '000";
        ws.Range("A2:E2").Merge();
        
        // Year headers
        ws.Cell("B3").Value = "Line Item";
        ws.Cell("C3").Value = "Year 1";
        ws.Cell("D3").Value = "Year 2";
        ws.Cell("E3").Value = "Year 3";
        ws.Range("B3:E3").Style.Font.Bold = true;
        ws.Range("B3:E3").Style.Fill.BackgroundColor = XLColor.LightGray;
        
        int row = 5;
        
        // Revenue
        ws.Cell($"A{row}").Value = "REVENUE";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        row++;
        
        AddInputRow(ws, ref row, "Revenue from Operations");
        AddInputRow(ws, ref row, "Other Operating Income");
        int totalRevenueRow = row; // Capture BEFORE AddFormulaRow increments
        AddFormulaRow(ws, ref row, "Total Revenue", row - 2, row - 1, isBold: true);
        
        row++;
        
        // Cost of Sales
        AddInputRow(ws, ref row, "Cost of Sales");
        int cogsRow = row - 1;
        
        // Gross Profit
        int grossProfitRow = row;
        ws.Cell($"A{row}").Value = "GROSS PROFIT";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        ws.Cell($"C{row}").FormulaA1 = $"=C{totalRevenueRow}-C{cogsRow}";
        ws.Cell($"D{row}").FormulaA1 = $"=D{totalRevenueRow}-D{cogsRow}";
        ws.Cell($"E{row}").FormulaA1 = $"=E{totalRevenueRow}-E{cogsRow}";
        row++;
        
        row++;
        
        // Operating Expenses
        ws.Cell($"A{row}").Value = "OPERATING EXPENSES";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        row++;
        
        int opexStart = row;
        AddInputRow(ws, ref row, "Selling & Distribution Expenses");
        AddInputRow(ws, ref row, "Administrative Expenses");
        int daRow = row; // D&A row - capture before increment
        AddInputRow(ws, ref row, "Depreciation & Amortization");
        AddInputRow(ws, ref row, "Other Operating Expenses");
        int totalOpexRow = row; // Capture BEFORE AddFormulaRow increments
        AddFormulaRow(ws, ref row, "Total Operating Expenses", opexStart, row - 1);
        
        row++;
        
        // Operating Profit
        int ebitRow = row;
        ws.Cell($"A{row}").Value = "OPERATING PROFIT (EBIT)";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        ws.Cell($"C{row}").FormulaA1 = $"=C{grossProfitRow}-C{totalOpexRow}";
        ws.Cell($"D{row}").FormulaA1 = $"=D{grossProfitRow}-D{totalOpexRow}";
        ws.Cell($"E{row}").FormulaA1 = $"=E{grossProfitRow}-E{totalOpexRow}";
        row++;
        
        // EBITDA
        ws.Cell($"A{row}").Value = "EBITDA";
        ws.Cell($"A{row}").Style.Font.Italic = true;
        ws.Cell($"C{row}").FormulaA1 = $"=C{ebitRow}+C{daRow}";
        ws.Cell($"D{row}").FormulaA1 = $"=D{ebitRow}+D{daRow}";
        ws.Cell($"E{row}").FormulaA1 = $"=E{ebitRow}+E{daRow}";
        row++;
        
        row++;
        
        // Finance
        ws.Cell($"A{row}").Value = "FINANCE";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        row++;
        
        AddInputRow(ws, ref row, "Interest Income");
        int intIncRow = row - 1;
        AddInputRow(ws, ref row, "Interest Expense");
        int intExpRow = row - 1;
        AddInputRow(ws, ref row, "Other Finance Costs");
        int otherFinRow = row - 1;
        
        ws.Cell($"A{row}").Value = "Net Finance Cost";
        ws.Cell($"C{row}").FormulaA1 = $"=C{intExpRow}+C{otherFinRow}-C{intIncRow}";
        ws.Cell($"D{row}").FormulaA1 = $"=D{intExpRow}+D{otherFinRow}-D{intIncRow}";
        ws.Cell($"E{row}").FormulaA1 = $"=E{intExpRow}+E{otherFinRow}-E{intIncRow}";
        int netFinRow = row;
        row++;
        
        row++;
        
        // Profit Before Tax
        ws.Cell($"A{row}").Value = "PROFIT BEFORE TAX";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        ws.Cell($"C{row}").FormulaA1 = $"=C{ebitRow}-C{netFinRow}";
        ws.Cell($"D{row}").FormulaA1 = $"=D{ebitRow}-D{netFinRow}";
        ws.Cell($"E{row}").FormulaA1 = $"=E{ebitRow}-E{netFinRow}";
        int pbtRow = row;
        row++;
        
        AddInputRow(ws, ref row, "Income Tax Expense");
        int taxRow = row - 1;
        
        row++;
        
        // Net Profit
        ws.Cell($"A{row}").Value = "NET PROFIT";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        ws.Cell($"C{row}").FormulaA1 = $"=C{pbtRow}-C{taxRow}";
        ws.Cell($"D{row}").FormulaA1 = $"=D{pbtRow}-D{taxRow}";
        ws.Cell($"E{row}").FormulaA1 = $"=E{pbtRow}-E{taxRow}";
        ws.Range($"C{row}:E{row}").Style.Fill.BackgroundColor = XLColor.LightYellow;
        row++;
        
        AddInputRow(ws, ref row, "Dividends Declared");
        
        // Format columns
        ws.Column("A").Width = 35;
        ws.Column("B").Width = 5;
        ws.Columns("C", "E").Width = 15;
        ws.Range("C5:E100").Style.NumberFormat.Format = "#,##0";
    }

    private void CreateCashFlowTemplate(IXLWorkbook workbook)
    {
        var ws = workbook.Worksheets.Add("Cash Flow");
        
        // Header
        ws.Cell("A1").Value = "STATEMENT OF CASH FLOWS";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Range("A1:E1").Merge();
        
        ws.Cell("A2").Value = $"All amounts in {CURRENCY} '000";
        ws.Range("A2:E2").Merge();
        
        // Year headers
        ws.Cell("B3").Value = "Line Item";
        ws.Cell("C3").Value = "Year 1";
        ws.Cell("D3").Value = "Year 2";
        ws.Cell("E3").Value = "Year 3";
        ws.Range("B3:E3").Style.Font.Bold = true;
        ws.Range("B3:E3").Style.Fill.BackgroundColor = XLColor.LightGray;
        
        int row = 5;
        
        // Operating Activities
        ws.Cell($"A{row}").Value = "CASH FLOWS FROM OPERATING ACTIVITIES";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        row++;
        
        int opStart = row;
        AddInputRow(ws, ref row, "Profit Before Tax");
        AddInputRow(ws, ref row, "Depreciation & Amortization");
        AddInputRow(ws, ref row, "Interest Expense (add back)");
        AddInputRow(ws, ref row, "Changes in Working Capital");
        AddInputRow(ws, ref row, "Tax Paid (negative)");
        AddInputRow(ws, ref row, "Other Operating Adjustments");
        
        int opCashRow = row;
        ws.Cell($"A{row}").Value = "Net Cash from Operating Activities";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        ws.Cell($"C{row}").FormulaA1 = $"=SUM(C{opStart}:C{row - 1})";
        ws.Cell($"D{row}").FormulaA1 = $"=SUM(D{opStart}:D{row - 1})";
        ws.Cell($"E{row}").FormulaA1 = $"=SUM(E{opStart}:E{row - 1})";
        row++;
        
        row++;
        
        // Investing Activities
        ws.Cell($"A{row}").Value = "CASH FLOWS FROM INVESTING ACTIVITIES";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        row++;
        
        int invStart = row;
        AddInputRow(ws, ref row, "Purchase of PPE (negative)");
        AddInputRow(ws, ref row, "Sale of PPE");
        AddInputRow(ws, ref row, "Purchase of Investments (negative)");
        AddInputRow(ws, ref row, "Sale of Investments");
        AddInputRow(ws, ref row, "Interest Received");
        AddInputRow(ws, ref row, "Dividends Received");
        AddInputRow(ws, ref row, "Other Investing Activities");
        int invCashRow = row; // Capture BEFORE AddFormulaRow increments
        AddFormulaRow(ws, ref row, "Net Cash from Investing Activities", invStart, row - 1, isBold: true);
        
        row++;
        
        // Financing Activities
        ws.Cell($"A{row}").Value = "CASH FLOWS FROM FINANCING ACTIVITIES";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        row++;
        
        int finStart = row;
        AddInputRow(ws, ref row, "Proceeds from Borrowings");
        AddInputRow(ws, ref row, "Repayment of Borrowings (negative)");
        AddInputRow(ws, ref row, "Interest Paid (negative)");
        AddInputRow(ws, ref row, "Dividends Paid (negative)");
        AddInputRow(ws, ref row, "Proceeds from Share Issue");
        AddInputRow(ws, ref row, "Other Financing Activities");
        int finCashRow = row; // Capture BEFORE AddFormulaRow increments
        AddFormulaRow(ws, ref row, "Net Cash from Financing Activities", finStart, row - 1, isBold: true);
        
        row++;
        
        // Net Change
        ws.Cell($"A{row}").Value = "NET CHANGE IN CASH";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        ws.Cell($"C{row}").FormulaA1 = $"=C{opCashRow}+C{invCashRow}+C{finCashRow}";
        ws.Cell($"D{row}").FormulaA1 = $"=D{opCashRow}+D{invCashRow}+D{finCashRow}";
        ws.Cell($"E{row}").FormulaA1 = $"=E{opCashRow}+E{invCashRow}+E{finCashRow}";
        int netChangeRow = row;
        row++;
        
        AddInputRow(ws, ref row, "Opening Cash Balance");
        int openingRow = row - 1;
        
        ws.Cell($"A{row}").Value = "CLOSING CASH BALANCE";
        ws.Cell($"A{row}").Style.Font.Bold = true;
        ws.Cell($"C{row}").FormulaA1 = $"=C{openingRow}+C{netChangeRow}";
        ws.Cell($"D{row}").FormulaA1 = $"=D{openingRow}+D{netChangeRow}";
        ws.Cell($"E{row}").FormulaA1 = $"=E{openingRow}+E{netChangeRow}";
        ws.Range($"C{row}:E{row}").Style.Fill.BackgroundColor = XLColor.LightYellow;
        
        // Format columns
        ws.Column("A").Width = 40;
        ws.Column("B").Width = 5;
        ws.Columns("C", "E").Width = 15;
        ws.Range("C5:E100").Style.NumberFormat.Format = "#,##0";
    }

    #endregion

    #region Sample Data

    /// <summary>
    /// Creates sample Balance Sheet data for Acme Manufacturing Ltd.
    /// All figures are properly balanced: Total Assets = Total Liabilities + Total Equity
    /// Amounts are in NGN '000 (thousands)
    /// 
    /// EXCEL ROW MAPPING (from CreateBalanceSheetTemplate):
    /// ASSETS:
    ///   Row 8-12: Current Assets (input rows: Cash, Receivables, Inventory, Prepaid, Other)
    ///   Row 13: Total Current Assets (formula = SUM rows 8:12)
    ///   Row 16-20: Non-Current Assets (input rows: PPE, Intangibles, LT Investments, Deferred Tax, Other)
    ///   Row 21: Total Non-Current Assets (formula = SUM rows 16:20)
    ///   Row 23: TOTAL ASSETS (formula = Row 13 + Row 21)
    /// LIABILITIES:
    ///   Row 28-33: Current Liabilities (input rows: Payables, ST Borrowings, Current LTD, Accrued, Tax, Other)
    ///   Row 34: Total Current Liabilities (formula = SUM rows 28:33)
    ///   Row 37-40: Non-Current Liabilities (input rows: LT Debt, Deferred Tax, Provisions, Other)
    ///   Row 41: Total Non-Current Liabilities (formula = SUM rows 37:40)
    ///   Row 43: TOTAL LIABILITIES (formula = Row 34 + Row 41)
    /// EQUITY:
    ///   Row 47-50: Equity (input rows: Share Capital, Share Premium, Retained Earnings, Other Reserves)
    ///   Row 51: TOTAL EQUITY (formula = SUM rows 47:50)
    /// RECONCILIATION:
    ///   Row 54: TOTAL LIABILITIES + EQUITY (formula = Row 43 + Row 51)
    ///   Row 57: Balance Check (formula = Row 23 - Row 54, should be 0)
    /// </summary>
    private void CreateBalanceSheetWithSampleData(IXLWorkbook workbook)
    {
        CreateBalanceSheetTemplate(workbook);
        var ws = workbook.Worksheet("Balance Sheet");
        
        // Year headers
        ws.Cell("C3").Value = 2023;
        ws.Cell("D3").Value = 2024;
        ws.Cell("E3").Value = 2025;
        
        // ============================================
        // 2023 BALANCE SHEET (Year 1)
        // Total Assets = 5,500,000
        // Total Liabilities = 2,450,000
        // Total Equity = 3,050,000
        // Check: 2,450,000 + 3,050,000 = 5,500,000 ✓
        // ============================================
        
        // Current Assets (rows 8-12): Total = 2,600,000
        ws.Cell("C8").Value = 450000;    // Cash and Cash Equivalents
        ws.Cell("C9").Value = 1200000;   // Trade Receivables
        ws.Cell("C10").Value = 800000;   // Inventory
        ws.Cell("C11").Value = 50000;    // Prepaid Expenses
        ws.Cell("C12").Value = 100000;   // Other Current Assets
        
        // Non-Current Assets (rows 16-20): Total = 2,900,000
        ws.Cell("C16").Value = 2500000;  // Property, Plant & Equipment
        ws.Cell("C17").Value = 150000;   // Intangible Assets
        ws.Cell("C18").Value = 200000;   // Long-term Investments
        ws.Cell("C19").Value = 50000;    // Deferred Tax Assets
        ws.Cell("C20").Value = 0;        // Other Non-Current Assets
        
        // Current Liabilities (rows 28-33): Total = 1,300,000
        ws.Cell("C28").Value = 600000;   // Trade Payables
        ws.Cell("C29").Value = 200000;   // Short-term Borrowings
        ws.Cell("C30").Value = 150000;   // Current Portion of Long-term Debt
        ws.Cell("C31").Value = 180000;   // Accrued Expenses
        ws.Cell("C32").Value = 120000;   // Tax Payable
        ws.Cell("C33").Value = 50000;    // Other Current Liabilities
        
        // Non-Current Liabilities (rows 37-40): Total = 1,150,000
        ws.Cell("C37").Value = 1000000;  // Long-term Debt
        ws.Cell("C38").Value = 80000;    // Deferred Tax Liabilities
        ws.Cell("C39").Value = 70000;    // Provisions
        ws.Cell("C40").Value = 0;        // Other Non-Current Liabilities
        
        // Equity (rows 47-50): Total = 3,050,000
        ws.Cell("C47").Value = 500000;   // Share Capital
        ws.Cell("C48").Value = 300000;   // Share Premium
        ws.Cell("C49").Value = 2250000;  // Retained Earnings
        ws.Cell("C50").Value = 0;        // Other Reserves
        
        // ============================================
        // 2024 BALANCE SHEET (Year 2) - Growth Year
        // Total Assets = 6,450,000
        // Total Liabilities = 2,480,000
        // Total Equity = 3,970,000
        // Check: 2,480,000 + 3,970,000 = 6,450,000 ✓
        // ============================================
        
        // Current Assets (rows 8-12): Total = 3,150,000
        ws.Cell("D8").Value = 580000;    // Cash (+29%)
        ws.Cell("D9").Value = 1450000;   // Trade Receivables (+21%)
        ws.Cell("D10").Value = 950000;   // Inventory (+19%)
        ws.Cell("D11").Value = 60000;    // Prepaid Expenses
        ws.Cell("D12").Value = 110000;   // Other Current Assets
        
        // Non-Current Assets (rows 16-20): Total = 3,300,000
        ws.Cell("D16").Value = 2800000;  // PPE (investment)
        ws.Cell("D17").Value = 180000;   // Intangible Assets
        ws.Cell("D18").Value = 250000;   // Long-term Investments
        ws.Cell("D19").Value = 70000;    // Deferred Tax Assets
        ws.Cell("D20").Value = 0;        // Other Non-Current Assets
        
        // Current Liabilities (rows 28-33): Total = 1,500,000
        ws.Cell("D28").Value = 720000;   // Trade Payables
        ws.Cell("D29").Value = 150000;   // Short-term Borrowings (reduced)
        ws.Cell("D30").Value = 200000;   // Current Portion of Long-term Debt
        ws.Cell("D31").Value = 200000;   // Accrued Expenses
        ws.Cell("D32").Value = 180000;   // Tax Payable
        ws.Cell("D33").Value = 50000;    // Other Current Liabilities
        
        // Non-Current Liabilities (rows 37-40): Total = 980,000
        ws.Cell("D37").Value = 800000;   // Long-term Debt (reducing)
        ws.Cell("D38").Value = 100000;   // Deferred Tax Liabilities
        ws.Cell("D39").Value = 80000;    // Provisions
        ws.Cell("D40").Value = 0;        // Other Non-Current Liabilities
        
        // Equity (rows 47-50): Total = 3,970,000
        // Retained Earnings = Opening 2,250,000 + Net Profit 1,070,000 - Dividends 350,000 = 2,970,000
        ws.Cell("D47").Value = 500000;   // Share Capital
        ws.Cell("D48").Value = 300000;   // Share Premium
        ws.Cell("D49").Value = 3170000;  // Retained Earnings
        ws.Cell("D50").Value = 0;        // Other Reserves
        
        // ============================================
        // 2025 BALANCE SHEET (Year 3) - Continued Growth
        // Total Assets = 7,500,000
        // Total Liabilities = 2,500,000
        // Total Equity = 5,000,000
        // Check: 2,500,000 + 5,000,000 = 7,500,000 ✓
        // ============================================
        
        // Current Assets (rows 8-12): Total = 3,700,000
        ws.Cell("E8").Value = 720000;    // Cash
        ws.Cell("E9").Value = 1680000;   // Trade Receivables
        ws.Cell("E10").Value = 1100000;  // Inventory
        ws.Cell("E11").Value = 70000;    // Prepaid Expenses
        ws.Cell("E12").Value = 130000;   // Other Current Assets
        
        // Non-Current Assets (rows 16-20): Total = 3,800,000
        ws.Cell("E16").Value = 3200000;  // PPE
        ws.Cell("E17").Value = 200000;   // Intangible Assets
        ws.Cell("E18").Value = 300000;   // Long-term Investments
        ws.Cell("E19").Value = 80000;    // Deferred Tax Assets
        ws.Cell("E20").Value = 20000;    // Other Non-Current Assets
        
        // Current Liabilities (rows 28-33): Total = 1,680,000
        ws.Cell("E28").Value = 850000;   // Trade Payables
        ws.Cell("E29").Value = 100000;   // Short-term Borrowings
        ws.Cell("E30").Value = 200000;   // Current Portion of Long-term Debt
        ws.Cell("E31").Value = 220000;   // Accrued Expenses
        ws.Cell("E32").Value = 250000;   // Tax Payable
        ws.Cell("E33").Value = 60000;    // Other Current Liabilities
        
        // Non-Current Liabilities (rows 37-40): Total = 820,000
        ws.Cell("E37").Value = 600000;   // Long-term Debt
        ws.Cell("E38").Value = 120000;   // Deferred Tax Liabilities
        ws.Cell("E39").Value = 100000;   // Provisions
        ws.Cell("E40").Value = 0;        // Other Non-Current Liabilities
        
        // Equity (rows 47-50): Total = 5,000,000
        // Retained Earnings = Opening 3,170,000 + Net Profit 1,330,000 - Dividends 500,000 = 4,000,000
        ws.Cell("E47").Value = 500000;   // Share Capital
        ws.Cell("E48").Value = 300000;   // Share Premium
        ws.Cell("E49").Value = 4200000;  // Retained Earnings
        ws.Cell("E50").Value = 0;        // Other Reserves
    }

    /// <summary>
    /// Creates sample Income Statement data for Acme Manufacturing Ltd.
    /// Net Profit flows to Retained Earnings in Balance Sheet.
    /// Amounts are in NGN '000 (thousands)
    /// </summary>
    /// <summary>
    /// Creates sample Income Statement data for Acme Manufacturing Ltd.
    /// Net Profit flows to Retained Earnings in Balance Sheet.
    /// Amounts are in NGN '000 (thousands)
    /// 
    /// EXCEL ROW MAPPING (from CreateIncomeStatementTemplate):
    /// Row 6: Revenue from Operations (input)
    /// Row 7: Other Operating Income (input)
    /// Row 8: Total Revenue (formula)
    /// Row 10: Cost of Sales (input)
    /// Row 11: Gross Profit (formula)
    /// Row 14: Selling & Distribution (input)
    /// Row 15: Administrative (input)
    /// Row 16: D&A (input)
    /// Row 17: Other OpEx (input)
    /// Row 18: Total OpEx (formula)
    /// Row 20: EBIT (formula)
    /// Row 21: EBITDA (formula)
    /// Row 24: Interest Income (input)
    /// Row 25: Interest Expense (input)
    /// Row 26: Other Finance Costs (input)
    /// Row 27: Net Finance Cost (formula)
    /// Row 29: Profit Before Tax (formula)
    /// Row 30: Income Tax Expense (input)
    /// Row 32: Net Profit (formula)
    /// Row 33: Dividends Declared (input)
    /// 
    /// RECONCILIATION WITH BALANCE SHEET:
    /// 2023: Net Profit 920,000 - Dividends 350,000 = Retained 570,000
    ///       Opening RE 1,680,000 + 570,000 = Closing RE 2,250,000
    /// 2024: Net Profit 1,420,000 - Dividends 500,000 = Retained 920,000
    ///       Opening RE 2,250,000 + 920,000 = Closing RE 3,170,000
    /// 2025: Net Profit 1,530,000 - Dividends 500,000 = Retained 1,030,000
    ///       Opening RE 3,170,000 + 1,030,000 = Closing RE 4,200,000
    /// </summary>
    private void CreateIncomeStatementWithSampleData(IXLWorkbook workbook)
    {
        CreateIncomeStatementTemplate(workbook);
        var ws = workbook.Worksheet("Income Statement");
        
        // Year headers
        ws.Cell("C3").Value = 2023;
        ws.Cell("D3").Value = 2024;
        ws.Cell("E3").Value = 2025;
        
        // ============================================
        // 2023 INCOME STATEMENT
        // Net Profit: 920,000 | Dividends: 350,000 | Retained: 570,000
        // ============================================
        
        // Revenue (rows 6-7)
        ws.Cell("C6").Value = 8500000;   // Revenue from Operations
        ws.Cell("C7").Value = 150000;    // Other Operating Income
        // Total Revenue (row 8) = 8,650,000
        
        // Cost of Sales (row 10)
        ws.Cell("C10").Value = 5100000;  // Cost of Sales (59%)
        // Gross Profit (row 11) = 3,550,000
        
        // Operating Expenses (rows 14-17)
        ws.Cell("C14").Value = 850000;   // Selling & Distribution
        ws.Cell("C15").Value = 680000;   // Administrative
        ws.Cell("C16").Value = 320000;   // Depreciation & Amortization
        ws.Cell("C17").Value = 150000;   // Other Operating Expenses
        // Total OpEx (row 18) = 2,000,000
        // EBIT (row 20) = 1,550,000
        
        // Finance (rows 24-26)
        ws.Cell("C24").Value = 25000;    // Interest Income
        ws.Cell("C25").Value = 180000;   // Interest Expense
        ws.Cell("C26").Value = 20000;    // Other Finance Costs
        // Net Finance Cost (row 27) = 175,000
        // PBT (row 29) = 1,375,000
        
        // Tax (row 30)
        ws.Cell("C30").Value = 455000;   // Income Tax (33%)
        // Net Profit (row 32) = 920,000
        
        // Dividends (row 33)
        ws.Cell("C33").Value = 350000;   // Dividends Declared
        // Retained = 570,000
        
        // ============================================
        // 2024 INCOME STATEMENT (+18% revenue)
        // Net Profit: 1,420,000 | Dividends: 500,000 | Retained: 920,000
        // ============================================
        
        // Revenue
        ws.Cell("D6").Value = 10030000;  // Revenue
        ws.Cell("D7").Value = 180000;    // Other Income
        // Total Revenue = 10,210,000
        
        // COGS
        ws.Cell("D10").Value = 5920000;  // Cost of Sales (58%)
        // Gross Profit = 4,290,000
        
        // Operating Expenses
        ws.Cell("D14").Value = 950000;   // Selling
        ws.Cell("D15").Value = 750000;   // Admin
        ws.Cell("D16").Value = 380000;   // D&A
        ws.Cell("D17").Value = 160000;   // Other
        // Total OpEx = 2,240,000
        // EBIT = 2,050,000
        
        // Finance
        ws.Cell("D24").Value = 35000;    // Interest Income
        ws.Cell("D25").Value = 150000;   // Interest Expense
        ws.Cell("D26").Value = 15000;    // Other Finance
        // Net Finance Cost = 130,000
        // PBT = 1,920,000
        
        // Tax
        ws.Cell("D30").Value = 500000;   // Tax (26%)
        // Net Profit = 1,420,000
        
        // Dividends
        ws.Cell("D33").Value = 500000;   // Dividends
        // Retained = 920,000
        
        // ============================================
        // 2025 INCOME STATEMENT (+15% revenue)
        // Net Profit: 1,530,000 | Dividends: 500,000 | Retained: 1,030,000
        // ============================================
        
        // Revenue
        ws.Cell("E6").Value = 11535000;  // Revenue
        ws.Cell("E7").Value = 200000;    // Other Income
        // Total Revenue = 11,735,000
        
        // COGS
        ws.Cell("E10").Value = 6690000;  // Cost of Sales (57%)
        // Gross Profit = 5,045,000
        
        // Operating Expenses
        ws.Cell("E14").Value = 1050000;  // Selling
        ws.Cell("E15").Value = 820000;   // Admin
        ws.Cell("E16").Value = 420000;   // D&A
        ws.Cell("E17").Value = 175000;   // Other
        // Total OpEx = 2,465,000
        // EBIT = 2,580,000
        
        // Finance
        ws.Cell("E24").Value = 45000;    // Interest Income
        ws.Cell("E25").Value = 120000;   // Interest Expense
        ws.Cell("E26").Value = 10000;    // Other Finance
        // Net Finance Cost = 85,000
        // PBT = 2,495,000
        
        // Tax
        ws.Cell("E30").Value = 965000;   // Tax (39%)
        // Net Profit = 1,530,000
        
        // Dividends
        ws.Cell("E33").Value = 500000;   // Dividends
        // Retained = 1,030,000
    }

    /// <summary>
    /// Creates sample Cash Flow Statement data for Acme Manufacturing Ltd.
    /// Cash movements reconcile with Balance Sheet cash changes.
    /// Amounts are in NGN '000 (thousands)
    /// 
    /// EXCEL ROW MAPPING (from CreateCashFlowTemplate):
    /// Operating Activities (rows 6-11):
    ///   Row 6: Profit Before Tax (input) - must match P&L
    ///   Row 7: Depreciation & Amortization (input) - must match P&L
    ///   Row 8: Interest Expense add back (input)
    ///   Row 9: Changes in Working Capital (input)
    ///   Row 10: Tax Paid (input) - must match P&L
    ///   Row 11: Other Operating Adjustments (input)
    ///   Row 12: Net Cash from Operating (formula)
    /// Investing Activities (rows 15-21):
    ///   Row 15: Purchase of PPE (input, negative)
    ///   Row 16: Sale of PPE (input)
    ///   Row 17: Purchase of Investments (input, negative)
    ///   Row 18: Sale of Investments (input)
    ///   Row 19: Interest Received (input)
    ///   Row 20: Dividends Received (input)
    ///   Row 21: Other Investing (input)
    ///   Row 22: Net Cash from Investing (formula)
    /// Financing Activities (rows 25-30):
    ///   Row 25: Proceeds from Borrowings (input)
    ///   Row 26: Repayment of Borrowings (input, negative)
    ///   Row 27: Interest Paid (input, negative)
    ///   Row 28: Dividends Paid (input, negative)
    ///   Row 29: Share Issue (input)
    ///   Row 30: Other Financing (input)
    ///   Row 31: Net Cash from Financing (formula)
    /// Summary:
    ///   Row 33: Net Change in Cash (formula)
    ///   Row 34: Opening Cash Balance (input)
    ///   Row 35: Closing Cash Balance (formula)
    /// 
    /// RECONCILIATION WITH BALANCE SHEET:
    /// 2023: Opening 300,000 + Net Change 150,000 = Closing 450,000 ✓
    /// 2024: Opening 450,000 + Net Change 130,000 = Closing 580,000 ✓
    /// 2025: Opening 580,000 + Net Change 140,000 = Closing 720,000 ✓
    /// </summary>
    private void CreateCashFlowWithSampleData(IXLWorkbook workbook)
    {
        CreateCashFlowTemplate(workbook);
        var ws = workbook.Worksheet("Cash Flow");
        
        // Year headers
        ws.Cell("C3").Value = 2023;
        ws.Cell("D3").Value = 2024;
        ws.Cell("E3").Value = 2025;
        
        // ============================================
        // 2023 CASH FLOW STATEMENT
        // Opening Cash: 300,000 → Closing Cash: 450,000
        // Net Change Required: 150,000
        // ============================================
        
        // Operating Activities (rows 6-11)
        ws.Cell("C6").Value = 1375000;   // PBT (from P&L: 8,650,000 rev - 5,100,000 COGS - 2,000,000 OpEx - 175,000 fin)
        ws.Cell("C7").Value = 320000;    // D&A (from P&L row 16)
        ws.Cell("C8").Value = 155000;    // Interest Expense add back (180,000 - 25,000 interest income)
        ws.Cell("C9").Value = -245000;   // Changes in Working Capital
        ws.Cell("C10").Value = -455000;  // Tax Paid (negative - cash outflow)
        ws.Cell("C11").Value = 0;        // Other Adjustments
        // Net Operating = 1,375,000 + 320,000 + 155,000 - 245,000 - 455,000 = 1,150,000
        
        // Investing Activities (rows 15-21)
        ws.Cell("C15").Value = -500000;  // Purchase of PPE
        ws.Cell("C16").Value = 50000;    // Sale of PPE
        ws.Cell("C17").Value = -100000;  // Purchase of Investments
        ws.Cell("C18").Value = 0;        // Sale of Investments
        ws.Cell("C19").Value = 25000;    // Interest Received
        ws.Cell("C20").Value = 0;        // Dividends Received
        ws.Cell("C21").Value = 0;        // Other
        // Net Investing = -525,000
        
        // Financing Activities (rows 25-30)
        ws.Cell("C25").Value = 200000;   // Proceeds from Borrowings
        ws.Cell("C26").Value = -250000;  // Repayment of Borrowings
        ws.Cell("C27").Value = -180000;  // Interest Paid
        ws.Cell("C28").Value = -200000;  // Dividends Paid (prior year)
        ws.Cell("C29").Value = 0;        // Share Issue
        ws.Cell("C30").Value = -45000;   // Other
        // Net Financing = -475,000
        
        // Net Change = 1,150,000 - 525,000 - 475,000 = 150,000 ✓
        ws.Cell("C34").Value = 300000;   // Opening Cash Balance
        // Closing Cash = 450,000 ✓
        
        // ============================================
        // 2024 CASH FLOW STATEMENT
        // Opening Cash: 450,000 → Closing Cash: 580,000
        // Net Change Required: 130,000
        // ============================================
        
        // Operating Activities
        ws.Cell("D6").Value = 1920000;   // PBT (from P&L)
        ws.Cell("D7").Value = 380000;    // D&A (from P&L row 16)
        ws.Cell("D8").Value = 115000;    // Interest add back (150,000 - 35,000)
        ws.Cell("D9").Value = -280000;   // Working Capital changes
        ws.Cell("D10").Value = -500000;  // Tax Paid (negative - cash outflow)
        ws.Cell("D11").Value = 0;        // Other
        // Net Operating = 1,920,000 + 380,000 + 115,000 - 280,000 - 500,000 = 1,635,000
        
        // Investing Activities
        ws.Cell("D15").Value = -680000;  // PPE purchase
        ws.Cell("D16").Value = 0;        // PPE sale
        ws.Cell("D17").Value = -50000;   // Investment purchase
        ws.Cell("D18").Value = 0;        // Investment sale
        ws.Cell("D19").Value = 35000;    // Interest received
        ws.Cell("D20").Value = 0;        // Dividends received
        ws.Cell("D21").Value = 0;        // Other
        // Net Investing = -695,000
        
        // Financing Activities
        ws.Cell("D25").Value = 0;        // Borrowing proceeds
        ws.Cell("D26").Value = -200000;  // Borrowing repayment
        ws.Cell("D27").Value = -150000;  // Interest paid
        ws.Cell("D28").Value = -350000;  // Dividends paid (2023 declared from P&L)
        ws.Cell("D29").Value = 0;        // Share issue
        ws.Cell("D30").Value = -110000;  // Other
        // Net Financing = -810,000
        
        // Net Change = 1,635,000 - 695,000 - 810,000 = 130,000 ✓
        ws.Cell("D34").Value = 450000;   // Opening Cash Balance
        // Closing Cash = 580,000 ✓
        
        // ============================================
        // 2025 CASH FLOW STATEMENT
        // Opening Cash: 580,000 → Closing Cash: 720,000
        // Net Change Required: 140,000
        // ============================================
        
        // Operating Activities
        ws.Cell("E6").Value = 2495000;   // PBT (from P&L)
        ws.Cell("E7").Value = 420000;    // D&A (from P&L row 16)
        ws.Cell("E8").Value = 75000;     // Interest add back (120,000 - 45,000)
        ws.Cell("E9").Value = -320000;   // Working Capital changes
        ws.Cell("E10").Value = -965000;  // Tax Paid (negative - cash outflow)
        ws.Cell("E11").Value = 0;        // Other
        // Net Operating = 2,495,000 + 420,000 + 75,000 - 320,000 - 965,000 = 1,705,000
        
        // Investing Activities
        ws.Cell("E15").Value = -820000;  // PPE purchase
        ws.Cell("E16").Value = 0;        // PPE sale
        ws.Cell("E17").Value = -70000;   // Investment purchase
        ws.Cell("E18").Value = 0;        // Investment sale
        ws.Cell("E19").Value = 45000;    // Interest received
        ws.Cell("E20").Value = 0;        // Dividends received
        ws.Cell("E21").Value = 0;        // Other
        // Net Investing = -845,000
        
        // Financing Activities
        ws.Cell("E25").Value = 0;        // Borrowing proceeds
        ws.Cell("E26").Value = -200000;  // Borrowing repayment
        ws.Cell("E27").Value = -120000;  // Interest paid
        ws.Cell("E28").Value = -500000;  // Dividends paid (2024 declared from P&L)
        ws.Cell("E29").Value = 0;        // Share issue
        ws.Cell("E30").Value = 100000;   // Other (receipt)
        // Net Financing = -720,000
        
        // Net Change = 1,705,000 - 845,000 - 720,000 = 140,000 ✓
        ws.Cell("E34").Value = 580000;   // Opening Cash Balance
        // Closing Cash = 720,000 ✓
    }

    #endregion

    #region Parsing Helpers

    /// <summary>
    /// Parses Balance Sheet data from Excel worksheet.
    /// Row mapping matches CreateBalanceSheetTemplate:
    ///   Current Assets: rows 8-12
    ///   Non-Current Assets: rows 16-20
    ///   Current Liabilities: rows 28-33
    ///   Non-Current Liabilities: rows 37-40
    ///   Equity: rows 47-50
    /// </summary>
    private BalanceSheetData ParseBalanceSheet(IXLWorksheet ws, int col)
    {
        return new BalanceSheetData
        {
            // Current Assets (rows 8-12)
            CashAndCashEquivalents = GetDecimal(ws, 8, col),
            TradeReceivables = GetDecimal(ws, 9, col),
            Inventory = GetDecimal(ws, 10, col),
            PrepaidExpenses = GetDecimal(ws, 11, col),
            OtherCurrentAssets = GetDecimal(ws, 12, col),
            
            // Non-Current Assets (rows 16-20)
            PropertyPlantEquipment = GetDecimal(ws, 16, col),
            IntangibleAssets = GetDecimal(ws, 17, col),
            LongTermInvestments = GetDecimal(ws, 18, col),
            DeferredTaxAssets = GetDecimal(ws, 19, col),
            OtherNonCurrentAssets = GetDecimal(ws, 20, col),
            
            // Current Liabilities (rows 28-33)
            TradePayables = GetDecimal(ws, 28, col),
            ShortTermBorrowings = GetDecimal(ws, 29, col),
            CurrentPortionLongTermDebt = GetDecimal(ws, 30, col),
            AccruedExpenses = GetDecimal(ws, 31, col),
            TaxPayable = GetDecimal(ws, 32, col),
            OtherCurrentLiabilities = GetDecimal(ws, 33, col),
            
            // Non-Current Liabilities (rows 37-40)
            LongTermDebt = GetDecimal(ws, 37, col),
            DeferredTaxLiabilities = GetDecimal(ws, 38, col),
            Provisions = GetDecimal(ws, 39, col),
            OtherNonCurrentLiabilities = GetDecimal(ws, 40, col),
            
            // Equity (rows 47-50)
            ShareCapital = GetDecimal(ws, 47, col),
            SharePremium = GetDecimal(ws, 48, col),
            RetainedEarnings = GetDecimal(ws, 49, col),
            OtherReserves = GetDecimal(ws, 50, col)
        };
    }

    /// <summary>
    /// Parses Income Statement data from Excel worksheet.
    /// Row mapping matches CreateIncomeStatementTemplate:
    ///   Revenue: rows 6-7
    ///   COGS: row 10
    ///   Operating Expenses: rows 14-17
    ///   Finance: rows 24-26
    ///   Tax: row 30
    ///   Dividends: row 33
    /// </summary>
    private IncomeStatementData ParseIncomeStatement(IXLWorksheet ws, int col)
    {
        return new IncomeStatementData
        {
            Revenue = GetDecimal(ws, 6, col),
            OtherOperatingIncome = GetDecimal(ws, 7, col),
            CostOfSales = GetDecimal(ws, 10, col),
            SellingExpenses = GetDecimal(ws, 14, col),
            AdministrativeExpenses = GetDecimal(ws, 15, col),
            DepreciationAmortization = GetDecimal(ws, 16, col),
            OtherOperatingExpenses = GetDecimal(ws, 17, col),
            InterestIncome = GetDecimal(ws, 24, col),
            InterestExpense = GetDecimal(ws, 25, col),
            OtherFinanceCosts = GetDecimal(ws, 26, col),
            IncomeTaxExpense = GetDecimal(ws, 30, col),
            DividendsDeclared = GetDecimal(ws, 33, col)
        };
    }

    /// <summary>
    /// Parses Cash Flow data from Excel worksheet.
    /// Row mapping matches CreateCashFlowTemplate:
    ///   Operating: rows 6-11
    ///   Investing: rows 15-21
    ///   Financing: rows 25-30
    ///   Opening Cash: row 34
    /// </summary>
    private CashFlowData? ParseCashFlow(IXLWorksheet ws, int col)
    {
        return new CashFlowData
        {
            ProfitBeforeTax = GetDecimal(ws, 6, col),
            DepreciationAmortization = GetDecimal(ws, 7, col),
            InterestExpenseAddBack = GetDecimal(ws, 8, col),
            ChangesInWorkingCapital = GetDecimal(ws, 9, col),
            TaxPaid = Math.Abs(GetDecimal(ws, 10, col)), // Stored as positive, entered as negative
            OtherOperatingAdjustments = GetDecimal(ws, 11, col),
            
            PurchaseOfPPE = Math.Abs(GetDecimal(ws, 15, col)),
            SaleOfPPE = GetDecimal(ws, 16, col),
            PurchaseOfInvestments = Math.Abs(GetDecimal(ws, 17, col)),
            SaleOfInvestments = GetDecimal(ws, 18, col),
            InterestReceived = GetDecimal(ws, 19, col),
            DividendsReceived = GetDecimal(ws, 20, col),
            OtherInvestingActivities = GetDecimal(ws, 21, col),
            
            ProceedsFromBorrowings = GetDecimal(ws, 25, col),
            RepaymentOfBorrowings = Math.Abs(GetDecimal(ws, 26, col)),
            InterestPaid = Math.Abs(GetDecimal(ws, 27, col)),
            DividendsPaid = Math.Abs(GetDecimal(ws, 28, col)),
            ProceedsFromShareIssue = GetDecimal(ws, 29, col),
            OtherFinancingActivities = GetDecimal(ws, 30, col),
            
            OpeningCashBalance = GetDecimal(ws, 34, col)
        };
    }

    private decimal GetDecimal(IXLWorksheet ws, int row, int col)
    {
        var cell = ws.Cell(row, col);
        if (cell.IsEmpty()) return 0;
        
        if (cell.Value.IsNumber)
            return (decimal)cell.Value.GetNumber();
        
        if (decimal.TryParse(cell.GetString(), out var result))
            return result;
        
        return 0;
    }

    private void AddInputRow(IXLWorksheet ws, ref int row, string label)
    {
        ws.Cell($"A{row}").Value = label;
        ws.Cell($"C{row}").Value = 0;
        ws.Cell($"D{row}").Value = 0;
        ws.Cell($"E{row}").Value = 0;
        row++;
    }

    private void AddFormulaRow(IXLWorksheet ws, ref int row, string label, int startRow, int endRow, bool isBold = false, bool isSum = true)
    {
        ws.Cell($"A{row}").Value = label;
        if (isBold) ws.Cell($"A{row}").Style.Font.Bold = true;
        
        if (isSum)
        {
            ws.Cell($"C{row}").FormulaA1 = $"=SUM(C{startRow}:C{endRow})";
            ws.Cell($"D{row}").FormulaA1 = $"=SUM(D{startRow}:D{endRow})";
            ws.Cell($"E{row}").FormulaA1 = $"=SUM(E{startRow}:E{endRow})";
        }
        
        ws.Range($"C{row}:E{row}").Style.Font.Bold = isBold;
        row++;
    }

    #endregion
}

#region Upload Data Models

public class FinancialStatementUploadData
{
    public int Year { get; set; }
    public string YearType { get; set; } = "Audited";
    public BalanceSheetData BalanceSheet { get; set; } = new();
    public IncomeStatementData IncomeStatement { get; set; } = new();
    public CashFlowData? CashFlow { get; set; }
}

public class BalanceSheetData
{
    // Current Assets
    public decimal CashAndCashEquivalents { get; set; }
    public decimal TradeReceivables { get; set; }
    public decimal Inventory { get; set; }
    public decimal PrepaidExpenses { get; set; }
    public decimal OtherCurrentAssets { get; set; }
    public decimal TotalCurrentAssets => CashAndCashEquivalents + TradeReceivables + Inventory + PrepaidExpenses + OtherCurrentAssets;

    // Non-Current Assets
    public decimal PropertyPlantEquipment { get; set; }
    public decimal IntangibleAssets { get; set; }
    public decimal LongTermInvestments { get; set; }
    public decimal DeferredTaxAssets { get; set; }
    public decimal OtherNonCurrentAssets { get; set; }
    public decimal TotalNonCurrentAssets => PropertyPlantEquipment + IntangibleAssets + LongTermInvestments + DeferredTaxAssets + OtherNonCurrentAssets;

    // Current Liabilities
    public decimal TradePayables { get; set; }
    public decimal ShortTermBorrowings { get; set; }
    public decimal CurrentPortionLongTermDebt { get; set; }
    public decimal AccruedExpenses { get; set; }
    public decimal TaxPayable { get; set; }
    public decimal OtherCurrentLiabilities { get; set; }
    public decimal TotalCurrentLiabilities => TradePayables + ShortTermBorrowings + CurrentPortionLongTermDebt + AccruedExpenses + TaxPayable + OtherCurrentLiabilities;

    // Non-Current Liabilities
    public decimal LongTermDebt { get; set; }
    public decimal DeferredTaxLiabilities { get; set; }
    public decimal Provisions { get; set; }
    public decimal OtherNonCurrentLiabilities { get; set; }
    public decimal TotalNonCurrentLiabilities => LongTermDebt + DeferredTaxLiabilities + Provisions + OtherNonCurrentLiabilities;

    // Equity
    public decimal ShareCapital { get; set; }
    public decimal SharePremium { get; set; }
    public decimal RetainedEarnings { get; set; }
    public decimal OtherReserves { get; set; }
    public decimal TotalEquity => ShareCapital + SharePremium + RetainedEarnings + OtherReserves;
}

public class IncomeStatementData
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

public class CashFlowData
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
    public decimal OtherInvestingActivities { get; set; }
    
    public decimal ProceedsFromBorrowings { get; set; }
    public decimal RepaymentOfBorrowings { get; set; }
    public decimal InterestPaid { get; set; }
    public decimal DividendsPaid { get; set; }
    public decimal ProceedsFromShareIssue { get; set; }
    public decimal OtherFinancingActivities { get; set; }
    
    public decimal OpeningCashBalance { get; set; }
}

#endregion
