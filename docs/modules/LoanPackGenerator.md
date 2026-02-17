# LoanPackGenerator Module

## Overview

The LoanPackGenerator module generates comprehensive PDF loan packs for corporate loan applications. These loan packs are used for committee review and contain all relevant information about the loan application, borrower, credit assessment, and recommendations.

## Key Features

- **Comprehensive PDF Generation**: Complete loan pack with all assessment sections
- **Version Control**: Track multiple versions of loan packs per application
- **Content Hashing**: SHA256 hash for document integrity verification
- **Modular Sections**: Include/exclude sections based on available data
- **Professional Layout**: Clean, professional formatting suitable for committee review

## Technology

- **QuestPDF**: Modern fluent PDF generation library for .NET
- **License**: Community license (free for businesses with < $1M annual revenue)

## Domain Model

### LoanPack Entity

| Property | Description |
|----------|-------------|
| LoanApplicationId | Associated loan application |
| ApplicationNumber | Application reference number |
| Version | Version number (auto-incremented) |
| Status | Generating, Generated, Failed, Archived |
| GeneratedAt | Generation timestamp |
| GeneratedByUserId | User who generated the pack |
| FileName | Generated PDF file name |
| StoragePath | Path in file storage |
| FileSizeBytes | PDF file size |
| ContentHash | SHA256 hash of PDF content |

### Content Summary Fields

| Property | Description |
|----------|-------------|
| CustomerName | Borrower name |
| ProductName | Loan product |
| RequestedAmount | Amount requested |
| RecommendedAmount | AI recommended amount |
| OverallRiskScore | AI risk score |
| RiskRating | Risk rating (Low, Moderate, High, etc.) |
| DirectorCount | Number of directors |
| BureauReportCount | Number of bureau reports |
| CollateralCount | Number of collaterals |
| GuarantorCount | Number of guarantors |

### Section Flags

Tracks which sections are included:
- IncludesExecutiveSummary
- IncludesBureauReports
- IncludesFinancialAnalysis
- IncludesCashflowAnalysis
- IncludesCollateralDetails
- IncludesGuarantorDetails
- IncludesAIAdvisory
- IncludesWorkflowHistory
- IncludesCommitteeComments

## PDF Sections

### 1. Executive Summary
- Application overview (customer, product, amount, tenor)
- Key metrics boxes (Risk Score, Collateral Coverage, Bureau Score, Recommendation)
- Red flags (if any)
- Mitigating factors (if any)

### 2. Customer Profile
- Company details (name, registration, incorporation date)
- Industry and sector
- Contact information
- Account details

### 3. Directors & Signatories
- Director list with shareholding percentages
- Credit scores and delinquency flags
- Signatory list with roles

### 4. Credit Bureau Reports
- Individual reports for each subject (directors, signatories)
- Credit scores and ratings
- Active loans and delinquencies
- Legal issues flagged

### 5. Financial Analysis
- Financial statements summary (3-year trend)
- Key financial ratios:
  - Liquidity (Current, Quick, Cash)
  - Leverage (Debt/Equity, Interest Coverage)
  - Profitability (Net Margin, ROE, ROA)
  - Coverage (DSCR)

### 6. Cashflow Analysis
- Monthly averages (inflows, outflows, net)
- Balance analysis (average, lowest, highest)
- Quality metrics (volatility, returned cheques)
- Trust assessment score

### 7. Collateral
- Collateral list with valuations
- Market value, FSV, acceptable value
- Total collateral coverage ratio
- Lien and insurance status

### 8. Guarantors
- Guarantor list with net worth
- Guarantee amounts
- Credit scores and status
- Total guarantee amount

### 9. AI Advisory Assessment
- Overall risk score (large, colored)
- Component scores (8 categories)
- Recommendations (amount, tenor, pricing, structuring)
- Recommended conditions

### 10. Workflow History
- Chronological list of status changes
- Action, performer, timestamp, comments

### 11. Committee Comments
- Comments from committee members
- Votes (if recorded)
- Visibility level

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/LoanPack/generate/{loanApplicationId} | Generate new loan pack |
| GET | /api/LoanPack/{id} | Get loan pack metadata |
| GET | /api/LoanPack/latest/{loanApplicationId} | Get latest pack for application |
| GET | /api/LoanPack/versions/{loanApplicationId} | Get all versions |
| GET | /api/LoanPack/download/{id} | Download PDF (TODO: file storage) |

## Usage

### Generate a Loan Pack

```http
POST /api/LoanPack/generate/550e8400-e29b-41d4-a716-446655440000
Authorization: Bearer {token}
```

Response:
```json
{
  "loanPackId": "123e4567-e89b-12d3-a456-426614174000",
  "applicationNumber": "LA20260217ABC123",
  "version": 1,
  "fileName": "LoanPack_LA20260217ABC123_v1_20260217_143022.pdf",
  "fileSizeBytes": 245678,
  "status": "Generated"
}
```

### Get Latest Pack

```http
GET /api/LoanPack/latest/550e8400-e29b-41d4-a716-446655440000
```

## Domain Events

| Event | When Published |
|-------|----------------|
| `LoanPackGenerationStartedEvent` | Generation begins |
| `LoanPackGeneratedEvent` | Generation completes successfully |
| `LoanPackGenerationFailedEvent` | Generation fails |

## File Storage

**Current Implementation**: PDF bytes are generated but file storage is not yet implemented.

**TODO**: Implement file storage using:
- AWS S3
- Azure Blob Storage
- MinIO (for development)

The `StoragePath` property stores the intended path for when file storage is implemented.

## Files

### Domain
- `Aggregates/LoanPack/LoanPack.cs` - Aggregate root with versioning
- `Enums/LoanPackEnums.cs` - Status and section enums
- `Interfaces/ILoanPackRepository.cs` - Repository interface

### Application
- `LoanPack/Commands/GenerateLoanPackCommand.cs` - Generate command with data aggregation
- `LoanPack/Queries/LoanPackQueries.cs` - Query handlers
- `LoanPack/DTOs/LoanPackData.cs` - Comprehensive data model for PDF generation
- `LoanPack/Interfaces/ILoanPackGenerator.cs` - PDF generator interface

### Infrastructure
- `Documents/LoanPackPdfGenerator.cs` - QuestPDF implementation (~800 lines)
- `Persistence/Configurations/LoanPack/LoanPackConfiguration.cs` - EF configuration
- `Persistence/Repositories/LoanPackRepository.cs` - Repository implementation

### API
- `Controllers/LoanPackController.cs` - REST endpoints

## Design Decisions

1. **QuestPDF over iText7**: Free community license, modern fluent API, excellent documentation
2. **Version Control**: Each generation creates a new version, previous versions archived
3. **Content Hash**: SHA256 ensures document integrity
4. **Section Flags**: Track what's included for quick reference without loading PDF
5. **Separate Data Model**: `LoanPackData` DTO decouples domain from PDF generation
6. **Parallel Data Loading**: All repositories queried in parallel for performance

## Future Enhancements

1. **File Storage Integration**: AWS S3 / Azure Blob Storage
2. **PDF Preview**: Generate thumbnail/preview images
3. **Digital Signatures**: Sign PDFs for authenticity
4. **Watermarks**: Draft/Final watermarks
5. **Customizable Templates**: Allow template customization per loan type
6. **Export Formats**: Word/Excel export options
