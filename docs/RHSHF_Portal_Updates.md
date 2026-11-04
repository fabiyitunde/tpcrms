# RH-SHF: what the CRMS sends the portal

This is the contract for the updates the CRMS pushes to the RH-SHF portal, and for the status check
the portal can call at any time. It answers CRMSQA#1 and #2. The portal team's side is tracked in
CRMSQA#3.

## How updates are sent

- **Where:** a `POST` to the `callbackUrl` the portal gave when it submitted the case.
- **Signed:** each request carries `X-CRMS-Timestamp`, `X-CRMS-Signature: sha256=HMAC(body)` and
  `X-CRMS-Signature-V2: sha256=HMAC("{timestamp}.{body}")`, keyed with the shared callback secret
  (`Rhshf:CallbackSigningSecret`). The portal should reject anything that fails V2 or carries a
  stale timestamp.
- **Retried:** 5 attempts, about 1, 3, 7 and 15 minutes apart. Every attempt and its HTTP response is
  listed on the case in the CRMS (Workflow tab, Portal Updates), with a Send again button.
- **Idempotent:** `eventId` is the same on every retry of one update. Process an `eventId` once.
- **Ordered:** every update carries the case **as it stands when it is sent**, plus `stateVersion`,
  which only goes up. Ignore an update whose `stateVersion` is lower than the one you hold. A late
  retry therefore never moves a case backwards.
- **Recovery:** `GET /v1/credit-profiles/{reference}/status` (API key) returns the same fields:
  `creditStatus`, `stateVersion`, `offer`, `actionRequired`, `actionUrl`, `actionDeadline`,
  `correction`, `decision`, `decisionBasis`, `statusReasonCode` and `schemaVersion`. Its `eventType` is the type of the last
  update the CRMS sent for the case, null if none, since a poll is not an event of its own. Use it for
  the Refresh Status button and for a scheduled check of open cases.

## Payload

```json
{
  "reference": "RHSHF-2026-142126",
  "submissionId": "…",
  "eventId": "evt_…",
  "eventType": "OFFER_ACCEPTED",
  "occurredAt": "2026-10-28T16:56:40Z",
  "status": "UNDER_REVIEW",
  "creditStatus": "OFFER_ACCEPTED",
  "stateVersion": 41,
  "actionRequired": null,
  "actionUrl": null,
  "actionDeadline": null,
  "schemaVersion": 1,
  "offer": { "status": "ACCEPTED", "version": 4, "approvedAmount": 12884464.00, "currency": "NGN",
             "issuedAt": "2026-10-28T10:24:36Z", "respondedAt": "2026-10-28T16:56:40Z" },
  "correction": null,
  "decision": null
}
```

`status` is the original coarse field, kept so older handling keeps working. Display `creditStatus`.
`actionRequired`, `actionUrl`, `actionDeadline`, `offer`, `correction`, `decision`, `decisionBasis` and
`statusReasonCode` are left out when empty.

- **`actionDeadline`:** by when the FAC must act. While an offer awaits acceptance it is the offer's
  expiry date, which an administrator sets on the RH-SHF product (7 days unless changed). A correction
  has no deadline. The CRMS does not yet lapse an offer on this date.
- **`schemaVersion`:** the version of this contract, on every update and status check. It goes up only
  for a change a reader must act on: a field removed, renamed or given a new meaning, or a new value in
  an existing field. A new optional field does not change it.

## Reason codes

A return or a decline carries a reason code beside the officer's note. The officer picks the reason
from a list an administrator keeps in the CRMS (Admin, RH-SHF Reason Codes). The list starts with the
defaults below and can be changed without a release: wording reworded, reasons added or switched off.
A code itself never changes once created, so a code the portal has stored keeps its meaning.

- **`correction.reasonCode`:** on a return, the reason for it.
- **`statusReasonCode`:** why the case is where it is. The return's code while a correction is
  outstanding, the decline's code when declined, `OFFER_REJECTED_BY_FAC` when the FAC rejected the
  offer, and left out otherwise.

Returns: `DOCUMENT_MISSING`, `DOCUMENT_UNREADABLE`, `DOCUMENT_INVALID`, `COMPANY_DETAILS_MISMATCH`,
`DIRECTOR_DETAILS_INCOMPLETE`, `GUARANTOR_DETAILS_INCOMPLETE`, `COLLATERAL_DETAILS_INCOMPLETE`,
`FINANCIAL_INFORMATION_INCOMPLETE`, `EOP_INFORMATION_INCONSISTENT`.

