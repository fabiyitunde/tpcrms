# 03 — TPBackEnd (BOA core banking): accounting, idempotency, status inquiry, maker-checker, events, transfers

Read-only study of `C:\absdev\tpbackend`, branch `master`. No builds, no live calls, no files changed there.

**Branch note.** `origin/bankofagriculture` (what BOA runs) is **7 commits behind `master` and 0 ahead**
(`git rev-list --left-right --count master...origin/bankofagriculture` → `7 0`). Those 7 commits cover
"CB To VA Transfer For Invoice Fix" (invoice funding through TPM) and the fixed/recurring-deposit SNS events
(`TPDepositEventPublisher`, `docs/deposit-sns-events.md`). Nothing about loan posting, loan accounting or
maker-checker. The intrabank idempotency claim (`TPIntrabankIdempotencyService`, changelog `v11/0011`)
**is on the BOA branch** (confirmed with `git ls-tree origin/bankofagriculture`). The line numbers below are from
`master`. Where the BOA copy differs, I say so.

**Base platform.** This is an older Apache Fineract fork: `javax.persistence`, Spring Boot 2.6.7
(`build.gradle:72`), Liquibase tenant changelog plus the TP changelog `tp-v2`. It comes from before upstream
Fineract 1.8+, which added the `Idempotency-Key` store. That matters for section 2.

All paths are relative to `fineract-provider/src/main/java/` unless they start with `docs/` or
`fineract-provider/src/main/resources/`.

---

## 1. Accounting

### 1.1 Accounting rule per loan product
`org/apache/fineract/accounting/common/AccountingRuleType.java:26-29`: `NONE(1)`, `CASH_BASED(2)`,
`ACCRUAL_PERIODIC(3)`, `ACCRUAL_UPFRONT(4)`. Each loan product sets one rule (`accountingRule` on `/loanproducts`).

`AccountingProcessorForLoanFactory.determineProcessor` (`org/apache/fineract/accounting/journalentry/service/AccountingProcessorForLoanFactory.java:~34-49`)
picks one of two processors:
- cash: `cashBasedAccountingProcessorForLoan`;
- upfront or periodic accrual: `accrualBasedAccountingProcessorForLoan`.

With `NONE`, `createJournalEntriesForLoan` does nothing (`JournalEntryWritePlatformServiceJpaRepositoryImpl.java:470-485`),
so **a loan product with accounting NONE posts loan transactions with no GL entries at all.** The CRMS should
check the product's `accountingRule` before it treats "posted" as "in the GL".

Periodic accrual entries are made by a job or by `POST /runaccruals`
(`org/apache/fineract/accounting/accrual/api/AccrualAccountingApiResource.java:43`), not by the repayment itself.

### 1.2 Product-to-GL and payment-type (fund source) mappings
- `CashAccountsForLoan` (`org/apache/fineract/accounting/common/AccountingConstants.java:37-40`): `FUND_SOURCE(1)`,
  `LOAN_PORTFOLIO(2)`, `INTEREST_ON_LOANS(3)`, `INCOME_FROM_FEES(4)`, `INCOME_FROM_PENALTIES(5)`,
  `LOSSES_WRITTEN_OFF(6)`, `TRANSFERS_SUSPENSE(10)`, `OVERPAYMENT(11)`, `INCOME_FROM_RECOVERY(12)`.
- Fund source by payment channel: `AccountingProcessorHelper.getLinkedGLAccountForLoanProduct`
  (`org/apache/fineract/accounting/journalentry/service/AccountingProcessorHelper.java:1132-1157`). For `FUND_SOURCE`
  it first looks for a product + payment-type mapping (`findByProductIdAndProductTypeAndFinancialAccountTypeAndPaymentTypeId`).
  If there is none, it falls back to the product's default fund source. **So the `paymentTypeId` sent on a repayment
  decides which cash, bank or clearing GL is debited.** That is the lever for "cash" versus "grain offset" versus
  "bank transfer": map one payment type to each, with its own fund-source GL.
- Organisation-level accounts (financial activities) are looked up through `financialActivityAccountRepository`
  (same method, lines 1134-1137).
- A missing mapping throws `ProductToGLAccountMappingNotFoundException` (line 1156), and the whole repayment rolls back.

