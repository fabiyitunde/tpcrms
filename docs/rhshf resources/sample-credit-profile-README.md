# Sample `POST /v1/credit-profiles` payload — end-to-end walkthrough

Use `sample-credit-profile-payload.json` (next to this file) to start a full case.

## Send it

**Local:**
```bash
curl -s -X POST http://localhost:5230/v1/credit-profiles \
  -H "Content-Type: application/json" \
  --data-binary @"docs/rhshf resources/sample-credit-profile-payload.json"
```

**Sandbox** (add the key once it's configured):
```bash
curl -s -X POST http://18.132.3.119:8980/v1/credit-profiles \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: <the-sandbox-key>" \
  --data-binary @"docs/rhshf resources/sample-credit-profile-payload.json"
```

A **201** returns `reference`, `token`, `profilingUrl`, `tokenExpiresAt`, `status`. Open `profilingUrl`
in a browser to begin the FAC profiling wizard.

## Field reference

| Field | Required | Notes |
|---|---|---|
| `submissionId` | yes | GUID. Idempotency key — resending the same one returns the same case, no duplicate. **Change it for a genuinely new case.** |
| `programme.code` / `.name` | yes | Programme identifier. |
| `session.code` / `.name` | yes | Season. Region is baked into the code (`2026-DRY-NORTH` vs `2026-DRY-SOUTH`) so concurrent north/south runs don't collide. |
| `fac.facId` | yes | GUID for the aggregator. |
| `fac.companyName` | yes | |
| `fac.rcNumber` | yes | CAC registration — the CAC lookup and company bureau check key on this. |
| `fac.tin` | **no** | Optional (BOA confirmed a case can be assessed without one). Omit it entirely to test that path. |
| `fac.boaAccountNumber` | yes | Drives branch resolution. `0000000029` resolves to Lagos Main Branch in mock mode. |
| `fac.contact.email` / `.phone` | no | |
| `fac.state` / `.lga` | no | |
| `totalEopValue` | yes | Must be > 0. **Must equal the sum of `eopLines[].lineValue`.** Also sets the committee tier — see below. |
| `currency` | yes | e.g. `NGN`. |
| `farmerCount` | no | |
| `eopLines[]` | no | `commodity` and `lineValue` are used. Send `quantityKg` / `unitPricePerKg` as **0** — the portal holds quantities per input (bags, litres), not a tonnage of the commodity, and CRMS neither displays nor reasons over those zeros. |
| `callbackUrl` | yes | Absolute URL. Stored per-case; CRMS calls it back with the decision. **Not** a CRMS setting — it comes in on each request. |
| `metadata` | no | Free-form. `certifiedByAdmin` / `certifiedAt` are read if present. |

## Committee tier is set by `totalEopValue`

The amount decides which committee reviews the case. To exercise a specific tier, set `totalEopValue`
(and the line values to match):

| Amount (NGN) | Tier |
|---|---|
| ≤ 2,000,000 | Branch |
| 2,000,001 – 10,000,000 | Zonal |
| 10,000,001 – 30,000,000 | Regional |
| > 30,000,000 | Head Office |

The sample is **18,500,000 → Regional**.

## The walkthrough

1. **Submit** → open `profilingUrl`.
2. **FAC wizard** (5 stages): confirm company (TIN shows "Not provided" here) → bureau check → EOP
   review + **add a farm plan** → attach **mandatory documents** → submit. It won't let you submit
   without a farm plan and the mandatory docs.
3. **Staff side** (`http://localhost:5292`, log in): Appraisal (must **save a financial appraisal**
   before "Proceed") → Risk review → Committee vote → Ratification.
4. **Offer**: after ratification, re-open `profilingUrl` — it now shows "Your offer is ready" with a
   link. `GET /v1/credit-profiles/{reference}/status` returns `actionRequired: "REVIEW_OFFER"` and a
   populated `actionUrl`.
5. Accept the offer → Legal clearance → Pre-deployment checklist → Disbursement → Loan account.

## If the token expires (20 min)

Mint a fresh one without resubmitting:
```bash
curl -s -X POST http://localhost:5230/v1/credit-profiles/{reference}/token
```

## Check status any time
```bash
curl -s http://localhost:5230/v1/credit-profiles/{reference}/status
```
