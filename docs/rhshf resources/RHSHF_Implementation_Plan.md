# RH-SHF Credit-Profiling — Implementation Plan & Prompts

**Companion to:** `RHSHF_Entities_Workflow_Design.md` (domain model, workflow, decisions) and `crmsintegration.html` (the portal's integration brief). Read those first — this document sequences the *build*, it doesn't re-derive the design.

**Ground rules, every phase:**
1. **RH-SHF is its own track.** No phase may modify `NampApplication`, `NampEnums`, `NampWorkflowSeeder`, `NampStagingRecord`, `INampCallbackService`, or any other `*Namp*` class/table. New code lives under an `Rhshf` namespace throughout Domain/Application/Infrastructure/API.
2. **No single-actor credit decisions, at any stage of the pipeline.** Credit Officer, Risk Officer, Committee, Final Approver, Legal Officer, and Disbursement Officer are all distinct people by construction — this is non-negotiable in this codebase (see design doc §3.6, §5, §6 #2).
3. **No generic form-engine.** Build typed entities and typed stage view-models for RH-SHF's known fields (design doc §3, §4). A schema-driven `FormDefinition`/`FieldType` platform is explicitly out of scope for v1 — there's one programme, with known fields; a generic engine is solving a problem nobody has asked for yet. Revisit only if a second, genuinely different configurable programme becomes real.
4. **Hosting: Razor Pages, not Blazor Server, for the public route.** The FAC-facing form must not hold an interactive circuit open for anonymous internet users on the same app that serves bank staff. Business logic lives in `CRMS.Application`/`CRMS.Infrastructure` regardless, so the front door can move to an isolated app later without a rewrite.
5. **Always attach both companion docs** to every prompt — check exact JSON shapes/status vocabulary against the brief directly, not from memory of an earlier phase.
6. **Manually-created EF migrations need their `.Designer.cs` companion** — without it the `[Migration]` attribute is missing and `MigrateAsync()` silently skips it.
7. **Register everything explicitly in DI** (`DependencyInjection.cs`) — no assembly scanning; a missed registration fails silently rather than erroring, per this codebase's existing pattern.
8. Run phases in order; each ends with a working, tested slice. Keep phases in separate sessions/commits so a bad phase doesn't force redoing a working one.
9. Before Phase 1 starts: **confirm the target branch** (verify current branch matches where this should land — don't assume).

---

## Phase 0 — Decisions gate (no code)

Not a build phase — a checklist. Do not start Phase 1 until these are answered (design doc §6):

- [ ] **#1 Equality rule** confirmed with credit policy + portal: `Approved` is strictly binary (`ApprovedAmount == TotalEopValue`); anything short is `InfoRequired`/`Declined`.
- [ ] **#9 "Approved" timing** confirmed with credit policy + portal: the design defaults to notifying the portal only once `Disbursement` completes, not at `Ratification` — this is a real behavior change from what the brief's own example implies and needs explicit sign-off, not a silent default.
- [ ] **#11 Committee tiering** decided: single flat committee for v1, or NAMP-style value-based tiers from day one.
- [ ] Which roles fill Credit Officer / Risk Officer / Final Approver / Legal Officer / Disbursement Officer for RH-SHF — confirmed as reuse of the existing generic `Roles.cs` constants (recommended) rather than new role names.
- [ ] Portal team has acknowledged the `RHSHF-{year}-{seq}` reference format (FYI, not a blocking approval).

Everything below assumes these are answered. #1 and #11 affect Phases 1 and 5; #9 affects Phases 9-10; role confirmation affects Phases 4-9. §6 #10 (how the FAC accepts the offer) is a separate open item that only blocks Phase 7 specifically — the rest of the build can proceed without it.

---

## Phase 1 — Domain model + submit endpoint (§4.1)

**Goal:** `POST /v1/credit-profiles` creates a case and returns a token, idempotently — using the fixed domain model from the design doc, not a generic form schema.

**Prompt:**
```
Attached: crmsintegration.html (integration brief) and
RHSHF_Entities_Workflow_Design.md (our domain design — follow it exactly,
do not invent a generic form-engine or FormDefinition/FieldType schema).

Repo: CRMS, Clean Architecture (CRMS.Domain/Application/Infrastructure/
API/Web.Intranet), Blazor Server + minimal APIs, .NET 8/9, EF Core.

Task: implement §4.1 of the brief end-to-end, per the design doc's §3.1-3.2.

1. Add RhshfCreditProfile (aggregate root) and RhshfEopLine (child entity)
   to CRMS.Domain/Aggregates/Rhshf/, using our existing AggregateRoot/
   Entity base classes (find and reuse — don't invent new ones). Add
   RhshfEnums.cs (RhshfCaseStatus, RhshfDecisionOutcome, RhshfProfilingStage
   per the design doc's exact enum values — 5 stages, no director stage).
2. Add IRhshfCreditProfileRepository to CRMS.Domain/Interfaces.
3. Add SubmitConsolidatedEopCommand + handler to
   CRMS.Application/Rhshf/Commands/, idempotent on submissionId.
4. EF Core configuration under Persistence/Configurations/Rhshf/, and a
   real migration against our actual DbContext (find it first — do not
   create a second context). Include the .Designer.cs companion file.
5. Add POST /v1/credit-profiles (new RhshfSubmissionController in
   CRMS.API), returning { reference, token, profilingUrl, tokenExpiresAt,
   status } matching the brief's §4.1 response shape exactly. Reference
   format: RHSHF-{year}-{6-digit sequence}.
6. Register everything explicitly in DependencyInjection.cs — no
   assembly scanning.

Tests: missing-required-field returns 400; re-submitting the same
submissionId returns the same reference/token, not a duplicate case;
happy path returns reference + signed token + profilingUrl matching the
brief's response shape exactly; RawSubmissionPayload is persisted for
traceability.

Flag anywhere our existing conventions (naming, layering, DI setup) pull
against this plan, and tell me before proceeding rather than guessing.
```

**Done when:** tests pass; POSTing the brief's §4.1 sample payload returns `201` with `reference`/`token`/`profilingUrl`; nothing under `Aggregates/Namp/` or `Enums/NampEnums.cs` was touched.

---

## Phase 2 — Token service + refresh endpoint (§4.2, §4.6)

**Prompt:**
```
Continuing the RH-SHF build (Phase 1 merged). Implement §4.2 and §4.6 of
crmsintegration.html per the design doc's §3.4.

1. Add RhshfIssuedToken (child entity) — Jti, RhshfCreditProfileId,
   IssuedAt, ExpiresAt, ConsumedAt.
2. Add an RhshfTokenService (JWT issue/verify) to CRMS.Infrastructure/
   ExternalServices/Rhshf/ — signing secret from configuration/
   user-secrets, not hardcoded. If a generic JWT-signing primitive
   already exists in the platform (check TokenService.cs), call into it;
   do not fork NAMP's own token code.
3. Add POST /v1/credit-profiles/{reference}/token (refresh) — issues a
   fresh token without altering case state.
4. Enforce single-use semantics: verifying a token marks it consumed;
   a second verification attempt against the same Jti fails.

Tests: expired token verifies to null/invalid; a token issued for case A
is rejected when used against case B; a consumed token is rejected on
reuse; refresh issues a new token without altering case state; claims
match the brief's suggested set (sub, facId, programme, aud, iat, exp,
jti).
```

**Done when:** tests pass; an expired and a reused token both demonstrably fail verification in a test, not just by inspection.

---

## Phase 3 — Resumable profiling form (§4.3)

Biggest phase — split into two sessions if it runs long (e.g. stages 1-2 first, 3-5 second).

**Prompt:**
```
Continuing the RH-SHF build (Phases 1-2 merged). Implement §4.3 per the
design doc's §4 stage list and §7 hosting decision.

1. Add an anonymous, public route (e.g. /rhshf/profiling/{reference}) as
   plain Razor Pages — NOT an interactive Blazor Server component. This
   is deliberate: anonymous internet users must not hold open a live
   SignalR circuit on the same app serving bank staff. It must not touch
   AuthService/AuthenticationStateProvider or our staff-login pipeline at
   all — auth is the ?token= query string only, verified via
   RhshfTokenService.
2. On first load: verify+consume the token (Phase 2), establish a normal
   first-party session (cookie, on our own domain), and render the
   FAC's CurrentStage. On an expired/invalid token, show a plain "session
   expired, return to the portal" page — no stack trace, no silent
   redirect.
3. Implement the 5 stages as typed pages/view-models, not a generic field
   renderer: CompanyVerification, CreditBureauCheck (calls our existing
   generic bureau-check services — do not reuse NAMP's bureau wiring
   directly if it's NAMP-specific; if it's generic, call it), EopReview,
   SupportingDocuments (file upload), ReviewAndSubmit.
4. Add GetCurrentStageQuery and SubmitStageDataCommand handlers in
   CRMS.Application/Rhshf/ — server-side validation per stage regardless
   of client-side checks.
5. Resuming a case (same or new browser session, valid/refreshed token)
   reloads previously-submitted values for the current stage.
6. On completing ReviewAndSubmit, show a "submitted — you can close this
   tab" confirmation and transition case status to UnderReview. Do NOT
   trigger any decision or webhook here — that's Phase 4/5, staff-side.

Tests: stage order is fixed and enforced server-side; resuming a case
reloads prior values for the current stage; submitting invalid data
(missing required field, malformed number) is rejected server-side even
if a malicious client bypasses browser validation; completing the final
stage transitions status to UnderReview and no further.
```

**Done when:** you can open the link from Phase 1's response in a browser with no staff login, complete all 5 stages, close the tab, reopen with a refreshed token (Phase 2), and land back where you left off; confirm via browser dev tools that no SignalR/Blazor circuit is established on this route.

---

## Phase 4 — Appraisal + Risk Review (design doc §3.6)

**Prompt:**
```
Continuing the RH-SHF build (Phases 1-3 merged). Implement the first two
stages of the post-profiling pipeline, per design doc §3.6/§5.

1. Add RhshfAppraisal and RhshfRiskReview (child entities of
   RhshfCreditProfile) exactly per §3.6's field lists, both scoped by
   CycleNumber.
2. Add Appraise(userId, outcome, notes) and ReviewRisk(userId, outcome,
   notes) methods on RhshfCreditProfile. ReviewRisk must throw if
   riskOfficerId == that cycle's creditOfficerId (distinct-actor rule).
   ReviewRisk cannot run before Appraise() for the same cycle.
3. Outcome ReturnToFac on either stage: reset case Status to
   ProfilingInProgress, CurrentStage to a caller-supplied
   RhshfProfilingStage (default ReviewAndSubmit), close the cycle. A
   fresh CycleNumber opens the next time the case reaches Appraisal.
4. Outcome Decline on either stage: case Status -> Declined (this IS
   terminal — raise RhshfCaseDecidedEvent here, since a decline this
   early never reaches Disbursement).
5. Branch-scoped queues (VisibilityScope.Branch, ResolvedBranchId/
   ResolvedOfficeId, §3.1) for both stages — reuse the existing pattern,
   don't invent a new one.
6. Case-review workspace (staff-side, inside authenticated
   CRMS.Web.Intranet, NOT the public Razor Pages route) surfacing
   company-verification result, bureau report, EOP breakdown, and
   uploaded documents — this is what Appraisal/RiskReview actually work
   from, not a bare approve/decline button.
7. Roles: [confirm from Phase 0] — recommend reusing existing generic
   Roles.cs constants (e.g. CreditOfficer, RiskManager) rather than
   inventing new role names.

Tests: RiskReview throws if the reviewer equals that cycle's Credit
Officer; RiskReview before Appraisal throws; ReturnToFac from either
stage resets status/stage correctly and opens a fresh cycle on the next
pass; Decline from either stage is terminal and fires
RhshfCaseDecidedEvent; queue visibility is branch-scoped.
```

**Done when:** a case can be appraised and risk-reviewed by two distinct staff accounts, with `ReturnToFac` and `Decline` both provably working and correctly gated.

---

## Phase 5 — Committee vote (design doc §3.6)

**Prompt:**
```
Continuing the RH-SHF build (Phases 1-4 merged). Implement committee
voting per design doc §3.6.

1. Add RhshfCommitteeReview as its OWN aggregate root (own table, own
   repository) — do NOT reuse the generic CommitteeReview (it has a
   hard, non-nullable FK to Corporate's LoanApplicationId) and do NOT
   reuse NampCommitteeReview. Fields per §3.6: RhshfCreditProfileId,
   CycleNumber, Members{UserId,Vote,VotedAt,Comment}, RequiredVotes,
   MinimumApprovalVotes, FinalDecision, DecidedAt.
2. [If Phase 0 #11 confirmed a single flat committee for v1] a fixed
   committee membership/quorum config; [if tiers were confirmed] route
   by TotalEopValue per whatever tiers were agreed — confirm which
   before writing this.
3. Voting reaching FinalDecision == Approved (quorum + majority met)
   advances RhshfCreditProfile to Ratification (Phase 6).
   Rejected/Deferred -> Declined (terminal, raises RhshfCaseDecidedEvent).
   ReturnToFac -> same reset mechanics as Phase 4.
4. Committee members must be distinct from that cycle's Appraisal/
   RiskReview actors — enforce in the command handler.

Tests: quorum/majority thresholds are enforced correctly; Approved
advances the case's internal stage to Ratification; Rejected/Deferred
is terminal; ReturnToFac resets correctly and opens a fresh cycle;
a committee member who was that cycle's Credit Officer or Risk Officer
is rejected.
```

**Done when:** a case can be voted through committee with quorum/majority enforced, and the three outcome paths (advance / decline / return-to-FAC) all behave correctly.

---

## Phase 6 — Ratification + offer generation (design doc §3.6)

**Prompt:**
```
Continuing the RH-SHF build (Phases 1-5 merged). Implement ratification
and offer generation per design doc §3.6.

1. Add RhshfRatification (child entity of RhshfCreditProfile) per §3.6:
   CycleNumber, FinalApproverId, RatifiedAt, Outcome, ApprovedAmount.
2. Ratify(userId, outcome, approvedAmount?) — Ratified requires
   approvedAmount == TotalEopValue exactly (Phase 0 #1), else throws.
   FinalApproverId must differ from the committee members who approved
   (and from Appraisal/RiskReview actors) that cycle.
3. On Ratified: generate RhshfOffer (own aggregate root, §3.6) with
   Status = Generated, an offer document (mirror how NAMP generates its
   offer letter — same shape of concern, independent code), and advance
   internal stage to OfferGenerated -> AwaitingOfferAcceptance. Do NOT
   raise RhshfCaseDecidedEvent here and do NOT call the outcome webhook
   yet — external status stays UnderReview per design doc §6 #9.
4. Declined/ReturnToFac at Ratification behave as in Phase 4/5.

Tests: Ratify with approvedAmount != TotalEopValue throws; a Final
Approver who already voted on committee that cycle is rejected;
Ratified generates exactly one RhshfOffer and does not fire
RhshfCaseDecidedEvent; external GET /status still reports UnderReview
immediately after Ratified.
```

**Done when:** ratifying a case produces a generated offer and the case sits in `AwaitingOfferAcceptance`, with no webhook fired yet — provable by asserting zero calls to `IRhshfCallbackService` at this point in a test.

---

## Phase 7 — Offer acceptance by the FAC (design doc §6 #10 — needs its own short design pass first)

**Do not start this phase's code until §6 #10 has an actual answer** — "reuse the profiling form's mechanism" is a direction, not a spec. Before writing the prompt below, decide: is this a single-page accept/reject action, or does it need document review/e-signature? Confirm with the credit/legal team.

**Prompt (once #10 is answered):**
```
Continuing the RH-SHF build (Phases 1-6 merged). Implement FAC offer
acceptance per the answer to design doc §6 #10.

1. A token-authenticated, anonymous, Razor Pages route (same hosting
   rule as Phase 3 — no Blazor Server circuit) where the FAC views the
   generated RhshfOffer and accepts or rejects it.
2. AcceptOffer()/RejectOffer() on RhshfOffer: Accepted -> internal stage
   advances to LegalClearance (Phase 8). Rejected or expired -> case
   Status -> Declined/Cancelled (terminal — raise RhshfCaseDecidedEvent;
   confirm with Phase 0 #9 whether this needs a distinct webhook status
   given the portal was never told Approved at this point).
3. Reuse Phase 2's token issuing pattern for this route's own token
   (same RhshfIssuedToken shape, scoped to the offer rather than the
   profiling session) — do not reuse a profiling-stage token for this.

Tests: accepting advances to LegalClearance; rejecting is terminal and
fires the correct event; an expired offer cannot be accepted.
```

**Done when:** the FAC can accept or reject a generated offer through a token-authenticated, non-circuit-holding page, and both outcomes transition the case correctly.

---

## Phase 8 — Legal clearance (design doc §3.6)

**Prompt:**
```
Continuing the RH-SHF build (Phases 1-7 merged). Implement legal
clearance per design doc §3.6.

1. Add RhshfLegalClearance as its own aggregate root (own table),
   independent of any NAMP legal-clearance code:
   RhshfCreditProfileId, LegalOfficerId, ClearedAt, Outcome, Comments.
2. Clear(userId, outcome, comments): Granted -> internal stage advances
   to Disbursement (Phase 9). Returned -> routes back to Ratification
   (design doc §6 #12) for the Final Approver's attention, NOT back to
   Appraisal. Declined -> case Status -> Declined (terminal, raises
   RhshfCaseDecidedEvent — same §9 timing question as Phase 7 applies).
3. LegalOfficerId should reasonably differ from FinalApproverId — enforce
   if confirmed as a requirement in Phase 0.

Tests: Granted advances to Disbursement; Returned re-opens Ratification
(not Appraisal) for the same cycle; Declined is terminal.
```

**Done when:** a case can be legally cleared, returned to ratification, or declined, with the routing verified by test (Returned must land back at Ratification specifically, not any other stage).

---

## Phase 9 — Disbursement / Core Banking booking (design doc §3.6)

**Prompt:**
```
Continuing the RH-SHF build (Phases 1-8 merged). Implement disbursement
booking per design doc §3.6 — this resolves the earlier open question of
whether RH-SHF becomes a real Fineract-booked facility: it does.

1. Add RhshfDisbursement (child entity of RhshfCreditProfile):
   DisbursementOfficerId, BookedAt, FineractLoanAccountNumber,
   DisbursedAmount, Status.
2. Book(userId): calls our existing Core Banking / Fineract integration
   (find and reuse the existing service interface — do not reinvent
   loan-booking wiring, but do NOT route through NAMP's own booking
   command/handler classes; call the same underlying Fineract client
   independently). DisbursedAmount must equal the ratified
   ApprovedAmount.
3. On Status == Booked: internal stage -> Completed. THIS is where
   RhshfCaseDecidedEvent fires with outcome Approved (per §6 #9 — the
   portal is told Approved here, not at Ratification).
4. On booking failure: Status == Failed, case stays in Disbursement
   (retryable), does not regress to an earlier stage.

Tests: a successful booking sets Completed and fires
RhshfCaseDecidedEvent with Approved; DisbursedAmount mismatches with
ApprovedAmount throw; a failed booking is retryable without losing the
case's prior ratification/legal-clearance state.
```

**Done when:** a full happy-path case — Appraisal through Disbursement — results in exactly one `RhshfCaseDecidedEvent` firing, at booking completion, not any earlier stage.

---

## Phase 10 — Outcome webhook (§4.4)

**Prompt:**
```
Continuing the RH-SHF build (Phases 1-9 merged). Implement §4.4 — this
phase reacts to RhshfCaseDecidedEvent (now fired from multiple possible
points: early Decline at Phases 4/5/6, offer rejection at Phase 7, legal
decline at Phase 8, or successful Disbursement at Phase 9). It does not
itself decide anything.

1. Add RhshfCallbackAttempt (child entity) — EventId, AttemptNumber,
   SentAt, ResponseStatusCode, Succeeded, NextRetryAt.
2. Add IRhshfCallbackService / RhshfCallbackService to
   CRMS.Infrastructure/ExternalServices/Rhshf/ — independent of
   INampCallbackService. HMAC-SHA256 signs the raw JSON body with a
   shared secret from configuration, sends
   X-CRMS-Signature: sha256=<hex>, retries with backoff on non-2xx
   (~5 attempts over 30 minutes), logs exhausted retries for manual
   reconciliation via RhshfCallbackAttempt.
3. Handler on RhshfCaseDecidedEvent calls RhshfCallbackService with the
   brief's §4.4 payload shape exactly (reference, submissionId, status,
   decision{outcome, approvedAmount, currency, reasons, decidedAt,
   decidedBy}, eventId, occurredAt). decidedBy should reflect which
   stage actually produced the terminal outcome (e.g. "CRMS Disbursement"
   for a completed booking, "CRMS Credit Committee" for an early
   decline at Phase 5) rather than a hardcoded string.

Tests: signature is verifiable against a known secret and known body;
webhook is retried on a simulated non-2xx response and stops retrying on
2xx; the payload JSON matches the brief's example field-for-field; an
Approved webhook only ever fires from Phase 9's booking completion, never
from Ratification alone (regression test against §6 #9).
```

**Done when:** every terminal path from Phases 4-9 fires exactly one webhook call (or the correct retry count against a failing mock) with a valid signature, and a test specifically proves Ratification alone does NOT trigger it.

---

## Phase 11 — Status endpoint (§4.5)

**Prompt:**
```
Continuing the RH-SHF build (Phases 1-10 merged). Implement §4.5:
GET /v1/credit-profiles/{reference}/status, returning reference,
submissionId, status, stage{current,index,total}, decision (null until
decided, same shape as §4.4's payload once populated), and updatedAt.
Match the brief's example response exactly. status must stay UnderReview
throughout Phases 4-8 (Appraisal through Legal Clearance) per §6 #9 —
only Phase 9's successful booking flips it to Approved.

Test: status reflects RECEIVED right after Phase 1 submit,
PROFILING_IN_PROGRESS during Phase 3's stages (and again during any
InfoRequired round-trip), UNDER_REVIEW from Appraisal all the way
through Legal Clearance, and the decision object populates only once
Phase 9's booking completes.
```

**Done when:** polling this endpoint at any point in a case's life — including mid-pipeline, post-Ratification, pre-Disbursement — returns `UNDER_REVIEW` with `decision: null`, and only flips after booking.

---

## Phase 12 — Auth, sandbox, and portal handoff

Not a code-generation prompt — coordination + config work:

1. Confirm the portal→CRMS auth mechanism. Recommendation: **API key** (matching the existing `X-Api-Key` pattern in `NampWebhookController`, reused as an independent implementation) — this platform has no OAuth2 token-issuing infrastructure today, and mTLS is a poor fit given the current ALB/EC2 deploy topology. Raise this recommendation with the portal team rather than defaulting to OAuth2 by habit.
2. Stand up sandbox `Rhshf:*` config (separate signing secrets, DB, base URLs) from production — never share NAMP's config section.
3. Send the portal team: confirmation of `profilingUrl` shape, the webhook signing secret (out-of-band), the `RHSHF-` reference format (FYI) — **and explicitly negotiate design doc §6 #9** (portal is told "Approved" only at disbursement, not at ratification). This is a real behavioral commitment the portal needs to design their own downstream flow around, not a footnote.
4. Run the full pipeline end-to-end with them in sandbox before production cutover (brief §9, acceptance criterion 6) — including at least one early-decline path (e.g. Phase 5) and the full happy path through Phase 9, not just a single happy-path call.

---

## What changed from the reference plan I was shown, and why

- Replaced the flat two-actor "maker-checker" model with the **full Appraisal → Risk Review → Committee → Ratification → Offer → Legal Clearance → Disbursement pipeline** (Phases 4-9), per direct instruction — this mirrors NAMP's and Corporate's own lifecycle shape and is what the brief's own webhook example (`decidedBy: "CRMS Credit Committee"`) already implied.
- Introduced **design doc §6 #9**: the portal is told "Approved" only once Disbursement completes (Phase 9), not at Ratification (Phase 6) — because the case can still die at offer-acceptance or legal-clearance after ratification, and telling the portal "Approved" early risks triggering their downstream EOP processing on a loan that never actually gets disbursed. This is a real behavior change from the brief's implied assumption and needs explicit portal sign-off (Phase 0, Phase 12).
- `RhshfCommitteeReview` and `RhshfLegalClearance` are modeled as their **own aggregates**, not child entities — confirmed by reading `CommitteeReview.cs` that the generic version is hard-FK'd to Corporate's `LoanApplicationId` and isn't reusable, the same reason NAMP built `NampCommitteeReview` independently.
- Flagged **offer acceptance (Phase 7) as needing its own design pass** before coding — "reuse the profiling form's mechanism" is a direction, not yet a spec.
- Replaced the generic **FormDefinition/FormStage/FormField** schema-driven engine with fixed, typed entities and stage view-models (Phase 1, Phase 3) — still holds from the earlier review.
- Fixed the public form's hosting to **Razor Pages, not interactive Blazor Server** (Phases 3, 7) — still holds from the earlier review.
- Kept the original reference plan's strongest ideas: phased delivery with a merge gate between phases, a concrete test list per phase, and a closing coordination phase for auth/sandbox/portal handoff.