### 1.3 Are journal entries created automatically, synchronously and atomically?
Yes, for cash and upfront accrual.
- `LoanAccountDomainServiceJpa.makeRepayment`
  (`org/apache/fineract/portfolio/loanaccount/domain/LoanAccountDomainServiceJpa.java:174-258`) saves the
  transaction (220), saves and flushes the loan (228), then calls `postJournalEntries(...)` (244).
  `postJournalEntries` (413-424) calls `journalEntryWritePlatformService.createJournalEntriesForLoan` directly.
  It is the same thread and the same Spring transaction.
- The outer transaction is `SynchronousCommandProcessingService.processAndLogCommand`, which is `@Transactional`
  (`org/apache/fineract/commands/service/SynchronousCommandProcessingService.java:74-150`). The loan transaction,
  its journal entries and the audit row (`m_portfolio_command_source`) commit together or not at all.
- Disbursement (`LoanAccountDomainServiceJpa.java:513-521`) and refund (`:470-478`) follow the same pattern.
- Journal entry `transaction_id` for a loan transaction is `"L" + m_loan_transaction.id`. See
  `LoansTransactionHookMessenger.java` (`net/aggregatebusiness/transactionpoint/portfolio/loanaccount/service/`),
  which looks entries up with `findJournalEntries("L"+loanTransaction.getId(), LOAN)`.

### 1.4 Savings-to-loan transfers use a transfer liability, not the fund source
`CashBasedAccountingProcessorForLoan.java:137-139, 172-174, 246-247`: when `isAccountTransfer`, the loan side is
posted against `FinancialActivity.LIABILITY_TRANSFER`, not the product's fund source. The savings side posts to the
same financial activity. **BOA must have the "Liability Transfer" financial activity mapped**, or a savings-to-loan
repayment fails. `TRANSFERS_SUSPENSE` is used for loan-to-loan and office transfers (`:306-311`).

### 1.5 Manual journal entries
- `POST /journalentries` (`org/apache/fineract/accounting/journalentry/api/JournalEntriesApiResource.java:174`) →
  `createJournalEntry` (`JournalEntryWritePlatformServiceJpaRepositoryImpl.java:112-~290`). It takes `officeId`,
  `currencyCode`, `transactionDate`, `debits[]`, `credits[]`, an optional `accountingRule`, payment details and
  `referenceNumber`.
- The server makes a **new `transactionId` on every call** (`generateTransactionId(officeId)`, line 132).
  `referenceNumber` is stored in `acc_gl_journal_entry.ref_num` (`journalentry/domain/JournalEntry.java:106-107`),
  with **no uniqueness and no GET filter** (see 3.3). **A replayed manual journal entry always posts again.**
- TP variant: `POST /tp/journalentries/acc_mapping`
  (`net/aggregatebusiness/transactionpoint/accounting/journalentry/api/TPJournalEntriesApiResource.java:72`,
  service `TPJournalEntryWritePlatformServiceJpaRepositoryImpl.java:66-115`). It posts one leg to the savings
  product's mapped GL for a payment type and the other leg to a GL the caller chooses, for a savings account found
  by `externalId` (NUBAN). The date is forced to today and the currency to NGN. It has no idempotency either.

### 1.6 Reversing journal entries
- `POST /journalentries/{transactionId}?command=reverse` (`JournalEntriesApiResource.java:204-215`) →
  `revertJournalEntry` (`:291-358`). It reverses **manual entries only**
  (`findUnReversedManualJournalEntriesByTransactionId`). It writes opposite-side entries under a new transaction
  id and flags the originals `reversed`. It refuses if the branch's latest GL closure is on or after the entry date
  (`:318-329`). A replay finds nothing left to reverse and throws `JournalEntriesNotFoundException`, so it is
  naturally idempotent.
