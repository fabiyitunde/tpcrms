# Core banking (tpbackend) study — 19 Nov 2026

Read-only study of the core banking source at `C:\absdev\tpbackend` (Apache Fineract ~1.7 fork plus the
TransactionPoint layer), done to make the CRMS's calls to it correct and to ground the CRMSQA LR-001..020 work.
Nothing in tpbackend was changed, built or called. Studied `master`; BOA runs `bankofagriculture`, which was
7 commits behind and differs only in savings events and virtual-account funding (nothing loan-related).

Internal only. These notes name internal systems and known defects; do not share outside the team.

| File | Covers |
|---|---|
| `01-crms-call-map.md` | Every CRMS call to core banking, matched to the tpbackend code that serves it; mismatches ranked |
| `02-loan-lifecycle.md` | Booking, schedule engine, repayments and allocation, overpayment, reversals, arrears, externalId guard |
| `03-accounting-idempotency-events.md` | GL posting, idempotency (none beyond externalId), status lookup, maker-checker responses, hooks/SNS, collection routes |
| `04-transactionpoint-layer.md` | TransactionPoint custom modules and routes, client lookup, bureau returns, arrears sweep, auth, BOA branch gap |
| `routes.txt` | Raw dump of TransactionPoint REST routes |

File:line references point into tpbackend (as of master `531c48b9c2`) or into this repo as of `ed5761b`.
