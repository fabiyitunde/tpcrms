# Itemised financing in the credit-profile submission

**Interface change · `POST /v1/credit-profiles`**

| | |
|---|---|
| Requested by | CRMS development team, 14 Oct 2026 |
| Status | On portal staging; production after BOA go-ahead |
| Compatibility | Additive, optional field |

The portal now sends each financed charge as its own named amount, beside the input lines and the total. The offer letter can print every row, and the rows reconcile exactly to the amount being profiled. CRMS does not need to reproduce the portal's formula.

---

## 1. What changes

One new optional object, `financing`, is added to the existing submission body. Nothing else changes.

- `totalEopValue`, `currency`, `farmerCount`, `programme`, `session`, `fac`, `callbackUrl`, `metadata` and the `Idempotency-Key` header are unchanged.
- `eopLines` (one row per crop, quantities 0) is still sent, unchanged. Use `financing.inputLines` for anything printed.
- A receiver that ignores `financing` behaves exactly as it does today.

---

## 2. What the portal guarantees

All amounts are in NGN, to 2 decimal places. The portal checks the following before every send. If any fails by more than one kobo (0.01), it does not send the case: it records the refusal and tells the FAC why.

1. `Σ inputLines.amount + Σ inputLines.packagingExcess + Σ charges.amount = approvedEopTotal`
2. The rows marked `inLoan: true` sum to `loanAmount`
3. `loanAmount = totalEopValue` — the top-level amount CRMS already profiles
4. `approvedEopTotal − loanAmount = excludedFromLoan`
5. `Σ eops[].loanAmount = loanAmount`

> **Use the amounts as sent.** Do not recompute them from quantity × unit price. Unit prices of lines bought in supplier packs are rounded down to the kobo, and insurance and working capital are rounded per farmer before being multiplied by the farmer count. Recomputing will be off by small amounts.

The portal team recommends CRMS also checks rules 1 to 3 on receipt and rejects the submission if they fail. They have asked to be told which error code CRMS uses.

---

## 3. Field reference

### `financing`

| Field | Type | Meaning |
|---|---|---|
| `currency` | string | Always `"NGN"` |
| `inputLines` | array | Every line of every approved EOP in the case |
| `charges` | array | Exactly three rows: `insurance`, `workingCapital`, `techServicesFee` |
| `inputsTotal` | number | Σ `inputLines.amount` |
| `packagingExcessTotal` | number | Σ `inputLines.packagingExcess` |
| `chargesTotal` | number | Σ `charges.amount` |
| `approvedEopTotal` | number | The total BOA approved across the case's EOPs |
| `loanAmount` | number | The amount to profile. Equals `totalEopValue` |
| `excludedFromLoan` | number | Approved but not financed under the programme's policy. 0 today, because the programme finances everything |
| `eops` | array | One summary per approved EOP |

### `inputLines[]`

| Field | Type | Meaning |
|---|---|---|
| `eopId` | uuid | The EOP the line belongs to |
| `crop` | string | The EOP's crop, e.g. `"Maize"` |
| `item` | string | Programme input, e.g. `"NPK Fertilizer"`, `"Extension Services"` |
| `category` | string \| null | e.g. `"Fertilizer"`, `"Seeds"`, `"Services"` |
| `specificInput` | string \| null | The product chosen, e.g. `"NPK 20:10:10"` |
| `supplier` | string \| null | Input supplier. Null for service lines and unassigned lines |
| `quantityPerFarmer` | number | Quantity for one farmer, in `unit` |
| `unit` | string | e.g. `"Kg"`, `"Litre"`, `"Per Farmer"`, `"Per Hectare"` |
| `unitPrice` | number | Price per `unit` |
| `basis` | string | `"perFarmer"`: `amount` is the per-farmer cost × `farmers`. `"wholeEop"`: a service priced for the whole EOP (per hectare or per service), not scaled by farmers |
| `farmers` | integer | The EOP's farmer count |
| `amount` | number | The line's total for the whole EOP |
| `packagingExcess` | number | What whole supplier packs cost beyond the per-farmer requirement (e.g. 3 bags bought where 2.4 are needed). Usually 0 |
| `inLoan` | boolean | Whether `amount` and `packagingExcess` form part of `loanAmount` |

### `charges[]`

| Field | Type | Meaning |
|---|---|---|
| `code` | string | `insurance`, `workingCapital` or `techServicesFee` |
| `label` | string | Text to print: "Insurance", "Working capital", "Technology services fee" |
| `rate` | number \| null | Insurance and working capital: the percentage. Tech fee: naira per farmer. **Null when the case's EOPs used different rates** — print the amount only, or use `eops[]` |
| `rateBasis` | string | Plain-language description of what the rate applies to |
| `amount` | number | The charge for the whole case |
| `inLoan` | boolean | Whether it forms part of `loanAmount` |

