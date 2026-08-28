# CRMS Integration — Implementation Plan & Claude Code Prompts

Repo: `fabiyitunde/tpcrms`, branch `namp-crms`. Layers: `CRMS.Domain` /
`CRMS.Application` / `CRMS.Infrastructure` / `CRMS.Web.Intranet`.

A reference code sketch for all six phases already exists
(`crms-profiling-form.zip` from earlier in this conversation). Each prompt
below tells Claude Code to treat that sketch as a starting point, not a
drop-in — it needs to be reconciled against your real `DbContext`, base
classes (`AggregateRoot`/`Entity`), and existing conventions (e.g. the
`AuthService`/`Blazored.LocalStorage` pattern it must NOT touch).

Run phases in order. Each ends with a working, tested slice — don't start
the next phase until the current one's tests pass.

---

## Phase 1 — Domain model + submit endpoint (§4.1)

**Goal:** `POST /v1/credit-profiles` creates a case and returns a token, idempotently.

**Prompt:**
```
Attached: crmsintegration.html (the integration brief) and the reference
sketch (Domain/ProfilingForms/*.cs, Application/ProfilingForms/Commands/
SubmitConsolidatedEop.cs). Our repo is fabiyitunde/tpcrms, branch
namp-crms, Clean Architecture (CRMS.Domain/Application/Infrastructure/
Web.Intranet), Blazor Server, .NET 8/9.

Task: implement §4.1 of the brief end-to-end.

1. Add FormDefinition/FormStage/FormField and ProfilingCase/StageSubmission
   to CRMS.Domain, matching our existing AggregateRoot/Entity base classes
   (find and reuse them — don't invent new ones).
2. Add SubmitConsolidatedEopCommand + handler to CRMS.Application, adapted
   from the reference sketch.
3. Add EF Core configuration + a real migration against our actual
   DbContext (find it first; don't create a second context unless ours
   genuinely can't host these entities).
4. Add the POST /v1/credit-profiles minimal API endpoint.
5. Seed one FormDefinition (programme code RH-SHF-DRY-2026) with 2-3
   stages so there's something to submit against.

Write tests for: missing-required-field returns 400; re-submitting the
same submissionId returns the same reference/token, not a duplicate case;
happy path returns reference + signed token + profilingUrl matching the
brief's response shape exactly.

Flag anywhere our existing conventions (naming, layering, DI setup) pull
against the reference sketch, and tell me before proceeding rather than
guessing.
```

**Done when:** tests pass; you can POST a sample payload from the brief's §4.1 example and get back a `201` with `reference`/`token`/`profilingUrl`.

---

## Phase 2 — Token service + refresh endpoint (§4.2, §4.6)

**Prompt:**
```
Continuing the CRMS integration (Phase 1 is merged). Implement §4.2 and
§4.6 of crmsintegration.html.

1. Add ProfilingTokenService (JWT issue/verify) to CRMS.Infrastructure,
   adapted from the reference sketch — signing secret from configuration/
   user-secrets, not hardcoded.
2. Add POST /v1/credit-profiles/{reference}/token (refresh).
3. Wire DI registration in Program.cs.

Tests: expired token verifies to null/invalid; a token issued for case A
is rejected when used against case B; refresh issues a new token without
altering case state; token claims match the brief's suggested set (sub,
facId, programme, aud, iat, exp, jti).
```

**Done when:** tests pass; an expired token demonstrably fails verification in a test, not just by inspection.

---

## Phase 3 — Resumable profiling form (§4.3)

This is the biggest phase — split it into two prompts if it runs long in one session.

