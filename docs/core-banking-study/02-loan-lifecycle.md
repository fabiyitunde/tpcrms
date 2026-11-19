# 02 — Core banking (tpbackend) loan lifecycle, schedule, repayments, idempotency

Read-only study of `C:\absdev\tpbackend`, branch `master` (HEAD 531c48b9c2). BOA runs `origin/bankofagriculture`
(last commit 2026-11-09). The 7 commits it is behind are all "CB To VA Transfer For Invoice Fix" (savings/virtual
account). `git diff origin/bankofagriculture master` over `portfolio/loanaccount`, `portfolio/loanproduct` and
`transactionpoint/portfolio/loanarrearsrecovery` is **empty**, so everything below holds for BOA production code.

Paths are abbreviated:
- `LA` = `fineract-provider/src/main/java/org/apache/fineract/portfolio/loanaccount`
- `LP` = `fineract-provider/src/main/java/org/apache/fineract/portfolio/loanproduct`
- `TP` = `fineract-provider/src/main/java/net/aggregatebusiness/transactionpoint`
- `CL` = `fineract-provider/src/main/resources/db/changelog`

## 0. Which Fineract this is

It is an upstream Apache Fineract of roughly **1.7 / early 1.8-dev (late 2022)**: the newest upstream loan commits
are FINERACT-1745/1781/1786 (Nov 2022), the tenant Liquibase parts stop at `0013_add_release_id_to_deposit_on_hold.xml`,
and `LoanTransactionType` stops at `GOODWILL_CREDIT(23)`. It therefore **does not have** the 1.8+/1.9 loan features:
no `Idempotency-Key` handling, no `/loans/external-id/{id}` routes, no delinquency ranges/buckets module, no
"advanced payment allocation" strategy, no chargeback, no charge-off, no re-age/re-amortise, no Loan COB.
(Confirmed: `grep -ri "idempot"` over `org/apache/fineract` finds only TP's transfer service; no `delinquency`
package under `portfolio/`; `transactionprocessor/impl` has only the 7 classic processors.)

Custom (TransactionPoint / BOA) commits touching loans: 16 non-upstream commits since 2024 (listed in section 6).

---

## 1. Loan lifecycle

### States (`LA/domain/LoanStatus.java:26-37`)
`100 SUBMITTED_AND_PENDING_APPROVAL`, `200 APPROVED`, `300 ACTIVE`, `303 TRANSFER_IN_PROGRESS`, `304 TRANSFER_ON_HOLD`,
`400 WITHDRAWN_BY_CLIENT`, `500 REJECTED`, `600 CLOSED_OBLIGATIONS_MET`, `601 CLOSED_WRITTEN_OFF`,
`602 CLOSED_RESCHEDULE_OUTSTANDING_AMOUNT`, `700 OVERPAID`. Sub-status: `100 FORECLOSED` (`LoanSubStatus.java:26`).
State machine: `LA/domain/DefaultLoanLifecycleStateMachine.java`.

### Endpoints / `?command=` values
`LA/api/LoansApiResource.java` (`@Path("/loans")`, line 151):
- `POST /loans` — create/submit application (line 796). `POST /loans?command=calculateLoanSchedule` — schedule
  preview without persisting (line 813).
- `PUT /loans/{id}` modify (pending only), `DELETE /loans/{id}` delete (pending only).
- `POST /loans/{id}?command=` (lines 894-928): `reject`, `withdrawnByApplicant`, `approve`, `disburse`,
  `disburseToSavings`, `undoapproval`, `undodisbursal`, `undolastdisbursal`, `assignloanofficer`,
  `unassignloanofficer`, `recoverGuarantees`.
- GLIM (group) variants at `/loans/glimAccount/{glimId}` (lines 986-1001): `reject`, `approve`, `disburse`,
  `glimrepayment`, `undodisbursal`, `undoapproval`.
- `GET /loans?externalId=...` list filter (line 777; SQL `and l.external_id = ?` at
  `LA/service/LoanReadPlatformServiceImpl.java:331`). `GET /loans/{id}?associations=repaymentSchedule,transactions,charges,collection,linkedAccount,multiDisburseDetails,...`.
