# CRMS Intranet UI - Gap Analysis

**Document Version:** 2.0  
**Date:** 2026-02-19  
**Status:** Priority 1 Issues RESOLVED | Priority 2-3 Pending

---

## Executive Summary

Following the 2026-02-18 gap analysis, significant progress has been made on Priority 1 issues. The core data entry workflow (Collateral, Guarantors, Documents, Financial Statements) is now functional. Workflow transitions work correctly. Financial statement validation now supports businesses of all ages.

---

## 1. Resolved Issues (Priority 1)

### 1.1 UI Buttons - NOW FUNCTIONAL ✅

| Location | Button | Backend Command | Status |
|----------|--------|-----------------|--------|
| `CollateralTab.razor` | "Add Collateral" | `AddCollateralCommand` | ✅ RESOLVED |
| `GuarantorsTab.razor` | "Add Guarantor" | `AddIndividualGuarantorCommand` | ✅ RESOLVED |
| `DocumentsTab.razor` | "Upload Document" | `UploadDocumentCommand` | ✅ RESOLVED |
| `DocumentsTab.razor` | View | `IFileStorageService` | ✅ RESOLVED |
| `DocumentsTab.razor` | Download | `IFileStorageService` | ✅ RESOLVED |
| `FinancialsTab.razor` | "Add Financial Year" | `CreateFinancialStatementCommand` | ✅ RESOLVED |
| `Detail.razor` | Approve/Return/Reject | `TransitionWorkflowCommand` | ✅ RESOLVED |

### 1.2 ApplicationService Methods - ADDED ✅

The following methods were added to `ApplicationService.cs`:

```csharp
// Collateral
- AddCollateralAsync(Guid applicationId, AddCollateralRequest request)
- SetCollateralValuationAsync(Guid collateralId, SetValuationRequest request)

// Guarantors
- AddGuarantorAsync(Guid applicationId, AddGuarantorRequest request)

// Documents
- UploadDocumentAsync(Guid applicationId, UploadDocumentRequest request)
- VerifyDocumentAsync(Guid documentId, Guid userId)

// Financial Statements
- CreateFinancialStatementAsync(Guid applicationId, CreateFinancialStatementRequest request)
- GetFinancialStatementsForApplicationAsync(Guid applicationId)

// Workflow
- TransitionWorkflowAsync(Guid workflowInstanceId, LoanApplicationStatus toStatus, WorkflowAction action, string? comments, Guid userId, string userRole)
- ApproveApplicationAsync(Guid applicationId, string comments, Guid userId, string userRole)
- ReturnApplicationAsync(Guid applicationId, string comments, Guid userId, string userRole)
- RejectApplicationAsync(Guid applicationId, string comments, Guid userId, string userRole)
```

### 1.3 Modal Components - CREATED ✅

| Component | Purpose | Features |
|-----------|---------|----------|
| `AddCollateralModal.razor` | Add collateral to application | Type, description, location, owner info |
| `AddGuarantorModal.razor` | Add guarantor to application | Personal info, BVN, guarantee type, net worth |
| `UploadDocumentModal.razor` | Upload documents | File picker, category selection |
| `FinancialStatementModal.razor` | 4-step financial data entry | Header → Balance Sheet → P&L → Cash Flow |

### 1.4 Financial Statement Validation - ENHANCED ✅

Business age-based validation now implemented:

| Business Age | Required Statements |
|-------------|---------------------|
| Startup (< 1 year) | 3 years Projected |
| 1 year | 1 Actual + 2 Projected |
| 2 years | 2 Actuals (1 Audited) + 1 Projected |
| 3+ years | 3 years Audited |

### 1.5 Document Viewer - IMPLEMENTED ✅

- PDF preview via iframe with `Content-Disposition: inline`
- Image preview (PNG, JPG, GIF)
- Download functionality with `Content-Disposition: attachment`
- API endpoints: `/api/documents/{id}/view` and `/api/documents/{id}/download`

---

## 2. Remaining Issues (Priority 2)

### 2.1 Collateral Management - Partial

| Feature | Status | Notes |
|---------|--------|-------|
| Add collateral | ✅ Done | Via modal |
| Set valuation | ❌ Pending | No UI for market/forced sale value |
| Approve collateral | ❌ Pending | No approval workflow UI |
| Upload collateral documents | ❌ Pending | No sub-document support |

