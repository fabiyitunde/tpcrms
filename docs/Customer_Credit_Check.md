# Customer credit check (BVN credit, fraud and account check)

One request that tells a partner system what is known about a person, by BVN: who they are, what
the credit bureaus hold on them, whether they look fraudulent, which accounts and loans they hold at
the Bank of Agriculture, every CRMS case they are tied to, and an AI advisory that scores it all.
Built for third-party systems doing credit checks on the bank's behalf. Read-only: it never changes
a case or an account.

A check calls several outside providers and takes up to a minute or two, so it does not run while
the caller waits. The CRMS accepts the request at once, runs it in the background, posts the result
to the consumer's callback URL, and keeps it for the consumer to fetch at any later time.

## Calling it

| | |
|---|---|
| Submit a check | `POST /api/v1/customer-credit-check`, answers `202 Accepted` |
| One check's status and result | `GET /api/v1/customer-credit-check/{jobId}` |
| Past checks | `POST /api/v1/customer-credit-check/history` |
| Auth | `X-Api-Key: <consumer key>` on every call |
| Body | JSON, `Content-Type: application/json` |

Each partner gets its own key, issued from **Admin > API Consumers** in the CRMS intranet. The key
is shown once at creation and stored hashed. A key can be rotated or switched off at any time
without touching the others. Every check is logged against the consumer that made it, with the BVN
kept only as a keyed hash. A consumer sees only its own checks.

### The flow

1. The consumer submits a check. The CRMS checks the key, the body, the consumer's entitlements and
   limits, stores the job, and answers `202` within milliseconds:

   ```json
   {
     "jobId": "7a2f1c9e4b8d4f0a9c3e5d1b2a6f8e7c",
     "requestId": "c7f1e2a0-9b1d-4e4e-8d0a-7b9f3c2d1a55",
     "status": "QUEUED",
     "statusUrl": "/api/v1/customer-credit-check/7a2f1c9e4b8d4f0a9c3e5d1b2a6f8e7c",
     "queuedAt": "2026-11-05T09:42:17Z",
     "callbackRegistered": true
   }
   ```

   The `Location` header carries `statusUrl` too.
2. A background worker runs the check: identity, bureau, fraud, core banking, CRMS, advisory.
3. The CRMS posts the result to the callback URL, signed (see **Callback**). The consumer answers
   any `2xx` to acknowledge. Anything else, or no answer within 15 seconds, is retried after 1, 3,
   7, 15 and 15 minutes. After that the job stays `COMPLETED` with `callback.status: FAILED` and the
   result is still there to fetch.
4. At any time, `GET /api/v1/customer-credit-check/{jobId}` returns the status and, once finished, the
   result. Fetching a stored result is free.

Sending the same `requestId` again within 24 hours returns the existing job (another `202`, with
its current status) rather than running and billing a second check. A new `requestId` for the same
BVN is a new, billed check.

A request is refused when:

| Code | `error` | Meaning |
|---|---|---|
| 401 | `unauthorized` | Key missing, unknown, or switched off. |
| 400 | `invalid_payload` | Body is not valid JSON. |
| 400 | `validation_failed` | `messages` lists what is wrong. The message names the field, never its value. |
| 404 | `not_found` | No job with that id belongs to this consumer. |
| 429 | `rate_limited` | The consumer's per-minute limit, or the overall limit, is reached. `Retry-After` is set. |
| 429 | `queue_full` | The consumer already has its maximum number of checks waiting or running. |
| 503 | `unavailable` | The job could not be stored. Nothing was queued; send it again. |

Sections the consumer is not entitled to are not an error: they come back with
`status: NOT_ALLOWED`.

## Request

Only `requestId` and `bvn` are required. This is a complete request, and it runs the full check:

```json
{
  "requestId": "c7f1e2a0-9b1d-4e4e-8d0a-7b9f3c2d1a55",
  "bvn": "22212344321"
}
```

Everything else is optional. With every optional field filled in:

```json
{
  "requestId": "c7f1e2a0-9b1d-4e4e-8d0a-7b9f3c2d1a55",
  "bvn": "22212344321",
  "nubans": ["0123455678"],
  "sections": ["identity", "creditBureau", "fraudCheck", "bankAccounts", "bankLoans", "crmsFacilities", "aiAdvisory"],
  "callbackUrl": "https://partner.example.com/crms/customer-credit-check",
  "options": {
    "activityWindowMonths": 12,
    "includeScore": true,
    "runAmlCheck": false,
    "loanAmount": null,
    "tenorMonths": null
  }
}
```

- `requestId`: required, up to 100 characters, unique per consumer. It is the idempotency key (see
  **The flow**), is echoed back, and is written to the audit log.
- `bvn`: required, 11 digits. Spaces, hyphens and dots are stripped first.
- `nubans`: optional, up to 5 account numbers, 10 digits each. Accounts and loans are found by BVN,
  so none is needed. Naming accounts puts them first in line for transaction activity. An account
  named here that core banking does not hold under this BVN is pointed out in
  `bankAccounts.reason` and nothing from it is returned.
- `sections`: optional. Defaults to all seven. Bureau, fraud and advisory calls are billed per
  call, so ask only for what you need. `aiAdvisory` scores what the other sections found, so it is
  only worth asking for alongside at least the bureau or the bank sections.
- `callbackUrl`: optional. Where the result is posted. The host must be one the bank has allowed
  for this consumer; in production it must be `https`. When absent, the consumer's registered
  callback URL is used. When neither exists, no callback is sent and the result is fetched from
  `statusUrl`.
- `options.activityWindowMonths`: 1 to 24, default 12. How far back the transaction activity on the
  customer's accounts is read.
- `options.includeScore`: default `true`. Also fetch the bureau's credit score (a separate billed
  call).
- `options.runAmlCheck`: default `false`. Ask SmartComply to add an AML screen to the fraud check.
- `options.loanAmount` and `options.tenorMonths`: optional. The fraud check scores a proposed loan;
  without them a nominal amount and tenor are sent and the financial-analysis sub-scores are
  reported as not assessed.

## Status and history

`GET /api/v1/customer-credit-check/{jobId}`:

```json
{
  "jobId": "7a2f1c9e4b8d4f0a9c3e5d1b2a6f8e7c",
  "requestId": "c7f1e2a0-9b1d-4e4e-8d0a-7b9f3c2d1a55",
  "status": "COMPLETED",
  "queuedAt": "2026-11-05T09:42:17Z",
  "startedAt": "2026-11-05T09:42:18Z",
  "completedAt": "2026-11-05T09:43:01Z",
  "sections": ["identity", "creditBureau", "fraudCheck", "bankAccounts", "bankLoans", "crmsFacilities", "aiAdvisory"],
  "error": null,
  "callback": {
    "status": "DELIVERED",
    "attempts": 1,
    "deliveredAt": "2026-11-05T09:43:02Z",
    "lastStatusCode": 200,
    "nextAttemptAt": null
  },
  "result": { "...the result, as under Result below..." }
}
```

- `status` is `QUEUED`, `PROCESSING`, `COMPLETED` or `FAILED`. `result` is present only when
  `COMPLETED`. A `FAILED` job carries `error: { "code", "message" }` and should be sent again with
  a new `requestId`; nothing is billed for a failed check.
- `callback.status` is `NOT_REQUESTED` (no URL), `PENDING`, `DELIVERED` or `FAILED` (every attempt
  refused).

`POST /api/v1/customer-credit-check/history` lists the consumer's past checks, newest first, without
their results. It is a `POST` because a BVN must never travel in a URL.

```json
{ "from": "2026-11-01", "to": "2026-11-30", "status": "COMPLETED", "requestId": null, "bvn": null, "page": 1, "pageSize": 50 }
```

Every field is optional. `bvn` is matched on its keyed hash. `pageSize` is at most 100. The reply is
`{ "page", "pageSize", "total", "items": [ { jobId, requestId, status, queuedAt, completedAt,
sections, bvnLast4, callback: { status } } ] }`. Fetch any item's result from its `jobId`.

## Callback

When a check finishes, the CRMS posts one JSON envelope to the callback URL:

```json
{
  "eventType": "CUSTOMER_CREDIT_CHECK_COMPLETED",
  "eventId": "evt_3f9c1a7e2b5d4c8e9a1f6b2d7c3e8a4f",
  "jobId": "7a2f1c9e4b8d4f0a9c3e5d1b2a6f8e7c",
  "requestId": "c7f1e2a0-9b1d-4e4e-8d0a-7b9f3c2d1a55",
  "status": "COMPLETED",
  "occurredAt": "2026-11-05T09:43:01Z",
  "statusUrl": "/api/v1/customer-credit-check/7a2f1c9e4b8d4f0a9c3e5d1b2a6f8e7c",
  "error": null,
  "result": { "...the result, as under Result below..." }
}
```

- `eventType` is `CUSTOMER_CREDIT_CHECK_COMPLETED` or `CUSTOMER_CREDIT_CHECK_FAILED`. A failed check carries
  `error` and no `result`.
- `eventId` is stable across every retry of the same event. A receiver that has already processed
  an `eventId` should answer `2xx` and ignore the repeat.
- Headers: `Content-Type: application/json`, `X-CRMS-Event-Id`, `X-CRMS-Event-Type`,
  `X-CRMS-Timestamp` (Unix seconds) and `X-CRMS-Signature-V2`.

### Verifying a callback

Each consumer has its own callback signing secret, issued from **Admin > API Consumers** next to
the key and shown once. The signature is HMAC-SHA256 over `"{timestamp}.{raw body}"` with that
secret, hex, sent as `X-CRMS-Signature-V2: sha256=<hex>`. Verify it before reading the body:

1. Take `X-CRMS-Timestamp`. Reject the call if it is more than five minutes from now.
2. Compute HMAC-SHA256 over the timestamp, a dot, and the raw request body, exactly as received.
3. Compare, in constant time, with the value after `sha256=` in `X-CRMS-Signature-V2`.

```csharp
static bool Verify(string timestamp, string rawBody, string header, string secret)
{
    if (Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - long.Parse(timestamp)) > 300) return false;
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{rawBody}"))).ToLowerInvariant();
    var given = header.StartsWith("sha256=") ? header[7..] : header;
    return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given));
}
```

## Result

The result is the same object whether it arrives in the callback or from `statusUrl`:

```json
{
  "requestId": "c7f1e2a0-9b1d-4e4e-8d0a-7b9f3c2d1a55",
  "dataAsOf": "2026-11-04T09:42:17Z",
  "consumer": "AgriPay Ltd",
  "subject": { "bvnLast4": "4321", "nubansChecked": ["****5678"] },
  "sectionsRun": ["identity", "creditBureau", "fraudCheck", "bankAccounts", "bankLoans", "crmsFacilities"],

  "identity": {
    "status": "OK",
    "source": "SmartComply BVN Advanced",
    "fullName": "ADEBAYO MUSA OKORO",
    "dateOfBirth": "1985-03-14",
    "gender": "Male",
    "phoneNumber": "0803****123",
    "stateOfResidence": "Kaduna",
    "enrollmentBank": "Bank of Agriculture",
    "registrationDate": "2014-08-22",
    "watchListed": false
  },

  "creditBureau": {
    "status": "OK",
    "bureau": "FIRST_CENTRAL",
    "bureausSkipped": [],
    "reused": false,
    "reportDate": "2026-11-04T09:42:11Z",
    "score": { "value": 612, "band": "Fair", "source": "FIRST_CENTRAL" },
    "summary": {
      "totalLoans": 5, "institutions": 3, "activeLoans": 2, "closedLoans": 3,
      "performingLoans": 1, "delinquentLoans": 1,
      "highestLoanAmount": 2500000.00, "totalBorrowed": 6100000.00,
      "totalOutstanding": 1840000.00, "totalOverdue": 215000.00,
      "maxDaysOverdue": 94, "currency": "NGN"
    },
    "loans": [
      {
        "lender": "FairMoney MFB",
        "accountNumberLast4": "2210",
        "type": "Personal Loan",
        "amount": 300000.00,
        "outstanding": 215000.00,
        "overdue": 215000.00,
        "status": "OVERDUE",
        "performance": "Delinquent",
        "disbursedDate": "2026-05-02",
        "maturityDate": "2026-08-02",
        "lastPaymentDate": "2026-06-30",
        "tenor": "3 months",
        "repaymentFrequency": "Monthly"
      }
    ],
    "recentEnquiries": [
      { "institution": "Carbon MFB", "reason": "Loan application", "date": "2026-10-19" }
    ]
  },

  "fraudCheck": {
    "status": "OK",
    "riskScore": 38,
    "riskBand": "MEDIUM",
    "recommendation": "Proceed with caution",
    "amlChecked": false,
    "financialAnalysis": {
      "assessed": false,
      "reason": "No loan amount, tenor or income supplied; defaults were used, so these sub-scores are not meaningful.",
      "items": null
    }
  },

  "bankAccounts": {
    "status": "OK",
    "bvnMatch": "MATCH",
    "clientId": "1001",
    "clientStatus": "Active",
    "kycLevel": "2",
    "customerSince": "2019-02-11",
    "accounts": [
      {
        "accountNumberLast4": "5678",
        "productName": "Farmers Savings",
        "accountType": "SAVINGS",
        "status": "ACTIVE",
        "rawStatus": "Active",
        "openedDate": "2019-02-11",
        "balance": 142300.55,
        "currency": "NGN",
        "lastTransaction": "2026-11-01",
        "activity": {
          "windowMonths": 12,
          "firstTransaction": "2025-11-04",
          "lastTransaction": "2026-11-01",
          "transactionsInWindow": 87,
          "creditsInWindow": 1850000.00,
          "debitsInWindow": 1702400.00,
          "monthsSinceOpened": 92
        }
      }
    ]
  },

  "bankLoans": {
    "status": "OK",
    "loans": [
      {
        "loanAccountLast4": "9981",
        "productName": "RH-SHF Dry Season Inputs",
        "principal": 2500000.00,
        "outstanding": 1625000.00,
        "overdueAmount": 0.00,
        "daysPastDue": 0,
        "status": "RUNNING",
        "rawStatus": "Active",
        "disbursedDate": "2025-06-10",
        "maturityDate": "2027-06-10",
        "nextDueDate": "2026-11-30",
        "nextDueAmount": 112500.00
      }
    ]
  },

  "crmsFacilities": {
    "status": "OK",
    "matchStatus": "MATCH",
    "summary": { "direct": 2, "indirect": 1, "activeOutstanding": 1625000.00, "balancesComplete": true },
    "relationships": [
      {
        "role": "BORROWER",
        "track": "RHSHF",
        "caseReference": "RHSHF-2025-00318",
        "facilityType": "Dry-season inputs",
        "obligor": "Grandbai Limited",
        "status": "ACTIVE",
        "amountApproved": 2500000.00,
        "guaranteeAmount": null,
        "outstanding": 1625000.00,
        "daysPastDue": 0,
        "balanceSource": "CORE_BANKING_LIVE"
      }
    ]
  },

  "aiAdvisory": {
    "status": "OK",
    "overallScore": 61.4,
    "overallRating": "Low",
    "recommendation": "ApproveWithConditions",
    "categories": [
      {
        "category": "CreditHistory", "assessed": true, "score": 55.0, "weight": 0.40, "rating": "Medium",
        "rationale": "FIRST_CENTRAL report: 5 loans at 2 active, 1 delinquent, 0 written off, NGN 1,840,000 outstanding. With the bank: 1 running, 0 past due, 2 repaid, 0 written off.",
        "positives": ["2 loans with the bank repaid in full.", "1 running loan with the bank, none past due."],
        "concerns": ["1 bureau loan is delinquent (NGN 215,000 overdue).", "Up to 94 days overdue at a bureau."]
      },
      {
        "category": "RepaymentCapacity", "assessed": true, "score": 75.0, "weight": 0.25, "rating": "Low",
        "rationale": "Estimated instalment NGN 48,333 a month on NGN 500,000 over 12 months, plus NGN 112,500 already due monthly with the bank, against average monthly inflow of NGN 420,000 (38%).",
        "positives": ["Obligations would take 38% of monthly inflow."],
        "concerns": ["Existing instalments with the bank of NGN 112,500 a month are counted."]
      },
      { "category": "CashflowStability", "assessed": true, "score": 70.0, "weight": 0.20, "rating": "Low", "rationale": "87 transactions over 12 months on 1 account. Average inflow NGN 420,000, outflow NGN 390,000 a month; balances total NGN 142,301.", "positives": ["Positive net monthly cashflow of NGN 30,000."], "concerns": [] },
      { "category": "IdentityAndFraud", "assessed": true, "score": 65.0, "weight": 0.15, "rating": "Low", "rationale": "BVN verified; watch-list clear; fraud risk 38 (MEDIUM); bank customer yes.", "positives": ["BVN verified with the identity provider.", "A customer of the bank for 92 months."], "concerns": ["Fraud risk score 38 (medium)."] }
    ],
    "redFlags": ["1 delinquent bureau facility."],
    "conditions": ["Clear all overdue facilities before disbursement."],
    "narrative": {
      "summary": "...",
      "strengths": "...",
      "weaknesses": "...",
      "mitigatingFactors": "...",
      "keyRisks": "..."
    },
    "proposal": { "amount": 500000.00, "tenorMonths": 12, "supplied": true, "recommendedAmount": 375000.00, "recommendedTenorMonths": 12 },
    "basis": "Scored from FIRST_CENTRAL report, statements on 1 account over 12 months, loans with the bank, identity check, fraud check. The overall score is weighted over the assessed categories only.",
    "narrativeSource": "LLM",
    "modelVersion": "hybrid-v1.0-rules+llm",
    "generatedAt": "2026-11-04T09:42:40Z"
  },

  "coverage": {
    "bankAccounts": "Every account under the customer records core banking holds for this BVN. Transaction activity is read for a bounded number of them; reason says how many.",
    "bankLoans": "Loans booked in core banking for this customer. Days past due is computed from the repayment schedule.",
    "creditBureau": "FIRST_CENTRAL answered; no other bureau was called.",
    "fraudCheck": "Overall score only. The financial analysis needs a loan amount, tenor and income.",
    "crmsFacilities": "Every CRMS case the BVN appears on, in any role. No prudential classification is held."
  },
  "warnings": [
    "One bureau loan is 94 days overdue."
  ]
}
```