**Prompt:**
```
Continuing the CRMS integration (Phases 1-2 merged). Implement §4.3.

1. Add a Blazor Server route /profiling/{reference} in CRMS.Web.Intranet,
   completely separate from our existing AuthService/AuthenticationStateProvider
   staff-login pipeline — this route authenticates via the ?token= query
   string only, no staff credentials involved.
2. Apply @rendermode with prerender: true explicitly from the start — we
   already hit a blank-render bug on the reset-password page from
   prerendering being disabled in email-client webviews; don't repeat it
   here.
3. Add GetCurrentStageForRenderQuery and SubmitStageDataCommand handlers
   in CRMS.Application, generic over the FormDefinition schema (a new
   loan/programme must never require new C# code — only a new
   FormDefinition seed).
4. Render the form generically: one component that walks FormStage.Fields
   and renders the right input per FieldType (Text/Number/Decimal/Date/
   Select/Boolean/File).
5. On an expired/invalid token, show a plain "session expired, return to
   the portal" page — no stack trace, no silent redirect.
6. On completing the last stage, show a "submitted, you can close this
   tab" confirmation and flip case status to UNDER_REVIEW.

Tests: stage order follows the FormDefinition; resuming a case reloads
previously-submitted values for the current stage; submitting a value
that violates the field's ValidationRulesJson (regex/min/max/options) is
rejected server-side even if a malicious client skips the browser
validation; completing the final stage transitions status correctly.
```

**Done when:** you can open `/profiling/{reference}?token=...` in a browser, complete a multi-stage form seeded in Phase 1, close the tab, reopen the link, and land back where you left off.

---

## Phase 4 — Outcome webhook (§4.4)

**Prompt:**
```
Continuing the CRMS integration (Phases 1-3 merged). Implement §4.4.

1. Add WebhookDispatcher to CRMS.Infrastructure: HMAC-SHA256 signs the
   raw JSON body with a shared secret from configuration, sends
   X-CRMS-Signature: sha256=<hex>, retries with backoff on non-2xx
   (roughly 5 attempts over 30 minutes), logs exhausted retries for
   manual reconciliation.
2. Add a domain event (ProfilingCaseDecided) raised from
   ProfilingCase.Decide(...), and a handler in Infrastructure that calls
   WebhookDispatcher with the exact payload shape from the brief's §4.4
   example (reference, submissionId, status, decision{outcome,
   approvedAmount, currency, reasons, decidedAt, decidedBy}, eventId,
   occurredAt).
3. Enforce the brief's hard rule in ProfilingCase.Decide: approving with
   an amount that doesn't exactly equal TotalEopValue must throw, not
   silently proceed.

Tests: signature is verifiable against a known secret and known body;
webhook is retried on a simulated non-2xx response and stops retrying on
2xx; Decide() throws when approvedAmount != TotalEopValue; the payload
JSON matches the brief's example field-for-field.
```

**Done when:** deciding a case in a test fires exactly one webhook call (or the correct number of retries against a failing mock endpoint) with a valid signature.

---

## Phase 5 — Status endpoint (§4.5)

**Prompt:**
```
Continuing the CRMS integration (Phases 1-4 merged). Implement §4.5:
GET /v1/credit-profiles/{reference}/status, returning reference,
submissionId, status, stage{current,index,total}, decision (null until
decided, same shape as the §4.4 webhook payload once populated), and
updatedAt. Match the brief's example response exactly.

Test: status reflects RECEIVED right after Phase 1 submit, UNDER_REVIEW
after Phase 3's final stage, and the decision object populates correctly
after Phase 4's Decide() runs.
```

**Done when:** polling this endpoint at any point in a case's life returns an accurate, correctly-shaped snapshot.

---

## Phase 6 — Auth, sandbox, and portal handoff

Not a code-generation prompt — this is coordination + config work:

1. Confirm the portal→CRMS auth mechanism (§10, still open) and swap the placeholder API-key handler for whatever's agreed.
2. Stand up sandbox `Profiling:*` config (separate signing secrets, DB, base URLs) from production.
3. Send the portal team: confirmation of `profilingUrl` shape (base vs. fully-formed), the webhook signing secret (exchanged out-of-band, never over email in plaintext), and sandbox base URL + credentials for their side.
4. Run all five touchpoints end-to-end with them in sandbox before production cutover (brief §9, acceptance criterion 6).

---

## Notes for every phase's prompt

- Attach `crmsintegration.html` every time — Claude Code should check the exact JSON shapes and status vocabulary (§5) against it directly rather than from memory of earlier phases.
- Always ask it to flag deviations from our stack rather than silently reinterpreting the brief.
- Keep phases in separate Claude Code sessions/commits so a bad phase doesn't force re-doing prior, working phases.