- Tranches: `LA/api/LoanDisbursementDetailApiResource.java` (edit expected tranche); schedule variations
  `POST /loans/{id}/schedule?command=calculateLoanSchedule|addVariations|deleteVariations`
  (`LA/api/LoanScheduleApiResource.java:96-105`); rescheduling `POST /rescheduleloans` (+ approve/reject)
  (`LA/rescheduleloan/api/RescheduleLoansApiResource.java:54`).

**There is no custom "createAndDisburse"/single-call booking endpoint in tpbackend.** Booking is the upstream
three-step `POST /loans` -> `?command=approve` -> `?command=disburse|disburseToSavings`. (The CRMS already does exactly
this in `CRMS.Infrastructure/ExternalServices/FineractDirect/FineractDirectService.cs:701,779,812`, with an
externalId probe/resume around it at lines 566-575 and 709-721.) The TP Batch API (`TP/batch/api/TPBatchApiResource.java`)
can chain requests, but its strategies are client/savings/transfer only (`TP/batch/command/internal/*`), not loans,
and it is not transactional across loan steps.

### Disbursement (`LA/service/LoanWritePlatformServiceJpaRepositoryImpl.java:369-620`)
- Multi-tranche: product `multiDisburseLoan`, `maxTrancheCount`, `outstandingLoanBalance`,
  `disallowExpectedDisbursements` (custom upstream fix bd8dab1946 allows tranches without expected-tranche details;
  artificial tranche created at lines 381-390). `undolastdisbursal` undoes only the latest tranche.
- Collateral check against disbursed amount (lines 397-412). Top-up (`isTopup`, `loanIdToClose`) closes the old loan
  out of the new disbursal (lines 477-501).
- `disburseToSavings` credits the linked savings account via an account transfer (`disburseLoanToSavings`, line 504).
- After disbursement: optional auto-creation of a **standing instruction** to debit the linked savings for repayments
  (`createStandingInstruction(loan)` line 550, flag `createStandingInstructionAtDisbursement`), journal entries
  (`postJournalEntries`, line 552), and disbursement charges with payment mode ACCOUNT_TRANSFER are collected from the
  linked savings (lines 555-590; custom fixes c8df17e9e2..e7883378ca, May 2025, plus a savings-side note
  `saveLoanChargeNote`).
- Externalid on the disbursement transaction is accepted (`txnExternalId`, line 477/515).

---

## 2. Schedule generation

Generators: `LA/loanschedule/domain/DecliningBalanceInterestLoanScheduleGenerator.java`,
`FlatInterestLoanScheduleGenerator.java`, base `AbstractLoanScheduleGenerator.java`; factory
`DefaultLoanScheduleGeneratorFactory.java`; preview service
`LA/loanschedule/service/LoanScheduleCalculationPlatformServiceImpl.java:103` (`calculateLoanSchedule`).