### Conventions

- **Section status.** Every section is always present and carries its own `status`:
  - `OK`: answered.
  - `NO_RECORD`: the provider answered and holds nothing for this person (a bureau, or core
    banking for `bankAccounts` and `bankLoans`).
  - `UNAVAILABLE`: the provider failed or did not answer in time. `reason` says which.
  - `NOT_REQUESTED`: not in `sections`.
  - `NOT_ALLOWED`: the consumer is not entitled to it.
  - `REFUSED` and `NOT_POSSIBLE` are reserved and not returned at present.

  One section failing never blanks another.
- **Loan status words** are the same in every section: `RUNNING`, `OVERDUE`, `PAID`, `WRITTEN_OFF`,
  `IN_PROCESS`, `UNKNOWN`. The provider's own word is kept in `performance` (bureau) or `rawStatus`
  (core banking).
- **Account status words:** `ACTIVE`, `DORMANT`, `CLOSED`, `INACTIVE`, `UNKNOWN`, with `rawStatus`
  alongside. A matured fixed deposit is `INACTIVE`. `accountType` is the kind of account as core
  banking names it, upper-cased: `SAVINGS`, `FIXED DEPOSIT`, `RECURRING DEPOSIT`.
- **Masking.** Account numbers come back as their last four digits. Phone numbers are partly masked.
  The full BVN never appears in the reply or in any log line.
- **Bureau fallback.** Bureaus are tried in the configured order (First Central, CRC, Credit
  Registry by default). A bureau that errors is skipped and named in `bureausSkipped`; the next is
  tried. A bureau that answers "no record" is an answer: the section is `NO_RECORD` and no further
  bureau is billed.
- **Bureau reuse.** When the consumer's "reuse bureau reports" switch is on, a report for the same
  BVN newer than the consumer's maximum age is returned from the stored copy. `reused` is then
  `true` and `reportDate` is the original date.
