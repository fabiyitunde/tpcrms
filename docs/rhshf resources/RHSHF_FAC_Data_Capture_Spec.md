# RH-SHF — FAC-Originated Data Capture (Directors, Guarantors, Collateral)

**Status:** Approved direction (2026-10-07) · **Owner:** CRMS team · **Related:** RHSHF_Entities_Workflow_Design.md

## 1. Problem

Three categories of case data are currently **entered by CRMS officers** rather than supplied by the FAC:

| Data | Entered today by | In the portal payload? |
|---|---|---|
| Directors + **BVN** | Credit Officer — CAC fetch, then BVNs typed by hand | No |
| Guarantors | Credit Officer — manual entry | No |
| Collateral | Legal Officer — recorded fresh at Legal Clearance | No |

The officer who is meant to **verify** the application is also **sourcing and entering** its data. That is a segregation-of-duties weakness: it invites manipulation, gives no record of what the FAC actually asserted vs what the bank verified, and leaves BVNs (which drive the credit-bureau checks) dependent on an officer's keyboard.

## 2. Principle

**The FAC originates; CRMS verifies.** The applicant supplies the facts and the supporting documents during profiling; CRMS officers cross-check that evidence against trusted systems (CAC, core banking, credit bureau) and **act** on it — they do not source or invent it.

## 3. Decisions

1. **Capture point = the CRMS-hosted profiling form** (`Pages/Rhshf/Profiling.cshtml`, the `/rhshf/profiling` token flow). It is under our control and needs no change from the external portal team. The portal payload (Door A) *may later* send the same data — the webhook contract will accept it additively when the portal team is ready, but that is **not** a dependency for this work.
2. **Director BVN: FAC-supplied, CRMS cross-checked.** The FAC enters each director's BVN; CRMS cross-references against **CAC** (identity) and **core banking** (BVN) and flags mismatches. (CAC returns director identity but **no BVN**; `ICoreBankingService.GetDirectorsAsync` / `GetSignatoriesAsync` return directors/signatories **with BVN** for the BOA account — but that is the account mandate, not necessarily the full statutory board, so it assists rather than replaces the FAC's declaration.)
3. **Officers become verifiers.** The Directors / Guarantors / Collateral staff tabs change from *create* surfaces to *review-and-approve* surfaces. Editing stays available **only to correct** a bad entry — the default expectation is that the FAC supplied it.
4. **Collateral is declared at profiling** (type, value, title/reference + uploaded evidence). The Legal Officer **verifies and perfects** it rather than recording it from scratch at Legal Clearance.

## 4. Target state by data type

### Directors (+ BVN)
- **Profiling:** a Directors step at the **Credit-Bureau** profiling stage (where BVNs are needed). The FAC adds each director: full name, **BVN** (11 digits), optional email/phone/shareholding/chairman. A **"Pull from core banking"** assist pre-fills directors/signatories *with BVN* from the BOA account; the FAC confirms/edits.
- **Staff (Directors tab):** shows each declared director with cross-check badges — **CAC match** (identity present in the CAC record), **CBS match** (BVN matches a core-banking director/signatory), **BVN present**. The Credit Officer verifies and may correct. Bureau checks run on every director with a BVN (already built).

### Guarantors
- **Profiling:** a Guarantors step. The FAC declares each guarantor (individual with BVN, or corporate with RC), relationship, guarantee amount, and **uploads the guarantee document(s)**.
- **Staff (Guarantors tab):** verify + run bureau (already built). Editing only to correct.

### Collateral
- **Profiling:** a Collateral step. The FAC declares each instrument (bank guarantee / NIRSAL CRG / legal mortgage) with its value and title/reference, and **uploads evidence**.
- **Staff (Collateral tab / Legal Officer):** verify and perfect (perfection status for legal mortgages). No fresh data entry — the records already exist.

## 5. Phasing

- **Phase 1 — Directors + BVN at profiling** (highest value: feeds bureau). Profiling capture step + core-banking pre-fill + Directors tab becomes a verify surface.
- **Phase 2 — Guarantors at profiling.** Capture step + evidence upload.
- **Phase 3 — Collateral at profiling.** Capture step + evidence upload; Legal shifts to verify/perfect.
- **Deferred — Portal payload ingestion.** Accept directors/guarantors/collateral from the webhook payload when the portal team sends them (additive to the contract).

## 6. Out of scope / notes

- No change to the credit-decision stages or maker-checker rules — only *where the data originates*.
- The domain entities already exist (`RhshfDirector`, `RhshfGuarantor`, `RhshfCollateral`); this is largely a capture-surface relocation + cross-check presentation, not new aggregates.
- Mismatches (FAC-declared BVN vs CBS/CAC) are **surfaced, not auto-blocked** — the officer decides, consistent with the rest of the RH-SHF flow.

## 7. Open items

- Exact profiling sub-stage layout (new dedicated steps vs. extending existing Company/Bureau/Documents steps) — settled per phase during build.
- Whether a director with no BVN should block profiling submission, or just warn (lean: warn, mirroring the current staff-side behaviour).
