# Intranet UI Requirements - Corporate Loan Portal

## Overview

Blazor Server application for bank staff to manage corporate loan applications. Role-based access controls all functionality.

---

## User Roles & Access Matrix

| Role | Dashboard | Initiate Loan | View Loans | Branch Approve | HO Review | Committee Vote | Final Approve | Generate Pack | Reports | Audit | Admin |
|------|-----------|---------------|------------|----------------|-----------|----------------|---------------|---------------|---------|-------|-------|
| SystemAdmin | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| LoanOfficer | ✅ | ✅ | Own | ❌ | ❌ | ❌ | ❌ | ❌ | Limited | ❌ | ❌ |
| BranchApprover | ✅ | ❌ | Branch | ✅ | ❌ | ❌ | ❌ | ❌ | Limited | ❌ | ❌ |
| CreditOfficer | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ | ❌ | ❌ |
| HOReviewer | ✅ | ❌ | ✅ | ❌ | ✅ | ❌ | ❌ | ✅ | ✅ | ❌ | ❌ |
| CommitteeMember | ✅ | ❌ | Assigned | ❌ | ❌ | ✅ | ❌ | ✅ | Limited | ❌ | ❌ |
| FinalApprover | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ | ✅ | ✅ | ❌ | ❌ |
| RiskManager | ✅ | ❌ | ✅ | ❌ | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ | ❌ |
| Operations | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | Limited | ❌ | ❌ |
| Auditor | ✅ | ❌ | ✅ (RO) | ❌ | ❌ | ❌ | ❌ | ✅ (RO) | ✅ | ✅ | ❌ |

---

## Page Structure

### 1. Authentication Pages

#### 1.1 Login (`/login`)
- Email/username and password fields
- "Remember me" checkbox
- "Forgot password" link
- Error messages for invalid credentials
- Redirect to dashboard on success

#### 1.2 Forgot Password (`/forgot-password`)
- Email input
- Send reset link

#### 1.3 Reset Password (`/reset-password`)
- New password + confirmation
- Password strength indicator

---

### 2. Dashboard (`/`)

#### 2.1 Role-Based Widgets

**All Roles:**
- My pending tasks count
- Recent activity feed
- Quick action buttons (based on role)

**LoanOfficer:**
- My applications summary (Draft, Submitted, In Progress)
- Quick "New Application" button

**BranchApprover:**
- Pending branch approvals count
- Applications awaiting my action

**HOReviewer / CommitteeMember:**
- Pending reviews count
- Upcoming committee deadlines

**RiskManager / CreditOfficer:**
- Portfolio risk overview
- SLA breach alerts
- Overdue applications

**SystemAdmin:**
- System health metrics
- User activity summary
- Recent audit events

#### 2.2 Charts (based on permissions)
- Loan funnel (Submitted → Approved → Disbursed)
- Applications by status
- Monthly trend
- SLA compliance gauge

---

### 3. Loan Applications

#### 3.1 Application List (`/applications`)
- Filterable data grid
- Filters: Status, Date Range, Product, Amount Range, Assigned To
- Search by Application Number, Customer Name, Account Number
- Columns: App #, Customer, Product, Amount, Status, Created, Assigned To, Actions
- Row click → Application Detail
- Role-based filtering (LoanOfficer sees own, BranchApprover sees branch, etc.)

#### 3.2 New Application (`/applications/new`)
**Access:** LoanOfficer, SystemAdmin

**Step 1: Customer Selection**
- Enter corporate account number
- "Fetch from Core Banking" button
- Display: Company Name, Registration #, Account Balance
- Validate account exists and is corporate

**Step 2: Loan Details**
- Select Loan Product (dropdown from ProductCatalog)
- Requested Amount (with product min/max validation)
- Requested Tenor (months)
- Interest Rate (auto-filled from product, editable)
- Interest Rate Type (Per Annum / Flat / Reducing)
- Purpose (text area)

**Step 3: Review & Submit**
- Summary of entered data
- Terms acceptance checkbox
- "Save as Draft" or "Submit" buttons

#### 3.3 Application Detail (`/applications/{id}`)

**Header Section:**
- Application Number, Status Badge, Customer Name
- Product, Amount Requested, Tenor
- Created Date, Last Updated
- Action buttons (based on status and role)

**Tabbed Content:**

**Tab: Overview**
- Customer profile summary
- Loan details
- Current workflow stage visualization
- Timeline of status changes

**Tab: Directors & Signatories**
- List of directors with: Name, BVN (masked), Position, Shareholding %
- List of signatories with: Name, BVN (masked), Mandate Type
- Credit check status indicator per person
- "Request Credit Check" button (if not done)
- View bureau report modal