- **Loan transactions are not reversed through journal entries.** They are reversed by adjusting the loan
  transaction: `POST /loans/{loanId}/transactions/{transactionId}` with `transactionAmount: 0`
  (`org/apache/fineract/portfolio/loanaccount/api/LoanTransactionsApiResource.java:299-317`). That calls
  `Loan.adjustExistingTransaction` (`domain/Loan.java:3598-3627`), which reverses the transaction, and the
  accounting bridge writes reversal journal entries (debits and credits swapped:
  `AccountingProcessorHelper.java:335-360`).
  - **Risk:** I found no "already reversed" guard in `adjustExistingTransaction` or in `adjustLoanTransaction`
    (`LoanWritePlatformServiceJpaRepositoryImpl.java:1081-1150`). An adjust with a **non-zero** amount creates a
    new replacement repayment each time it runs, so a replayed adjust could post the replacement twice. Verify on
    the sandbox before relying on it.

### 1.7 GL closures and accounting rules
- `/glclosures` (`org/apache/fineract/accounting/closure/api/GLClosuresApiResource.java:61`). Each automatic loan
  posting checks `checkForBranchClosures` (`AccountingProcessorHelper.java:520-535`; called at
  `CashBasedAccountingProcessorForLoan.java:55`) and throws `ACCOUNTING_CLOSED` when the transaction date is on or
  before the branch's latest closure. **A back-dated repayment into a closed period is refused.**
- `/accountingrules` (`org/apache/fineract/accounting/rule/api/AccountingRuleApiResource.java:73`): templates for
  manual journal entries.

### 1.8 Branch / office
- The loan transaction is created with `loan.getOffice()` (`LoanAccountDomainServiceJpa.java:201-207`), and its
  journal entries go to that office. Loan postings are always booked at the **loan's office**, whoever posts them.
- Inter-branch accounting (`docs/design/INTER_BRANCH_TRANSACTION_DESIGN.md`) adds Inter-Branch Receivable and
  Payable financial activities (104/105) and the flag `enable-interbranch-accounting`. The code wiring is in
  `CashBasedAccountingProcessorForSavings`, the teller services and the TP savings and transfer services. Loan
  processors do not use it. **Loan repayments are not inter-branch-split.**
- The internal findings file records that `POST /tp/interbranch/settle` has no idempotency and does not check the
  real position.

---

## 2. Idempotency

### 2.1 Is the `Idempotency-Key` header honoured? **No. Nothing reads it.**
- A case-insensitive search for `idempotency-key`, `idempotency_key` and `idempotencyKey` across `*.java`, `*.xml`,
  `*.properties` and `*.yml` finds nothing.
- `CommandSource` (`org/apache/fineract/commands/domain/CommandSource.java:40-102`) has no `idempotency_key`
  column. Its columns are action, entity, office, client, loan, savings, resource ids, `command_as_json`, maker,
  checker, `processing_result_enum`, `transaction_id`, and the credit bureau ids.
- No servlet filter or command-level idempotency store exists. That arrived only in upstream Fineract 1.8+, after
  this fork's base.
- **Verdict on claim 1: consistent with the code.** TPBackEnd ignores `Idempotency-Key` on every route, loan and
  savings alike. If the CRMS sent it to BOA's Fineract and replayed the request, Fineract would treat the replay as
  a new command. The only protection is a business key (see 2.2 and 2.3). I can't tell from this repo whether a
  gateway in front (tpapi or tpgateway) handles the header. Neither is in this repo.

### 2.2 Loan repayment and disbursement: a uniqueness guard on `externalId`, if the caller sends one
- `m_loan_transaction.external_id` is **unique**:
  - entity: `org/apache/fineract/portfolio/loanaccount/domain/LoanTransaction.java:63`
    (`@UniqueConstraint(columnNames = "external_id", name = "external_id_UNIQUE")`) and `:113`;
  - schema: `fineract-provider/src/main/resources/db/changelog/tenant/parts/0001_initial_schema.xml:2709-2711`
    (`<constraints unique="true"/>`).
- The repayment JSON accepts `externalId`:
  `LoanEventApiJsonValidator.validateNewRepaymentTransaction`
  (`org/apache/fineract/portfolio/loanaccount/serialization/LoanEventApiJsonValidator.java:227-229`).
  Disbursement accepts it too (`:88, :92`).
- It is read at `LoanWritePlatformServiceJpaRepositoryImpl.java:978` (repayment) and `:477` and `:774`
  (disbursements), and is passed into the new `LoanTransaction`.
