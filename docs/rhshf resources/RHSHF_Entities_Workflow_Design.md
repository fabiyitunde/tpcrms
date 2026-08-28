# RH-SHF Credit-Profiling — Entities & Workflow Design

**Status:** Draft — not yet implemented
**Scope:** CRMS side of the RH-SHF (Renewed Hope Dry Season) input-financing integration described in `docs/namp resources/crmsintegration.html`
**Isolation constraint:** RH-SHF is a **separate loan track from NAMP**, already in production. Nothing here shares entities, enums, tables, or workflow code with NAMP. Where the same *kind* of infrastructure is reused (webhook pattern, token signing, bureau checks), it is reused as independent code, never by extending NAMP's own classes. See "Non-Goals" below.

---

## 1. Overview

RH-SHF FACs (Farmer Aggregator Companies) apply for input financing (seeds, fertilizer, etc.) for a given programme + farming session. The portal certifies a FAC's consolidated EOP and hands it to CRMS for a credit check. CRMS owns everything from that hand-off through decision:

```
Portal certifies EOP ──► CRMS creates case ──► FAC completes profiling on CRMS ──► CRMS decides ──► CRMS notifies portal
```

This document defines the **domain model** (entities/enums) and the **status/stage workflow** that back the four integration touchpoints (submit, profiling form, outcome webhook, status query).

---

## 2. Module Layout

New, independent module — mirrors CRMS's existing layering, own namespace, no cross-references into `CRMS.Domain.Aggregates.Namp` or `CRMS.Domain.Enums.NampEnums`:

```
CRMS.Domain/Aggregates/Rhshf/
  RhshfCreditProfile.cs        (aggregate root — profiling + the internal Appraisal/RiskReview/
                                 Ratification/Disbursement records, see §3)
  RhshfEopLine.cs               (child entity)
  RhshfIssuedToken.cs            (child entity)
  RhshfCallbackAttempt.cs        (child entity)
  RhshfCommitteeReview.cs        (own aggregate root, own table — mirrors NampCommitteeReview's
                                   pattern of NOT reusing the generic CommitteeReview, which is
                                   hard-FK'd to Corporate's LoanApplicationId)
  RhshfOffer.cs                  (own aggregate root — offer generation + FAC acceptance)
  RhshfLegalClearance.cs         (own aggregate root)
CRMS.Domain/Enums/RhshfEnums.cs
CRMS.Domain/Interfaces/IRhshfCreditProfileRepository.cs
CRMS.Domain/Interfaces/IRhshfCommitteeReviewRepository.cs
CRMS.Domain/Interfaces/IRhshfOfferRepository.cs
CRMS.Domain/Interfaces/IRhshfLegalClearanceRepository.cs

CRMS.Application/Rhshf/Commands/...
CRMS.Application/Rhshf/Queries/...
CRMS.Application/Rhshf/DTOs/...

CRMS.Infrastructure/Persistence/Configurations/Rhshf/...
CRMS.Infrastructure/ExternalServices/Rhshf/
  IRhshfCallbackService.cs / RhshfCallbackService.cs   (outbound webhook, own retry logic)
  IRhshfTokenService.cs / RhshfTokenService.cs           (may call the same generic JWT signing
                                                            helper the platform already uses —
                                                            not NAMP's token code)

CRMS.API/Controllers/RhshfSubmissionController.cs   (4.1 submit, 4.5 status, 4.6 refresh — inbound from portal)
CRMS.Web.<Public>/Pages/Rhshf/Profile.cshtml          (4.3 profiling form — see hosting decision below)
```

Generic, non-NAMP-specific infrastructure it's fine to call into as-is: credit bureau services, CAC lookup, branch/location resolution, the JWT signing primitive, notification templating. None of these are NAMP's own domain code.

---

## 3. Domain Model

### 3.1 `RhshfCreditProfile` (aggregate root) — "the case"