**Tab: Documents**
- Document upload grid
- Categories: Bank Statements, Audited Financials, Registration Docs, etc.
- Upload button, download button, status (Pending/Verified/Rejected)
- Document verification actions (for CreditOfficer+)

**Tab: Financial Analysis**
- Balance Sheet summary (if uploaded)
- Income Statement summary
- Cash Flow summary
- Calculated ratios with trend indicators
- Add/Edit financial statement button

**Tab: Bank Statements**
- List of uploaded statements (Internal/External)
- Cashflow analysis summary
- Transaction categorization breakdown
- Charts: Monthly inflows/outflows, balance trend

**Tab: Credit Bureau**
- Bureau reports for each director/signatory
- Credit score, rating, delinquency count
- Active loans summary
- Legal issues flagged
- Request new check button

**Tab: Collateral**
- List of pledged collaterals
- Type, Description, Market Value, FSV, LTV
- Valuation history
- Add/Edit collateral button
- Upload collateral documents

**Tab: Guarantors**
- List of guarantors
- Personal details, guarantee amount, status
- Credit check status
- Add/Edit guarantor button

**Tab: AI Advisory**
- Overall risk score (large, colored)
- Score breakdown by category (radar chart)
- Recommendations section
- Red flags and mitigating factors
- "Generate Advisory" button (if not done)

**Tab: Workflow History**
- Timeline of all transitions
- Who, When, Action, Comments
- SLA status per stage

**Tab: Committee**
- Committee review status
- Member list with vote status
- Comments thread
- Voting interface (for CommitteeMember)
- Decision recording (for Chairperson)

**Tab: Loan Pack**
- List of generated versions
- Generate new pack button
- Download PDF button
- Version comparison

#### 3.4 Application Actions (Context-Sensitive)

**Status: Draft**
- Edit, Submit, Delete (LoanOfficer)

**Status: Submitted / BranchReview**
- Approve, Return, Reject (BranchApprover)
- Add Comment (all with view access)

**Status: BranchApproved**
- System auto-triggers credit checks
- View progress

**Status: CreditAnalysis**
- View bureau reports
- Generate AI Advisory (CreditOfficer)
- Proceed to HO Review (CreditOfficer)

**Status: HOReview**
- Approve, Return, Reject (HOReviewer)
- Send to Committee (HOReviewer)

**Status: CommitteeCirculation**
- Cast Vote (CommitteeMember)
- Add Comment (CommitteeMember)
- Record Decision (Chairperson/CreditOfficer)

**Status: Approved**
- Generate Loan Pack
- Mark as Disbursed (Operations) - after manual core banking booking

---

### 4. Workflow Queues (`/queues`)

#### 4.1 My Queue (`/queues/my`)
- Applications assigned to current user
- Grouped by status
- Quick action buttons

#### 4.2 Role Queues (`/queues/{role}`)
- Applications in queue for a specific role
- Claim/Assign functionality
- SLA countdown display

#### 4.3 Overdue Queue (`/queues/overdue`)
**Access:** RiskManager, SystemAdmin
- Applications that breached SLA
- Escalation indicators
- Reassign capability

---

### 5. Committee Module (`/committee`)

#### 5.1 My Pending Votes (`/committee/my-votes`)
**Access:** CommitteeMember
- Reviews awaiting my vote
- Quick vote buttons
- Deadline countdown

#### 5.2 Committee Reviews (`/committee/reviews`)
**Access:** CreditOfficer, RiskManager, SystemAdmin
- List of all committee reviews
- Filter by status, committee type
- Create new review button

#### 5.3 Review Detail (`/committee/reviews/{id}`)
- Application summary
- Member list with vote status
- Voting interface
- Comments thread
- Document attachments
- Decision recording

---

### 6. Reports (`/reports`)

#### 6.1 Dashboard (`/reports/dashboard`)
- Embedded dashboard summary
- Date range picker
- Export options

#### 6.2 Loan Funnel (`/reports/funnel`)
- Funnel visualization
- Stage-by-stage breakdown
- Daily trend chart

#### 6.3 Portfolio (`/reports/portfolio`)
- Portfolio by product
- Portfolio by risk rating
- Aging analysis

#### 6.4 Performance (`/reports/performance`)
**Access:** Manager, RiskManager, SystemAdmin
- Processing times by stage
- User performance table
- SLA compliance

#### 6.5 Committee (`/reports/committee`)
**Access:** Manager, RiskManager, SystemAdmin
- Committee activity
- Member participation
- Average review time

