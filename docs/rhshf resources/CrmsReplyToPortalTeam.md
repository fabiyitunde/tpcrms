# RH-SHF CRMS ⇄ Portal — reply to your integration note

**From:** CRMS engineering team
**To:** the RH-SHF portal team
**Re:** your note of 2026-09-22 — moving the contract to AGREED

---

## Summary

Thank you — the note was clear and everything you flagged was actionable. All five requests are
addressed. Two need a small thing from you (a header to verify, a policy point to note); the rest are
done. Details below, in your numbering.

We'd like to hold the formal **AGREED** sign-off until our sandbox config is fully in place (§4.2/§4.3
below), so we're not agreeing a contract whose authentication is still being switched on. That's days,
not weeks.

---

## Your requests

### 4.1 `actionUrl` — done
The status response and the `OfferReady` webhook now return `actionUrl` beside `actionRequired`,
pointing at the offer page, with no token attached (mint and append your own, as you do for
`profilingUrl`). It's omitted from the JSON when there's no pending action.

Note: `actionUrl` is empty until our public host is configured (§4.2). Once that lands, it populates
automatically.

### 4.2 A real profiling host — in progress
`crms.example.com` is gone. The sandbox host is being configured now; we'll send you the resolved
`profilingUrl`/`actionUrl` base once it's live. Expect real, resolvable URLs shortly.

### 4.3 Sandbox API key — in progress
Being switched on with the same config change as §4.2. We'll share the key out-of-band once set.
Until then the endpoint remains open, so please don't treat the sandbox as authenticated yet.

### 4.4 `X-CRMS-Timestamp` — done, but please read this
We now send **three** headers:

| Header | Covers | Notes |
|---|---|---|
| `X-CRMS-Timestamp` | — | Unix epoch seconds |
| `X-CRMS-Signature` | `body` | **Unchanged** — your current receiver keeps working as-is |
| `X-CRMS-Signature-V2` | `{timestamp}.{body}` | New — this is the one that actually prevents replay |

Important: the timestamp only closes the replay window if you verify **V2** and reject stale
timestamps. The bare `X-CRMS-Timestamp` header on its own gives no protection — an attacker replaying
a captured callback can rewrite it. V1 is kept only so nothing breaks on your side today; once you've
moved to V2, tell us and we'll retire V1.

### 4.5 `fac.tin` — now optional
Confirmed with BOA: a case can be assessed without a TIN. `fac.tin` is no longer required.

This means **BOA does not need to collect TINs for the existing FAC population before go-live** — it
can be gathered alongside. We still store and surface it when you send it.

---

## On what you send (your §5)

- **5.1 `eopLines` zero quantities** — understood, and handled. We no longer display or reason over the
  zeros; only `commodity` and `lineValue` are used. No per-commodity quantity needed from you.
- **5.2 `session.code` with region** — no problem on our side. `2026-DRY-SOUTH` and `2026-DRY-NORTH`
  are treated as distinct sessions, as intended.

---

## Test data (your §6)
The two cases will be removed. (The "third" was your idempotent repeat — it correctly returned the same
reference rather than creating a second case, so there's nothing extra to delete.) We also created one
config-probe case of our own during testing; it's going in the same cleanup.

---

## Next steps

Here's the order we'd suggest to finish this off:

1. **We** set up the sandbox web address and API key (a few days).
2. **We** send them to you.
3. **You** point your test system at our sandbox instead of your mock, and start verifying the new
   signature.
4. **Both of us** confirm it works end to end, and we mark the contract AGREED.

Happy to get on a call for any of it.