For the credit officer's information only: insurance and working capital are a percentage of the per-farmer input subtotal, excluding services priced for the whole EOP. The technology services fee is a fixed naira amount per farmer. Each EOP's own rates apply, as they stood when it was approved.

### `eops[]`

| Field | Type | Meaning |
|---|---|---|
| `eopId`, `crop`, `state`, `farmers` | | Identify the EOP |
| `inputs` | number | Σ of its input line amounts |
| `packagingExcess` | number | Σ of its packaging excess |
| `insurance`, `workingCapital`, `techServicesFee` | number | Its charges |
| `approvedTotal` | number | Its approved total |
| `loanAmount` | number | Its share of `loanAmount` |

---

## 4. Example

One EOP, 10 farmers, everything financed. Illustrative figures; some lines are shortened with `"…"`.

```jsonc
"totalEopValue": 48239800.00,
"financing": {
  "currency": "NGN",
  "inputLines": [
    { "eopId": "3f2a…", "crop": "Maize", "item": "NPK Fertilizer", "category": "Fertilizer",
      "specificInput": "NPK 20:10:10", "supplier": "Example Supplier Ltd",
      "quantityPerFarmer": 10, "unit": "Kg", "unitPrice": 1120.00, "basis": "perFarmer",
      "farmers": 10, "amount": 112000.00, "packagingExcess": 0.00, "inLoan": true },
    { "item": "Urea Fertilizer", "…": "…", "amount": 108000.00, "packagingExcess": 0.00, "inLoan": true },
    { "item": "Herbicide",       "…": "…", "amount": 3200000.00, "packagingExcess": 0.00, "inLoan": true },
    { "item": "Pesticide",       "…": "…", "amount": 800000.00, "packagingExcess": 0.00, "inLoan": true },
    { "item": "Improved Seeds",  "…": "…", "amount": 40000000.00, "packagingExcess": 0.00, "inLoan": true },
    { "item": "Extension Services", "category": "Services", "supplier": null, "unit": "Per Farmer",
      "basis": "perFarmer", "farmers": 10, "amount": 0.00, "packagingExcess": 0.00, "inLoan": true }
  ],
  "charges": [
    { "code": "insurance", "label": "Insurance", "rate": 4, "rateBasis": "…",
      "amount": 1768800.00, "inLoan": true },
    { "code": "workingCapital", "label": "Working capital", "rate": 5, "rateBasis": "…",
      "amount": 2211000.00, "inLoan": true },
    { "code": "techServicesFee", "label": "Technology services fee", "rate": 4000, "rateBasis": "…",
      "amount": 40000.00, "inLoan": true }
  ],
  "inputsTotal": 44220000.00,
  "packagingExcessTotal": 0.00,
  "chargesTotal": 4019800.00,
  "approvedEopTotal": 48239800.00,
  "loanAmount": 48239800.00,
  "excludedFromLoan": 0.00,
  "eops": [
    { "eopId": "3f2a…", "crop": "Maize", "state": "Yobe", "farmers": 10,
      "inputs": 44220000.00, "packagingExcess": 0.00, "insurance": 1768800.00,
      "workingCapital": 2211000.00, "techServicesFee": 40000.00,
      "approvedTotal": 48239800.00, "loanAmount": 48239800.00 }
  ]
}
```

Reconciliation for this example:

- Inputs: 112,000 + 108,000 + 3,200,000 + 800,000 + 40,000,000 + 0 = **44,220,000.00**
- Charges: 1,768,800 + 2,211,000 + 40,000 = **4,019,800.00**
- Inputs + packaging excess + charges = 44,220,000 + 0 + 4,019,800 = **48,239,800.00** = `approvedEopTotal` = `loanAmount` = `totalEopValue`

---

## 5. Implementation checklist for CRMS

1. Add `financing` (and its nested types) to the credit-profile submission DTO as an **optional** property; a missing `financing` must behave exactly as today.
2. Store amounts as `decimal` (2 dp); never `double`.
3. On receipt, when `financing` is present, validate rules 1–3 from §2 with a tolerance of 0.01; reject with a dedicated error code on failure, and report that code back to the portal team.
4. Do **not** recompute any amount from quantity × unit price or rate × base; persist and print the amounts exactly as sent.
5. Offer letter: print `inputLines` and `charges` rows from `financing` (not from `eopLines`). When a charge's `rate` is null, print the amount only, or break it down per EOP from `eops[]`.
6. Keep consuming `totalEopValue` as the profiled amount; it equals `financing.loanAmount`.

---

> **Note on source completeness:** this document was converted from the portal team's spec page. The final part of that page (after the example — likely an offer-letter layout illustration and footer) could not be retrieved. The closing `approvedTotal` / `loanAmount` values of the `eops[]` example entry are filled in from rules 3 and 5. §5 is an implementation summary derived from §§1–4, not part of the original spec.