| Option | Values (file:line) |
|---|---|
| Amortisation | `EQUAL_PRINCIPAL(0)`, `EQUAL_INSTALLMENTS(1)` (`LP/domain/AmortizationMethod.java:23-24`); plus `isEqualAmortization` flag (equal interest too) |
| Interest type | `DECLINING_BALANCE(0)`, `FLAT(1)` (`LP/domain/InterestMethod.java:23`) |
| Interest calculation period | `DAILY(0)`, `SAME_AS_REPAYMENT_PERIOD(1)` (`LP/domain/InterestCalculationPeriodMethod.java:23-24`); `allowPartialPeriodInterestCalcualtion` |
| Days in year | `ACTUAL`, `360`, `364`, `365` (`portfolio/common/domain/DaysInYearType.java:37-40`); overridable per loan (`daysInYearType`, b0c45da472) |
| Days in month | `ACTUAL`, `30` (`portfolio/common/domain/DaysInMonthType.java:35-36`) |
| Grace | `graceOnPrincipalPayment`, `graceOnInterestPayment`, `graceOnInterestCharged` (interest-free periods), `recurringMoratoriumOnPrincipalPeriods`, `graceOnArrearsAgeing` (`LA/serialization/LoanApplicationCommandFromApiJsonHelper.java` supportedParameters; product validator `LP/serialization/LoanProductDataValidator.java`) |
| Rounding | product `digitsAfterDecimal`, `inMultiplesOf`, `installmentAmountInMultiplesOf`; global `rounding-mode` config (seeded value 6 = HALF_EVEN, `CL/tenant/parts/0002_initial_data.xml:219-221`), read by `organisation/monetary/domain/MoneyHelper.java:49-58` |
| Balloon / bullet | `numberOfRepayments = 1` gives a single bullet; `fixedPrincipalPercentagePerInstallment` gives balloon (FINERACT-1348, 72640bac6a); `principalThresholdForLastInstallment` |
| Fixed / variable installments | `fixedEmiAmount` (`emiAmount`), `canDefineInstallmentAmount`, `allowVariableInstallments` + `minimumGap`/`maximumGap`, schedule `addVariations` |
| Floating rates | `isLinkedToFloatingInterestRates`, `floatingRatesId`, `interestRateDifferential` |
| Interest recalculation | `isInterestRecalculationEnabled`; compounding `NONE/INTEREST/FEE/INTEREST_AND_FEE` (`LP/domain/InterestRecalculationCompoundingMethod.java:36-39`); rest/compounding frequency `SAME_AS_REPAYMENT_PERIOD/DAILY/WEEKLY/MONTHLY` (`RecalculationFrequencyType.java:28-31`); reschedule strategy `RESCHEDULE_NEXT_REPAYMENTS/REDUCE_NUMBER_OF_INSTALLMENTS/REDUCE_EMI_AMOUNT` (`LoanRescheduleStrategyMethod.java:36-38`); pre-closure `TILL_PRE_CLOSURE_DATE/TILL_REST_FREQUENCY_DATE` (`LoanPreClosureInterestCalculationStrategy.java:27-28`); `isArrearsBasedOnOriginalSchedule`, `allowCompoundingOnEod`, `isCompoundingToBePostedAsTransaction` |
| **Custom (BOA)** advance-payment interest | `advancePaymentInterestCalculationStrategy`: `FULL_PERIOD(0)` default / `TILL_PAYMENT_DATE(1)` (`LP/domain/AdvancePaymentInterestCalculationType.java:23-24`) — see section 6 |

Holidays/working days are honoured (`validateRepaymentDateIsOnHoliday`, `Loan.java:3072-3074`; job
`APPLY_HOLIDAYS_TO_LOANS`). APR is computed (`AprCalculator.java`). Schedule history is archived on reschedule /
recalculation (`LoanRepaymentScheduleHistory`).

---

## 3. Repayments and other transactions

### Transaction types (`LA/domain/LoanTransactionType.java:23-58`)
`DISBURSEMENT 1, REPAYMENT 2, CONTRA 3, WAIVE_INTEREST 4, REPAYMENT_AT_DISBURSEMENT 5, WRITEOFF 6,
MARKED_FOR_RESCHEDULING 7, RECOVERY_REPAYMENT 8, WAIVE_CHARGES 9, ACCRUAL 10, INITIATE/APPROVE/WITHDRAW/REJECT_TRANSFER
12-15, REFUND 16, CHARGE_PAYMENT 17, REFUND_FOR_ACTIVE_LOAN 18, INCOME_POSTING 19, CREDIT_BALANCE_REFUND 20,
MERCHANT_ISSUED_REFUND 21, PAYOUT_REFUND 22, GOODWILL_CREDIT 23`. **No CHARGEBACK, no CHARGE_OFF.**

### Endpoints (`LA/api/LoanTransactionsApiResource.java`, `@Path("/loans/{loanId}/transactions")`)
`POST ?command=` (lines 251-287): `repayment`, `merchantIssuedRefund`, `payoutRefund`, `goodwillCredit`,
`waiveinterest`, `writeoff`, `close-rescheduled`, `close`, `undowriteoff`, `recoverypayment`, `refundByCash`,
`foreclosure`, `creditBalanceRefund`. Templates (`GET template?command=`, lines 135-190) also expose `prepayLoan`
(payoff quote), `refundbytransfer`, `disburse`. `GET /{transactionId}` (line 201). `POST /{transactionId}` = **adjust**
(line 299; amount 0 = undo/reverse). `PUT /{transactionId}` = undo waive-charge (line 321).
Charges: `/loans/{id}/charges` add / `?command=pay|waive` / update / delete (`LoanChargesApiResource.java:180-245`).
Repayment from savings: `POST /accounttransfers` (savings -> loan, `AccountTransferType.LOAN_REPAYMENT`) and standing
instructions (job `EXECUTE_STANDING_INSTRUCTIONS`).