| Property | Type | Notes |
|---|---|---|
| `Id` | Guid | Internal PK |
| `Reference` | string | CRMS-generated, e.g. `RHSHF-2026-000123` — **distinct prefix from NAMP's numbering**, avoids any collision/confusion |
| `SubmissionId` | Guid | Portal's id — idempotency key for the submit endpoint |
| `ProgrammeCode` / `ProgrammeName` | string | e.g. `RH-SHF-DRY-2026` |
| `SessionCode` / `SessionName` | string | Farming season |
| `FacId` | Guid | Portal's FAC id |
| `CompanyName`, `RcNumber`, `Tin` | string | Company identity |
| `BoaAccountNumber` | string | Used to resolve the FAC to a branch/office (own resolution logic — do not call into `NampStagingRecord`'s resolver) |
| `ContactEmail`, `ContactPhone` | string | Default profiling-link recipient |
| `State`, `Lga` | string | Location |
| `TotalEopValue` | decimal | The amount being profiled for |
| `Currency` | string | `NGN` |
| `FarmerCount` | int? | Context only |
| `CallbackUrl` | string | Where outcome webhooks are sent |
| `CertifiedByAdmin`, `CertifiedAt` | string / DateTime | From portal metadata |
| `ResolvedBranchId` / `ResolvedOfficeId` | Guid? | Resolved from `BoaAccountNumber` |
| `Status` | `RhshfCaseStatus` | See §4 |
| `CurrentStage` | `RhshfProfilingStage?` | FAC-facing form stage (null once past profiling) |
| `DecisionOutcome` | `RhshfDecisionOutcome?` | Set on decision |
| `ApprovedAmount` | decimal? | Must equal `TotalEopValue` on `Approved` (see §6 #1) — enforced strictly, no partial-approval amount in v1 |
| `DecisionReasons` | string[] | Populated on decline / info-required |
| `DecidedAt`, `DecidedBy` | DateTime? / string | Audit of the decision |
| `RawSubmissionPayload` | string | Raw JSON as received — mirrors the "staging" pattern for traceability, without touching `NampStagingRecord` |
| `ReceivedAt`, `UpdatedAt` | DateTime | Timestamps |

Child collection:

### 3.2 `RhshfEopLine` (child entity)

| Property | Type |
|---|---|
| `Commodity` | string |
| `QuantityKg` | decimal |
| `UnitPricePerKg` | decimal |
| `LineValue` | decimal |

Context-only breakdown from the submit payload; not independently workflow-driven.

### 3.3 `RhshfDirector` — **deferred, not in v1** (see §6 #3)

The submit payload (§4.1 of the brief) carries only company-level identity (RC/TIN/BOA account) — no director/shareholder data, unlike NAMP's Layer-2 KYC. Building a director/BVN entity speculatively is scope creep. If the credit team later decides company-level KYC isn't sufficient, add `RhshfDirector` (own entity/table, never `NampDirector`) at that point — same shape of concern as NAMP's director layer, but independent code.

### 3.4 `RhshfIssuedToken` (child entity)

The reference token (§4.2 of the brief) is a stateless signed JWT — this table exists only to enforce **single-use / replay prevention**, not to store progress.

| Property | Type | Notes |
|---|---|---|
| `Jti` | string | Token's unique id claim |
| `RhshfCreditProfileId` | Guid | FK — binds token to one case |
| `IssuedAt`, `ExpiresAt` | DateTime | |
| `ConsumedAt` | DateTime? | Set on first use — token authenticates only the form's initial page load (see §6 #5); the rest of the multi-stage session runs on a normal first-party CRMS session cookie |

### 3.5 `RhshfCallbackAttempt` (child entity)

Append-only delivery log for the outbound webhook (§4.4), supporting the idempotent-retry-with-backoff requirement.

| Property | Type | Notes |
|---|---|---|
| `EventId` | string | Unique per logical event — portal de-dupes on this |
| `AttemptNumber` | int | |
| `SentAt` | DateTime | |
| `ResponseStatusCode` | int? | |
| `Succeeded` | bool | |
| `NextRetryAt` | DateTime? | Null once delivered or retries exhausted |

### 3.6 The post-profiling decision pipeline (supersedes the earlier flat maker-checker pair)

Once the FAC finishes profiling, the case runs through the same shape of multi-actor lifecycle every other loan track in this bank already uses: **Credit Officer appraisal → Risk Officer review → Committee vote → Final Approver ratification (+ offer generation) → FAC accepts the offer → Legal clearance → Disbursement Officer books the loan in Core Banking.** This is a materially bigger pipeline than the earlier two-actor "Recommend/Approve" model and replaces it outright — `RhshfReviewCycle` as previously specified is dropped in favour of `CycleNumber` living on each stage record below, so a full pass (or a re-run after an `InfoRequired`) is traceable end to end.

Everything below is scoped to a `CycleNumber` (int, starts at 1, increments only if the case is sent back to the FAC via `InfoRequired` and later re-reaches the pipeline — §6 #7/#8's "fresh cycle" rule still applies, now across the whole pipeline rather than one recommend/approve pair).

#### `RhshfAppraisal` (child entity of `RhshfCreditProfile`)

| Property | Type | Notes |
|---|---|---|
| `CycleNumber` | int | |
| `CreditOfficerId` | Guid | |
| `AppraisedAt` | DateTime | |
| `Outcome` | `Proceed` \| `ReturnToFac` \| `Decline` | |
| `Notes` | string | |

#### `RhshfRiskReview` (child entity of `RhshfCreditProfile`)

| Property | Type | Notes |
|---|---|---|
| `CycleNumber` | int | |
| `RiskOfficerId` | Guid | **Must differ from that cycle's `CreditOfficerId`** — enforced in the domain |
| `ReviewedAt` | DateTime | |
| `Outcome` | `Cleared` \| `ReturnToFac` \| `Decline` | |
| `Notes` | string | |

#### `RhshfCommitteeReview` (own aggregate root — own table, own repository)

Same shape of concern as `CommitteeReview`/`NampCommitteeReview`, but its own independent entity — `CommitteeReview.LoanApplicationId` is a hard, non-nullable FK into Corporate's `LoanApplication` aggregate, so it isn't reusable as-is (confirmed by reading the class). NAMP already solved this the same way: its own `NampCommitteeReview` rather than extending the generic one.

| Property | Type | Notes |
|---|---|---|
| `RhshfCreditProfileId` | Guid | FK, not an embedded child — own aggregate |
| `CycleNumber` | int | |
| `Members` | list of `{UserId, Vote, VotedAt, Comment}` | |
| `RequiredVotes`, `MinimumApprovalVotes` | int | Quorum/majority, same concept as the generic module |
| `FinalDecision` | `Approved` \| `Rejected` \| `Deferred` \| `ReturnToFac` | |
| `DecidedAt` | DateTime? | |

Whether this needs NAMP-style **value-based tiers** (Branch/Zonal/Regional/HO) or a single flat committee is open — see §6 #11.

#### `RhshfRatification` (child entity of `RhshfCreditProfile`)

| Property | Type | Notes |
|---|---|---|
| `CycleNumber` | int | |
| `FinalApproverId` | Guid | Distinct role from committee members — ratifies the committee's decision, mirroring `Roles.RatifierRoleForTier` |
| `RatifiedAt` | DateTime | |
| `Outcome` | `Ratified` \| `ReturnToFac` \| `Declined` | |
| `ApprovedAmount` | decimal? | Must equal `TotalEopValue` when `Ratified` (§6 #1) |

`Ratified` triggers offer generation (`RhshfOffer`, below) — it does **not** by itself fire the outcome webhook to the portal; see §6 #9 on why.

#### `RhshfOffer` (own aggregate root)

| Property | Type | Notes |
|---|---|---|
| `RhshfCreditProfileId` | Guid | |
| `GeneratedAt` | DateTime | |
| `OfferDocumentPath` | string | Generated offer letter (same shape of concern as NAMP's offer-letter generation) |
| `Status` | `Generated` \| `AwaitingFacResponse` \| `Accepted` \| `Rejected` \| `Expired` | |
| `FacRespondedAt` | DateTime? | |

How the FAC actually accepts the offer is a real, unresolved piece of the design — see §6 #10.

#### `RhshfLegalClearance` (own aggregate root)

| Property | Type | Notes |
|---|---|---|
| `RhshfCreditProfileId` | Guid | |
| `LegalOfficerId` | Guid | |
| `ClearedAt` | DateTime | |
| `Outcome` | `Granted` \| `Returned` \| `Declined` | Mirrors NAMP's own Legal Clearance stage shape (grant/return/decline) |
| `Comments` | string | |

`Returned` routes back to `RhshfRatification` for the Final Approver's attention, not all the way back to Appraisal (§6 #12) — a legal issue is not a re-appraisal of the credit itself.

#### `RhshfDisbursement` (child entity of `RhshfCreditProfile`)

| Property | Type | Notes |
|---|---|---|
| `DisbursementOfficerId` | Guid | |
| `BookedAt` | DateTime | |
| `FineractLoanAccountNumber` | string | Same shape of concern as NAMP's Fineract-booking fields — own independent integration call, not shared code |
| `DisbursedAmount` | decimal | |
| `Status` | `Booked` \| `Failed` | |

This resolves the earlier open question in §7 (old draft) about whether RH-SHF becomes a real Fineract-booked facility: **yes** — the Disbursement Officer books it, same as NAMP.

### Routing

`RhshfCreditProfile.ResolvedBranchId`/`ResolvedOfficeId` (§3.1) are not just informational — they're what puts the case into the right **branch-scoped queue** once it reaches `UnderReview`, the same visibility pattern (`VisibilityScope.Branch`) every other CRMS module already uses. Without this, "who sees the case" is undefined.

---

## 4. Enums (`RhshfEnums.cs`)

### `RhshfCaseStatus` (matches §5 of the integration brief)

```
Received → ProfilingPending → ProfilingInProgress → UnderReview → { Approved | Declined | InfoRequired }
                                                    ▲                                          │
                                                    └──────────── InfoRequired ─────────────────┘
                                                      (status resets to ProfilingInProgress,
                                                       CurrentStage set to InfoRequiredStage,
                                                       a fresh review cycle is required next time
                                                       it reaches UnderReview — §6 #7, #8)
                                                                 ↘ Expired / Cancelled (from any non-terminal state)
```

### `RhshfDecisionOutcome`
`Approved` · `Declined` · `InfoRequired`

### `RhshfInternalStage` (new — tracks position inside the post-profiling pipeline, §3.6)

`Appraisal → RiskReview → CommitteeVoting → Ratification → OfferGenerated → AwaitingOfferAcceptance → LegalClearance → Disbursement → Completed`

This is **internal-only** granularity behind the single external `UnderReview` status (§5) — the brief's status vocabulary doesn't distinguish these sub-stages, and there's an open question (§6 #9) about whether the portal ever needs to see any of it beyond the final outcome.

### `RhshfProfilingStage` (v1 — FAC-facing steps inside the CRMS-hosted form)

| Order | Stage | Purpose |
|---|---|---|
| 1 | `CompanyVerification` | Confirm RC/TIN/BOA account match portal data |
| 2 | `CreditBureauCheck` | Run bureau checks (reuses existing generic bureau services) |
| 3 | `EopReview` | FAC confirms the commodity/EOP breakdown |
| 4 | `SupportingDocuments` | Upload required documents |
| 5 | `ReviewAndSubmit` | Final confirmation, locks the form |

Director/shareholder capture dropped per §6 #3. This is an internal-only decision — the portal only sees `{ current, index, total }`, so nothing here needs to go back to them.

---

## 5. Workflow / State Machine

**External status** (`RhshfCaseStatus`, what the portal sees via §4.4/§4.5) stays coarse — it does not expose the internal pipeline's sub-stages:

```
  submit ──► Received ──► ProfilingPending ──► ProfilingInProgress ──(5 stages, §4)──► UnderReview ──► { Approved | Declined }
                                                     ▲                                      │
                                                     └────────────── InfoRequired ──────────┘
                                                       (any internal stage can send the case back
                                                        to the FAC — status → ProfilingInProgress,
                                                        CurrentStage → InfoRequiredStage, next pass
                                                        is a new CycleNumber — §6 #7/#8)

  Any non-terminal state ──(lapsed)──► Expired        Any non-terminal state ──(withdrawn)──► Cancelled
```

**Internal pipeline** (`RhshfInternalStage`, §4) — everything that happens while the external status is `UnderReview`:

```
UnderReview
   │
   ▼
Appraisal ──(Credit Officer)──► Proceed ──► RiskReview ──(Risk Officer, different person)──► Cleared
   │                                                                                              │
   ├─(ReturnToFac/Decline)──► back to FAC / Declined                                              ▼
   │                                                                                       CommitteeVoting
   │                                                                                              │
   │                                              ┌─(Rejected/Deferred/ReturnToFac)◄──────────────┤
   │                                              ▼                                       (Approved, quorum met)
   │                                     back to FAC / Declined                                    │
   │                                                                                              ▼
   │                                                                                        Ratification
   │                                                                     ┌─(ReturnToFac/Declined)──┤
   │                                                                     ▼                  (Ratified,
   │                                                            back to FAC / Declined    ApprovedAmount == TotalEopValue)
   │                                                                                              │
   │                                                                                              ▼
   │                                                                                       OfferGenerated
   │                                                                                              │
   │                                                                                              ▼
   │                                                                                 AwaitingOfferAcceptance
   │                                                                  ┌─(FAC rejects/offer expires)──┤
   │                                                                  ▼                    (FAC accepts)
   │                                                         Declined / Cancelled                  │
   │                                                                                                ▼
   │                                                                                        LegalClearance
   │                                                          ┌─(Returned → back to Ratification)───┤
   │                                                          │                    (Declined)   (Granted)
   │                                                          ▼                        │              ▼
   │                                                   Ratification (again)     Declined      Disbursement
   │                                                                                                   │
   │                                                                                                   ▼
   │                                                                                              Completed
   │                                                                                          (→ external Approved,
   │                                                                                             see §6 #9)
   ▼
(any ReturnToFac from Appraisal/RiskReview/Committee/Ratification: external status → ProfilingInProgress,
 InfoRequiredStage set, new CycleNumber on the next pass through this pipeline)
```

Notes on the diagram:
- Every arrow into "back to FAC" is the same external `InfoRequired` mechanism (§6 #7) — there's one loop-back mechanism, not a different one per stage.
- `Appraisal`/`RiskReview`/`CommitteeVoting`/`Ratification` mirror the maker-checker floor already established (§6 #2), now expressed across four distinct actors instead of two — Credit Officer, Risk Officer, Committee members, Final Approver are all different people by construction.
- `LegalClearance.Returned` routes back to `Ratification`, not to `Appraisal` — a legal issue isn't a re-appraisal of the credit (§3.6, §6 #12).
- A rejection at `AwaitingOfferAcceptance` or a decline at `LegalClearance`, happening *after* the credit was already ratified, is the scenario behind §6 #9 — see below for why the external "Approved" signal is deliberately deferred past this point.

Each stage transition raises a domain event feeding `GET /status` (§4.5) directly off the aggregate's current state. The outbound callback (§4.4, via `IRhshfCallbackService`, independent of `INampCallbackService`) fires only at the points defined in §6 #9 — not on every internal stage transition.

---

## 6. Decisions

| # | Decision | Default adopted | Status |
|---|---|---|---|
| 1 | Equality vs. partial approval | Strictly binary: `Approved` only ever equals `TotalEopValue`; anything short surfaces as `InfoRequired`/`Declined`, never a lower `ApprovedAmount`. Safest default, no partial-approval financial logic built in v1. | **Needs sign-off** from credit policy + portal before build |
| 2 | Staff decisioning model | **Superseded — see §3.6/§5.** Full pipeline adopted: Credit Officer appraisal → Risk Officer review → Committee vote → Final Approver ratification → offer → FAC acceptance → Legal clearance → Disbursement booking, mirroring NAMP's/Corporate's own shape. This exceeds the earlier two-actor floor. | Decided — proceed, pending #11 (tiering) |
| 3 | Director/shareholder capture | Dropped from v1 — payload has no director data, unlike NAMP. `RhshfDirector` documented as a future add-on only. | Decided — proceed |
| 4 | Profiling stage list | 5 stages (§4): CompanyVerification → CreditBureauCheck → EopReview → SupportingDocuments → ReviewAndSubmit. | Decided — internal only, no external confirmation needed |
| 5 | Token semantics | Single-use: token authenticates the form's first page load only; remainder of the multi-stage session runs on a first-party CRMS session cookie. Token-refresh (§4.6) covers a lapsed/returning session. | Decided — proceed |
| 6 | Case reference format | `RHSHF-{year}-{6-digit sequence}` (e.g. `RHSHF-2026-000123`) — distinct prefix from NAMP. | Decided internally — inform portal as FYI, not a request (field is opaque to them) |
| 7 | `InfoRequired` resume point | On `InfoRequired`, case status returns to `ProfilingInProgress` and `CurrentStage` resets to a stage carried on the decision (`InfoRequiredStage` — the maker/checker picks which stage to reopen when recording the outcome, defaulting to `ReviewAndSubmit` if unspecified). The FAC re-enters the same way as first-time entry: via the portal's "Continue on CRMS" button, which calls token-refresh (§4.6) first — no new mechanics needed there. | Decided — proceed |
| 8 | Maker-checker on repeat rounds | A fresh pass through the pipeline (new `CycleNumber`, §3.6) is required **every time** a case re-reaches `UnderReview` after an `InfoRequired` round-trip — segregation of duties is not a one-time gate. Distinct-actor checks (Credit Officer ≠ Risk Officer, etc.) apply per cycle. How many cycles a case may take before escalation is a separate, still-open question. | Decided — proceed |
| 9 | **When does the portal get told "Approved"?** | Recommend: **only once `Disbursement` completes**, not at `Ratification`. Reasoning: the brief's rule is that the portal's downstream EOP processing (credit paper, supplier orders, real fund commitments) starts the moment CRMS says "Approved" — but between `Ratification` and `Disbursement` the case can still die (FAC rejects the offer, Legal declines). Telling the portal "Approved" at `Ratification` risks a false positive that triggers real downstream financial activity on a loan that never actually gets disbursed. External status stays `UnderReview` throughout `OfferGenerated → LegalClearance → Disbursement`; only `Completed` maps to external `Approved`. | **Needs sign-off** from credit policy + portal — this is a real behavior change from the brief's own example (which implies `decidedBy: "CRMS Credit Committee"`, i.e. approval at committee/ratification time) and must be negotiated, not assumed |
| 10 | How does the FAC accept the offer? | Not yet designed. Likely mirrors the profiling form's mechanism (a token-authenticated page, reached via the portal), but "accepting an offer" is a different, simpler interaction than the 5-stage profiling flow and deserves its own short design pass before Phase 7 (implementation plan) is built. | **Open — needs its own design pass**, not blocking Phases 1-6 |
| 11 | Committee tiering | Whether `RhshfCommitteeReview` needs NAMP-style value-based tiers (Branch/Zonal/Regional/HO) or a single flat committee regardless of `TotalEopValue`. | **Open** — needs credit policy input; v1 can ship with a single flat committee and add tiers later without a rewrite (committee is already its own aggregate) |
| 12 | Legal-return routing | `LegalClearance.Returned` routes back to `Ratification` (Final Approver), not to `Appraisal` — a legal issue isn't a re-appraisal of the credit itself. | Decided — proceed |

#1, #2's tiering follow-up (#11), and #9 block full pipeline completion — they change what "Approved" means externally and whether committee has tiers. #3–#8, #12 are locked in as v1 defaults. #10 blocks only the offer-acceptance phase specifically, not the rest of the build.

---

## 7. Non-Goals (explicitly out of scope / explicitly not shared)

- No changes to `NampApplication`, `NampEnums`, `NampWorkflowSeeder`, or any NAMP database table.
- No reuse of `NampStagingRecord`, `INampCallbackService`, or NAMP's branch-resolution logic — RH-SHF gets its own copies of this shape of code.
- ~~No assumption yet that RH-SHF cases become live Fineract-booked facilities~~ — **resolved (2026-08-27):** yes, CRMS books the loan in Core Banking via a Disbursement Officer, same as NAMP (`RhshfDisbursement`, §3.6). Own integration call, not shared code with NAMP's Fineract-booking path.
- Hosting mechanics for the profiling form (§4.3) are covered separately — current direction is an in-app, non-interactive (Razor Pages) anonymous route inside `CRMS.Web.Intranet`, with business logic kept in `Application`/`Infrastructure` so it can be extracted to an isolated app later without a rewrite.
