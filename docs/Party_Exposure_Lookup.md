# Party-exposure lookup (Farmer 360)

What the CRMS holds about one person, in any capacity, found by BVN, NIN or account number. Built
for the RH-SHF portal's Farmer 360 (CRMSQA#4). Read-only: it never changes a case.

## Calling it

| | |
|---|---|
| Single | `POST /api/v1/party-exposure/lookup`. The answer comes back in the same response. |
| Batch | `POST /api/v1/party-exposure/lookup-batch` (up to 500 subjects). It answers `202` with a `batchId`. |
| Poll | `GET /api/v1/party-exposure/lookup-batch/{batchId}` |

Every call carries all of these:

- `X-Api-Key`: the same key as the RH-SHF submission API.
- `X-Portal-Timestamp`: unix seconds.
- `X-Portal-Signature: sha256=<hex>`: HMAC-SHA256 of `"{timestamp}.{raw body}"`, keyed with the
  shared RH-SHF callback signing secret. This is the CRMS's own `X-CRMS-Signature-V2` scheme, run in
  the other direction. A GET signs an empty body.

A request is refused (401 `invalid_signature`) when any of these hold:
- the signature does not match;
- the timestamp is more than 5 minutes either side of the CRMS clock;
- a header is missing.

`Idempotency-Key`, if sent, must equal `requestId`. A batch sent again with the same `requestId`
returns the existing batch; it does not create a new one.

## Request

The request is as proposed in CRMSQA#4 §4. Notes:

- `purpose` must be one of `FARMER360_PROFILE_VIEW`, `FINANCING_ELIGIBILITY_CHECK` or
  `REGISTER_KPI_REFRESH`.
- Identifier formats:
  - **BVN and NIN:** 11 digits.
  - **NUBAN:** 10 digits.
  - Spaces, hyphens and dots are stripped before checking. Anything else is refused with 400, and
    the error names the field but never repeats its value.
  - `accounts` holds at most 10 entries.
- `include.closedFacilities` defaults to `true`.
- `include.historyMonths` defaults to `36` and must be between 1 and 120. It applies to facilities
  that have ended, measured from the date the case last changed. Open facilities are always
  returned.
- **Batch:**
  - Each subject is `{ subjectRef, identifiers }`. A subject with bad identifiers gets
    `matchStatus: ERROR` and a reason; it does not fail the batch.
  - `callbackUrl` is optional. When given, it must be on the CRMS's allow-list (by default, the
    portal's host). The CRMS posts `PARTY_EXPOSURE_BATCH_READY` there, signed like every other CRMS
    callback, when the batch finishes. Polling works either way.

## Response

The response follows CRMSQA#4 §5. These are the points where the CRMS differs or adds to it:

- **`relationships[].track`:** `CORPORATE`, `NAMP` or `RHSHF`.
- **`relationships[].partyRef`:**
  - The CRMS has no party master. Two relationships share a `partyRef` only when their records carry
    the same BVN (or failing that the same NIN, or for a company borrower the same account).
  - Different labels mean the CRMS cannot show it is the same person.
  - The label is a keyed hash and cannot be turned back into an identifier.
- **`relationships[].status`:**

  | Status | Meaning |
  |---|---|
  | `IN_PROCESS` | Being assessed, or approved but not yet booked. |
  | `ACTIVE` | Booked in core banking. |
  | `CLOSED` | Repaid. |
  | `DECLINED` | Refused. |
  | `CANCELLED` | Withdrawn, lapsed or expired. |

  The CRMS does not hold `WRITTEN_OFF`.
- **`relationships[].creditStatus`:** RH-SHF only. It is the same value the portal receives on
  updates for that case.
- **Amounts:**
  - `approved` comes from the CRMS.
  - `outstanding`, `overdue` and `daysPastDue` are read **live from core banking** for booked
    loans, within 1.5 seconds in total.
  - `balanceSource` says which applied:

    | Value | Meaning |
    |---|---|
    | `CORE_BANKING_LIVE` | Read now. `balanceAsOf` holds the time. |
    | `UNAVAILABLE` | Booked, but core banking did not answer in time. The amounts are `null`, never `0`. |
    | `NOT_BOOKED` | Nothing is owed yet, or any longer. |
    | `NOT_REQUESTED` | Batch results. Balances are not read for batches. |