- A duplicate is caught in `saveLoanTransactionWithDataIntegrityViolationChecks`
  (`LoanAccountDomainServiceJpa.java:306-320`). It becomes HTTP 400 `validation.msg.validation.errors.exist` /
  `externalId value.must.be.unique` **only if the DB error message contains `external_id_unique`**.
  - **Caveat:** Liquibase's inline `unique="true"` doesn't name the index, so on MariaDB/MySQL it is probably
    called `external_id`. If BOA's schema was built that way, the string check fails and the exception is
    swallowed in that catch. The shared transaction is already marked rollback-only, so nothing commits, but the
    caller probably gets an unmapped 500 (`UnexpectedRollbackException`) instead of a clean 400. **Either way,
    nothing is posted twice.** The CRMS should handle both responses and then confirm with a read (section 3).
    Check the real index name with `SHOW INDEX FROM m_loan_transaction`.
  - The same catch swallows any other integrity error that isn't about `external_id` (`:313-319`), which is an
    upstream quirk.
- **No `externalId` means no guard.** `externalId` is nullable and a unique index allows any number of NULLs.
- **Loan disbursement** is also guarded by loan state: you can't disburse an already-disbursed loan. The loan
  itself has `loan_externalid_UNIQUE` (`domain/Loan.java:157-158`), which the CRMS already uses for booking and
  resume (`tpcrms/src/CRMS.Infrastructure/ExternalServices/FineractDirect/FineractDirectService.cs:682, 907-911`).

### 2.3 Savings-to-loan repayment by transfer: **no guard at all**
- `POST /accounttransfers` (savings account type 2 → loan account type 1) → `AccountTransfersWritePlatformServiceImpl.create`
  (`org/apache/fineract/portfolio/account/service/AccountTransfersWritePlatformServiceImpl.java:126, 190-215`).
- It calls `makeRepayment(..., noteText=null, txnExternalId=null, ...)` at **line 208**. The validator's allowed
  parameters (`org/apache/fineract/portfolio/account/data/AccountTransfersDataValidator.java:50-56`) don't include
  `externalId` or `transactionReference`, and the reference claim is only applied to savings-to-savings transfers
  (`:153-189`). **A replayed savings-to-loan transfer withdraws and repays again.**
- `GET /accounttransfers?externalId=` is accepted (`org/apache/fineract/portfolio/account/api/AccountTransfersApiResource.java:136`),
  but `AccountTransfersReadPlatformServiceImpl.retrieveAll` (`:219-245`) applies only order and limit, so the
  parameter is ignored.

### 2.4 The one real reference-based idempotency: intrabank savings-to-savings and core-to-virtual-account (VA) transfers
- Design: `docs/intrabank-transfer-idempotency.md`. Code:
  `net/aggregatebusiness/transactionpoint/portfolio/transfer/service/TPIntrabankIdempotencyService.java:123-172`
  (BOA branch: `:118-167`).
- How a claim works: one `INSERT IGNORE` into `tp_intrabank_transfer_reference`, unique on
  `(source_savings_account_id, transaction_reference)`
  (`fineract-provider/src/main/resources/db/changelog/tp-v2/v11/0011_intrabank_transfer_reference.xml:62-63`),
  committed `REQUIRES_NEW` before the posting. Completion is fenced by the attempt number, a request hash compares
  source, destination and amount, and claims stuck past 10 minutes are settled as stale.
- Routes: `POST /tp/transfer/intrabank` and `POST /tp/transfer/intrabank/subledger`
  (`net/aggregatebusiness/transactionpoint/portfolio/transfer/api/InterbankTransferApiResource.java:241-259`), keyed
  on the body field `transactionReference`. No reference means no claim.
- **Enforcement is OFF by default:** `intrabank-transfer-idempotency-enforced` and
  `virtual-account-transfer-idempotency-enforced` are seeded `enabled=false`
  (`0011_intrabank_transfer_reference.xml:68-82`). While off, a duplicate is only logged as
  `Intrabank Duplicate Reference Not Enforced` and **posts again** (`TPIntrabankIdempotencyService.java:142-146`).
  The one exception: a `FAILED` claim for the same request is reopened (`:137-140`).
