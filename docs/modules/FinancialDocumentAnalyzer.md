# FinancialDocumentAnalyzer Module

## Overview

The FinancialDocumentAnalyzer module captures, validates, and analyzes corporate financial statements (Balance Sheet, Income Statement, Cash Flow Statement) attached to loan applications. It automatically calculates 20+ financial ratios and provides qualitative assessments for credit risk evaluation.

## Domain Model

### Aggregate: FinancialStatement

```
FinancialStatement (Aggregate Root)
├── BalanceSheet (Entity)
├── IncomeStatement (Entity)
├── CashFlowStatement (Entity)
└── FinancialRatios (Value Object - Calculated)
```

### Financial Year Types

| Type | Description |
|------|-------------|
| Audited | Externally audited financial statements |
| Reviewed | Reviewed by external accountant |
| ManagementAccounts | Internally prepared management accounts |
| Interim | Mid-year or quarterly statements |
| Projected | Future projections/forecasts |

### Input Methods

| Method | Description |
|--------|-------------|
| ManualEntry | Step-by-step form entry |
| ExcelUpload | Upload from Excel template |
| PdfExtraction | PDF parsing (future) |
| ApiImport | Integration with accounting systems |

### Status Workflow

```
Draft → PendingReview → Verified
                     → Rejected → Draft (revert)
```

## Balance Sheet Structure

### Assets
- **Current Assets**: Cash, Trade Receivables, Inventory, Prepaid Expenses, Other
- **Non-Current Assets**: PPE, Intangibles, Long-term Investments, Deferred Tax, Other

### Liabilities
- **Current Liabilities**: Trade Payables, Short-term Borrowings, Current Portion LTD, Accrued Expenses, Tax Payable, Other
- **Non-Current Liabilities**: Long-term Debt, Deferred Tax, Provisions, Other

### Equity
- Share Capital, Share Premium, Retained Earnings, Other Reserves

### Validation
- **Balance Check**: Total Assets must equal Total Liabilities + Total Equity (±1 NGN tolerance)

## Income Statement Structure

| Line Item | Calculation |
|-----------|-------------|
| Total Revenue | Revenue + Other Operating Income |
| Gross Profit | Total Revenue - Cost of Sales |
| Operating Profit (EBIT) | Gross Profit - Operating Expenses |
| EBITDA | EBIT + Depreciation & Amortization |
| Profit Before Tax | EBIT - Net Finance Costs |
| Net Profit | PBT - Income Tax |

## Cash Flow Statement Structure

### Operating Activities
- Profit Before Tax + Non-cash adjustments + Working Capital changes - Tax Paid

### Investing Activities
- PPE purchases/sales, Investment purchases/sales, Interest/Dividends received

### Financing Activities
- Borrowings proceeds/repayments, Interest paid, Dividends paid, Share issues

### Key Metrics
- **Free Cash Flow**: Net Cash from Operations - PPE Purchases
- **FCFF**: Net Cash from Operations + Interest Paid - PPE Purchases

## Calculated Financial Ratios

### Liquidity Ratios
| Ratio | Formula | Assessment Thresholds |
|-------|---------|----------------------|
| Current Ratio | Current Assets / Current Liabilities | ≥2.0 Excellent, ≥1.5 Good, ≥1.0 Adequate |
| Quick Ratio | (Current Assets - Inventory) / Current Liabilities | ≥1.5 Excellent, ≥1.0 Good, ≥0.8 Adequate |
| Cash Ratio | Cash / Current Liabilities | Higher is better |

### Leverage Ratios
| Ratio | Formula | Assessment Thresholds |
|-------|---------|----------------------|
| Debt-to-Equity | Total Debt / Total Equity | ≤0.5 Excellent, ≤1.0 Good, ≤2.0 Adequate |
| Debt-to-Assets | Total Debt / Total Assets | Lower is better |
| Interest Coverage | EBIT / Interest Expense | ≥5.0 Excellent, ≥3.0 Good, ≥2.0 Adequate |
| DSCR | EBITDA / (Interest + Principal) | ≥1.25 minimum for loan approval |

### Profitability Ratios
| Ratio | Formula | Assessment Thresholds |
|-------|---------|----------------------|
| Gross Margin % | Gross Profit / Revenue × 100 | Industry dependent |
| Operating Margin % | Operating Profit / Revenue × 100 | Higher is better |
| Net Profit Margin % | Net Profit / Revenue × 100 | ≥15% Excellent, ≥10% Good, ≥5% Adequate |
| EBITDA Margin % | EBITDA / Revenue × 100 | Higher is better |
| ROA | Net Profit / Total Assets × 100 | Higher is better |
| ROE | Net Profit / Total Equity × 100 | ≥20% Excellent, ≥15% Good, ≥10% Adequate |

