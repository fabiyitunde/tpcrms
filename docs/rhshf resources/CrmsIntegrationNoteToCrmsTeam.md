# RH-SHF Portal ⇄ CRMS — integration findings and requests

**From:** the RH-SHF portal team
**To:** the CRMS engineering team
**Date:** 2026-09-22
**Re:** `docs/crmsintegration.md` — moving the contract from **PROPOSED** to **AGREED**

---

## 1. Summary

We have built and tested the portal side against your sandbox at `http://18.132.3.119:8980`. **Your
implementation follows the brief closely and all three portal→CRMS touchpoints work end to end.** We found
the defects on our side, not yours, and they are fixed.

We would like to **mark the contract AGREED** on the basis below, with five small requests in §4 — one of
which is needed before the FAC-facing flow can complete.

---

## 2. What we verified working

| Touchpoint | Result |
|---|---|
| `POST /v1/credit-profiles` | **201** — `reference`, signed `token`, `profilingUrl`, `tokenExpiresAt`, `status` |
| `GET /v1/credit-profiles/{reference}/status` | **200** — `status`, `stage{current,index,total}`, `decision`, `updatedAt`, `actionRequired` |
| `POST /v1/credit-profiles/{reference}/token` | **200** — fresh `{token, tokenExpiresAt}` |
| Idempotency | **Confirmed** — resending the same `submissionId` returns the same reference, no duplicate case |

Your request schema matches §4.1 field for field, and your status vocabulary matches §5 exactly. Your webhook
signature scheme (`X-CRMS-Signature: sha256=<hex HMAC-SHA256 over the raw body>`) matches what our receiver
verifies. Thank you — that made our side straightforward to reconcile.

We also noted you have implemented the equality rule in your domain model:

> *"Approved amount must equal the total EOP value exactly — no partial approval in v1."*

That is exactly the rule §10 asked us to confirm. **Confirmed from our side too.**

### Open items from §10, settled by your implementation

| Question | Your answer |
|---|---|
| Portal→CRMS auth | **`X-Api-Key` header.** We send it. |
| `profilingUrl` base or fully-formed? | **Fully-formed**, token embedded. |
| Partial approval? | **No** — equality enforced. |
| Token lifetime / refresh | ~30 minutes, refreshable. |

---

## 3. Two defects we found on our side (no action needed from you)

Recorded only so you know why nothing had ever reached you from our staging environment.

1. **We were not sending `programme.code`** (and were sending null for `fac.rcNumber`, `fac.tin`,
   `fac.boaAccountNumber`). Every submission would have been rejected. Your 400 response naming all four
   fields is what told us — it was clear and immediately actionable, so thank you for validating properly
   rather than accepting a half-empty case.
2. **We were appending our own query string** to your fully-formed `profilingUrl`, producing a URL with two
   `?` and the token twice. Our fault for not reading your response shape carefully.

Both are fixed, and the corrected payload now returns **201** from your sandbox.

---

## 4. Requests

### 4.1 Please return an `actionUrl` alongside `actionRequired` — *this one blocks the FAC flow*

You have built an **offer acceptance step** that our brief did not describe: at ratification the case moves to
`InternalStage = AwaitingOfferAcceptance` while `Status` stays `UNDER_REVIEW`, an `OfferReady` webhook fires
with `actionRequired: "REVIEW_OFFER"`, and the FAC accepts or rejects on your `/rhshf/offer/{reference}` page.

We think the step itself is right, and we are **not** asking you to expose accept/reject as an API — accepting
a credit offer is a contractual act that belongs on your domain with your terms in front of the FAC.

The problem is purely one of routing. The `profilingUrl` you return at submit points at
`/rhshf/profiling/{reference}`, and **that page does not link to the offer page**. So a FAC who clicks
"Continue on CRMS" while an offer is waiting lands on a completed wizard with no way forward, and the case
stalls.

**Requested:** include a resolvable `actionUrl` beside `actionRequired`, in both the status response and the
`OfferReady` webhook — e.g.