- When on: SUCCESSFUL or PENDING_APPROVAL → the original result is replayed; IN_PROGRESS → 409; a different
  request with the same reference → rejected (`:148-171`).
- None of this applies to loans.

### 2.5 Other routes with no idempotency (from the internal findings file)
- Outward and interbank transfers (`/tp/transfer`, `/authrequest`, `/subledger*`, `/bulk/authrequest`): the
  reference is not checked, the index is not unique, and a retry sends a second transfer to the gateway.
- `/tp/transfer/subledger/sync`: a timeout followed by a retry sends twice, and a gateway failure still returns
  HTTP 200.
- `/tp/bulktransfer/intrabank`: lines are not de-duplicated.
- `/tp/transfer/reverse`: under maker-checker it can queue a second refund.

### 2.6 How a replay could plausibly have moved money twice
In order of likelihood:
1. **The client timed out but the server carried on.** Fineract keeps processing after the caller's HTTP timeout
   and commits. The retry is a new command. With no `externalId` (or through `/accounttransfers`, where none is
   possible), it posts again. This is exactly the 2026-10-29 incident described in
   `docs/intrabank-transfer-idempotency.md` ("Why this exists").
2. **The idempotency claim is in place but switched off.** Even on intrabank routes with `transactionReference`,
   a duplicate posts unless the per-tenant switch is on. The doc says plainly: "Until the intrabank switch is on,
   the duplicate posting bug remains open."
3. **A new reference on every retry.** The doc notes this is not protected (Known limits).
4. **Replaying a non-zero adjust** of a loan transaction (see 1.6).
5. The internal deadlock-retry loop (`PortfolioCommandSourceWritePlatformServiceImpl.java:91-114`) retries only
   after a full rollback of the `@Transactional` `processAndLogCommand`. **It does not double-post.**

---

## 3. Status inquiry by reference

| Need | Endpoint | Key | Evidence |
|---|---|---|---|
| Intrabank / VA transfer status | `GET /tp/transfer/intrabank/status/{transactionReference}?sourceAccountNUBAN=` | reference + source NUBAN | `InterbankTransferApiResource.java:261-267`; 404 if never claimed. Covers only transfers made after release, with a reference |
| Loan by CRMS reference | `GET /loans?externalId=` | loan `external_id` | `LoanReadPlatformServiceImpl.java:330-333` (the CRMS already uses it) |
| Loan transactions incl. their `externalId` | `GET /loans/{id}?associations=transactions` or `GET /loans/{loanId}/transactions/{txnId}` | loan id / txn id | `LoanTransactionsMapper` selects `tr.external_id` (`LoanReadPlatformServiceImpl.java:1317, 1376`); `LoanTransactionsApiResource.java:200-222` |
| Loan transaction **by externalId** | **None.** You have to list the loan's transactions and filter client-side | n/a | No repository method or SQL `tr.external_id = ?` found |
| Journal entries | `GET /journalentries?transactionId=&loanId=&savingsId=&officeId=&glAccountId=&fromDate=&toDate=&manualEntriesOnly=&entityType=&transactionDetails=&runningBalance=` | `transactionId` (`L<loanTxnId>` for loans) | `JournalEntriesApiResource.java:97-125`; **no `referenceNumber` filter** |
| Command / maker-checker status | `GET /audits/{commandId}` (returns `processingResult`), `GET /audits?...&processingResult=` | `commandId` from the POST response | `org/apache/fineract/commands/api/AuditsApiResource.java:79-137` |
| Pending checker queue | `GET /makercheckers?loanid=&entityName=&actionName=`, `GET /tp/makercheckers` | filters | `MakercheckersApiResource.java:76-96`, `TPMakercheckersApiResource.java:140-167` |
| Interbank / outward transfer | `GET /tp/transfer/transferdetails/{paymentReference}`, `tpgatewaytransactiondetails/{ref}`, `POST /tp/transfer/enquiry` | gateway refs | `InterbankTransferApiResource.java:231-238, 317-338`; findings: duplicated refs give a 500 |
| Savings transactions | `GET /tp/savingsaccounts/transactions/{externalId(NUBAN)}` | NUBAN + dates | findings file: a bad date is ignored silently and there is no paging |