### Efficiency Ratios
| Ratio | Formula | Interpretation |
|-------|---------|----------------|
| Asset Turnover | Revenue / Total Assets | Higher = more efficient |
| Inventory Turnover | Cost of Sales / Inventory | Higher = faster inventory movement |
| Receivables Days | (Receivables × 365) / Revenue | Lower = faster collection |
| Payables Days | (Payables × 365) / Cost of Sales | Balance needed |
| Cash Conversion Cycle | Receivables Days + Inventory Days - Payables Days | Lower is better |

## Overall Assessment Logic

```
Liquidity + Leverage + Profitability → Overall Assessment

Excellent (2+) → "Strong"
Critical (2+) → "High Risk"
Good (2+) or Excellent (1+) → "Acceptable"
Otherwise → "Needs Review"
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/v1/financial-statements | Create new statement |
| GET | /api/v1/financial-statements/{id} | Get by ID with all details |
| GET | /api/v1/financial-statements/by-loan-application/{id} | List for loan application |
| GET | /api/v1/financial-statements/by-loan-application/{id}/trend | Multi-year trend analysis |
| PUT | /api/v1/financial-statements/{id}/balance-sheet | Set balance sheet data |
| PUT | /api/v1/financial-statements/{id}/income-statement | Set income statement data |
| PUT | /api/v1/financial-statements/{id}/cash-flow-statement | Set cash flow data |
| POST | /api/v1/financial-statements/{id}/submit | Submit for review |
| POST | /api/v1/financial-statements/{id}/verify | Verify (approve) statement |

## Business Age-Based Validation

Financial statement requirements vary based on how long the business has been operating (calculated from `IncorporationDate`):

| Business Age | Required Financial Statements |
|-------------|-------------------------------|
| **Startup (< 1 year)** | 3 years **Projected** + Business Plan |
| **New Business (1 year)** | 1 year **Actual** (Audited or Management) + 2 years **Projected** |
| **Growing Business (2 years)** | 2 years **Actual** (at least 1 Audited) + 1 year **Projected** |
| **Established (3+ years)** | 3 years **Audited** |

### Validation Logic

The system automatically:
1. Calculates business age from `IncorporationDate`
2. Validates submitted financial statements match requirements
3. Shows dynamic requirements info in the UI
4. Prevents submission for review if requirements not met

### UI Implementation

The `FinancialsTab.razor` component:
- Displays requirements info box with business age badge
- Shows validation status (success/warning)
- Lists entered financial statements with type indicators
- Provides trend analysis for 2+ years of data

## Data Entry Workflow

```
1. Create FinancialStatement (year, type, auditor info)
          ↓
2. Set Balance Sheet (24 line items)
          ↓
3. Set Income Statement (12 line items)
          ↓
4. Set Cash Flow Statement (optional, 20 line items)
          ↓
   [Ratios auto-calculated after BS + IS]
          ↓
5. Submit for Review
          ↓
6. Verify or Reject
```

## Trend Analysis

When multiple years are available, the system analyzes:

| Metric | Trend Categories |
|--------|-----------------|
| Revenue | Strong Growth (>20%), Growing (>5%), Stable, Declining, Significant Decline |
| Profitability | Same as above |
| Liquidity | Same as above |
| Leverage | Improving Significantly, Improving, Stable, Deteriorating, Deteriorating Significantly |
| **Overall** | Positive, Moderately Positive, Mixed, Moderately Negative, Negative |

## Integration Points

- **AIAdvisoryEngine**: Consumes ratios and trends for risk scoring
- **CorporateLoanInitiation**: Financial statements required for credit analysis
- **WorkflowEngine**: Statement verification triggers workflow progression

## Files

### Domain
- `Aggregates/FinancialStatement/FinancialStatement.cs` - Aggregate root
- `Aggregates/FinancialStatement/BalanceSheet.cs` - Balance sheet entity
- `Aggregates/FinancialStatement/IncomeStatement.cs` - P&L entity
- `Aggregates/FinancialStatement/CashFlowStatement.cs` - Cash flow entity
- `Aggregates/FinancialStatement/FinancialRatios.cs` - Calculated ratios value object
- `Enums/FinancialStatementEnums.cs` - Year types, status, input methods
- `Interfaces/IFinancialStatementRepository.cs`

### Application
- `FinancialAnalysis/Commands/FinancialStatementCommands.cs` - All commands and handlers
- `FinancialAnalysis/Queries/FinancialStatementQueries.cs` - Queries including trend analysis
- `FinancialAnalysis/DTOs/FinancialStatementDtos.cs` - All DTOs

### Infrastructure
- `Persistence/Configurations/FinancialStatement/FinancialStatementConfiguration.cs`
- `Persistence/Repositories/FinancialStatementRepository.cs`

### API
- `Controllers/FinancialStatementsController.cs`