### 2.2 Guarantor Management - Partial

| Feature | Status | Notes |
|---------|--------|-------|
| Add guarantor | ✅ Done | Via modal |
| Run credit check | ❌ Pending | Button exists, handler needed |
| View credit report | ❌ Pending | No detail modal |
| Approve/Reject | ❌ Pending | No workflow UI |

### 2.3 Document Management - Partial

| Feature | Status | Notes |
|---------|--------|-------|
| Upload | ✅ Done | Via modal |
| View | ✅ Done | PDF/image preview |
| Download | ✅ Done | File download |
| Verify | ❌ Pending | Checkbox exists, not wired |
| Reject | ❌ Pending | No command exists |

### 2.4 Credit Bureau Checks

| Feature | Status | Notes |
|---------|--------|-------|
| List bureau reports | ⚠️ Mock | Shows sample data |
| Request check | ❌ Pending | PartiesTab callback not implemented |
| View report detail | ❌ Pending | No modal |

### 2.5 Directors/Signatories Management

| Feature | Status | Notes |
|---------|--------|-------|
| List parties | ✅ Done | PartiesTab displays |
| Add director | ❌ Pending | No modal |
| Add signatory | ❌ Pending | No modal |
| Run bureau check | ❌ Pending | Button callback empty |

---

## 3. Remaining Issues (Priority 3)

### 3.1 Admin Pages

| Page | Route | Status | Issues |
|------|-------|--------|--------|
| Scoring Config | `/admin/scoring` | Display Only | No edit functionality |
| Templates | `/admin/templates` | Display Only | No CRUD operations |
| Users | `/admin/users` | Display Only | No create/edit/deactivate |
| Products | `/admin/products` | Partial | Create works, edit/delete missing |

### 3.2 Report Pages

| Page | Route | Status | Issues |
|------|-------|--------|--------|
| Performance | `/reports/performance` | Mock Data | Not connected to `ReportingService` |
| Committee | `/reports/committee` | Mock Data | Not connected to backend |
| Audit | `/reports/audit` | Partial | Connected but limited filtering |

---

## 4. Files Modified (2026-02-19)

### Components Created
- `Components/Pages/Applications/Modals/AddCollateralModal.razor`
- `Components/Pages/Applications/Modals/AddGuarantorModal.razor`
- `Components/Pages/Applications/Modals/UploadDocumentModal.razor`
- `Components/Pages/Applications/Modals/FinancialStatementModal.razor`
- `Components/Pages/Applications/Modals/_Imports.razor`

### Components Modified
- `Components/Pages/Applications/Detail.razor` - Added modal integration, workflow actions, financial validation
- `Components/Pages/Applications/Index.razor` - Fixed to show user's Draft applications
- `Components/Pages/Applications/New.razor` - Changed "Submit" to "Create Application" (Draft only)
- `Components/Pages/Applications/Tabs/CollateralTab.razor` - Added OnAddCollateral event
- `Components/Pages/Applications/Tabs/GuarantorsTab.razor` - Added OnAddGuarantor event
- `Components/Pages/Applications/Tabs/DocumentsTab.razor` - Added upload, view, download handlers
- `Components/Pages/Applications/Tabs/FinancialsTab.razor` - Complete redesign with business age validation

### Services Modified
- `Services/ApplicationService.cs` - Added 15+ methods for CRUD operations
- `Services/AuthService.cs` - Minor fixes

### Infrastructure Modified
- `Infrastructure/DependencyInjection.cs` - Registered 30+ Application handlers
- `Program.cs` - Added file serving endpoints

### Models Modified
- `Models/ApplicationModels.cs` - Added FinancialStatementInfo, request/response classes

---

## 5. Next Steps

### Priority 2 (Data Enrichment)
1. Collateral valuation form
2. Guarantor credit check integration
3. Document verification workflow
4. Directors/Signatories CRUD

### Priority 3 (Admin & Reports)
1. Scoring configuration editor
2. Template management CRUD
3. User management CRUD
4. Connect reports to ReportingService

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-18 | Initial gap analysis (78 issues identified) |
| 2.0 | 2026-02-19 | Priority 1 resolved: Data entry modals, workflow transitions, document viewer, financial statement validation |