**Verdict on claim 2 ("no status-inquiry-by-reference endpoint"): partly true.** For **loan repayments** there is
no direct lookup by reference. But a repayment posted with `externalId` **can be confirmed**: read
`GET /loans/{loanId}?associations=transactions` and find the non-reversed transaction carrying that `externalId`.
For a payment still awaiting a checker, use `GET /audits/{commandId}`. A reference-based status endpoint exists
only for intrabank and VA transfers.

---

## 4. Maker-checker

### 4.1 Fineract maker-checker
- The global config `maker-checker` plus per-permission `can_maker_checker` →
  `configurationDomainService.isMakerCheckerEnabledForTask(wrapper.taskPermissionName())`
  (`SynchronousCommandProcessingService.java:79`).
- The handler runs fully (the posting and its journal entries), then if maker-checker applies it throws
  `RollbackTransactionAsCommandIsNotApprovedByCheckerException` (`:133-145`). That rolls everything back. The
  catch (`PortfolioCommandSourceWritePlatformServiceImpl.java:115-118`) then saves the command as
  `AWAITING_APPROVAL` (`logCommand`, `SynchronousCommandProcessingService.java:153-161`; enum
  `CommandProcessingResultType`: 1 PROCESSED, 2 AWAITING_APPROVAL, 3 REJECTED).
- **What the API caller gets back:** HTTP **200** with `{"commandId": <auditId>, "resourceId": <id>}`. For a
  repayment the `resourceId` is the id the **rolled-back** loan transaction had
  (`commandSourceResult.updateResourceId(result.resourceId())` at `:100`, returned by `logCommand` at `:160`). It
  points to a transaction that does not exist. There is no `loanId` or `changes`.
- A processed repayment returns `officeId`, `clientId`, `loanId`, `resourceId` (the transaction id) and `changes`,
  with no `commandId` (`LoanWritePlatformServiceJpaRepositoryImpl.java:1016-1021`).
- **CRMS rule:** a 200 that has `commandId` and no `loanId`/`changes` means **pending checker, not posted**. Never
  store that `resourceId` as a transaction id. Poll `GET /audits/{commandId}` until `processingResult` is 1
  (processed) or 3 (rejected), then confirm the transaction as described in section 3.
- **Bypass:** users with `markerCheckerOverride=true` or the permission `BATCHPOSTING_TPDIRECT` skip maker-checker
  even when it is enabled (`SynchronousCommandProcessingService.java:119-130`). If the CRMS's service user has
  either, its postings commit at once.
- A checker approves with `POST /makercheckers/{auditId}?command=approve` or through the TP routes. The internal
  findings file records that approval limits are checked only on some of those routes.

### 4.2 Role-based approval limits (custom)
Documented in `docs/ROLE_BASED_APPROVAL_LIMITS.md` and `docs/role-approval-limits-design.md`.
- The global flag is `enable-role-approval-limits`. Limits per role, entity, action and currency are in
  `tp_role_approval_limit`.
- Supported entity/actions include `LOAN` `APPROVE`, `DISBURSE`, `DISBURSETOSAVINGS`, `REPAYMENT`, and
  `ACCOUNTTRANSFER` `CREATE` and `REFUNDBYTRANSFER` (doc lines 463-485).
- The limits control **who can see and approve** a pending command. They don't change whether the command goes to
  pending. Super admins (`ALL_FUNCTIONS`) bypass them. Fail-closed applies only where limits are configured (doc
  lines 559-583).
- `originalMakerId` is injected into the command JSON so that the posted transaction and journal entries show the
  maker, not the checker (doc lines 322-372, 543-557).

---

## 5. Outbound events and notifications

### 5.1 Command hooks (Fineract webhooks), the main channel
- Every command that runs through the pipeline calls `publishEvent(entityName, actionName, command, result)`
  (`SynchronousCommandProcessingService.java:127, 148, 251-306`). Failures publish too, with `status: "Exception"`
  (`:229-239`).
- The payload has `entityName`, `actionName`, `createdBy*`, `officeId`, `clientId`, the full `request` JSON, the
  `response` (`CommandProcessingResult`) and a `timestamp`.