### Allocation strategies
`LA/domain/LoanRepaymentScheduleTransactionProcessorFactory.java:36-67`; codes seeded in
`ref_loan_transaction_processing_strategy` (`CL/tenant/parts/0002_initial_data.xml`):
- `mifos-standard-strategy` — Penalties, Fees, Interest, Principal (FineractStyle)
- `heavensfamily-strategy`, `creocore-strategy`
- `rbi-india-strategy` — Overdue/Due Fee/Int, Principal
- `principal-interest-penalties-fees-order-strategy` (also the factory default, line 38)
- `interest-principal-penalties-fees-order-strategy`
- `early-repayment-strategy`

Per-product choice, overridable per loan (`transactionProcessingStrategyId`). No "advanced payment allocation"
(per-transaction-type, due/past-due/in-advance rules).

### Partial, over- and advance payments
- Partial payments: allocated installment by installment in the strategy order; `inArrearsTolerance` sets the arrears
  threshold.
- Overpayment: unallocated remainder goes to `onLoanOverpayment` and `overPaymentPortion`
  (`AbstractLoanRepaymentScheduleTransactionProcessor.java:131-134, 481`); `Loan.totalOverpaid` (`Loan.java:305,
  1367-1370`); loan moves to `OVERPAID(700)` (`doPostLoanTransactionChecks`, `Loan.java:3373`). Refund with
  `creditBalanceRefund` (`LoanWritePlatformServiceJpaRepositoryImpl.java:3173`, checks against totalOverpaid at
  `Loan.java:3126`). If the loan is linked to savings, the guarantor/overpayment logic can push money back to savings.
- Paid-in-advance tracked per installment; job `UPDATE_LOAN_PAID_IN_ADVANCE`.

### Backdated transactions and reprocessing
`Loan.makeRepayment` -> `handleRepaymentOrRecoveryOrWaiverTransaction` (`Loan.java:3177-3262`): if the transaction is
not chronologically latest (or interest recalculation is on), **all post-disbursement transactions are replayed**
(`retreiveListOfTransactionsPostDisbursement` + `handleTransaction`, lines 3256-3262). In the replay
(`AbstractLoanRepaymentScheduleTransactionProcessor.java:155-180`) any existing repayment whose principal/interest/fee/
penalty split changes is **reversed and replaced by a new row with a new id**; the original's externalId is cleared
(`loanTransaction.updateExternalId(null)`, line 178) and copied to the replacement
(`LoanTransaction.copyTransactionProperties`, `LoanTransaction.java:288-296`). Journal entries are reversed/reposted
accordingly. Future-dated transactions are rejected ("The transaction date cannot be in the future", `Loan.java:3198`).

Consequence for the CRMS: a CBS loan-transaction **id is not stable** after a backdated posting; the transaction
**externalId is** (it moves to the replacement row).

### Reversal / adjust / undo
`adjustLoanTransaction` (`LoanWritePlatformServiceJpaRepositoryImpl.java:1083`; domain `Loan.adjustExistingTransaction`
`Loan.java:3599-3632`): only repayment-type or waiver transactions; reverses the old one, posts a new one (may carry a
new externalId), reprocesses. Blocked on foreclosed loans, transfer transactions and written-off loans. Also
`undodisbursal`, `undolastdisbursal`, `undoapproval`, `undowriteoff` (custom fix 22ad44a138 clears stale close/write-off
dates), undo charge waiver.

---

## 4. Charges, penalties, arrears, accruals, close/reschedule

- Charge time types for loans (`portfolio/charge/domain/ChargeTimeType.java`): `DISBURSEMENT(1)`,
  `SPECIFIED_DUE_DATE(2)`, `INSTALMENT_FEE(8)`, `OVERDUE_INSTALLMENT(9)` (penalty), `TRANCHE_DISBURSEMENT(12)`.
  Calculation: `FLAT`, `% of amount`, `% of amount+interest`, `% of interest`, `% of disbursement amount`
  (`ChargeCalculationType.java:24-27`). Payment mode `REGULAR` or `ACCOUNT_TRANSFER` (from linked savings,
  `ChargePaymentMode.java:23-24`). Charges can carry an `externalId` (b0e37c24eb, `0008_loan_charge_add_external_id.xml`).