- **`classification`, `summary.worstClassification`:** always `null`. The CRMS holds no prudential
  (CBN) classification. `coverage.classification` is `NONE`.
- **`summary`:**
  - `activeContingentExposure` counts guarantees only. A director or signatory is not liable for
    the debt.
  - A guarantee with no stated amount counts at the loan's outstanding balance.
  - `balancesComplete` is `false` when any active facility's balance could not be read.
- **Masking:**
  - Identifiers in `identifierResults` show the last four digits only.
  - Individual obligors' names are reduced to initials (`A**** B****`); company names are shown in
    full.
  - No identifier of any other party is returned.
- **`identityConsistency`:** the response is `IDENTITY_CONFLICT` with a note naming the case
  (never the identifier) when either of these holds:
  - a record found by one identifier holds a different value for another;
  - the account given belongs to a company borrower while a person's BVN or NIN was also given.
- **`truncated`:** at most 200 relationships are returned. The summary and `identifierResults` still
  count all of them.

## Coverage

What the CRMS holds, as returned in `coverage`:

| Relationship | Coverage | What the CRMS holds |
|---|---|---|
| `BORROWER` | PARTIAL | Individual borrowers on NAMP only: BVN, NIN and account, since May 2026. Corporate and RH-SHF borrowers are companies, found by account only. |
| `CO_BORROWER` | NONE | Not held. |
| `SCHEDULE_BENEFICIARY` | NONE | Only farmer counts per EOP are held, not the farmers. |
| `GUARANTOR` | PARTIAL | BVN on all tracks: corporate since Feb 2026, NAMP since May 2026, RH-SHF since Oct 2026. NIN on NAMP guarantors only. |
| `ACCOUNT_SIGNATORY` | PARTIAL | Corporate applications only (BVN, since Feb 2026). NAMP and RH-SHF signatories are read from core banking when needed and not stored. |
| `DIRECTOR_OR_OWNER` | PARTIAL | BVN only: corporate since Feb 2026, NAMP since Jun 2026, RH-SHF since Sep 2026. Often blank where it is entered by hand. |
| `NEXT_OF_KIN_OR_REFEREE` | NONE | Not held. |

## Limits and audit

- **Rate limits:**
  - 60 single lookups per minute per `requestedBy.portalUserId`, and 600 per minute overall.
  - Over the limit, the CRMS answers `429` with `Retry-After`.
  - The host's per-IP limit does not apply here; the portal's users all arrive from one address.
- **Audit:**
  - Every lookup, single or per batch subject, writes a row to `PartyExposureLookupLogs`.
  - Each row holds `requestId`, `purpose`, `requestedBy`, `subjectRef`, keyed hashes of the
    identifiers, the result counts and the time taken.
  - A lookup that cannot be audited is not answered (`503`).
  - No plain identifier is written to any log or table.
- **Batches:**
  - Worked in the background, 25 subjects at a time with a pause between.
  - Subjects' identifiers are held in memory only, never stored.
  - A batch caught by a CRMS restart is marked `FAILED` and must be sent again.
- **Indexes:**
  - Every identifier column searched has its own index (migration `AddPartyExposureLookup`).
  - Each identifier is a separate exact-match query on a normalised column.
  - Identifiers are normalised to digits when written.
  - MySQL 8 builds these indexes online, without locking the tables.

## Settings (`PartyExposure` section)

| Setting | Default | |
|---|---|---|
| `RequireSignature` | `true` | Switch off only for local work. |
| `SignatureToleranceSeconds` | `300` | |
| `PerUserLookupsPerMinute` / `OverallLookupsPerMinute` | `60` / `600` | |
| `MaxRelationships` | `200` | |
| `LiveBalanceBudgetMs` | `1500` | |
| `BatchMaxSubjects` / `BatchChunkSize` / `BatchChunkPauseMs` | `500` / `25` / `200` | |
| `AllowedCallbackHosts` | host of `Rhshf:PortalUrl` | |
| `HashKey` | the callback signing secret | Key for audit hashes and `partyRef`. |
