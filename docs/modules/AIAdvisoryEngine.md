# AIAdvisoryEngine Module

## Overview

The AIAdvisoryEngine module generates AI-powered credit risk assessments for corporate loan applications. It aggregates data from CreditBureauIntegration, StatementAnalyzer, FinancialDocumentAnalyzer, CollateralManagement, and GuarantorManagement to produce a comprehensive risk score matrix with recommendations.

## Domain Model

### Aggregate: CreditAdvisory

```
CreditAdvisory (Aggregate Root)
└── RiskScore[] (Value Objects)
```

### Risk Categories

| Category | Weight | Description |
|----------|--------|-------------|
| CreditHistory | 25% | Bureau data from directors, signatories, guarantors |
| FinancialHealth | 25% | Balance sheet ratios, profitability metrics |
| CashflowStability | 15% | Cashflow volatility, consistency |
| DebtServiceCapacity | 20% | DSCR, interest coverage |
| CollateralCoverage | 15% | LTV, lien status, asset quality |
| ManagementRisk | Optional | Director experience, track record |
| IndustryRisk | Optional | Sector-specific risk factors |
| ConcentrationRisk | Optional | Exposure concentration |

### Risk Ratings

| Score Range | Rating | Interpretation |
|-------------|--------|----------------|
| 80-100 | VeryLow | Excellent credit quality |
| 65-79 | Low | Good credit quality |
| 50-64 | Medium | Acceptable with conditions |
| 35-49 | High | Needs additional mitigation |
| 0-34 | VeryHigh | Not recommended |

### Recommendations

| Overall Score | Red Flags | Recommendation |
|---------------|-----------|----------------|
| >= 75 | 0 | StrongApprove |
| >= 65 | <= 1 | Approve |
| >= 50 | Any | ApproveWithConditions |
| 35-49 | Any | Refer |
| < 35 | Any | Decline |

### Advisory Status

```
Pending → Processing → Completed
                    → Failed
```

## Scoring Logic

### Credit History Score

Based on bureau reports from all related parties:

```
Base Score: 70

+ 20 if avg credit score >= 700
+ 10 if avg credit score >= 650
- 20 if avg credit score < 600
- 30 for any defaults
- 15 for delinquent loans
+ bonus for multiple performing loans
```

### Financial Health Score

Based on latest audited financial statements:

```
Base Score: 60

+ 10 if current ratio >= 2.0
- 15 if current ratio < 1.0
+ 10 if D/E <= 1.0
- 20 if D/E > 3.0
+ 15 if net profit margin >= 10%
- 25 if loss-making
+ 10 if ROE >= 15%
```

### DSCR Score

Based on debt service coverage ratio:

```
DSCR >= 2.0  → Score: 90
DSCR >= 1.5  → Score: 75
DSCR >= 1.25 → Score: 60
DSCR >= 1.0  → Score: 45
DSCR < 1.0   → Score: 25

+ 5 if interest coverage >= 5.0
- 10 if interest coverage < 2.0
```

### Collateral Score

Based on loan-to-value ratio:

```
LTV <= 50%  → Score: 90
LTV <= 70%  → Score: 75
LTV <= 100% → Score: 55
LTV > 100%  → Score: 35

+ 5 if all liens perfected
- 10 if liens pending
```

## Loan Recommendations

### Amount Recommendation

| Score | Multiplier | Example (₦100M requested) |
|-------|------------|---------------------------|
| >= 80 | 100% | ₦100M approved |
| >= 70 | 90% | ₦90M approved |
| >= 60 | 75% | ₦75M approved |
| >= 50 | 60% | ₦60M approved |
| < 50 | 50% | ₦50M approved |

### Interest Rate Recommendation

| Score | Rate Adjustment | Example (18% base) |
|-------|-----------------|-------------------|
| >= 80 | -2.0% | 16.0% |
| >= 70 | -1.0% | 17.0% |
| >= 60 | +0.0% | 18.0% |
| >= 50 | +2.0% | 20.0% |
| < 50 | +4.0% | 22.0% |

### Tenor Recommendation

- Score >= 70: Full requested tenor
- Score < 70: Maximum 36 months

## Conditions and Covenants

### Auto-Generated Conditions

**For Score < 70:**
- Quarterly financial statements submission
- Maintain minimum current ratio of 1.2x

**For Score < 60:**
- Monthly bank statement submission for first 12 months
- Personal guarantee from principal shareholders
- Maintain DSCR above 1.25x

**For Collateral Issues:**
- Additional collateral to achieve 70% LTV