- Penalties: job **Apply penalty to overdue loans** (`APPLY_CHARGE_TO_OVERDUE_LOAN_INSTALLMENT`,
  `LA/service/LoanSchedularServiceImpl.java:79`) applies OVERDUE_INSTALLMENT charges.
- Arrears: job **Update Loan Arrears Ageing** (`LoanArrearsAgingServiceImpl.java:90`) maintains `m_loan_arrears_aging`
  (principal/interest/fee/penalty overdue and `overdue_since_date_derived`, lines 105-113), honours
  `graceOnArrearsAgeing` and `isArrearsBasedOnOriginalSchedule` (line 504). Read side: `LoanSummaryData` has
  `principalOverdue ... totalOverdue, overdueSinceDate` (`LoanSummaryData.java:36-65`); `LoanAccountData.inArrears`,
  `isNPA` (lines 197-199); `GET /loans/{id}?associations=collection` returns `pastDueDays`, `delinquentDays`,
  `delinquentAmount`, `nextPaymentDueDate`, `lastPaymentDate/Amount` (`LoansApiResource.java:650-654`,
  `LoanReadPlatformServiceImpl.java:2373-2400`; per-loan last payment fix 22ad44a138).
- NPA: product `overdueDaysForNPA`, `accountMovesOutOfNPAOnlyOnArrearsCompletion`; job **Update Non Performing Assets**
  (`ScheduledJobRunnerServiceImpl.java:293`). Provisioning: `organisation/provisioning` (categories/criteria by age
  bucket) + job `GENERATE_LOANLOSS_PROVISIONING` (`ProvisioningEntriesWritePlatformServiceJpaRepositoryImpl.java:136`).
  This is the only "bucket" concept — **no delinquency ranges/buckets module and no delinquency tagging history**.
- Accruals: accounting rule `NONE/CASH_BASED/ACCRUAL_PERIODIC/ACCRUAL_UPFRONT` (`accounting/common/AccountingRuleType.java:26-29`);
  jobs `ADD_ACCRUAL_ENTRIES`, `ADD_PERIODIC_ACCRUAL_ENTRIES`, `..._FOR_LOANS_WITH_INCOME_POSTED_AS_TRANSACTIONS`
  (`LoanAccrualPlatformServiceImpl.java:54,83,127`); recalculation job `RECALCULATE_INTEREST_FOR_LOAN`
  (`LoanSchedularServiceImpl.java:127,182`). Custom accrual fix 6bedc3fb2a (section 6).
- Close / write-off / reschedule: `close`, `close-rescheduled`, `writeoff` + `undowriteoff`, `recoverypayment`
  (post-write-off recovery), `foreclosure` (custom: pays from linked savings, section 6), `/rescheduleloans`
  (change dates, add grace, change rate, extend term, EMI), top-up loans. Guarantor recovery (`recoverGuarantees`).

GL postings: each loan transaction posts journal entries per the product-to-GL mapping (`postJournalEntries` in the
write services; TP accounting overrides live under `TP/accounting/*`).

---

## 5. External id and idempotency

### Uniqueness — enforced by the database
- `m_loan.external_id VARCHAR(100) UNIQUE` (`CL/tenant/parts/0001_initial_schema.xml:2002-2003`; entity
  `@UniqueConstraint(... name = "loan_externalid_UNIQUE")`, `LA/domain/Loan.java:158,170`). A duplicate on create maps to
  a validation error (`LoanApplicationWritePlatformServiceJpaRepositoryImpl.java:1268-1269`, `value.must.be.unique`).
- `m_loan_transaction.external_id VARCHAR(100) UNIQUE` (`0001_initial_schema.xml:2709-2710`; entity
  `@UniqueConstraint(... name = "external_id_UNIQUE")`, `LA/domain/LoanTransaction.java:63,113`). Duplicate on save maps
  to `PlatformApiDataValidationException` param `externalId` code `value.must.be.unique`
  (`LoanWritePlatformServiceJpaRepositoryImpl.java:687-718`). Accepted (≤100 chars,
  `LoanEventApiJsonValidator.java:534-535`) on disburse, repayment, adjust, recovery, refund, credit-balance refund,
  goodwill/merchant/payout refund. NULLs are not unique-checked.