Declines: `ELIGIBILITY_NOT_MET`, `ADVERSE_CREDIT_HISTORY`, `NON_PERFORMING_OBLIGATIONS`,
`KYC_VERIFICATION_FAILED`, `INSUFFICIENT_SECURITY`, `INSUFFICIENT_CAPACITY`, `INCOMPLETE_APPLICATION`,
`POLICY_EXCLUSION`.

`OTHER` serves both, and is what a committee decline carries. Treat a code you do not recognise as
`OTHER` and show the note: an administrator may add codes at any time. Cases returned or declined
before reason codes existed carry none.

## Events

| `eventType` | Sent when | `creditStatus` afterwards | FAC's next step |
|---|---|---|---|
| `SUBMITTED_FOR_REVIEW` | The FAC submits its profile on the CRMS | `UNDER_CREDIT_REVIEW` | Wait |
| `CORRECTION_REQUIRED` | An officer returns the case to the FAC | `CORRECTION_REQUIRED` | `actionRequired: CORRECT_PROFILE`; `correction` has the stage and the officer's reason |
| `CORRECTION_SUBMITTED` | The FAC resubmits | `UNDER_CREDIT_REVIEW` | Wait |
| `OFFER_READY` | An offer is issued, including a corrected one (higher `offer.version`) | `OFFER_READY` | `actionRequired: REVIEW_OFFER`, `actionUrl` = the CRMS offer page |
| `OFFER_ACCEPTED` | The FAC accepts the current offer version | `OFFER_ACCEPTED` | Wait for BOA's checks before disbursement |
| `DECIDED` | The final outcome | `CREDIT_APPROVED`, `CREDIT_DECLINED` or `CANCELLED` | See `decision` |

## The final decision

`DECIDED` is the final credit decision.

- **Approved** is sent only when the loan has been booked in core banking. That is after the offer is
  accepted and BOA's pre-deployment checks are complete. `creditStatus` is `CREDIT_APPROVED` and
  `decision` carries `outcome: "APPROVED"`, `approvedAmount`, `currency`, `decidedAt` and `decidedBy`.
- **Declined** can come from any review stage. `creditStatus` is `CREDIT_DECLINED` and
  `decision.reasons` carries the decision notes.
- **Cancelled** means the FAC rejected the offer. `decision` is null, because no credit decision was
  made.

None of these events, including `CREDIT_APPROVED`, authorises supply. Supplier release, release
letters and fulfilment stay with the portal and BOA's own release controls.

## Not sent yet

- Offer expiry: offers state a 7-day validity and the FAC is emailed a reminder two days before, but
  the CRMS does not yet expire an offer, so there is no `OFFER_EXPIRED`.

## What the CRMS reads from a submission (portal Slice B)

All of these are optional. When the portal sends them, the CRMS stores them and prints them on the
offer letter. A case submitted without them works exactly as before. None of them is used to compute
or check an amount; `amount` stays the figure that counts.

The names are the portal's frozen v1 contract (CRMSQA#3, 29 Oct). The CRMS first read the names from
the portal's earlier assessment; those are still accepted, shown in brackets, and a v1 name wins
when both are sent.

**On each `financing.inputLines[]` entry:**

| Field | Meaning |
|---|---|
| `packSizeUnit` (`baseUnit`) | The unit a pack is counted in, e.g. "Kg". |
| `packSize` | What one supplier pack holds, in `packSizeUnit`. |
| `packUnit` | The name of a pack, e.g. "50 Kg Bag". |
| `numberOfPacks` (`packCount`) | Whole packs bought for the EOP. |
| `pricePerPack` | The price of one pack. |
| `procurementQuantity` | What was actually bought, in `packSizeUnit`. |
| `packagingAdjustmentQuantity` (`packagingExcessQuantity`) | What was bought above the requirement because packs are whole. |
| `priceVersionId` | The supplier price-list entry used. |
| `priceVersion` | That entry's version number. The letter prints it ("price version 3"). |
| `supplierProductId` | The supplier's product. |
| `serviceProvider` | On a service line, the provider BOA approved for the EOP: `{ providerId, legalName (registeredName), serviceType, approvedRate (rate), rateBasis, rateVersion, quantity (billableQuantity), assignmentReference (assignmentId) }`. `rateVersion` may be a number or text. |

`legalName` is the name on BOA's provider register; it is not checked against CAC. The other v1
fields (`lineId`, `supplierCatalogueReference`, `packagingAdjustment`, `providerStatus`,
`serviceProvider.registrationReference`, `rateId`, `assignmentStatus`) are kept in the stored
submission but not used yet.

When `serviceProvider` is present, the letter names the provider instead of "To be confirmed". A
financed service with a provider no longer blocks ratification.

**On each `financing.eops[]` entry:**