#### 6.6 Audit Trail (`/reports/audit`)
**Access:** Auditor, RiskManager, SystemAdmin
- Searchable audit log
- Filter by action, user, entity, date
- Data access log (sensitive data views)

---

### 7. Administration (`/admin`)
**Access:** SystemAdmin

#### 7.1 Users (`/admin/users`)
- User list with search
- Create/Edit user modal
- Assign roles
- Activate/Deactivate
- Reset password

#### 7.2 Roles (`/admin/roles`)
- Role list
- View permissions per role
- (Roles are predefined, not editable in MVP)

#### 7.3 Loan Products (`/admin/products`)
- Product list
- Create/Edit product
- Pricing tiers
- Eligibility rules
- Document requirements
- Activate/Deactivate

#### 7.4 Workflow Configuration (`/admin/workflow`)
- View workflow stages
- SLA settings per stage
- (Workflow structure predefined, SLAs editable)

#### 7.5 Scoring Parameters (`/admin/scoring`)
- List of scoring parameters
- Request change (maker)
- Approve/Reject changes (checker)
- Change history

#### 7.6 Notification Templates (`/admin/notifications`)
- Template list
- Create/Edit templates
- Variable substitution reference

---

### 8. User Profile (`/profile`)

#### 8.1 My Profile
- View/Edit personal details
- Change password
- Notification preferences

#### 8.2 My Activity
- Recent actions
- Login history

---

## Shared Components

### Navigation
- Sidebar with role-based menu items
- Top bar with user info, notifications bell, logout
- Breadcrumb navigation

### Data Grid
- Sortable columns
- Pagination
- Column visibility toggle
- Export to Excel

### Modals
- Confirmation dialogs
- Form modals (Add Comment, Cast Vote, etc.)
- Document viewer

### Notifications
- Toast notifications for actions
- Real-time updates (SignalR) for status changes
- Notification dropdown in header

### Status Badges
- Color-coded status indicators
- Consistent across all views

---

## API Integration

The frontend will call the CRMS.API endpoints:

| Feature | API Controller |
|---------|---------------|
| Authentication | AuthController |
| Users | UsersController |
| Loan Products | LoanProductsController |
| Loan Applications | LoanApplicationsController |
| Workflow | WorkflowController |
| Committee | CommitteeController |
| Credit Bureau | CreditBureauController |
| Financial Statements | FinancialStatementsController |
| Statement Analysis | StatementAnalysisController |
| Collateral | CollateralController |
| Guarantors | GuarantorsController |
| AI Advisory | AdvisoryController |
| Loan Pack | LoanPackController |
| Reports | ReportingController |
| Audit | AuditController |
| Notifications | NotificationController |
| Scoring Config | ScoringConfigurationController |
| Core Banking | CoreBankingController |

---

## File Structure

```
CRMS.Web.Intranet/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor
│   │   ├── NavMenu.razor
│   │   └── TopBar.razor
│   ├── Pages/
│   │   ├── Auth/
│   │   │   ├── Login.razor
│   │   │   └── ForgotPassword.razor
│   │   ├── Dashboard/
│   │   │   └── Index.razor
│   │   ├── Applications/
│   │   │   ├── Index.razor
│   │   │   ├── New.razor
│   │   │   └── Detail.razor
│   │   ├── Queues/
│   │   │   ├── MyQueue.razor
│   │   │   └── RoleQueue.razor
│   │   ├── Committee/
│   │   │   ├── MyVotes.razor
│   │   │   ├── Reviews.razor
│   │   │   └── ReviewDetail.razor
│   │   ├── Reports/
│   │   │   ├── Dashboard.razor
│   │   │   ├── Funnel.razor
│   │   │   └── Audit.razor
│   │   ├── Admin/
│   │   │   ├── Users.razor
│   │   │   ├── Products.razor
│   │   │   └── ScoringConfig.razor
│   │   └── Profile/
│   │       └── Index.razor
│   └── Shared/
│       ├── DataGrid.razor
│       ├── StatusBadge.razor
│       ├── ConfirmDialog.razor
│       └── DocumentViewer.razor
├── Services/
│   ├── ApiClient.cs
│   ├── AuthService.cs
│   └── NotificationService.cs
├── Models/
│   └── (DTOs mirrored from API)
└── wwwroot/
    ├── css/
    └── js/
```

---

## Technical Considerations

1. **Authentication:** JWT tokens stored in browser, refreshed automatically
2. **Authorization:** Role checks on both client (UI visibility) and server (API)
3. **State Management:** Cascading parameters for user context
4. **Error Handling:** Global error boundary, toast notifications
5. **Loading States:** Skeleton loaders, spinners for async operations
6. **Responsive Design:** Mobile-friendly for tablet use in branches