- So "send the CRMS reference as externalId" **is** a working duplicate guard for both loan creation and loan
  transactions: a retried repayment with the same externalId is rejected, not double-posted.

### Idempotency-Key / command idempotency — **absent**
- No `Idempotency-Key` header handling anywhere in `org/apache/fineract` (`CommandSource` has no idempotency column:
  `commands/domain/CommandSource.java:40-101`). That feature arrived in upstream 1.8/1.9; this fork predates it.
- `PortfolioCommandSourceWritePlatformServiceImpl.java:91-118` retries a command internally on
  `CannotAcquireLockException`/`ObjectOptimisticLockingFailureException` (rolled-back attempts only — safe).
- Maker-checker: if enabled for a permission, the command returns "awaiting approval" (no resource applied) via
  `RollbackTransactionAsCommandIsNotApprovedByCheckerException` (`SynchronousCommandProcessingService.java:79,144`).
  TP adds role approval-limit filtering of the checker inbox (`TP/commands/approvallimit/*`, loan APPROVE/DISBURSE amount
  extraction `CommandJsonAmountExtractor.java:110-115`). The CRMS must treat a 200 with `commandId` but no applied change
  as "pending checker", not success.

### Custom duplicate protection (TP) — transfers only
`TP/portfolio/transfer/service/TPIntrabankIdempotencyService.java:64-258` — reference-claim table with request hash,
statuses (PENDING_APPROVAL/SUCCESSFUL/FAILED/UNKNOWN/CREDIT_FAILED), stale handling, and a status lookup
(`InterbankTransferApiResource.java:266`, by source NUBAN + transactionReference). Gated by global configs
`intrabank-transfer-idempotency-enforced` / `virtual-account-transfer-idempotency-enforced`. It is wired only into the
TP intrabank/interbank/virtual-account transfer API (`InterbankTransferWriteServiceImpl.java:995-1300`), **not** into
`/loans/...` commands or plain `/accounttransfers`.

### Lookup by reference
- Loan by externalId: yes — `GET /loans?externalId=` (exact) and TP search (`TPSearchReadPlatformServiceImpl.java:245-247`,
  `account_no like` / `external_id like`).
- Loan transaction by externalId: **no dedicated endpoint**. Only `GET /loans/{loanId}/transactions/{transactionId}`;
  externalId is returned in each transaction (`LoanReadPlatformServiceImpl.java:1317`), so the CRMS must fetch
  `?associations=transactions` and filter client-side (remember: filter `manuallyReversed/reversed=false`).

---

## 6. Custom TransactionPoint / BOA changes that alter loan behaviour