- **Fraud check.** `riskBand` is `LOW` (below 30), `MEDIUM` (30 to 69) or `HIGH` (70 and above).
  `financialAnalysis.assessed` is `true` only when the request supplied both `loanAmount` and
  `tenorMonths`; income is never known, which is why the block is marked as such.
- **Bank accounts.** The customer is found in core banking by BVN, so `bvnMatch` is `MATCH`
  whenever the section is `OK`. When more than one customer record carries the BVN, accounts and
  loans from all of them are listed and `reason` says so; `clientId`, `clientStatus` and
  `customerSince` are the first record's. `openedDate` and `lastTransaction` per account come from
  core banking. `activity` (the statement over `activityWindowMonths`) is read for at most
  `MaxActivityAccounts` accounts: the ones named in `nubans` first, then active accounts, then by
  balance. Other accounts carry `activity: null`, and `reason` says how many were read. An
  account whose statement could not be read in time also carries `activity: null` and is named
  in `reason`.
- **Days past due** on bank loans is the age of the oldest unpaid instalment, computed from the
  schedule.
- **`crmsFacilities`** is the party-exposure lookup (see `Party_Exposure_Lookup.md`) reduced to the
  fields a partner needs. `role` is one of `BORROWER`, `GUARANTOR`, `DIRECTOR_OR_OWNER`,
  `ACCOUNT_SIGNATORY`, `NEXT_OF_KIN_OR_REFEREE`, `OTHER`.
- **`aiAdvisory`** runs last and scores what the other sections found, with the same engine the
  bank's loan tracks use: the rules decide every number, and the narrative only explains them.
  - Four categories, each scored 0 to 100: `CreditHistory` (bureau plus the person's loans with
    the bank), `RepaymentCapacity` (the proposed instalment plus existing instalments, against
    average monthly inflow), `CashflowStability` (statement activity) and `IdentityAndFraud` (BVN
    verification, watch-list, fraud score, relationship with the bank). Default weights 40, 25,
    20 and 15; the bank can change them.
  - A category the data cannot support is returned with `assessed: false`, no score, and the
    reason; `overallScore` is weighted over the assessed categories only. `basis` says what was
    scored from.
  - `overallRating` is `VeryLow`, `Low`, `Medium`, `High` or `VeryHigh` risk. `recommendation` is
    `StrongApprove`, `Approve`, `ApproveWithConditions`, `Refer` or `Decline`.
  - `proposal` is what capacity was measured against: `options.loanAmount` and `options.tenorMonths`
    when supplied, otherwise the configured stand-ins with `supplied: false` and a small penalty.
    `recommendedAmount` is what the rules would lend, present only when a proposal was supplied.
  - `narrativeSource` is `LLM` when the narrative came from the language model, `TEMPLATE` when it
    was assembled from the rules' own reasons (the model off, slow or failing). The prompt sent to
    the model carries figures and flags only: no name, BVN, account number, phone or date of
    birth leaves the CRMS.
  - The section is `UNAVAILABLE` when no other section produced anything to score.

## What the CRMS cannot supply

- Income. The fraud check's financial analysis is therefore never fully assessed.
- Prudential (CBN) classification of any loan.
- A bureau's full PDF. The structured report is returned instead.

## Administration

**Admin > API Consumers** (SystemAdmin): add a consumer, copy its key and callback signing secret
once, set which sections it may ask for, its per-minute limit, how many checks it may have waiting
at once, its callback URL and the hosts a per-request `callbackUrl` may point to, and whether recent
bureau reports may be reused and for how many days. Rotate the key or the signing secret, or switch
the consumer off, from the same page.

**Admin > Partner API** (SystemAdmin): the dashboard. For a period and, optionally, one consumer:
checks submitted, completed and failed; billable third-party calls by kind; the amount billable
under the price list; time to result; callback delivery; checks per day by consumer; section
answers; advisory ratings and recommendations; bureau usage; when partners call; a consumer
leaderboard; recent failures. The **price list** on the same page is what a consumer is charged
per item: per completed check, per section answered (`OK` or `NO_RECORD`), and per third-party
call by kind. Every item starts at zero. **Statement** downloads a CSV for the selected consumer
and period: one line per check with its calls and amount, then totals by item.