```jsonc
{
  "status": "UNDER_REVIEW",
  "actionRequired": "REVIEW_OFFER",
  "actionUrl": "https://<crms-host>/rhshf/offer/RHSHF-2026-000123",
  ...
}
```

We would append a freshly minted token exactly as we do for `profilingUrl`.

*We could construct `/rhshf/offer/{reference}` ourselves, but we have deliberately avoided hardcoding your
route shape anywhere — it is yours to change, and we would rather not be the reason you cannot.*

### 4.2 A real profiling host

`profilingUrl` currently returns `https://crms.example.com/rhshf/profiling/...`, which does not resolve. Until
there is a real host we cannot take our staging environment off its CRMS mock, because a FAC clicking
"Continue on CRMS" would land nowhere.

**Requested:** the sandbox and production hostnames for the profiling/offer pages.

### 4.3 An API key on the sandbox

`POST /v1/credit-profiles` currently succeeds with **no `X-Api-Key` header at all** — your
`IsAuthorized()` returns `true` when no key is configured, which we understand is the intended local/dev
fallback. On a reachable host it means anyone who can route to it can create credit cases.

**Requested:** configure a sandbox key and share it with us. We already send the header; it is a
configuration change on both sides, not code.

### 4.4 `X-CRMS-Timestamp` on the webhook

§4.4 proposed a timestamp header alongside the signature, to prevent replay. `RhshfCallbackService` currently
sends `X-CRMS-Signature` only. Our receiver does not require a timestamp, so **we are compatible today** — but
a captured callback can be replayed indefinitely. Low severity behind TLS and with `eventId` de-duplication
(which we have now implemented), but worth closing.

**Requested:** add `X-CRMS-Timestamp`; we will verify it and reject anything outside a tolerance window.

### 4.5 Please confirm `fac.tin` really is required

`fac.tin` is `[Required]` on your request model. **The portal has never captured a TIN**, so this blocks every
FAC until BOA collects them.

We are not asking you to change it if it is genuinely needed — a credit file without a tax identity is
arguably incomplete, and we have already built the capture (FAC self-service plus a BOA-on-behalf route with
an audit trail). We only want to be sure, because the answer decides whether BOA must collect TINs for the
existing FAC population **before** go-live or can do it alongside.

**Requested:** confirm required, or make it optional if a case can be assessed without it.

---

## 5. Two things to know about what we send

### 5.1 `eopLines` — `quantityKg` and `unitPricePerKg` will be `0`

Please **do not read those zeros as real quantities.** The portal holds quantities per *input* — bags of
fertiliser, litres of herbicide, kilogrammes of seed — and never a tonnage of the commodity being grown. So
for a maize EOP we can tell you truthfully that maize accounts for ₦34,000,000, but we cannot tell you how
many kilogrammes of maize that is, because nothing in the programme records it.

`commodity` and `lineValue` are accurate. We would rather send zeros and say so than invent a figure that a
credit officer might rely on.

If a per-commodity quantity matters to your assessment, tell us and we will discuss with BOA where it could
be captured.

### 5.2 `session.code` includes the region

We derive it as `{year}-{seasonType}-{region}` — e.g. `2026-DRY-SOUTH`. The programme runs
"Dry Season (North) – 2026" and "Dry Season (South) – 2026" concurrently, so without the region both would
arrive under `2026-DRY` and you would have two different FAC populations under one identifier.

---

## 6. Test data in your sandbox

While testing we created **three real credit cases** in your sandbox, with fictitious FAC identifiers:

- `RHSHF-2026-920295`
- `RHSHF-2026-570338`
- one idempotent repeat of the first

Flagging them so they are not mistaken for genuine applications in a queue. Happy for you to delete them.

---

## 7. What we would like to agree

1. Mark the contract **AGREED**, with §4.1 (`actionUrl`) added to it.
2. You supply: a profiling host, a sandbox API key, and confirmation on `fac.tin`.
3. We then take our staging environment off its mock and run the four touchpoints end to end against your
   sandbox, including a FAC completing profiling and accepting an offer.

Happy to walk through any of this on a call.