| Commit | Date | Effect |
|---|---|---|
| ae93533fa5 "boa loan update module" + 5c1ce23a8e | 2026-04-17/19 | New product/loan setting `advancePaymentInterestCalculationStrategy` (`FULL_PERIOD` default, `TILL_PAYMENT_DATE`) on interest-recalculation details. With TILL_PAYMENT_DATE an early payment is charged interest only from period start to the payment date; only the principal part reduces the balance (`AbstractLoanScheduleGenerator.java` ~282-290 and ~861-943; `LoanApplicationTerms.java`; columns `advance_payment_interest_calc_type` on `m_product_loan_recalculation_details` / `m_loan_recalculation_details`, `CL/tp-v2/v3/0003_migration.xml` changesets 3:18, 3:19). Only applies when interest recalculation is enabled. |
| 6bedc3fb2a "adjusted accrual bug" | 2026-04-21 | Periodic accrual recognises the whole installment interest at once when it is fully paid (`LoanAccrualWritePlatformServiceImpl.java:162-170`, `LoanScheduleAccrualData.isInterestFullyPaid`). |
| 1361a1b393 "loan foreclosure" | 2026-06-26 | `foreclosure` on a loan with a linked savings account debits that savings account (fails on insufficient balance) instead of booking cash, and disables standing instructions of the closed loan (`LoanWritePlatformServiceJpaRepositoryImpl.java:3319-3356`, `AccountTransfersWritePlatformServiceImpl.transferFundsForForeclosure`, `LoanAccountDomainServiceJpa.foreCloseLoan`). |
| 18308c452a "loan recovery update" (+63171b8a85) | 2026-08-31 | **Loan arrears recovery from savings**: on every deposit into a client's savings account, automatically debits it to settle arrears on that client's overdue loans (past due > N grace days), each loan in its own transaction, recorded in a recovery table, reversible via `POST /tp/loanarrearsrecoveries/{id}?command=reverse`, listable via `GET /tp/loanarrearsrecoveries` (`TP/portfolio/loanarrearsrecovery/*`; executor `LoanArrearsRecoveryExecutorImpl.java`). Global config `loan-arrears-recovery-from-savings` (value = grace days) seeded **disabled** (`CL/tp-v2/v4/0004_loan_arrears_recovery.xml:91-98`); per-account exemption (`UpdateArrearsRecoveryExemptionCommandHandler`). |
| 22ad44a138 | 2026-10-07 | `associations=collection` last payment per loan fixed; undo write-off clears closed/written-off dates. |
| c8df17e9e2, 094405f228, 32c313b3a5, bf4ea77583, cfe62a762e, 828391a99a, d7e2071ca6, e7883378ca | 2025-05 | Disbursement-time charges collected from linked savings (re-enabled transfer; fixed a double withdrawal), with a savings note per charge. |
| 834669b990 | 2024-11 | Disbursement/repayment narration notes on transfers. |
| 0963ed28c1, 2457b1d9d1, cfe401056a, 0bec913509 | 2026-04 | Maker-checker / role approval-limit filtering touching loan approve/disburse. |
| `TP/portfolio/loanaccount/service/LoansTransactionHookMessenger.java:80-93` | — | Publishes `LOAN_DISBURSAL` to SNS; TP business-event constants cover approve/disburse/repayment/adjust/undo/writeoff/close etc. (`TPBusinessEventNotificationConstants.java:28-47`) — usable as a push feed for reconciliation. |

No custom repayment-allocation strategy, no custom schedule type beyond the TILL_PAYMENT_DATE tweak, no custom
loan externalId or idempotency handling.

---

## 7. What the CRMS can rely on vs what it lacks

**Rely on core banking for (do not rebuild):**
1. Amortisation engine: equal principal / equal installment, flat / declining, daily or period interest, actual/360/364/365,
   actual/30 months, all grace types incl. interest-free, moratorium, balloon/bullet, fixed EMI, variable installments,
   floating rates, rounding (digits, multiples, installment multiples, global rounding mode). Preview with
   `POST /loans?command=calculateLoanSchedule` — already used by the CRMS.
2. Loan subledger: installments with derived paid/waived/written-off/outstanding/overdue per component, loan summary,
   arrears ageing, NPA flag, provisioning, accruals, GL journal entries.
3. Repayment allocation (7 strategies), partial / advance / overpayment (OVERPAID + credit balance refund), backdated
   repayments with automatic replay, adjust/undo, waivers, write-off + recovery, foreclosure/prepay quote, reschedule,
   top-up, penalties via job, standing-instruction auto-debit, and (if switched on) automatic arrears sweep from savings.
4. Duplicate protection by externalId: DB-unique on loans and loan transactions — the CRMS can make every posting
   idempotent by always sending a deterministic externalId and treating `value.must.be.unique` as "already posted, look it up".

**Genuinely lacking (CRMS or a CBS change must supply):**
1. No `Idempotency-Key`/request replay: a retry with a *new* or *missing* externalId will double-post; the CRMS must
   generate and persist the externalId before calling.
2. No lookup of a loan transaction by externalId or reference — must fetch all transactions and filter; and transaction
   ids change on backdated replay (externalId moves to the new row).
3. No single atomic create+approve+disburse; partial states (created-not-approved, approved-not-disbursed) must be
   resumed by the caller (the CRMS already does this).
4. No delinquency buckets/classification history, no chargeback/charge-off, no advanced per-type payment allocation.
5. Maker-checker may silently park a command as "awaiting approval"; the CRMS must detect that.
6. Reconciliation reports between CRMS and CBS — there is no CBS-side "posted by external system X" report; the CRMS
   would reconcile using loan externalId + transaction externalId pulled from `GET /loans/{id}?associations=transactions`,
   or consume the SNS business events.