**Admin > Customer Credit Checks** (SystemAdmin): every job, filtered by consumer, status, date and
request id. Opening one shows its sections and outcomes, the advisory headline, the callback's
attempts and last error, and the result exactly as it was sent. **Resend callback** queues the
same event again; the API host posts it within a few seconds.

**Admin > Scoring Config**, category *Customer Credit Check (individuals)*: the advisory's weights,
obligation-ratio bands and penalties. **Seed defaults** adds any parameter that does not exist
yet without touching the ones that do.

Deployment settings (`CustomerCheck` section):

| Key | Default | Meaning |
|---|---|---|
| `BureauOrder` | `FirstCentral, CRC, CreditRegistry` | Fallback order. |
| `BureauTimeoutMs` | 25000 | Per bureau call. |
| `CoreBankingTimeoutMs` | 20000 | Per core banking call. |
| `BankSectionTimeoutMs` | 120000 | Cap on the bank sections as a whole. |
| `JobTimeoutMs` | 300000 | Cap on one whole check. |
| `WorkerConcurrency` | 4 | Checks run at the same time. |
| `MaxRunAttempts` | 2 | Times a check interrupted by a restart is run again before it is failed. |
| `IdempotencyWindowHours` | 24 | How long a repeated `requestId` returns the existing job. |
| `CallbackTimeoutMs` | 15000 | Per callback attempt. |
| `RequireHttpsCallbacks` | true | Set `false` only on a local or test box. |
| `AdvisoryTimeoutMs` | 60000 | Cap on the advisory, scoring and narrative together. |
| `DataKey` | (derived from `HashKey`) | Base64 32-byte AES key for stored requests, results and callback secrets. Set it in production, the same value in the API and the intranet. |
| `FraudDefaultLoanAmount` | 100000 | Sent when the request has none. |
| `FraudDefaultTenorMonths` | 12 | Sent when the request has none. |
| `MaxNubans` | 5 | Account numbers accepted per request. |
| `MaxActivityAccounts` | 5 | Accounts whose statement is read per check. |
| `OverallLookupsPerMinute` | 300 | Across all consumers. |
| `HashKey` | (RH-SHF callback secret) | Key for the audit and reuse hashes. Set it in production, the same value in the API and the intranet. |

The API host needs the `SmartComply` section configured with a real key for the bureau, fraud and
identity sections; with `UseMock: true` those sections return sample data. For the advisory's
narrative it needs `OpenAI:ApiKey` and `AIAdvisory:UseLLMNarrative: true`, like the intranet;
without them the narrative is assembled from the rules' own reasons and `narrativeSource` says
`TEMPLATE`.

### Going live

Do these once, in this order, on the environment partners will call.

1. **Keys.** Set `CustomerCheck:HashKey` and `CustomerCheck:DataKey` in the settings of both hosts,
   the API and the intranet, to the same pair. `DataKey` is the base64 of 32 random bytes
   (`openssl rand -base64 32`); `HashKey` is any long random string. Keep a copy somewhere safe
   outside the server. Do not change either after the first check: stored results and callback
   secrets can no longer be read, and stored bureau reports are no longer found for reuse. Left
   blank, both are derived from the RH-SHF callback secret, so changing that secret would have
   the same effect.
2. **Integrations on the API host.** `SmartComply` with a real key and `UseMock: false`,
   `CoreBanking` and `FineractDirect` pointing at the live core banking. The checks run in the API
   host, so the intranet's settings do not cover it.
3. **Publish both hosts.** The API runs the worker and applies the migrations at startup. The
   intranet carries the administration pages.
4. **Price list.** Admin > Partner API. Every item starts at zero, and a check is priced with the
   list as it stands when the statement is drawn.
5. **Consumer.** Admin > API Consumers: add the partner, with an `https` callback URL, the hosts a
   per-request `callbackUrl` may use, the sections it may ask for and its limits. The key and the
   callback signing secret are shown once; hand them over by a private channel.
6. **First check.** Have the partner send one request with a real BVN. In Admin > Customer Credit
   Checks the job should read `COMPLETED` and its callback `DELIVERED`.

The reference at `/scalar/v1` is served when `EnableApiDocs` is `true`. It describes the API and
holds no data, but set it to `false` if the address should not be public.