- Loan events use the command's entity/action, e.g. `LOAN` / `REPAYMENT`, `LOAN` / `DISBURSE`,
  `LOAN` / `DISBURSETOSAVINGS`, `LOAN` / `ADJUST`, and `ACCOUNTTRANSFER` / `CREATE`. Hooks are registered with
  `POST /hooks`, giving events as entity/action pairs.
- Delivery: `FineractHookListener implements ApplicationListener<HookEvent>`
  (`org/apache/fineract/infrastructure/hooks/listener/FineractHookListener.java:38, 55-80`).
  - It runs **synchronously, inside the still-open posting transaction, before commit.** An event can go out for a
    posting that then fails to commit.
  - Processor exceptions are logged and dropped (`:73-77`). Delivery is best effort with no outbox and no retry.
  - Processors: Web (`WebHookProcessor.java:66-85`: async Retrofit `enqueue`, headers `X-Fineract-Entity`,
    `X-Fineract-Action`, …), SMS / message gateway, ElasticSearch, and the custom **SNS Topic** processor
    (`net/aggregatebusiness/transactionpoint/infrastructure/hooks/processor/SNSTopicProcessor.java`). The SNS
    processor uses a FIFO group `tpsns_<entity>_<action>` (`:134`) and a **random dedup id** (`:135`), so a
    replayed event is a new message.
  - The latest BOA commit `cf712770fa` "Allowing Hook Config" lets the SNS hook use the IAM role when its access
    keys are blank.
- Pending maker-checker commands publish nothing (the exception is thrown before `publishEvent`). **Approval**
  publishes then, because the approve path re-runs `processAndLogCommand`. Override and `BATCHPOSTING_TPDIRECT`
  users publish at once.

### 5.2 Business-event listeners and SNS doc events
- Loans: `LoansTransactionHookMessenger` listens for `LOAN_DISBURSAL` (`:81`), but its `publishEvent` call is
  **commented out** (`:172`), so it sends nothing.
- Fineract's built-in `LOAN_MAKE_REPAYMENT` business event is used only for SMS campaigns
  (`org/apache/fineract/infrastructure/campaigns/sms/service/SmsCampaignDomainServiceImpl.java:99`).
- Savings deposits and withdrawals have `SavingsTransactionJournalEntryEventPublisher`.
- Savings interest (`docs/savings-interest-sns-events.md`) and FD/RD events (`docs/deposit-sns-events.md`, **master
  only, not on BOA**) are sent after commit through the hook pipeline. Neither covers loans.
- **System-initiated loan repayments emit no hook**, because they bypass the command pipeline: standing
  instructions (job `EXECUTE_STANDING_INSTRUCTIONS`), the arrears sweep (5.3) and the jobs.

### 5.3 Can the CRMS subscribe instead of polling?
Partly. A Web or SNS hook on `LOAN/REPAYMENT`, `LOAN/DISBURSE`, `LOAN/DISBURSETOSAVINGS`, `LOAN/ADJUST` and
`ACCOUNTTRANSFER/CREATE` gives quick notice of API-initiated postings. But it can arrive before the posting commits,
can be lost, can be duplicated, and misses system-initiated postings. **Use it as a trigger to re-read, never as
proof of posting.** Reconciliation still has to poll `GET /loans/{id}?associations=transactions` and
`GET /journalentries`.

---

## 6. Savings, transfers and repayment collection

- **Direct repayment:**
  `POST /loans/{loanId}/transactions?command=repayment` with `transactionDate`, `transactionAmount`,
  `paymentTypeId` (which picks the fund-source GL), payment details (`receiptNumber`, `accountNumber`, …), `note`
  and **`externalId`** (`LoanTransactionsApiResource.java:243-296`, validator `:227-229`). This is the only
  repayment route with a duplicate guard (2.2).
- Other loan commands on the same route: `recoverypayment`, `merchantIssuedRefund`, `payoutRefund`,
  `goodwillCredit`, `waiveinterest`, `writeoff`, `close`, `undowriteoff` (`:251-293`).
  - A **grain offset** is best posted as a normal `repayment` with a dedicated payment type mapped to a
    grain-offset or clearing GL, not as a waiver.
  - Reversal: adjust with amount 0 (1.6).
  - A refund of an overpayment goes back to savings by transfer, loan → savings (`AccountTransfersWritePlatformServiceImpl.java:217-240`, `makeRefund`).
