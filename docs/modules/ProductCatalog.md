# ProductCatalog Module

**Module ID:** 1  
**Status:** 🟢 Completed (Core Implementation)  
**Priority:** P1  
**Bounded Context:** Lending  
**Last Updated:** 2026-02-16

---

## 1. Purpose

Manage loan products, eligibility rules, pricing configurations, and required documentation definitions. This module serves as the foundation for both retail and corporate lending operations.

---

## 2. Implementation Summary

### Domain Layer (CRMS.Domain)

**Aggregate Root:** `LoanProduct`
- Location: `Aggregates/ProductCatalog/LoanProduct.cs`
- Factory method pattern with validation
- Domain events: `LoanProductCreatedEvent`, `LoanProductActivatedEvent`

**Entities:**
- `PricingTier` - Interest rates and fees by credit score bands
- `EligibilityRule` - Configurable rules with evaluation logic
- `DocumentRequirement` - Required documents with file validation

**Enums:** `Enums/LoanProductEnums.cs`
- `LoanProductType` (Retail, Corporate)
- `ProductStatus` (Draft, Active, Suspended, Discontinued)
- `EligibilityRuleType`, `ComparisonOperator`, `DocumentType`

### Application Layer (CRMS.Application)

**Commands:**
- `CreateLoanProductCommand` / `CreateLoanProductHandler`
- `UpdateLoanProductCommand` / `UpdateLoanProductHandler`
- `ActivateLoanProductCommand` / `ActivateLoanProductHandler`
- `AddPricingTierCommand` / `AddPricingTierHandler`

**Queries:**
- `GetLoanProductByIdQuery` / `GetLoanProductByCodeQuery`
- `GetAllLoanProductsQuery`
- `GetLoanProductsByTypeQuery`
- `GetActiveLoanProductsByTypeQuery`

**DTOs:** `ProductCatalog/DTOs/LoanProductDto.cs`
- `LoanProductDto`, `LoanProductSummaryDto`
- `PricingTierDto`, `EligibilityRuleDto`, `DocumentRequirementDto`

**Mappings:** `ProductCatalog/Mappings/LoanProductMappings.cs`

### Infrastructure Layer (CRMS.Infrastructure)

**DbContext:** `Persistence/CRMSDbContext.cs`

**EF Configurations:**
- `LoanProductConfiguration` - Owns Money value objects
- `PricingTierConfiguration`
- `EligibilityRuleConfiguration`
- `DocumentRequirementConfiguration`

**Repository:** `LoanProductRepository`

---

## 3. Database Schema

### LoanProducts Table
| Column | Type | Notes |
|--------|------|-------|
| Id | GUID | PK |
| Code | VARCHAR(50) | Unique |
| Name | VARCHAR(200) | |
| Description | VARCHAR(1000) | |
| Type | INT | Enum |
| Status | INT | Enum |
| MinAmount | DECIMAL(18,2) | |
| MinAmountCurrency | VARCHAR(3) | |
| MaxAmount | DECIMAL(18,2) | |
| MaxAmountCurrency | VARCHAR(3) | |
| MinTenorMonths | INT | |
| MaxTenorMonths | INT | |
| CreatedAt, CreatedBy, ModifiedAt, ModifiedBy | Audit fields |

### Related Tables
- PricingTiers (FK: LoanProductId)
- EligibilityRules (FK: LoanProductId)
- DocumentRequirements (FK: LoanProductId)

---

## 4. Key Design Decisions

1. **Aggregate Root Pattern** - LoanProduct controls all child entities
2. **Factory Method** - `LoanProduct.Create()` ensures valid state
3. **Result Pattern** - All operations return `Result<T>` for error handling
4. **Value Objects** - Money is owned entity in EF Core
5. **Auto-Include Navigation** - Related entities loaded automatically

---

## 5. Pending Enhancements

- [ ] Add remaining commands (RemovePricingTier, AddEligibilityRule, etc.)
- [ ] Add unit tests for domain logic
- [ ] Add API endpoints in Web layer
- [ ] Add seed data for sample products

---

## Document History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-16 | Initial implementation |