| Field | Meaning |
|---|---|
| `eopReference` | The EOP reference the portal's release letters print. The letter uses it in Schedule A. |
| `boaApprovalDate` (`approvedAt`) | When BOA approved the EOP. |
| `revision` | Which approved revision of the EOP this is. |
| `snapshotHash`, `snapshotHashScheme` | The portal's v1 fingerprint of the EOP and the rules it was built by. |
| `snapshotHashV2`, `snapshotHashV2Scheme` | The portal's second fingerprint, which also covers the provider assignment and rate. Sent beside v1. |

Text values longer than 200 characters are shortened rather than refused. A repeat of a submission
the CRMS already holds does not replace a breakdown it already has, so these fields reach new cases,
not existing ones (see CRMSQA#3).

## EOP versions and decisions (CRMSQA#3 contract points 3 to 5)

**Both hashes are checked.** When an EOP arrives with `snapshotHashScheme` `rhshf-eop-snapshot-v1`, or
`snapshotHashV2Scheme` `rhshf-eop-snapshot-v2`, the CRMS works each hash out from the EOP and its lines in
the same payload, by the portal's published rules, and compares. The v2 rules are the portal's of 4 Nov;
the CRMS reproduces all five of its worked hashes. The result is kept on the EOP: verified, mismatch, not
sent, or unsupported scheme. Either hash not matching is a mismatch, since both describe the same EOP. A
mismatch does not refuse the submission, but it blocks ratification until a Final Approver overrides it
with a reason, the same way a service line without a provider does.

In v2, `rateVersion` is hashed as a JSON integer, `assignmentReference` and `rateId` as lowercase GUIDs,
and `serviceInputReference` is read from the line as sent (null until the portal's Slice 2).

**Each offer and decision names the EOP version it was made on.** Once a case has an offer or a final
decision, the webhook and `GET /v1/credit-profiles/{reference}/status` carry `decisionBasis`:

```json
"decisionBasis": [
  { "eopId": "3cc02579-…", "eopReference": "EOP-3CC02579", "revision": 1,
    "snapshotHash": "…", "snapshotHashScheme": "rhshf-eop-snapshot-v2" }
]
```

The hash is v2 when the portal sent one, otherwise v1. Before an offer or decision it is left out of
the webhook and null on the status check. A decision applies only to the versions listed; the portal
does not carry it over to an amended EOP.

**An amended EOP for a case the CRMS already holds is refused, not ignored.** A repeat with the same
`submissionId` whose EOP hash differs from the one held (v2 where both sides have it, otherwise v1),
or that adds or drops an EOP, is answered with **409**:

```json
{ "error": "eop_changed", "message": "…", "changedEops": ["EOP-3CC02579"] }
```

Nothing from that repeat is taken, the farmer list included. A plain retry carries the same hashes and
is answered as before. When neither side carries a hash, the CRMS cannot tell an amendment from a retry,
and the repeat is answered as before.

**The same EOP is not taken on two live cases.** A new submission (a new `submissionId`) carrying an
EOP that is already on another case that is open or approved is answered with **409**, and no case is
created:

```json
{ "error": "eop_on_another_case", "message": "…",
  "eops": [ { "eopId": "3cc02579-…", "eopReference": "EOP-3CC02579", "caseReference": "RHSHF-2026-142126" } ] }
```

It is permanent: a retry gets the same answer. A declined, cancelled or expired case does not hold its
EOPs, so a new submission after one of those is taken as a new case. Amending a case's EOP is a separate
process, not built yet; until it is, an amendment must not be sent as a new submission.

## The farmer schedule

`farmerSchedule` is optional, at the top level of the submission. It holds one entry per farmer
per EOP:

```json
"farmerSchedule": [
  { "eopId": "…", "farmerRef": "portal farmer id", "bvn": "22212345678", "nin": null, "nuban": "0123456789" }
]
```

- **Each entry:** needs at least one of `bvn`, `nin` or `nuban`. BVN and NIN are 11 digits and
  NUBAN is 10. Spaces and hyphens are stripped.
- **Which EOP:** `eopId` must be one of the case's EOPs when the breakdown names them.
- **Duplicates:** the same farmer may not appear twice under one EOP.
- **Refusal:** a schedule with any bad entry is refused whole (400). The error gives the entry's
  position, never its identifiers.
- **Size:** at most 20,000 farmers.
- **What is kept:** only the identifiers and `farmerRef`. No name, phone or address.
- **What it is for:** the party-exposure lookup (CRMSQA#4), which can then report a farmer as a
  `SCHEDULE_BENEFICIARY` of the FAC's facility. It changes no amount and no decision.
- **Repeats:** a repeat of a submission the CRMS already holds adds the schedule if the case has
  none, at any stage. This is how existing cases, Grandbai among them, get their farmer list. A
  schedule already held is never replaced.
