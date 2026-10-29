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
- **Recovery:** `GET /v1/credit-profiles/{reference}/status` (API key) returns the same fields. Use
  it for the Refresh Status button and for a scheduled check of open cases.

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
  "offer": { "status": "ACCEPTED", "version": 4, "approvedAmount": 12884464.00, "currency": "NGN",
             "issuedAt": "2026-10-28T10:24:36Z", "respondedAt": "2026-10-28T16:56:40Z" },
  "correction": null,
  "decision": null
}
```

`status` is the original coarse field, kept so older handling keeps working. Display `creditStatus`.
`actionRequired`, `actionUrl`, `offer`, `correction` and `decision` are left out when empty.

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

**On each `financing.inputLines[]` entry:**

| Field | Meaning |
|---|---|
| `baseUnit` | The unit the requirement is counted in, e.g. "Kg". |
| `packSize` | What one supplier pack holds, in `baseUnit`. |
| `packUnit` | The name of a pack, e.g. "Bag". |
| `packCount` | Whole packs bought. |
| `pricePerPack` | The price of one pack. |
| `procurementQuantity` | What was actually bought, in `baseUnit`. |
| `packagingExcessQuantity` | What was bought above the requirement because packs are whole. |
| `priceVersionId` | The supplier price-list entry used. |
| `supplierProductId` | The supplier's product. |
| `serviceProvider` | On a service line, the provider BOA approved for the EOP: `{ providerId, registeredName, serviceType, rate, rateBasis, rateVersion, billableQuantity, assignmentId }`. |

When `serviceProvider` is present, the letter names the provider instead of "To be confirmed". A
financed service with a provider no longer blocks ratification.

**On each `financing.eops[]` entry:**

| Field | Meaning |
|---|---|
| `eopReference` | The EOP reference the portal's release letters print. The letter uses it in Schedule A. |
| `approvedAt` | When the EOP was approved. |
| `revision` | Which approved revision of the EOP this is. |
| `snapshotHash` | The portal's fingerprint of the EOP. |

Text values longer than 200 characters are shortened rather than refused. A repeat of a submission
the CRMS already holds does not replace a breakdown it already has, so these fields reach new cases,
not existing ones (see CRMSQA#3).
