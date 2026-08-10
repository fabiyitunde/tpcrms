# CRMS Loan Processing Tutorial

> **Platform:** Credit and Risk Management System (CRMS) — Bank of Agriculture  
> **Last updated:** 2026-07-31  
> **Scope:** Two main loan processes — Corporate and NAMP Agricultural Equipment Financing

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Admin Configuration — Before You Begin](#2-admin-configuration--before-you-begin)
   - 2.1 [Common Setup (Both Processes)](#21-common-setup-both-processes)
   - 2.2 [Corporate Loan Configuration](#22-corporate-loan-configuration)
   - 2.3 [NAMP Loan Configuration](#23-namp-loan-configuration)
3. [Corporate Loan Process](#3-corporate-loan-process)
   - 3.1 [Stage 1 — Draft (Loan Officer)](#31-stage-1--draft-loan-officer)
   - 3.2 [Stage 2 — Credit Review (Credit Officer + Legal Officer, Parallel)](#32-stage-2--credit-review-credit-officer--legal-officer-parallel)
   - 3.3 [Stage 3 — Committee Circulation (Committee Members)](#33-stage-3--committee-circulation-committee-members)
   - 3.4 [Stage 4 — Ratification (Tiered Managers)](#34-stage-4--ratification-tiered-managers)
   - 3.5 [Stage 5 — Offer Generated (Loan Officer + Customer)](#35-stage-5--offer-generated-loan-officer--customer)
   - 3.6 [Stage 6 — Security Perfection (Legal Officer)](#36-stage-6--security-perfection-legal-officer)
   - 3.7 [Stage 7 — Disbursement (Disbursement Officer)](#37-stage-7--disbursement-disbursement-officer)
   - 3.8 [Corporate Loan Status Summary](#38-corporate-loan-status-summary)
4. [NAMP Agricultural Equipment Financing Process](#4-namp-agricultural-equipment-financing-process)
   - 4.1 [Pre-Stage — PAYS Portal Intake](#41-pre-stage--pays-portal-intake)
   - 4.2 [Stage 1 — Loan Officer Review (Draft)](#42-stage-1--loan-officer-review-draft)
   - 4.3 [Stage 2 — Financial Appraisal (Credit Officer)](#43-stage-2--financial-appraisal-credit-officer)
   - 4.4 [Stage 3 — Committee Circulation (Tiered)](#44-stage-3--committee-circulation-tiered)
   - 4.5 [Stage 4 — Ratification (Tier Manager)](#45-stage-4--ratification-tier-manager)
   - 4.6 [Stage 5 — Offer Documents](#46-stage-5--offer-documents)
   - 4.7 [Stage 6 — Legal Clearance (Legal Officer)](#47-stage-6--legal-clearance-legal-officer)
   - 4.8 [Stage 7 — Pre-Deployment Verification (Deployment Officer)](#48-stage-7--pre-deployment-verification-deployment-officer)
   - 4.9 [Stage 8 — Deployment (Deployment Officer)](#49-stage-8--deployment-deployment-officer)
   - 4.10 [Stage 9 — Active and Closed](#410-stage-9--active-and-closed)
   - 4.11 [NAMP Status Summary](#411-namp-status-summary)
5. [User Roles Quick Reference](#5-user-roles-quick-reference)
   - 5.1 [Unified Role List](#51-unified-role-list)
   - 5.2 [Role-to-Stage Reference](#52-role-to-stage-reference)
   - 5.3 [Roles No Longer Active](#53-roles-no-longer-active)

---

## 1. System Overview

The CRMS platform manages two distinct loan origination channels for Bank of Agriculture:

| Channel | Who Applies | Purpose | Entry Point |
|---------|-------------|---------|-------------|
| **Corporate Loan** | Existing corporate customers | SME or business lending via direct bank officer engagement | Loan Officer creates a new application |
| **NAMP** | Agripreneurs and Agro-Service companies | Agricultural equipment financing under the National Agricultural Mechanisation Programme | External PAYS portal sends application via webhook |

Both processes share the same underlying infrastructure (committees, credit bureaus, document storage, core banking integration) and — as of the unified actor framework — the same set of roles. A Loan Officer, Credit Officer, Legal Officer, or BranchManager performs the same function regardless of whether the loan is Corporate or NAMP. The processes follow separate workflow paths because they differ in how applications enter the system, how committees are tiered, and what the terminal stages involve.

**Why two separate paths?**  
The Corporate process starts inside the bank — a Loan Officer walks through a 3-step wizard to create the application. The NAMP process starts outside the bank — applicants apply through the PAYS portal, and the application arrives in a staging queue. A Loan Officer then "recalls" it for internal review. The final stages also differ: Corporate ends with Security Perfection then Disbursement (cash); NAMP ends with Legal Clearance, Pre-Deployment Verification, and physical equipment Deployment with GPS activation.

**One person, multiple roles:**  
Staff members who work across both loan types can hold both the `DeploymentOfficer` role (for NAMP equipment deployment) and the `DisbursementOfficer` role (for Corporate/Retail cash disbursement). The system grants access based on whichever role is relevant to the application currently on screen.

---

## 2. Admin Configuration — Before You Begin

The system will not function correctly unless the following configuration has been done by a **SystemAdmin** user. This section covers what must be set up and why each setting matters.

### 2.1 Common Setup (Both Processes)

These settings are required for both Corporate and NAMP loans.

---

#### A. Locations (Branches, Zones, Regions)

**Where:** Admin → Locations

**Why:** Every loan application is associated with a branch. The branch determines visibility (a Loan Officer at Lagos Main Branch sees only that branch's applications) and, for NAMP, is used to route applications to the correct committee tier.

**What to configure:**
- Create a Region (e.g., "South-West Region")
- Under the Region, create Zones (e.g., "Lagos Zone")
- Under each Zone, create Branches (e.g., "Lagos Main Branch", "Ikeja Branch")

Each branch will be assigned a Location ID that users and applications reference. Get this hierarchy right before creating users, because users are assigned to a branch on creation.

---

#### B. Users and Roles

**Where:** Admin → Users

**Why:** Every action in CRMS is role-gated. The system will silently hide buttons from users who lack the right role. A user with no role, or the wrong role, will see an empty application detail page with no available actions.

**What to configure:**

Create a user account for each staff member and assign them the appropriate role. Each user has exactly one primary role. The full list of roles and what they can do is in [Section 5](#5-user-roles-quick-reference).

Key points:
- Assign the user to their branch. For Head Office roles, the branch can be left blank or set to the HO location.
- A user's branch controls what they see on the application list. A Loan Officer at Branch A will not see Branch B's applications.
- The password for all test/seeded accounts is `Password1$$$`.

---

#### C. Collateral Types

**Where:** Admin → Collateral Types

**Why:** When a Loan Officer adds collateral to a corporate application (e.g., "Landed Property at 14 Broad Street"), they must select a collateral type from a dropdown. If no types are configured, the dropdown will be empty and collateral cannot be added.

**What to configure:**  
Create the categories of acceptable collateral (e.g., Landed Property, Vehicle, Equipment, Stock/Inventory, Cash Deposit). For each type, you can optionally set valuation rules or coverage ratios.

---

### 2.2 Corporate Loan Configuration

These settings are specific to the Corporate loan process.

---

#### A. Loan Products

**Where:** Admin → Products

**Why:** Every loan application must be linked to a product. The product defines the parameters (min/max amount, tenor range, interest rate) and also controls which documents are mandatory and what conditions precedent must be satisfied before disbursement.

**What to configure:**

1. Click **New Product** and fill in:
   - **Product Name**: e.g., "SME Business Loan"
   - **Product Code**: e.g., "SME-BIZ"
   - **Type**: Select **Corporate**
   - **Minimum Amount / Maximum Amount**: The range of loan amounts this product supports (e.g., ₦5,000,000 – ₦500,000,000)
   - **Minimum Tenor / Maximum Tenor (months)**: e.g., 6 – 60 months
   - **Base Interest Rate (% p.a.)**: The standard rate, e.g., 12.0%
   - **Fineract Product ID**: The numeric ID of the corresponding product in the Fineract core banking system. This is required for loan account booking at disbursement. Obtain this from the IT/CBS team.

2. Under the product, add **Pricing Tiers** (optional):  
   Pricing tiers let you define different interest rates for different credit risk bands. For example, a customer with a credit score above 750 may get 10% p.a., while one between 600–749 gets 13% p.a. Each tier needs:
   - Tier Name (e.g., "Prime", "Standard", "Sub-standard")
   - Interest Rate (% p.a.)
   - Credit Score Range (minimum and maximum score for this tier to apply)

3. Under the product, add **Document Requirements**:  
   These are the document categories that must be uploaded before the application can be submitted. If a required document is missing, the Submit button will be blocked. Common requirements:
   - Bank Statement (at least 6 months)
   - Audited Financials (most recent 3 years)
   - Company Registration
   - Board Resolution (authorising the loan)
   - Tax Clearance Certificate
   - Identity Documents for Directors

4. Under the product, add **Disbursement Checklist Template Items**:  
   These are conditions precedent — items that must be confirmed or satisfied before the Operations team can disburse funds. They are auto-seeded onto the application when the customer accepts the offer. Examples:
   - "Board Resolution for loan drawdown"
   - "Signed Offer Letter returned to bank"
   - "Signed KFS (Key Facts Statement) returned to bank"
   - "Collateral registration confirmed"
   - "Insurance policy on collateral"

   For each item, configure:
   - Title and description
   - Whether it requires a document upload (yes/no)
   - Whether it blocks disbursement if not checked (mandatory)
   - Whether it can be waived

---

#### B. Standing Committees

**Where:** Admin → Committees

**Why:** In the Corporate process, after credit review, the application goes to a committee for a collective decision. The system needs to know which committees exist, what their quorum rules are, and who the members are. Without a configured committee, the Credit Officer cannot set up the committee vote.

**What to configure:**

For each committee:
1. Click **New Committee** and fill in:
   - **Committee Name**: e.g., "Branch Credit Committee — Lagos Main"
   - **Committee Type**: Select from BranchCredit, RegionalCredit, HeadOfficeCredit, ManagementCredit, or BoardCredit
   - **Location** (optional): Assign to a specific branch if it is a branch-level committee. Leave blank for HO-wide committees.
   - **Minimum Loan Amount / Maximum Loan Amount**: The range of loan amounts this committee handles. If a loan falls outside this range, the Credit Officer will not see this committee as an option.
   - **Required Votes**: Total number of votes needed to constitute a quorum
   - **Minimum Approval Votes**: How many "Approve" votes are needed for the committee to pass the application
   - **Default Deadline (hours)**: How long members have to vote (e.g., 120 hours = 5 days)

2. Add **Members** to the committee:
   - Select a user from the system
   - Assign their role label (e.g., "Member", "Chairperson")
   - Mark one member as Chairperson — this person will have additional responsibilities to formally record the decision after voting

**Tip:** Set up at least one committee before testing the Corporate flow end-to-end. Without a committee, the workflow will stall at CommitteeCirculation.

---

### 2.3 NAMP Loan Configuration

These settings are specific to the NAMP process. Most of them are **seeded automatically** when the system first starts (via `NampWorkflowSeeder`), but they can be reviewed and adjusted through the admin UI.

---

#### A. NAMP Loan Product

**Where:** Admin → Products

**What to configure:**  
Create at least one product of **Type: Namp**. The fields are the same as a corporate product (name, code, amounts, tenor, Fineract product ID). The NAMP product is linked to applications when a Loan Officer recalls them from staging.

---

#### B. NAMP Routing Configuration

**Where:** Admin → NAMP Routing Config

**Why:** NAMP applications go to different committee tiers depending on the loan amount and the applicant category. This configuration defines those routing bands. Without it, the system cannot determine which committee to circulate the application to.

**Default bands (seeded at startup):**

| Applicant Category | Loan Amount Range | Committee Tier |
|--------------------|-------------------|----------------|
| Youth Agripreneur | ₦0 – ₦5,000,000 | Branch |
| Youth Agripreneur | ₦5,000,001 – ₦20,000,000 | Zonal |
| Youth Agripreneur | ₦20,000,001 – ₦50,000,000 | Regional |
| Youth Agripreneur | Above ₦50,000,000 | Head Office |
| Women Agripreneur | ₦0 – ₦5,000,000 | Branch |
| Women Agripreneur | ₦5,000,001 – ₦20,000,000 | Zonal |
| Women Agripreneur | ₦20,000,001 – ₦50,000,000 | Regional |
| Women Agripreneur | Above ₦50,000,000 | Head Office |
| Agro-Service Company | ₦0 – ₦20,000,000 | Zonal |
| Agro-Service Company | ₦20,000,001 – ₦100,000,000 | Regional |
| Agro-Service Company | Above ₦100,000,000 | Head Office |

**Note:** Agro-Service Companies start at the Zonal tier regardless of size. This is intentional — companies have more complex structures and require at least zonal-level oversight.

To adjust these bands: click a row, update the Min/Max amounts, and save.

---

#### C. NAMP Workflow Stage Configuration

**Where:** Admin → NAMP Workflow Config

**Why:** Each NAMP stage has an assigned role (who is responsible), an SLA (how many hours they have to act), and a display name. These are seeded at startup.

**Default configuration (for reference):**

| Stage | Assigned Role | SLA |
|-------|--------------|-----|
| Received | Loan Officer | 48 hours |
| Recall Pending | Loan Officer | 48 hours |
| Draft | Loan Officer | 48 hours |
| Submitted | Credit Officer | 72 hours |
| Financial Appraisal | Credit Officer | 72 hours |
| Branch Committee Circulation | Committee Member | 120 hours |
| Zonal Committee Circulation | Committee Member | 120 hours |
| Regional Committee Circulation | Committee Member | 120 hours |
| HO Committee Circulation | Committee Member | 120 hours |
| Ratification | Tier Manager (BranchManager / ZonalManager / RegionalManager / MdCeo) | 48 hours |
| Offer Generated | Loan Officer | 168 hours (7 days) |
| Legal Clearance | Legal Officer | 72 hours |
| Legal Returned | Loan Officer | 48 hours |
| Pre-Deployment Verification | Deployment Officer | 48 hours |
| Deployment | Deployment Officer | 168 hours (7 days) |
| Active | Loan Officer | No SLA |

You can change the SLA hours and descriptions through the admin UI. You cannot change the assigned role from the UI — contact the system administrator if a role change is needed.

---

#### D. NAMP Pre-Deployment Checklist

**Where:** Admin → NAMP Pre-Deploy Checklist

**Why:** Before equipment can be deployed, four conditions must be verified. These are seeded as checklist items and appear on the Deployment Officer's screen during the Pre-Deployment Verification stage.

**Default items (seeded at startup):**

1. **Equity Deposit Confirmed** — The applicant must pay their equity contribution upfront (the portion of the equipment cost not covered by the loan). A receipt must be uploaded.
2. **Lease / Hire-Purchase Agreement Signed** — The applicant must sign the equipment lease or hire-purchase agreement before they can take possession of the equipment.
3. **GPS Tracking Consent Obtained** — The applicant must sign the GPS Tracking Consent Form authorising the bank to install and monitor a GPS device on the equipment.
4. **NAIC Insurance In Place** — A valid Nigerian Agricultural Insurance Corporation (NAIC) insurance policy must cover the equipment. The certificate must be uploaded.

All four items are mandatory. The Deployment Officer cannot proceed to the Deployment stage until all four are checked and evidence documents uploaded.

---

#### E. NAMP Document Templates

**Where:** Admin → NAMP Document Templates

**Why:** At ratification, three documents are auto-generated for the applicant:
1. **Offer Letter** — Formal offer of the NAMP credit facility
2. **Hire-Purchase / Lease Agreement** — The equipment financing contract
3. **GPS Tracking Consent Form** — Authorisation for GPS installation and monitoring

These documents are generated from templates with placeholders such as `{{ApplicantName}}`, `{{LoanAmount}}`, `{{TenorMonths}}`, `{{InterestRate}}`, etc. The templates are seeded at startup with appropriate default text, but the legal/compliance team can edit the body text through the admin UI.

**Important:** Do not remove the placeholder tags (e.g., `{{ApplicantName}}`). These are replaced with live data when the document is generated. If a placeholder is deleted, that field will be blank in the generated document.

---

#### F. NAMP Committees

**Where:** Admin → Committees

**Why:** The NAMP process routes applications to committees at up to four tiers (Branch, Zonal, Regional, Head Office). A committee must exist at each tier before the system can circulate applications at that tier.

Create one committee for each tier that will be used:
- **Branch Credit Committee** (Type: BranchCredit, linked to the relevant branch)
- **Zonal Credit Committee** (Type: ZonalCredit or as configured)
- **Regional Credit Committee** (Type: RegionalCredit)
- **HO Credit Committee** (Type: HeadOfficeCredit)

Assign members with the corresponding committee member roles:
- Branch committee → `BranchCommitteeMember`
- Zonal committee → `ZonalCommitteeMember`
- Regional committee → `RegionalCommitteeMember`
- HO committee → `HOCommitteeMember`

---

## 3. Corporate Loan Process

The Corporate loan process has **seven active stages**:

```
Draft
 └─ Submit for Credit Review
     └─ CreditReview (Credit Officer + Legal Officer, in parallel)
         └─ Send to Committee
             └─ CommitteeCirculation (Committee Members vote)
                 └─ Committee Approved → auto-advances
                     └─ Ratification (BranchManager / ZonalManager / RegionalManager / MdCeo)
                         └─ Ratify (offer letter generated automatically)
                             └─ OfferGenerated
                                 └─ Record Customer Acceptance
                                     └─ OfferAccepted → auto-advances
                                         └─ SecurityPerfection (Legal Officer)
                                             └─ Complete Security Perfection
                                                 └─ Disbursement (Disbursement Officer)
                                                     └─ Disburse → Disbursed ■
```

---

### 3.1 Stage 1 — Draft (Loan Officer)

**Status:** Draft  
**Who acts:** Loan Officer  
**Navigation:** Applications → New Application

#### What is happening at this stage?

The Loan Officer creates a new loan application on behalf of a corporate customer. This involves three steps: identifying the customer via their CBS account number, entering the loan details, and then enriching the application with parties, documents, collateral, and financials before submitting.

---

#### Step A: Create the Application (3-Step Wizard)

**Step 1 — Customer Selection:**

1. Navigate to **Applications** in the left menu, then click **New Application** (top right).
2. In the "Corporate Account Number" field, enter the customer's 10-digit account number and click **Fetch**.
3. The system queries Core Banking and retrieves the company name, registration details, and signatories. If the account is not found or is not a corporate account, an error message will appear.
4. Once the customer card appears showing the company name and an "Account Verified" badge, click **Next**.

**Step 2 — Loan Details:**

Fill in the financial terms of the proposed loan:
- **Product**: Select the loan product from the dropdown. This determines the allowable amount/tenor range, required documents, and disbursement checklist items. Products must be configured in Admin → Products before they appear here.
- **Requested Amount (₦)**: The amount the customer wants to borrow. Must fall within the product's Min/Max range.
- **Tenor (months)**: The proposed loan duration. Must fall within the product's Min/Max tenor.
- **Interest Rate (%)**: The proposed interest rate (per annum).
- **Interest Rate Type**: Fixed or Variable.

Click **Next** to proceed to Review.

**Step 3 — Review and Create:**

Review the summary and click **Create Application**. The system will:
1. Create the loan application with status **Draft**
2. Generate an application number in the format `LA{yyyyMMdd}{6-char hex}` (e.g., `LA202607301A4B2F`)
3. Automatically fetch the 6-month internal bank statement from Core Banking
4. Automatically add the company's directors and signatories from CBS and SmartComply

You are now on the application detail page.

---

#### Step B: Review Directors and Signatories (Parties Tab)

Click the **Directors & Signatories** tab.

You will see:
- **Directors**: Fetched from CAC via SmartComply. Review each director's name, nationality, and shareholding percentage.
- **Signatories**: Fetched from Core Banking. Review each signatory's name and role.

**Complete missing BVNs:** Each director and signatory must have a BVN (Bank Verification Number) for the credit bureau check to run. If a BVN field shows as blank or "Not provided", click the edit icon on that party and enter the BVN. The system cannot run a credit bureau check on a party without a BVN.

**Enter shareholding percentages:** Each director's shareholding percentage must be entered. The credit bureau will use this to assess the extent of each director's financial exposure to the business.

---

#### Step C: Upload Documents (Documents Tab)

Click the **Documents** tab.

The tab shows which document categories are required by the product. Upload at least one file in each mandatory category. Common categories:

| Document Category | What It Is |
|-------------------|-----------|
| Bank Statement | Minimum 6 months — internal (auto-fetched) + external banks |
| Audited Financials | Last 2-3 years of audited accounts |
| Company Registration | CAC certificate and memorandum/articles |
| Board Resolution | Resolution authorising the loan and signatories |
| Tax Clearance | TCC from FIRS |
| Identity Documents | Valid ID for each director/signatory |

To upload a document:
1. Click **Upload Document**
2. Select the document category
3. Choose the file (PDF or image)
4. Click **Upload**

Documents can be re-uploaded or replaced at any point while the application is in Draft status.

---

#### Step D: Add Collateral (Collateral Tab)

Click the **Collateral** tab.

Collateral is security the customer pledges against the loan. The total market value of all collateral is used by the Credit Officer to compute the Loan-to-Value ratio (LTV).

To add collateral:
1. Click **Add Collateral**
2. Select the **Collateral Type** (from the types configured in Admin)
3. Enter a **Description** (e.g., "3-bedroom flat at 14 Allen Avenue, Ikeja")
4. Enter the **Market Value (₦)** — the current estimated market value
5. Enter the **Forced Sale Value (₦)** — what the bank could realistically recover if it had to sell quickly (typically 60-70% of market value)
6. Optionally add the address/legal description
7. Click **Save**

Add all pledged collateral items. The system sums them automatically to compute the LTV later.

---

#### Step E: Add Guarantors (Guarantors Tab)

Click the **Guarantors** tab.

A guarantor is a person or entity who agrees to repay the loan if the borrower defaults. To add a guarantor:
1. Click **Add Guarantor**
2. Enter the guarantor's name, BVN, and relationship to the borrower
3. Click **Save**

Each guarantor's BVN will be checked against the credit bureau automatically when the application is submitted.

---

#### Step F: Enter Financial Statements (Financial Analysis Tab)

Click the **Financial Analysis** tab.

Enter the company's financial data for the last two or three financial years. The data entered here powers the automatic computation of certain financial ratios used by the Credit Officer.

For each year, enter:
- Year
- Total Revenue
- Net Profit After Tax
- Total Assets
- Total Liabilities
- Current Assets
- Current Liabilities
- Total Equity
- Total Debt (interest-bearing borrowings)

**Why this matters:** The Credit Officer will compute the Leverage ratio and Current Ratio from these figures. Accurate financial data leads to accurate ratios.

---

#### Step G: Submit for Credit Review

Once all required documents are uploaded and all party BVNs are filled in, the **Submit for Credit Review** button becomes active in the top-right corner.

Before clicking, confirm:
- At least one bank statement is present
- All director and signatory BVNs are filled
- All mandatory documents are uploaded

Click **Submit for Credit Review**. The application status changes from **Draft** to **CreditReview** and the application enters the Credit Officer's queue. The system simultaneously triggers an automated credit bureau check on all directors, signatories, guarantors, and (if an RC number is present) the company itself.

---

### 3.2 Stage 2 — Credit Review (Credit Officer + Legal Officer, Parallel)

**Status:** CreditReview  
**Who acts:** Credit Officer (primary reviewer) AND Legal Officer (parallel)  
**SLA:** 72 hours

#### What is happening at this stage?

Two things happen in parallel:

1. **Automated credit checks** run in the background for all parties with BVNs. The Credit Officer cannot proceed until these are complete (or until they re-run checks that failed).
2. **The Legal Officer** reviews the legal standing of the application and marks their review as complete. The Credit Officer cannot send the application to committee until the Legal Officer has signed off.

Both tasks must be completed before the application can advance.

---

#### Credit Bureau Checks (Automatic)

As soon as the application enters CreditReview, the system automatically checks the credit bureau for all directors, signatories, guarantors, and the company (by RC number). This runs in the background — no action is needed.

**View results on the Credit Bureau tab.** Each party gets a card showing:
- **Credit Score** (e.g., 756/A — for individuals only; businesses show N/A)
- **Score Grade**: A+ (excellent), A, B, C, D, E (very poor)
- **Active Loans**: Number of currently active credit facilities
- **Delinquencies**: Number of overdue accounts
- **Overdue Amount (₦)**
- **Fraud Risk Score** and **Fraud Recommendation**

**Card Status Indicators:**
- **Completed** (normal card) — check ran successfully
- **Consent Required** — no NDPA consent record found for this BVN; check was skipped
- **Check Failed** — the bureau API returned an error
- **Not Found** — the BVN/RC number is not in the bureau database

**Re-running checks:**  
If any check shows "Failed", "Consent Required", or "Not Found", the **Re-run Credit Checks** button will appear in the header. Click it to retry only the failed/incomplete checks. Successfully completed checks are not re-run.

---

#### Legal Review (Legal Officer's Task)

The Legal Officer sees the **Legal Review Complete** button in the top-right corner.

The Legal Officer should:
1. Review the Directors & Signatories tab to confirm the legal structure is correct
2. Review the Documents tab to confirm all legal documents (Company Registration, Board Resolution, etc.) are present and valid
3. Review any collateral documentation
4. Enter their review note in the modal
5. Click **Legal Review Complete**

This records the time of legal review and the note. Once this is done, the Legal Review Complete button disappears.

**Important:** The Credit Officer's "Send to Committee" button will not be available until the Legal Officer has completed their review AND the Credit Officer has saved a credit appraisal. Both conditions must be met.

---

#### Credit Appraisal (Credit Officer's Task)

Click the **Credit Appraisal** tab (visible during CreditReview).

The Credit Officer must assess the borrower's creditworthiness and record a formal credit appraisal. The form has two sections:

**Financial Ratios:**

These ratios give a quantitative picture of the borrower's financial health. The system auto-computes some ratios from the financial statements you entered, but the Credit Officer must verify and may override them.

**1. DSCR — Debt Service Coverage Ratio**  
*What it measures:* Can the borrower generate enough income to cover their loan repayments?

```
DSCR = Net Operating Income ÷ Annual Debt Service

Where:
  Net Operating Income = EBIT (Earnings Before Interest and Tax)
                       = Total Revenue – Operating Expenses
  Annual Debt Service  = Total annual loan principal + interest payments
                       = (Proposed monthly repayment × 12)
                       + any existing loan obligations in the same period

Example:
  Net Operating Income = ₦18,000,000 per year
  Annual Debt Service  = ₦12,000,000 (proposed loan) + ₦2,400,000 (existing)
                       = ₦14,400,000

  DSCR = ₦18,000,000 ÷ ₦14,400,000 = 1.25x
```

**Interpretation:**
- DSCR ≥ 1.25 — Strong (displayed in green). Income comfortably covers repayments.
- DSCR 1.00–1.24 — Acceptable (yellow). Income barely covers repayments; any shortfall is a risk.
- DSCR < 1.00 — Weak (red). The borrower cannot generate enough income to service the debt.

**2. Leverage (Debt/Equity Ratio)**  
*What it measures:* How much of the company is financed by debt versus owner's equity?

```
Leverage = Total Debt ÷ Total Equity

Where:
  Total Debt   = All interest-bearing borrowings (short + long-term loans)
  Total Equity = Total Assets – Total Liabilities

Example:
  Total Debt   = ₦40,000,000
  Total Equity = ₦25,000,000

  Leverage = ₦40,000,000 ÷ ₦25,000,000 = 1.60x
```

The system auto-computes this from the financial statements you entered. Check the "Auto from financials" hint to see the computed value. A lower leverage ratio indicates a less debt-heavy business. High leverage (e.g., above 3x) is a risk signal.

**3. Current Ratio**  
*What it measures:* Can the business meet its short-term (current year) financial obligations?

```
Current Ratio = Current Assets ÷ Current Liabilities

Where:
  Current Assets      = Cash + receivables + inventory + other assets realisable within 12 months
  Current Liabilities = Payables + short-term loans + other amounts due within 12 months

Example:
  Current Assets      = ₦30,000,000
  Current Liabilities = ₦18,000,000

  Current Ratio = ₦30,000,000 ÷ ₦18,000,000 = 1.67x
```

**Interpretation:**
- Current Ratio ≥ 2.0 — Strong (green). Business has comfortable liquidity.
- Current Ratio 1.0–1.99 — Acceptable (yellow). Liquid enough but with little buffer.
- Current Ratio < 1.0 — Weak (red). Business may struggle to pay short-term obligations.

**4. LTV — Loan-to-Value Ratio**  
*What it measures:* How much of the collateral's market value is covered by the loan? A lower LTV means better security for the bank.

```
LTV (%) = Loan Amount ÷ Market Value of Collateral × 100

Where:
  Loan Amount               = The requested loan amount
  Market Value of Collateral = Sum of market values of all pledged collateral items

Example:
  Loan Amount                = ₦80,000,000
  Total Collateral Market Value = ₦120,000,000

  LTV = ₦80,000,000 ÷ ₦120,000,000 × 100 = 66.7%
```

The system auto-computes this from the collateral values entered during Draft. Check the "Auto from collateral" hint to see the computed value.

**Interpretation:**
- LTV ≤ 70% — Good (green). Collateral comfortably covers the loan.
- LTV 70–85% — Borderline (yellow). Some risk if the collateral value drops.
- LTV > 85% — High risk (red). The bank is under-secured.

---

**Credit Officer Assessment Section:**

After entering the ratios, the Credit Officer must select:

- **Repayment Capacity Rating**: An overall qualitative judgement on the borrower's ability to repay:
  - *Strong* — Excellent financial position, comfortable margins
  - *Adequate* — Acceptable position, may have some weaknesses
  - *Marginal* — On the borderline; significant risk factors present
  - *Insufficient* — Borrower is unlikely to be able to service the debt

- **Recommendation**: The Credit Officer's formal recommendation to the committee:
  - *Approve* — Recommend approval on the stated terms
  - *Conditional Approve* — Recommend approval subject to stated conditions
  - *Decline* — Recommend rejection

- **Appraisal Notes**: Write your detailed assessment — key findings, risk factors, mitigants, and any conditions you are recommending.

- **Credit Appraisal Memo** (optional): Upload the full formal credit appraisal memo as a PDF or Word document. This is the detailed write-up and becomes part of the permanent record.

Click **Save Appraisal** to save. You can save multiple times and update the appraisal while the application is in CreditReview.

---

#### Sending to Committee

Once:
1. All credit bureau checks are complete (or have been re-run)
2. The Legal Officer has marked their review as complete
3. The Credit Officer has saved a credit appraisal with at least a Capacity Rating and Recommendation

…the **Send to Committee** button (labelled "Approve" in some contexts) becomes active for the Credit Officer.

Before clicking, the system runs an approval gate check. If there are issues (e.g., some credit checks failed, or certain documents are missing), a warning modal appears listing them. The Credit Officer can:
- **Go back and fix the issues** — close the modal and resolve the flagged items
- **Proceed with override** — enter a reason explaining why the issues are acceptable, and confirm. This creates an override record visible to all downstream reviewers.

After confirming, a "Send to Committee" modal appears where the Credit Officer enters a note. The application moves to **CommitteeCirculation**.

---

#### Returning or Rejecting at Credit Review

- **Return** (Credit Officer or Legal Officer who has not yet completed legal review): Sends the application back to Draft for the Loan Officer to correct. A mandatory note must be entered explaining what needs to be fixed.
- **Reject** (Credit Officer only): Permanently rejects the application. A mandatory reason must be entered. This is a terminal action — the application cannot be resubmitted after rejection.

---

### 3.3 Stage 3 — Committee Circulation (Committee Members)

**Status:** CommitteeCirculation  
**Who acts:** Credit Officer (setup and decision recording), Committee Members (voting)  
**SLA:** Configurable per committee (default 120 hours)

#### What is happening at this stage?

A group of designated committee members review the credit assessment and collectively decide whether to approve, reject, defer, or escalate the application. The Credit Officer facilitates this process — they set up the committee review, notify members, and eventually record the final collective decision.

---

#### Step A: Set Up the Committee Review (Credit Officer)

When the application first enters CommitteeCirculation, the Credit Officer sees a **Committee Setup** panel on the Committee tab.

1. Select the **Committee** from the dropdown. Only committees whose amount range covers this loan amount will appear.
2. Review the pre-filled committee members (from the committee configuration).
3. Adjust the **Deadline** if needed (default from the committee configuration).
4. Click **Set Up Committee Review** to create the review record.

Once set up, the committee's status changes to **Pending** and members can see the application.

---

#### Step B: Start Voting (Credit Officer)

After the committee is set up, the Credit Officer clicks **Start Voting** on the Committee tab. This changes the committee status to **InProgress** and enables the vote buttons for all assigned committee members.

**Note:** Members cannot vote until the Credit Officer explicitly starts the voting process.

---

#### Step C: Voting (Committee Members)

Each assigned committee member sees a **Vote** button when they open the application. To vote:

1. Navigate to the application (it will appear in their "In My Queue" list)
2. Click the **Committee** tab
3. Click the vote button and choose:
   - **Approve** — I recommend this application be approved
   - **Reject** — I recommend this application be rejected
   - **Abstain** — I am declining to vote on this application
4. Add a vote comment (optional)
5. Confirm the vote

Each member can vote only once. After voting, the vote counters update in real time. No vote can be changed after submission.

Committee members can also add **Comments** on the Comments tab at any time (both before and after voting). Comments can be marked Public (visible to all) or Private (visible to committee members only).

---

#### Step D: Record the Committee Decision (Credit Officer/Chairperson)

Once all members have voted (or the deadline is reached and there is a quorum), the Credit Officer or Chairperson records the formal decision.

Click **Record Decision** on the Committee tab and choose one of:

| Decision | When to Use | What Happens |
|----------|-------------|-------------|
| **Approved** | Clear majority voted to approve | Application advances to Ratification; approved terms are locked |
| **Approved with Conditions** | Majority approved but with stated conditions | Application advances to Ratification; conditions are recorded and visible to Final Approver |
| **Rejected** | Majority voted to reject | Application is rejected — terminal; no further action |
| **Deferred** | Committee needs more information | Application returns to the Credit Officer's queue for more work |
| **Escalated** | Exceeds this committee's authority | Application must be sent to a higher-tier committee |

For an **Approved** or **Approved with Conditions** decision, you must enter:
- **Approved Amount (₦)**: The amount the committee is recommending (may differ from the requested amount)
- **Approved Tenor (months)**: The approved term
- **Approved Interest Rate (% p.a.)**: The approved rate

These values become the final approved terms of the loan (subject to ratification).

After recording an Approved decision, the application automatically advances to **Ratification**.

---

### 3.4 Stage 4 — Ratification (Tiered Managers)

**Status:** Ratification  
**Who acts:** BranchManager, ZonalManager, RegionalManager, or MdCeo (depending on loan size)  
**SLA:** 48 hours

#### What is happening at this stage?

The committee's recommendation must be formally ratified by a senior authority — the same tiered ratification model used in the NAMP process. The ratifying manager reviews the committee's decision and either confirms or declines it. This is the last gate before the offer letter is generated.

**Who ratifies a Corporate loan:**

| Loan Amount (indicative) | Ratifying Authority |
|--------------------------|---------------------|
| Branch-level exposure | Branch Manager |
| Zonal-level exposure | Zonal Manager |
| Regional-level exposure | Regional Manager |
| HO / large exposures | MD/CEO |

The appropriate authority is determined by internal delegation limits. When the ratifying manager opens the application, they see:
- All application details
- The committee's decision, approved amount, tenor, and rate
- The credit appraisal (ratios, capacity rating, recommendation)
- Bureau reports for all parties
- Any approval override records

---

#### Ratify the Decision

If the manager agrees with the committee's recommendation:

1. Click **Ratify** (top-right corner)
2. A modal appears — review the approved terms (they can be adjusted here if needed)
3. Click **Confirm Ratification**

The system then:
1. Locks in the approved amount, tenor, and rate
2. Automatically generates the Offer Letter PDF
3. Advances the application to **OfferGenerated**

**The offer letter is generated automatically as part of ratification.** No separate trigger is needed.

---

#### Decline Ratification

If the manager disagrees with the committee's recommendation:

1. Click **Decline** (top-right corner)
2. Enter a mandatory reason explaining the decline
3. Confirm

The application moves to **RatificationDeclined** — a terminal status. The application is permanently declined and cannot be resubmitted through this process.

---

### 3.5 Stage 5 — Offer Generated (Loan Officer + Customer)

**Status:** OfferGenerated  
**Who acts:** Loan Officer (or Disbursement Officer)  
**SLA:** 168 hours (7 days)

#### What is happening at this stage?

The bank has approved the loan. The offer letter has been generated. The loan officer now needs to present the offer to the customer, obtain their signed acceptance, and return the signed documents to the bank before the application can proceed.

---

#### Download the Offer Letter

Navigate to the **Offer Letters** tab. You will see:
- The generated offer letter (PDF) — download and print this for the customer
- An **Amortisation Schedule** (downloadable) — shows the repayment schedule month by month
- A **Key Facts Statement (KFS)** — a simplified one-page summary of the loan terms required by regulation

Download all three documents and share them with the customer.

---

#### Customer Reviews and Signs

The customer (and their legal counsel, if applicable) reviews:
1. The offer letter — approved amount, tenor, interest rate, conditions
2. The amortisation schedule — monthly payment amounts, total interest payable
3. The Key Facts Statement — headline terms in plain language

If the customer accepts, they sign the offer letter and return it to the bank.

---

#### Upload Signed Documents

On the **Offer Letters** tab, there is a **Signed Copies** section:
1. **Signed Offer Letter** — Upload the customer-signed offer letter
2. **Signed KFS Acknowledgement** — Upload the signed KFS

Click **Upload** next to each row and select the scanned file.

---

#### Record Customer Acceptance

Once signed documents are uploaded, click **Record Customer Acceptance** (top-right corner).

In the modal:
- **Date Customer Signed**: Enter the actual date the customer signed the offer letter
- **Acceptance Method**: Select how the acceptance was obtained (In-Branch Signing, Courier, Electronic)
- **KFS Acknowledged**: Check this box to confirm the customer has received and acknowledged the KFS

Click **Confirm Acceptance**.

The application automatically advances to **OfferAccepted** and then immediately to **SecurityPerfection**. No further action is needed at this stage.

---

### 3.6 Stage 6 — Security Perfection (Legal Officer)

**Status:** SecurityPerfection  
**Who acts:** Legal Officer  
**No fixed SLA (complete as soon as possible)**

#### What is happening at this stage?

Before funds can be released, the legal team must perfect the security — meaning the bank's interest in the collateral must be formally registered and legally documented. This may include:
- Registration of mortgage or charge at the Corporate Affairs Commission
- Execution of debenture documents
- Transfer of title documents to the bank's custody
- Registration with the National Collateral Registry

---

#### Upload Security Documents (Legal Officer)

Click the **Security Perfection** tab.

For each piece of collateral, the Legal Officer uploads the relevant security documents:
1. Click **Upload Security Document**
2. Select the document category (e.g., mortgage deed, debenture)
3. Select the collateral item this document covers
4. Upload the file
5. Click **Save**

The Collateral section on this tab shows all pledged collateral items and allows the Legal Officer to link uploaded documents to specific collateral items.

---

#### Complete Security Perfection

Once all security documents are in order and registered, click **Security Perfection Complete** (top-right corner).

In the modal, enter a note summarising the security perfection steps taken (e.g., "Mortgage registered at CAC. Title documents received and held in strong room.").

Click **Confirm**.

The application advances to **Disbursement**.

---

#### Returning from Security Perfection

If issues arise (e.g., the customer has not provided all documents, or there is a legal problem with the collateral), the Legal Officer can click **Return** to send the application back to **OfferAccepted** status, where the Loan Officer can work with the customer to resolve the issues.

---

### 3.7 Stage 7 — Disbursement (Disbursement Officer)

**Status:** Disbursement  
**Who acts:** Disbursement Officer (and GM Finance for final release)  
**SLA:** None (complete as quickly as possible)

#### What is happening at this stage?

The loan is now ready to be drawn down. The Operations team prepares the disbursement instruction and books the loan in the core banking system.

---

#### Check the Disbursement Checklist

Navigate to the **Disbursement Checklist** tab (appears from OfferGenerated status onwards).

This tab shows all the conditions precedent seeded from the product template when the customer accepted the offer. For each item:
- If the item requires a document: upload the document — this automatically marks the item as satisfied.
- If the item requires a manual confirmation: click **Mark as Met**.

All mandatory items must be checked before the **Disburse Loan** button becomes active.

Common checklist items include:
- Board resolution for loan drawdown received
- Signed offer letter returned
- Signed KFS received
- Collateral registration confirmed
- Insurance policy on collateral in place

---

#### Disburse the Loan

Click **Disburse Loan** (top-right corner).

In the modal:
- **Core Banking Loan ID**: Enter the loan account ID as it appears in the Fineract/CBS system. This is the account number generated when the loan was booked in core banking. Obtain this from the CBS team.

Click **Confirm Disbursement**.

The application moves to **Disbursed** — a terminal status. The loan has been drawn down and the lifecycle is complete.

---

### 3.8 Corporate Loan Status Summary

```
Draft                    → [LoanOfficer: Submit for Credit Review]
CreditReview             → [CreditOfficer: Send to Committee]
                           [CreditOfficer: Return to Loan Officer]
                           [CreditOfficer: Reject] ■
CommitteeCirculation     → [CreditOfficer records: Approved] → CommitteeApproved
                           [CreditOfficer records: Rejected] ■
                           [CreditOfficer records: Deferred] → CreditReview (loop)
CommitteeApproved        → [Auto-advance] → Ratification
Ratification             → [BranchManager|ZonalManager|RegionalManager|MdCeo: Ratify] → OfferGenerated
                           [BranchManager|ZonalManager|RegionalManager|MdCeo: Decline] → RatificationDeclined ■
OfferGenerated           → [LoanOfficer: Record Customer Acceptance] → SecurityPerfection
SecurityPerfection       → [LegalOfficer: Complete] → Disbursement
                           [LegalOfficer: Return] → OfferAccepted (loop)
Disbursement             → [DisbursementOfficer: Disburse] → Disbursed ■

■ = Terminal status
```

---

## 4. NAMP Agricultural Equipment Financing Process

The NAMP process has **nine active stages** (plus a pre-stage for portal intake):

```
PAYS Portal
 └─ Received (staging)
     └─ RecallPending → [LoanOfficer: Recall]
         └─ Draft
             └─ [LoanOfficer: Submit for Appraisal]
                 └─ Submitted → FinancialAppraisal
                     └─ [CreditOfficer: Financial Appraisal Decision]
                         └─ Committee Circulation (Branch/Zonal/Regional/HO, based on amount)
                             └─ [Committee: Approved]
                                 └─ Ratification
                                     └─ [Manager: Ratify] → OfferGenerated
                                         └─ [LoanOfficer: Record Acceptance] → OfferAccepted
                                             └─ LegalClearance
                                                 └─ [LegalOfficer: Grant Clearance]
                                                     └─ PreDeploymentVerification
                                                         └─ [DeploymentOfficer: Verify 4 Gates]
                                                             └─ Deployment
                                                                 └─ [DeploymentOfficer: Confirm GPS Activation]
                                                                     └─ Active
                                                                         └─ [PAYS: Repayment Complete]
                                                                             └─ Closed ■
```

---

### 4.1 Pre-Stage — PAYS Portal Intake

**Status:** Received → RecallPending  
**Who acts:** External System (PAYS), then Loan Officer

#### What is happening at this stage?

NAMP applications do not start inside CRMS. Applicants apply through the external **PAYS portal**. When an application is submitted in PAYS, it sends a webhook notification to CRMS. CRMS saves the raw payload to a **staging table** and marks the application as **Received**.

The application stays in staging until a Loan Officer recalls it for review. CRMS does not automatically assign or route staging records — a Loan Officer must actively claim the application.

---

#### Viewing the Staging Queue

Navigate to **NAMP** in the left menu.

The NAMP index page shows:
- **Received / Staging** queue — applications waiting to be recalled
- **My Applications** — applications this Loan Officer has already recalled
- **Branch Applications** — all applications at your branch (if you have branch-level visibility)

Each staging record shows the applicant's name, BOA account number, equipment description, category (Youth/Women/Agro-Service), and the time it was received.

---

#### Recalling an Application

Click **Recall** on a staging record.

Recalling does the following:
1. Creates a formal `NampApplication` record with status **Draft**
2. Unpacks the PAYS payload — extracts applicant name, BVN, NIN, BOA account, equipment details, loan amount, equity amount
3. For **Agro-Service Company** applications: automatically fetches the CAC profile from SmartComply using the company's RC number

The application now appears in the Loan Officer's active queue and is no longer in staging.

---

### 4.2 Stage 1 — Loan Officer Review (Draft)

**Status:** Draft  
**Who acts:** Loan Officer  
**SLA:** 48 hours

#### What is happening at this stage?

The Loan Officer reviews the application data that came from PAYS, verifies it, and enriches it with any additional information or documents before submitting for financial appraisal.

---

#### Review the Overview Tab

Open the application and review the **Overview** tab. Key information to verify:

- **Applicant Name and BOA Account Number**: Does this match the customer on record?
- **Applicant Category**: Youth Agripreneur, Women Agripreneur, or Agro-Service Company — this determines the committee tier routing.
- **Equipment Description and Value**: What equipment is being financed? What is the total cart value?
- **Loan Amount and Equity Amount**: The loan amount is what the bank finances; the equity amount is what the applicant pays upfront.
- **BVN and NIN**: Essential for credit bureau checks. If missing, contact the applicant and update via the edit button.

---

#### For Agro-Service Company: Review CAC Data and Directors

If the applicant is an **Agro-Service Company**, the CAC section on the Overview tab will show the company profile fetched from SmartComply:
- Company name, RC number, registration date
- Nature of business, share capital
- Registered address

Below this, the **Directors** section shows directors fetched from the CAC registry. For each director:
1. Verify the director's name and shareholding percentage
2. Enter their **BVN** (required for credit bureau check — the CAC does not hold BVN data)
3. Confirm the BVN by clicking the verify button

**Important:** All directors of an Agro-Service Company must have BVNs entered before the application can be submitted. The system will block submission if any director is missing a BVN. This is because the bank runs credit bureau checks on every director — if a director's BVN is missing, their creditworthiness cannot be assessed.

---

#### Upload Documents (Documents Tab)

Click the **Documents** tab.

Upload any supporting documents for this application. Document categories used in NAMP:

| Category | When It's Needed |
|----------|-----------------|
| General | Any document that doesn't fit another category |
| Site Photo | Photos of the farm, equipment site, or applicant |
| Financial Model | At least one required before submission |
| Credit Report | Bureau reports (auto-generated, or uploaded externally) |
| Supporting Document | Any additional supporting material |
| Signed NAMP Offer Letter | Uploaded after the applicant signs the offer letter |
| Equity Deposit Receipt | Required as Gate 1 in pre-deployment |
| Lease Agreement | Required as Gate 2 in pre-deployment |
| GPS Consent Form | Required as Gate 3 in pre-deployment |
| Insurance Certificate | Required as Gate 4 in pre-deployment |

At minimum, upload at least one **Financial Model** document — this is required before the Credit Officer can complete their financial appraisal.

---

#### Submit for Appraisal

Once the application data has been reviewed and at least one Financial Model document is uploaded, click **Submit for Appraisal** (top-right corner).

A confirmation modal appears. Enter any notes for the Credit Officer, then confirm.

The application advances to **Submitted** and is routed to the Credit Officer's queue.

---

### 4.3 Stage 2 — Financial Appraisal (Credit Officer)

**Status:** FinancialAppraisal  
**Who acts:** Credit Officer  
**SLA:** 72 hours

#### What is happening at this stage?

The Credit Officer reviews the financial viability of the NAMP application and prepares a structured financial appraisal report. Unlike the Corporate process, the NAMP appraisal focuses on the applicant's rental revenue capacity (since the equipment generates income that repays the loan) rather than on business profit margins.

---

#### Click "Financial Appraisal Decision"

The Credit Officer clicks **Financial Appraisal Decision** in the header. A detailed financial appraisal form appears.

---

#### Financial Appraisal Fields and Formulas

**Repayment Source Selection:**  
First, select how the loan will be repaid:
- **Primary Income**: The applicant's existing salary or business income covers the repayment (more common for individuals)
- **Rental / Hire Revenue**: The equipment generates rental income that covers the repayment (more common for tractors, harvesters)
- **Mixed**: A combination of both
- **Company Cash Flow**: The company's existing revenue covers the repayment (for Agro-Service Companies)

The repayment source you select determines which additional fields appear in the form.

---

**For Rental / Hire Revenue model (most common in NAMP):**

**Projected Monthly Rental Revenue:**  
Estimate the monthly income the equipment will generate from hiring it out.

```
Projected Monthly Rental Revenue = Daily Hire Rate × Working Days per Month

Example:
  Daily hire rate for a tractor = ₦25,000
  Expected working days per month = 18

  Projected Monthly Revenue = ₦25,000 × 18 = ₦450,000
```

**Utilisation Rate Assumption (%):**  
The assumed proportion of the time the equipment will be in active use (not idle, under repair, etc.).

```
Effective Monthly Revenue = Projected Monthly Rental Revenue × Utilisation Rate

Example:
  Projected Monthly Revenue = ₦450,000
  Utilisation Rate           = 75%

  Effective Monthly Revenue = ₦450,000 × 0.75 = ₦337,500
```

**Evidence of Demand Note:**  
Describe the evidence that this level of demand exists — for example, existing customers who have expressed interest in hiring the equipment, comparable equipment hire rates in the area, or a feasibility study from an agricultural extension officer.

---

**Monthly Disposable Income:**  
The amount available after essential living or operating costs to service the loan.

```
Monthly Disposable Income = Effective Monthly Revenue – Monthly Operating Costs
                          – Living Expenses (for individuals)
                          – Existing Loan Obligations

Example (individual with equipment):
  Effective Monthly Revenue  = ₦337,500
  Equipment maintenance cost = ₦30,000/month
  Living expenses            = ₦80,000/month
  Existing loan payment      = ₦15,000/month

  Monthly Disposable Income = ₦337,500 – ₦30,000 – ₦80,000 – ₦15,000
                            = ₦212,500
```

---

**DSCR — Debt Service Coverage Ratio (NAMP):**

```
DSCR = Monthly Disposable Income ÷ Monthly Loan Payment

Where:
  Monthly Loan Payment = The proposed monthly repayment on this NAMP facility

Example:
  Monthly Disposable Income = ₦212,500
  Monthly Loan Payment      = ₦150,000 (computed from loan amount, tenor, interest rate)

  DSCR = ₦212,500 ÷ ₦150,000 = 1.42x
```

A DSCR ≥ 1.25 indicates the applicant can comfortably service the loan. Below 1.0 indicates they cannot.

---

**LTV — Loan-to-Value Ratio (NAMP):**

In NAMP, the "collateral" is the equipment itself. The bank holds ownership of the equipment under the hire-purchase arrangement until the loan is fully repaid.

```
LTV (%) = Loan Amount ÷ Equipment Value × 100

Where:
  Loan Amount      = The amount the bank is financing
  Equipment Value  = The total purchase price of the equipment (cart total)

Example:
  Loan Amount      = ₦3,500,000
  Equipment Value  = ₦5,000,000 (of which ₦1,500,000 is the applicant's equity)

  LTV = ₦3,500,000 ÷ ₦5,000,000 × 100 = 70%
```

A 70% LTV means the bank finances 70% of the equipment; the applicant contributes 30% as equity. The equipment acts as its own security.

---

**Repayment Capacity Rating:**  
Select the overall qualitative rating:
- **Strong** — Excellent projected income, comfortable margins
- **Adequate** — Sufficient income with reasonable confidence
- **Marginal** — Income just covers repayments; limited buffer
- **Insufficient** — Projected income is unlikely to cover repayments

---

**Credit Bureau Summary:**  
Enter a brief summary of the bureau results for the applicant's BVN (and for all directors if Agro-Service Company). Note any delinquencies, active loans, or fraud risk flags.

---

**Equity Assessment Note:**  
Confirm that the equity amount shown in the application is realistic and that the applicant has the means to make the equity deposit before equipment deployment.

---

**Credit Officer Recommendation:**  
- **Pass** — Recommend the application advances to committee
- **Fail** — Recommend the application is declined

---

#### Submit the Decision

After completing all fields, click **Submit Decision**.

- If **Pass**: The application advances to the appropriate committee tier (determined by the loan amount and applicant category per the routing configuration).
- If **Fail**: The application moves to **FinancialDeclined** — a terminal status.

---

### 4.4 Stage 3 — Committee Circulation (Tiered)

**Status:** BranchCommitteeCirculation, ZonalCommitteeCirculation, RegionalCommitteeCirculation, or HOCommitteeCirculation (only one at a time)  
**Who acts:** Credit Officer (setup), Committee Members (voting)  
**SLA:** 120 hours

#### What is happening at this stage?

The application is reviewed by a committee at the appropriate tier. The tier is determined automatically by the routing configuration:

| If the applicant is... | And the loan amount is... | Then it goes to... |
|------------------------|--------------------------|-------------------|
| Youth or Women Agripreneur | ₦0 – ₦5M | Branch Committee |
| Youth or Women Agripreneur | ₦5M – ₦20M | Zonal Committee |
| Youth or Women Agripreneur | ₦20M – ₦50M | Regional Committee |
| Youth or Women Agripreneur | Above ₦50M | Head Office Committee |
| Agro-Service Company | ₦0 – ₦20M | Zonal Committee |
| Agro-Service Company | ₦20M – ₦100M | Regional Committee |
| Agro-Service Company | Above ₦100M | Head Office Committee |

The committee circulation process for NAMP is identical to the Corporate process: the Credit Officer sets up the review and starts voting, committee members vote, and the Credit Officer records the collective decision. See [Section 3.3](#33-stage-3--committee-circulation-committee-members) for the step-by-step process.

**Key NAMP-specific committee roles:**

| Committee Tier | Member Role |
|----------------|------------|
| Branch | BranchCommitteeMember |
| Zonal | ZonalCommitteeMember |
| Regional | RegionalCommitteeMember |
| Head Office | HOCommitteeMember |

Make sure users with the correct committee member role have been created and added to the appropriate committee in Admin → Committees.

---

### 4.5 Stage 4 — Ratification (Tier Manager)

**Status:** Ratification  
**Who acts:** Depends on the committee tier  
**SLA:** 48 hours

#### What is happening at this stage?

The committee's recommendation must be ratified by the appropriate senior officer, who acts as the final approval authority for NAMP loans at that tier.

| Committee Tier | Who Ratifies |
|----------------|-------------|
| Branch Committee | Branch Manager (role: BranchManager) |
| Zonal Committee | Zonal Manager (role: ZonalManager) |
| Regional Committee | Regional Manager (role: RegionalManager) |
| Head Office Committee | MD/CEO (role: MdCeo) |

The ratification process is identical to the Corporate process — the manager sees the application and the committee's recommendation, then clicks **Ratify Decision** or **Decline**.

---

#### Ratify the Decision

Click **Ratify Decision** in the header.

The system generates three documents automatically:
1. **Offer Letter** — The formal NAMP credit offer
2. **Hire-Purchase / Lease Agreement** — The equipment financing contract
3. **GPS Tracking Consent Form** — Authorisation for GPS monitoring

These use the document templates configured in Admin → NAMP Document Templates.

The application advances to **OfferGenerated**.

---

#### Decline Ratification

If the manager does not agree with the committee's recommendation, click **Decline**. Enter a mandatory reason. The application moves to **RatificationDeclined** — terminal.

---

### 4.6 Stage 5 — Offer Documents

**Status:** OfferGenerated → OfferAccepted  
**Who acts:** Loan Officer  
**SLA:** 168 hours (7 days) — if applicant does not respond within 7 days, the offer lapses

#### What is happening at this stage?

The three generated documents are shared with the applicant:
1. The **Offer Letter** summarises the approved credit terms
2. The **Lease/Hire-Purchase Agreement** is the legal contract for the equipment
3. The **GPS Consent Form** must be signed before deployment

---

#### Download and Share the Documents

On the application's Overview or Documents tab, download:
- The Offer Letter (PDF)
- The Lease/Hire-Purchase Agreement (PDF)
- The GPS Tracking Consent Form (PDF)

Share these with the applicant. They should review all three carefully, ideally with legal counsel.

---

#### Record the Applicant's Acceptance

Once the applicant has signed and returned the documents:

1. Upload the **signed offer letter** to the Documents tab under the category "Signed NAMP Offer Letter"
2. Click **Record Acceptance** in the header
3. Confirm the acceptance in the modal

The application automatically advances to **OfferAccepted** and then immediately to **LegalClearance**.

---

#### Offer Lapse

If the applicant does not respond within 7 days (168 hours), click **Lapse Offer** in the header. This moves the application to **OfferLapsed** — a terminal status. The application is permanently closed.

---

### 4.7 Stage 6 — Legal Clearance (Legal Officer)

**Status:** LegalClearance  
**Who acts:** Legal Officer  
**SLA:** 72 hours

#### What is happening at this stage?

Before the equipment can be deployed, the Legal Officer reviews the application to ensure all legal prerequisites are in order. This is a gate to catch any legal issues before the bank commits physical equipment to the applicant.

The Legal Officer reviews:
- The signed offer letter and hire-purchase agreement
- The applicant's CAC status (for Agro-Service Companies)
- Director information and bureau reports
- Any outstanding legal concerns raised during the earlier stages

---

#### Grant Legal Clearance

If everything is in order, click **Grant Clearance**.

Enter a brief note summarising the legal review (e.g., "All legal documents in order. No adverse findings. Cleared for pre-deployment.").

The application advances to **PreDeploymentVerification**.

---

#### Return to Loan Officer

If there are issues the Loan Officer must resolve (e.g., missing documents, a director's information needs to be corrected), click **Return to LO**.

Enter a note explaining what needs to be fixed. The application moves to **LegalReturned** and the Loan Officer receives a notification.

The Loan Officer reviews the note, makes the necessary corrections, and clicks **Resubmit to Legal**. The application returns to **LegalClearance** for the Legal Officer to re-review.

---

#### Decline

If there is a fundamental legal issue that cannot be resolved (e.g., the company is not legally registered, the applicant is under litigation), click **Decline**.

Enter a mandatory reason. The application moves to **LegalDeclined** — a terminal status.

---

### 4.8 Stage 7 — Pre-Deployment Verification (Deployment Officer)

**Status:** PreDeploymentVerification  
**Who acts:** Deployment Officer  
**SLA:** 48 hours

#### What is happening at this stage?

Before the bank releases the equipment to the applicant, four mandatory conditions must be verified and documented. All four must be satisfied before the application can advance to the Deployment stage. These gates protect the bank's interests — if any condition is not met, the bank should not deploy the equipment.

---

#### The Four Mandatory Gates

**Gate 1 — Equity Deposit Confirmed**  
*What it means:* The applicant must pay their equity contribution (their portion of the equipment cost) before the bank releases the equipment. This confirms they have financial skin in the game.

*Evidence required:* Upload the equity deposit receipt showing the payment has been received at the branch.

How to complete:
1. Confirm the applicant has paid the equity amount shown in the application
2. Obtain the payment receipt from the branch cashier or treasury
3. Upload the receipt to the Documents tab under category "Equity Deposit Receipt"
4. The checklist item will automatically be marked as satisfied once the document is uploaded

**Gate 2 — Lease / Hire-Purchase Agreement Signed**  
*What it means:* The applicant must have signed the equipment lease or hire-purchase agreement. This legally binds them to the repayment terms.

*Evidence required:* Upload the signed agreement.

How to complete:
1. Confirm the signed agreement has been received (it should have been returned with the offer acceptance)
2. If not already uploaded, upload it to the Documents tab under category "Lease Agreement"
3. The checklist item will automatically be marked as satisfied

**Gate 3 — GPS Tracking Consent Obtained**  
*What it means:* The applicant must have signed the GPS Tracking Consent Form. This legally authorises the bank to install a GPS device on the equipment and monitor its location throughout the loan tenor.

*Evidence required:* Upload the signed GPS consent form.

How to complete:
1. Confirm the signed GPS consent form has been received
2. Upload it to the Documents tab under category "GPS Consent Form"
3. The checklist item will automatically be marked as satisfied

**Gate 4 — NAIC Insurance In Place**  
*What it means:* The equipment must be insured under a valid Nigerian Agricultural Insurance Corporation (NAIC) policy. This protects the bank if the equipment is lost, stolen, or damaged.

*Evidence required:* Upload the NAIC insurance certificate showing the equipment is covered and the Bank of Agriculture is named as co-insured or loss payee.

How to complete:
1. Request the insurance certificate from the applicant (or arrange it if the bank's insurance department handles this)
2. Upload the certificate to the Documents tab under category "Insurance Certificate"
3. The checklist item will automatically be marked as satisfied

---

#### Complete Verification

Once all four gates are satisfied (indicated by green check marks on the checklist), click **Complete Verification** in the header.

Enter a verification note summarising the actions taken (e.g., "Equity deposit of ₦1.5M confirmed. Lease agreement signed on 28 July 2026. GPS consent obtained. NAIC policy number 2026/NAMP/00234 received, valid to 31 Dec 2027.").

The application advances to **Deployment**.

---

### 4.9 Stage 8 — Deployment (Deployment Officer)

**Status:** Deployment  
**Who acts:** Deployment Officer  
**SLA:** 168 hours (7 days)

#### What is happening at this stage?

The physical equipment is delivered to the applicant and a GPS tracking device is installed and activated.

---

#### Confirm Equipment Delivery

Once the equipment has been delivered to the applicant's location:
1. Document the delivery — photograph the handover if possible
2. Confirm the equipment serial number matches what was financed
3. Confirm the applicant has physically received the equipment

---

#### Confirm GPS Activation

After the GPS device is installed and activated:
1. Confirm the device is online and tracking (check the GPS monitoring portal)
2. Note the GPS device serial number

---

#### Record Deployment Completion

Click **Confirm Deployment** in the header.

In the modal:
- Confirm equipment delivery
- Confirm GPS is activated
- Enter any relevant notes (equipment serial numbers, GPS device ID, delivery location, delivery date)

Click **Confirm**.

The application advances to **Active**.

---

### 4.10 Stage 9 — Active and Closed

**Status:** Active → Closed  
**Who acts:** PAYS (automated) / Loan Officer (monitoring)

#### Active

The loan is now live. The equipment is with the applicant and the GPS is tracking it. The **PAYS repayment system** handles the monthly repayment collection — applicants make their repayments through the PAYS portal, which automatically debits the BOA account or uses other payment channels.

**Monitoring via the Loan Account Tab:**  
The Loan Account tab appears on the application once it is in Active status. It shows:
- Core Banking loan account details (fetched from Fineract)
- Outstanding balance
- Amount paid to date
- Next repayment date
- Repayment history

The Loan Officer should monitor this tab periodically to identify any early signs of repayment difficulty.

---

#### Closed

When the final repayment is made and the full loan balance plus interest is settled, PAYS sends a callback to CRMS marking the application as **Closed**.

Closed is a terminal status. The equipment ownership transfers to the applicant at this point (per the hire-purchase agreement).

---

### 4.11 NAMP Status Summary

```
Received                → [Staging queue; awaiting recall]
RecallPending           → [LoanOfficer: Recall] → Draft
Draft                   → [LoanOfficer: Submit for Appraisal] → FinancialAppraisal
FinancialAppraisal      → [CreditOfficer: Pass] → Tier Committee Circulation
                          [CreditOfficer: Fail] → FinancialDeclined ■
*CommitteeCirculation   → [Committee: Approved] → Ratification
                          [Committee: Rejected] → *CommitteeDeclined ■
Ratification            → [Manager: Ratify] → OfferGenerated (3 docs auto-generated)
                          [Manager: Decline] → RatificationDeclined ■
OfferGenerated          → [LoanOfficer: Record Acceptance] → LegalClearance
                          [LoanOfficer: Lapse Offer] → OfferLapsed ■
LegalClearance          → [LegalOfficer: Grant Clearance] → PreDeploymentVerification
                          [LegalOfficer: Return to LO] → LegalReturned (loop)
                          [LegalOfficer: Decline] → LegalDeclined ■
LegalReturned           → [LoanOfficer: Resubmit to Legal] → LegalClearance
PreDeploymentVerification → [DeploymentOfficer: Complete Verification] → Deployment
Deployment              → [DeploymentOfficer: Confirm Deployment] → Active
Active                  → [PAYS: Repayment complete] → Closed ■

* "Tier Committee Circulation" = one of:
  BranchCommitteeCirculation, ZonalCommitteeCirculation,
  RegionalCommitteeCirculation, HOCommitteeCirculation

* "Tier Committee Declined" = one of:
  BranchCommitteeDeclined, ZonalCommitteeDeclined,
  RegionalCommitteeDeclined, HOCommitteeDeclined

■ = Terminal status
```

---

## 5. User Roles Quick Reference

This section maps every role to the stages and actions it unlocks. As of the unified actor framework, the same roles apply across both Corporate and NAMP loans — there is no longer a separate role list per process. A Legal Officer, BranchManager, or Disbursement Officer performs the same function regardless of which loan type they are working on.

---

### 5.1 Unified Role List

| Role | System Name | Function |
|------|-------------|----------|
| System Admin | `SystemAdmin` | Full access to all admin configuration, user management, and all application actions |
| Loan Officer | `LoanOfficer` | Creates Corporate applications; recalls and manages NAMP applications; records customer/applicant acceptance |
| Credit Officer | `CreditOfficer` | Manages credit appraisal (Corporate) and financial appraisal (NAMP); drives committee circulation on both |
| Legal Officer | `LegalOfficer` | Parallel legal review (Corporate CreditReview); Security Perfection (Corporate); Legal Clearance (NAMP) |
| Risk Manager | `RiskManager` | Read-only access to all applications; can replace committee members |
| GM Finance | `GMFinance` | Senior disbursement authority; shares Disbursement action with Disbursement Officer |
| Disbursement Officer | `DisbursementOfficer` | Prepares disbursement memo and books the loan in core banking (Corporate and Retail) |
| Deployment Officer | `DeploymentOfficer` | Pre-deployment verification and physical equipment deployment with GPS activation (NAMP) |
| Committee Member | `CommitteeMember` | Votes on Corporate committee reviews (generic, covers all Corporate committee tiers) |
| Branch Committee Member | `BranchCommitteeMember` | Votes on NAMP Branch-tier committee reviews |
| Zonal Committee Member | `ZonalCommitteeMember` | Votes on NAMP Zonal-tier committee reviews |
| Regional Committee Member | `RegionalCommitteeMember` | Votes on NAMP Regional-tier committee reviews |
| HO Committee Member | `HOCommitteeMember` | Votes on NAMP Head Office-tier committee reviews |
| Branch Manager | `BranchManager` | Ratifies Branch Credit Committee decisions — Corporate and NAMP |
| Zonal Manager | `ZonalManager` | Ratifies Zonal Credit Committee decisions — Corporate and NAMP |
| Regional Manager | `RegionalManager` | Ratifies Regional Credit Committee decisions — Corporate and NAMP |
| MD/CEO | `MdCeo` | Ratifies Head Office Credit Committee decisions — highest authority on both processes |
| Auditor | `Auditor` | Read-only access to all applications, documents, and audit trails |

**One person, two roles:** A staff member who handles both NAMP equipment disbursement and Corporate loan disbursement can hold both `DeploymentOfficer` and `DisbursementOfficer` simultaneously. The system grants the relevant buttons based on which role is applicable to the loan type on screen.

---

### 5.2 Role-to-Stage Reference

---

**Loan Officer**

| Process | Stage | Actions |
|---------|-------|---------|
| Corporate | Draft | Create new applications via the 3-step wizard. Add/edit directors, signatories, financial statements, collateral, guarantors, documents. Delete the application before submission. Submit for credit review. |
| Corporate | OfferGenerated | Upload signed offer letter and signed KFS. Record the customer's formal acceptance (date, method, KFS acknowledged). |
| NAMP | Staging queue | View incoming applications from the PAYS portal. Recall an application, which creates the NampApplication record in Draft. |
| NAMP | Draft | Review and verify applicant data from PAYS (name, BOA account, BVN, NIN, equipment, amounts). For Agro-Service Companies: review CAC profile, enter director BVNs. Upload documents (Financial Model required). Submit for appraisal. |
| NAMP | OfferGenerated | Download and share the three generated documents. Upload the signed offer letter. Record the applicant's acceptance. Lapse the offer if the applicant does not respond within the SLA. |
| NAMP | LegalReturned | Resubmit to Legal after resolving the issues flagged by the Legal Officer. |
| Both | Any | Download the Loan Pack PDF from CreditReview stage onwards (Corporate). |

---

**Credit Officer**

| Process | Stage | Actions |
|---------|-------|---------|
| Corporate | CreditReview | Verify bank statements and documents. Re-run bureau checks. Save/update the Credit Appraisal (DSCR, Leverage, Current Ratio, LTV, Capacity Rating, Recommendation, Notes, Memo). Send to committee once legal review is complete and appraisal is saved. Return to Draft. Reject outright (terminal). |
| Corporate | CommitteeCirculation | Set up committee review. Start voting. Record the collective committee decision (Approved / Conditions / Rejected / Deferred / Escalated) with approved amount, tenor, and rate. |
| NAMP | FinancialAppraisal | Complete the financial appraisal: repayment source, projected rental revenue, utilisation rate, disposable income, DSCR, LTV, equity assessment, bureau summary, capacity rating, recommendation. Submit Pass (advances to committee) or Fail (terminal). |
| NAMP | CommitteeCirculation | Set up committee review for the correct tier. Start voting. Record the committee decision. |

---

**Legal Officer**

| Process | Stage | Actions |
|---------|-------|---------|
| Corporate | CreditReview | Parallel legal review — mark complete with a note. Prerequisite: Credit Officer cannot send to committee until this is done. Return to Draft before completing if a document is wrong or missing. |
| Corporate | SecurityPerfection | Upload security documents linked to collateral items. Mark Security Perfection complete (advances to Disbursement). Return to OfferAccepted if issues arise. |
| NAMP | LegalClearance | Review the full application. Grant clearance (advances to PreDeploymentVerification). Return to Loan Officer with a note (moves to LegalReturned). Decline (terminal). |

---

**Disbursement Officer**

The Disbursement Officer handles the final cash release on Corporate (and Retail) loans. This is the renamed successor to the former "Operations" role.

| Process | Stage | Actions |
|---------|-------|---------|
| Corporate | OfferGenerated | Record the customer's acceptance (same access as Loan Officer at this stage). |
| Corporate | Disbursement | Work through the disbursement checklist — upload documents for conditions precedent, manually mark non-document items as met. Disburse the loan: enter the Core Banking loan ID and confirm (terminal — advances to Disbursed). Return to SecurityPerfection if a pre-disbursement issue is found. |

---

**Deployment Officer**

The Deployment Officer handles physical equipment release and GPS activation for NAMP loans.

| Process | Stage | Actions |
|---------|-------|---------|
| NAMP | PreDeploymentVerification | Review all four mandatory checklist gates (equity deposit, signed agreement, GPS consent, NAIC insurance). All four must show uploaded evidence documents before the Complete Verification button activates. Confirm completion — advances to Deployment. |
| NAMP | Deployment | Confirm physical equipment delivery, confirm GPS is installed and active. Record deployment details (equipment serial number, GPS device ID, delivery date). Confirm — advances to Active. |

---

**GM Finance**

| Process | Stage | Actions |
|---------|-------|---------|
| Corporate | Disbursement | Disburse the loan — same access as Disbursement Officer. Acts as the senior funds-release authority where the bank's process requires a higher sign-off before funds leave. |

---

**Tier Managers (BranchManager / ZonalManager / RegionalManager / MdCeo)**

All four tier manager roles perform the same function — ratification — but at different authority levels. The appropriate manager acts based on the loan amount and which committee tier reviewed the application.

| Process | Stage | Actions |
|---------|-------|---------|
| Corporate | Ratification | Review the committee decision, approved terms, credit appraisal, and bureau reports. Ratify — locks in the approved terms, auto-generates the Offer Letter PDF, advances to OfferGenerated. Decline — terminal, must enter a reason (RatificationDeclined). |
| NAMP | Ratification | Review the committee recommendation. Ratify — auto-generates the Offer Letter, Lease/Hire-Purchase Agreement, and GPS Consent Form, advances to OfferGenerated. Decline — terminal (RatificationDeclined). |

**Which manager ratifies:**

| Committee Tier | Corporate | NAMP |
|----------------|-----------|------|
| Branch | Branch Manager | Branch Manager |
| Zonal | Zonal Manager | Zonal Manager |
| Regional | Regional Manager | Regional Manager |
| Head Office | MD/CEO | MD/CEO |

---

**Committee Members**

| Role | Stage | Actions |
|------|-------|---------|
| CommitteeMember | CommitteeCirculation (Corporate) | Vote (Approve / Reject / Abstain). Add comments (Public or Private). One vote per member, not changeable after submission. Voting opens only after the Credit Officer starts the voting round. |
| BranchCommitteeMember | BranchCommitteeCirculation (NAMP) | Same as CommitteeMember above, for Branch-tier NAMP applications. |
| ZonalCommitteeMember | ZonalCommitteeCirculation (NAMP) | Same as above, for Zonal-tier. |
| RegionalCommitteeMember | RegionalCommitteeCirculation (NAMP) | Same as above, for Regional-tier. |
| HOCommitteeMember | HOCommitteeCirculation (NAMP) | Same as above, for Head Office-tier. |

---

**Risk Manager**

| Process | Stage | Actions |
|---------|-------|---------|
| Both | CommitteeCirculation | Replace a committee member (swap one member for another). Also available to SystemAdmin. |
| Both | Any | Read-only view of all application data, bureau reports, audit trail, and workflow history. No action buttons at any other stage. |

---

**Auditor**

| Process | Stage | Actions |
|---------|-------|---------|
| Both | Any | Read-only access to all applications, documents, workflow history, and audit trails. No action buttons shown at any stage. |

---

### 5.3 Roles No Longer Active

The following roles existed in earlier versions of the workflow but have been retired. Users assigned these roles will see the application list but will find no action buttons on any live application.

| Role | What it was for | Current status |
|------|-----------------|----------------|
| **BranchApprover** | Approved or returned applications at the BranchReview stage, which no longer exists in the current flow. | No active stage. |
| **HOReviewer** | Reviewed applications at the HOReview stage before committee, which no longer exists. | No active stage. |
| **HeadOfLegal** | Countersigned the legal opinion at the SecurityApproval stage (removed). In the current flow the Legal Officer completes security perfection directly. | No active stage. |
| **FinalApprover** | Sole ratification authority for Corporate loans in the original flow. Now replaced by the tiered BranchManager / ZonalManager / RegionalManager / MdCeo ratification model, which applies to both Corporate and NAMP. | Being phased out. Existing users should be re-assigned an appropriate tier manager role. |

---

*End of Tutorial*