**For Credit Issues:**
- Clear all outstanding delinquent facilities

### Standard Covenants

- No additional borrowing without bank consent
- Maintain insurance coverage on pledged assets

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/v1/advisory/generate | Generate new credit advisory |
| GET | /api/v1/advisory/{id} | Get advisory by ID |
| GET | /api/v1/advisory/by-loan-application/{id}/latest | Get latest completed advisory |
| GET | /api/v1/advisory/by-loan-application/{id}/history | Get all advisories for loan |
| GET | /api/v1/advisory/{id}/score-matrix | Get score matrix breakdown |

## Data Inputs

### Required Data

1. **Loan Application** - Requested amount, tenor, product
2. **Financial Statements** - At least one verified statement with ratios

### Optional Data (improves accuracy)

3. **Bureau Reports** - Credit history of related parties
4. **Collateral** - Pledged assets and valuations
5. **Guarantors** - Guarantee coverage and guarantor creditworthiness
6. **Cashflow Analysis** - Bank statement analysis results

## Output Structure

### Credit Advisory DTO

```json
{
  "id": "guid",
  "loanApplicationId": "guid",
  "status": "Completed",
  "overallScore": 72.5,
  "overallRating": "Low",
  "recommendation": "Approve",
  "riskScores": [
    {
      "category": "CreditHistory",
      "score": 75,
      "weight": 0.25,
      "weightedScore": 18.75,
      "rating": "Low",
      "rationale": "Credit history assessment...",
      "redFlags": [],
      "positiveIndicators": ["Strong credit score"]
    }
  ],
  "recommendedAmount": 90000000,
  "recommendedTenorMonths": 48,
  "recommendedInterestRate": 17.0,
  "conditions": ["Quarterly financial statements"],
  "covenants": ["Maintain insurance coverage"],
  "executiveSummary": "...",
  "strengthsAnalysis": "...",
  "weaknessesAnalysis": "...",
  "redFlags": [],
  "modelVersion": "mock-v1.0",
  "generatedAt": "2026-02-17T10:00:00Z"
}
```

## Integration Points

- **CreditBureauIntegration**: Bureau reports for credit history scoring
- **FinancialDocumentAnalyzer**: Financial ratios for health/DSCR scoring
- **StatementAnalyzer**: Cashflow metrics for stability scoring
- **CollateralManagement**: Collateral values for LTV calculation
- **GuarantorManagement**: Guarantee coverage assessment
- **WorkflowEngine**: Advisory completion triggers workflow progression
- **LoanPackGenerator**: Advisory included in loan pack PDF

## AI Service Interface

```csharp
public interface IAIAdvisoryService
{
    Task<AIAdvisoryResponse> GenerateAdvisoryAsync(
        AIAdvisoryRequest request, 
        CancellationToken ct);
    
    string GetModelVersion();
}
```

### Implementations

1. **MockAIAdvisoryService** (Current) - Rule-based scoring for development
2. **OpenAIAdvisoryService** (Future) - LLM-based analysis with GPT-4
3. **AzureOpenAIAdvisoryService** (Future) - Azure-hosted LLM

## Files

### Domain
- `Aggregates/Advisory/CreditAdvisory.cs` - Aggregate root
- `Aggregates/Advisory/RiskScore.cs` - Risk score value object
- `Enums/AIAdvisoryEnums.cs` - Risk ratings, recommendations, categories
- `Interfaces/ICreditAdvisoryRepository.cs`

### Application
- `Advisory/Commands/GenerateCreditAdvisoryCommand.cs` - Main generation logic
- `Advisory/Queries/GetCreditAdvisoryQueries.cs` - Retrieval queries
- `Advisory/DTOs/CreditAdvisoryDtos.cs` - All DTOs
- `Advisory/Interfaces/IAIAdvisoryService.cs` - AI service contract

### Infrastructure
- `ExternalServices/AIServices/MockAIAdvisoryService.cs` - Mock implementation
- `Persistence/Configurations/Advisory/CreditAdvisoryConfiguration.cs`
- `Persistence/Repositories/CreditAdvisoryRepository.cs`

### API
- `Controllers/AdvisoryController.cs`

## Future Enhancements

1. **LLM Integration**: Replace mock with actual OpenAI/Azure OpenAI
2. **Industry Benchmarking**: Compare against industry averages
3. **Trend Analysis**: Multi-year trend consideration
4. **Scenario Analysis**: Stress testing under different conditions
5. **Explanation Generation**: Natural language explanations for each score
6. **Confidence Intervals**: Provide score confidence ranges