- **Savings → loan transfer:** `POST /accounttransfers` (fromAccountType 2, toAccountType 1). It debits the savings
  account and repays the loan in one transaction, through Liability Transfer (1.4). It has **no
  externalId/reference** (2.3). TP's `/tp/accounttransfers` is savings → savings only
  (`TPAccountTransfersWritePlatformServiceImpl.java:~111`).
- **Standing instructions:** `/standinginstructions`, types `FIXED(1)` and `DUES(2)`
  (`org/apache/fineract/portfolio/account/domain/StandingInstructionType.java:27-28`). The job runs at
  `StandingInstructionWritePlatformServiceImpl.java:216-217`. DUES collects instalments due from a linked savings
  account automatically.
- **Arrears sweep (TP custom):** `LoanArrearsRecoveryDomainServiceImpl`
  (`net/aggregatebusiness/transactionpoint/portfolio/loanarrearsrecovery/service/`).
  - On every eligible savings deposit, after it commits (`:59-129`), it moves money savings → loan with
    `AccountTransferType.LOAN_REPAYMENT` (`LoanArrearsRecoveryTransactionServiceImpl.java:81-109`), in its own
    `REQUIRES_NEW` transaction.
  - It is gated by `loan-arrears-recovery-from-savings`, seeded **disabled**
    (`fineract-provider/src/main/resources/db/changelog/tp-v2/v4/0004_loan_arrears_recovery.xml:89-98`). Its value
    is the days past due.
  - Records are in `/tp/loanarrearsrecoveries` (with reversal).
  - If BOA turns it on, **repayments will appear that the CRMS never initiated.** Reconciliation must expect them.
- **NUBAN / virtual accounts / interbank:**
  - NUBAN is the savings account's `external_id`, looked up with `GET /tp/savingsaccounts/byexternalId/{nuban}`.
  - Intrabank savings → savings by NUBAN is `POST /tp/transfer/intrabank`. Core → VA (subledger) is
    `/tp/transfer/intrabank/subledger`. Both have the optional claim (2.4).
  - Interbank (NIP) outward transfers go through `/tp/transfer*` to the gateway, with no idempotency.
  - None of these credits a loan directly. A customer's repayment from their account means a savings → loan
    transfer, a standing instruction, the arrears sweep, or a withdrawal followed by a direct repayment.

---

## 7. What this means for the CRMS design

1. Treat `Idempotency-Key` as unsupported. Send a CRMS-unique **`externalId` on every loan repayment and
   disbursement** (at most 100 characters). Prefer `command=repayment` over `/accounttransfers` for anything the
   CRMS must be able to retry safely.
2. On a timeout, **don't retry first. Read first:** `GET /loans/{id}?associations=transactions`, then look for your
   `externalId`. If it isn't there and no audit row is pending, retry with the **same** `externalId`. Treat both a
   400 `value.must.be.unique` and an unexpected 500 on retry as "probably already posted" and read again.
3. A 200 that has `commandId` and no `loanId` means pending checker. Track it with `GET /audits/{commandId}` and
   never use its `resourceId`.
4. "Posted to GL" means a non-reversed loan transaction plus journal entries under `transactionId=L<txnId>`, on a
   product whose `accountingRule` is not NONE.
5. Hooks are hints only. Reconcile on a schedule: CRMS ledger, then Fineract loan transactions by externalId and
   date, then `GET /journalentries?loanId=&fromDate=&toDate=`, then the bank statement. Allow for sweep and
   standing-instruction repayments that the CRMS did not initiate.
6. Map one payment type per channel (cash, bank transfer, grain offset) to its own fund-source GL. Confirm the
   Liability Transfer financial activity is set if the CRMS will use savings → loan transfers.
7. Ask BOA or the platform team: (a) the real name of the unique index on `m_loan_transaction.external_id`;
   (b) whether the CRMS service user has `markerCheckerOverride` or `BATCHPOSTING_TPDIRECT`, and which LOAN
   permissions have maker-checker enabled; (c) whether the two intrabank idempotency switches are on for the
   `bankofagriculture` tenant; (d) whether the arrears sweep or standing instructions will be enabled.
