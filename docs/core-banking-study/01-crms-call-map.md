# 01 — CRMS → Core Banking call map (CRMS ↔ tpbackend)

Read-only study, 2026-11-19. CRMS at `C:\Users\adeta\source\repos\tpcrms` (branch RH_SHF-branch_2_cred_profile, HEAD ed5761b).
tpbackend at `C:\absdev\tpbackend` (master 531c48b9c2). BOA deploys `origin/bankofagriculture` (cf712770fa), 7 commits behind
master; the diff (deposit SNS events, invoice funding via TPM, intrabank idempotency) touches **none** of the routes below, so every
tpbackend finding here holds for the BOA build too. No credentials, tokens or connection strings are reproduced.

Paths: CRMS paths relative to `tpcrms/src/`; tpbackend Java paths relative to `fineract-provider/src/main/java/` unless stated.
`TP` = `net/aggregatebusiness/transactionpoint`, `FIN` = `org/apache/fineract`.

---

## 0. Topology

| CRMS client | Interface | Config section (keys only) | Base URL shape | Auth | Lands on |
|---|---|---|---|---|---|
| `FineractDirectService` | `IFineractDirectService` (`CRMS.Domain/Interfaces/IFineractDirectService.cs`) | `FineractDirect`: BaseUrl, Username, Password, TenantId, TimeoutSeconds, UseMock | `https://tpapi.<env>/core_banking/api/` | `POST {base}/authentication` → `Authorization: Basic <base64EncodedAuthenticationKey>` + `fineract-platform-tenantid` | **tpbackend directly** |
| `CoreBankingService` | `ICoreBankingService` (`CRMS.Domain/Interfaces/ICoreBankingService.cs`) | `CoreBanking`: BaseUrl, ClientId, ClientSecret, TokenEndpoint (`/security/authenticate`), TimeoutSeconds, UseMock | `https://tpgateway.<env>` + `/core/...` | OAuth2-ish client credentials at `{base}/security/authenticate` → `Authorization: Bearer` | **tpgateway — a separate service, NOT in tpbackend** |

No other CRMS HttpClient talks to core banking. The other typed clients in `CRMS.Infrastructure/DependencyInjection.cs` are SmartComply
(:181), Credit Registry (:166), NAMP/RH-SHF portal callbacks (:221, :998), customer-check partner callbacks (:415) and OpenAI (:469).
Grep for `core_banking|tpapi|tpgateway|/core/` across `src/` finds only the two services above plus comments.

### Config state (keys/flags only)
- `CRMS.Web.Intranet/appsettings.json:46-52,132-138` — CoreBanking UseMock=false, FineractDirect UseMock=false (TEST/sandbox hosts).
- `CRMS.API/appsettings.json:43-57` — CoreBanking UseMock=false but **FineractDirect UseMock=true**. So on TEST the API host (NAMP
  webhook schedule preview, loan-account query, Health probe) uses `MockFineractDirectService` unless an env var overrides it, while
  the intranet uses the live sandbox. Production files (`appsettings.Production.json` in both hosts) set both UseMock=false.
- Settings classes default `UseMock = true` (`FineractDirectSettings.cs:10`, `CoreBankingSettings.cs:10`) and DI only wires the real
  client when the section exists *and* UseMock is false (`DependencyInjection.cs:107,127`).
- `CoreBanking/FineractSettings.cs` (with RetryCount) is dead — never bound.

### HTTP pipeline (both clients) — `DependencyInjection.cs:106-151, 1022-1027`
Order (outer→inner): `ThirdPartyApiLoggingHandler` → auth handler → Polly `HandleTransientHttpError().WaitAndRetryAsync(2, 1s/2s)`.
- `HttpClient.Timeout` = `TimeoutSeconds` (30s) and covers the whole send **including both retries**.
- Polly retries on 5xx, 408 and `HttpRequestException` for **every verb**, including the non-idempotent `POST /loans`,
  `?command=approve`, `?command=disburse[ToSavings]`.
- Fineract client accepts certs with `RemoteCertificateNameMismatch` (`:138-143`; same in `FineractDirectAuthHandler.cs:74-79`).
  CBS client uses default validation.
- No `Idempotency-Key`/`X-Request-Id` header is ever sent. The only idempotency mechanism is the loan `externalId` (below).

### Auth handlers
- `FineractDirect/FineractDirectAuthHandler.cs:32-44` adds Basic key, `fineract-platform-tenantid`, `User-Agent: Mozilla/5.0`,
  `Accept: application/json`. Key cached 8h (`:115`), **never invalidated on 401**. If auth fails it returns null and the request is
  sent with no Authorization (`:35-38`) → tpbackend 401.
- `CoreBanking/CoreBankingAuthHandler.cs:65-148` tries form-urlencoded then JSON client-credentials; token cached `expires_in-60`
  (default 3600). Same silent "send without Authorization" on failure (`:35-38`). **Security: it logs the full token request payload,
  client secret included, at Information (`:87` form, `:107` JSON).**

### tpbackend URL mapping
- `fineract-provider/src/main/resources/application.properties:101` → `server.servlet.context-path=${FINERACT_SERVER_SERVLET_CONTEXT_PATH:/core_banking}`.
- `FIN/infrastructure/core/config/JerseyConfig.java:36` → `@ApplicationPath("/api")`; resources are `@Path("/loans")` etc.
- So `/core_banking/api/loans` = Fineract `LoansApiResource`. **There is no `/v1` segment** in this fork, and no
  `/fineract-provider` prefix. CRMS's base `.../core_banking/api/` is correct.
- **Doc drift:** `docs/api/tpbackend.openapi.json:8-20` (and tpbackend CLAUDE.md) give the default base as
  `/fineract-provider/api/v1`; that is upstream Fineract, not this deployment.
- Auth: `application.properties:61-63` basic auth on, OAuth off, 2FA off. `FIN/infrastructure/security/api/AuthenticationApiResource.java:95-117`
  reads `{username,password}` JSON body — matches CRMS.

### tpgateway (`/core/*`) — not in tpbackend
- tpbackend has no `/core/...` routes and no `fulldetailsbynuban` anywhere (grep of `fineract-provider/src/main/java` and docs).
- tpbackend exposes a gateway entry: `TP/infrastructure/security/configuration/TPGatewayCoreBankingConfig.java:41` registers
  `TPGatewayCoreBankingFilter` on `/api/tpgcore/*`. `TP/infrastructure/security/filter/TPGatewayCoreBankingFilter.java:61-150` requires a
  `clientID` header, checks `(method,url)` against `TPGatewayCoreBankingPermission` rows, strips `/tpgcore` and forwards to the normal
  `/api/...` route; any rejection is HTTP **401** with a JSON exception body.
- So the chain is CRMS → tpgateway `/core/...` → tpbackend `/core_banking/api/tpgcore/<tp route>`. The tpgateway code (which route it
  calls, how it reshapes) is **not in either repo** studied; mappings below marked *inferred* are by shape and naming only.

---

## 1. `IFineractDirectService` (tpapi → tpbackend) — method by method

All in `CRMS.Infrastructure/ExternalServices/FineractDirect/FineractDirectService.cs`. Common behaviour: non-2xx → `Result.Failure("Fineract returned {code}: {body}")`;
any exception (incl. HttpClient timeout, which surfaces as `TaskCanceledException` with the caller's token not cancelled) → `Result.Failure("Fineract error: ...")`;
caller cancellation is rethrown. JSON is case-insensitive; Fineract dates are read as `[y,m,d]` int arrays (`:1026-1038`).

### F1. Schedule preview — `CalculateRepaymentScheduleAsync`
- CRMS: `POST loans?command=calculateLoanSchedule` (`:57-137`). Skipped entirely (in-house calc) when `ProductId <= 0` (`:41-45`); on any failure
  falls back to the in-house calculator `MockFineractDirectService` (`:48-54`) with only a log warning.
- Sends: dateFormat, locale, productId, principal, loanTermFrequency (=repayments×every), loanTermFrequencyType, numberOfRepayments, repaymentEvery,
  repaymentFrequencyType, interestRatePerPeriod, interestRateFrequencyType, amortizationType, interestType, interestCalculationPeriodType,
  expectedDisbursementDate, transactionProcessingStrategyId (default 1), loanType ("individual"), optional graceOnPrincipalPayment/graceOnInterestPayment.
  **Does not send `clientId` or `submittedOnDate`.**
- Reads: totalPrincipalExpected, totalInterestCharged, totalFeeChargesCharged, totalPenaltyChargesCharged, totalRepaymentExpected, periods[period>0]
  {period, fromDate, dueDate, principalDue, interestDue, feeChargesDue, penaltyChargesDue, totalDueForPeriod, principalLoanBalanceOutstanding}.
- Callers: Corporate offer letter `CRMS.Application/OfferLetter/Commands/GenerateOfferLetterCommand.cs:97`; Corporate new-application preview
  `CRMS.Web.Intranet/Services/ApplicationService.cs:2207`; NAMP appraisal `CRMS.Application/Namp/Commands/NampWorkflowCommands.cs:634`;
  NAMP portal webhook `CRMS.API/Controllers/NampWebhookController.cs:328`.
- tpbackend: `FIN/portfolio/loanaccount/api/LoansApiResource.java:796-829` → `LoanScheduleCalculationPlatformServiceImpl.calculateLoanSchedule(query, true)`
  (`FIN/portfolio/loanaccount/loanschedule/service/LoanScheduleCalculationPlatformServiceImpl.java:103-116`) → with validateParams=true it runs
  `LoanApplicationCommandFromApiJsonHelper.validateForCreate` (`FIN/portfolio/loanaccount/serialization/LoanApplicationCommandFromApiJsonHelper.java:112-118`),
  which for `loanType=individual` requires `clientId` notNull (`:133-137`) and requires `submittedOnDate` (`:368-371`).
- **MISMATCH (high): every real-Fineract schedule preview is rejected with 400 (validation) and CRMS silently substitutes its own calculation.** The
  offer-letter / NAMP appraisal schedules are therefore never core banking's numbers (fees, rounding, day-count, product charges all CRMS-side).
  Fix on CRMS side: send `clientId` (where known) and `submittedOnDate`, and surface — not swallow — the fallback.

### F2. Client accounts — `GetClientAccountsAsync`
- CRMS: `GET clients/{clientId}/accounts` (`:139-204`). Reads loanAccounts {id, accountNo, productName, productId, status{id,value}, loanType.value, externalId,
  inArrears, timeline.actualDisbursementDate, timeline.expectedMaturityDate}; savingsAccounts {id, accountNo, productName, status.value, accountBalance, externalId,
  depositType/accountType.value, timeline.activatedOnDate, lastActiveTransactionDate}.
- Callers: Customer credit check `CRMS.Infrastructure/Services/CustomerCheckService.cs:488`; exposure (F4); Corporate loan-account view
  `CorporateLoanRestructureCommands.cs:499`.
- tpbackend: `FIN/portfolio/client/api/ClientsApiResource.java:381-393`; DTOs `FIN/portfolio/accountdetails/data/LoanAccountSummaryData.java:32-46`,
  `SavingsAccountSummaryData.java:35-51`; timeline `FIN/portfolio/loanaccount/data/LoanApplicationTimelineData.java:48,56`. All fields CRMS reads exist.
- Gap (minor): tpbackend also returns `loanBalance`, `originalLoan`, `amountPaid` per loan; CRMS ignores them and makes one `GET loans/{id}` per active loan instead (N+1).

### F3. Loan detail — `GetLoanDetailAsync`
- CRMS: `GET loans/{loanId}?associations=repaymentSchedule` (`:206-292`). Reads id, accountNo, productName|loanProductName, status{id,value}, principal,
  approvedPrincipal, annualInterestRate, numberOfRepayments, timeline.{actualDisbursementDate,expectedMaturityDate}, summary.* (15 fields),
  repaymentSchedule.periods[period>0].
- Callers: booking read-back (`:835,877`); exposure (F4); Customer check `CustomerCheckService.cs:605`; Party exposure / Farmer-360
  `CRMS.Infrastructure/Services/PartyExposureService.cs:416`; NAMP loan account `CRMS.Application/Namp/Queries/NampApplicationQueries.cs:622`,
  `NampWebhookController.cs:371`; RH-SHF loan account `CRMS.Application/Rhshf/Queries/GetRhshfLoanAccountQuery.cs:37`; Corporate `CorporateLoanRestructureCommands.cs:515`.
- tpbackend: `LoansApiResource.java:487-497`; `FIN/portfolio/loanaccount/data/LoanAccountData.java:80,93,94,107,132,135` (no `productName` field —
  CRMS's `loanProductName` fallback is what actually works).
- Gap: CRMS hard-codes `PrincipalPaid/InterestPaid/InterestOutstanding/TotalPaid = 0` per period (`:253-261`) although tpbackend returns
  `principalPaid`, `interestPaid`, `interestOutstanding`, `totalPaidForPeriod` (`FIN/portfolio/loanaccount/loanschedule/data/LoanSchedulePeriodData.java:41-66`).
  CRMS also maps period `PrincipalOutstanding` from `principalLoanBalanceOutstanding` (loan balance), not the period's `principalOutstanding`.

### F4. Customer exposure — `GetCustomerExposureAsync(clientId, ...)`
- Composite: F2, then F3 for every loan with status 300 (`:294-344`). "Overdue" is inferred as `totalOutstanding>0 && penaltyChargesOutstanding>0`
  — not Fineract's `inArrears` flag, which F2 already returns.
- Caller: Credit advisory `CRMS.Application/Advisory/Commands/GenerateCreditAdvisoryCommand.cs:363`.

### F5. Client by id — `GetClientByIdAsync`
- CRMS: `GET clients/{clientId}` (`:346-384`); reads id, officeId, officeName, displayName|firstname+lastname; **fails if officeName blank**.
- Callers: NAMP webhook branch resolution `NampWebhookController.cs:525`; RH-SHF EOP submit `CRMS.Application/Rhshf/Commands/SubmitConsolidatedEopCommand.cs:400`.
- tpbackend: `ClientsApiResource.java:178` (`retrieveOne`). Fine.

### F6. Client search by BVN/phone/email/NUBAN — `FindClientsAsync`
- CRMS: `POST tp/clients/find?accounts=true` (`:386-455`), body subset of {externalId, mobileNo, emailAddress, nuban}; **404 → empty list (success)**;
  accepts array, `{pageItems}` or single object (`:459-471`). Reads id, accountNo, externalId, status.value, active, activationDate|timeline.activatedOnDate,
  names, displayName, mobileNo, emailAddress, dateOfBirth, gender.name, clientType.name, officeId, officeName, kycLevel, accounts[]{id, accountNo, externalId,
  savingsProductName, summary.accountBalance}.
- Caller: Customer credit check API `CustomerCheckService.cs:455` (BVN only).
- tpbackend: `TP/portfolio/client/api/TPClientsApiResource.java:280-291` → `TP/portfolio/client/service/TPClientReadPlatformServiceImpl.java:612-738`.
  `externalId` matches `findByExternalIdOrEntityBVN` (`:661`) — i.e. personal BVN **or** a corporate entity BVN, so a BVN search can return company clients;
  `nuban` via savings externalId (`:679`); `mobileNo` via `MClientRepository.findByMobileNo` returning a **single** row (`:653`, `MClientRepository.java:37`) →
  duplicates would 500 (CRMS does not send mobile today). Accounts list = **active savings only** (`:706`) built from `SavingsAccountData.instance(MFSavingsAccount, Client)`
  (`TP/portfolio/savings/data/SavingsAccountData.java:287-317`) which fills `summary.accountBalance` and `availableBalance`. No match → `ClientNotFoundException` (`:737`) → 404,
  which CRMS correctly reads as "no record". Shape matches; `nin` is supported by tpbackend but CRMS never sends it.

### F7. Loan product catalogue — `GetLoanProductsAsync`
- CRMS: `GET loanproducts` (`:473-554`); bare array. Reads id, name, shortName, description, currency{code,displaySymbol}, principal/min/max,
  interestRatePerPeriod/min/max, annualInterestRate, interestRateFrequencyType.value, numberOfRepayments/min/max, repaymentEvery, repaymentFrequencyType.value,
  amortizationType.value, interestType.value, transactionProcessingStrategyId, startDate, closeDate, **charges[]**. "Active" = closeDate null or ≥ today (ignores `status`).
- Callers: booking (`:581`); RH-SHF product terms `CRMS.Application/Rhshf/Services/RhshfProductTerms.cs:53`; RH-SHF offer letter fees
  `CRMS.Application/Rhshf/Services/RhshfOfferDocumentDataBuilder.cs:385,406`; RH-SHF booking `BookRhshfDisbursementCommand.cs:60`; NAMP appraisal/recall
  `NampWorkflowCommands.cs:602`, `RecallNampApplicationCommand.cs:85`; Admin product link `ApplicationService.cs:3218`; Health probe `CRMS.API/Controllers/HealthController.cs:188`.
- tpbackend: `FIN/portfolio/loanproduct/api/LoanProductsApiResource.java:204-221` → `LoanProductReadPlatformServiceImpl.retrieveAllLoanProducts()`
  (`FIN/portfolio/loanproduct/service/LoanProductReadPlatformServiceImpl.java:103-120`) builds `new LoanProductMapper(null, null, null)` — **charges are null in the list**.
  Charges are only loaded for a single product (`:81-85`, `GET loanproducts/{id}`). Also filtered to the CRMS user's office when branch-specific products are on (`:113-117`).
- **MISMATCH (high): `FineractLoanProduct.Charges` is always empty against real core banking.** The RH-SHF offer letter's "fees core banking applies at booking"
  and the double-financing guard (`RhshfOfferDocumentDataBuilder.cs:406-420`) therefore never see a fee. Fix: `GET loanproducts/{id}` for the linked product.
  *Resolved:* the letter (46ce0fa) and booking (2026-11-23) now read `GET loanproducts/{id}`.
- **Found 2026-11-23: core banking never copies product charges onto a loan.** `LoanChargeAssembler.fromParsedJson` builds them only from the
  `charges` array on `POST /loans`; its web app sends them from `GET /loans/template`. CRMS sent none, so CRMS-booked loans carried no fees.
  Booking now sends the product's non-overdue charges (`FineractDirectService.LoanChargesFor`); RH-SHF excludes charges the EOP already finances.

### F8. BOA account → client — `GetNampBoaAccountAsync` (used by all three tracks)
- CRMS: `GET tp/savingsaccounts/byexternalId/{nuban}` (`:990-1021`); reads id, accountNo, clientId, status.value.
- Callers: NAMP intake/recall/booking (`NampWebhookController.cs:520`, `RecallNampApplicationCommand.cs:346`, `NampWorkflowCommands.cs:1347`); RH-SHF booking
  `BookRhshfDisbursementCommand.cs:56` and EOP `SubmitConsolidatedEopCommand.cs:393`; Corporate disbursement `CorporateLoanRestructureCommands.cs:399,495`;
  booking's linkAccountId resolution (`:601`).
- tpbackend: `TP/portfolio/savings/api/TPSavingsAccountsApiResource.java:155-200` → `TPSavingsAccountReadPlatformServiceImpl.retrieveOneByExternalId`
  (`TP/portfolio/savings/service/TPSavingsAccountReadPlatformServiceImpl.java:260-329`): `queryForObject ... where sa.external_id = ?`; not found →
  `SavingsAccountNotFoundException` → 404. Duplicate externalIds would raise `IncorrectResultSize` → 500 (Polly retries twice). Fields match.

### F9. Booking — `BookApprovedLoanAsync` (Corporate / NAMP / RH-SHF disbursement)
Sequence (`:556-988`):
1. **Probe** `GET loans?externalId={ext}` (`:907-976`) — best effort; reads pageItems[0] / array[0] / object {id, status{id,value}, accountNo, principal}.
2. `GET loanproducts` (F7) to map product settings.
3. F8 for linkAccountId; must belong to `ClientId` else not linked; hard failure if not resolved and DisburseToSavings or standing instruction requested (`:629-635`).
4. `POST loans` (`:656-701`): dateFormat, locale, clientId, productId, principal, loanTermFrequency (=TenorMonths×repaymentEvery), loanTermFrequencyType,
   numberOfRepayments (=TenorMonths), repaymentEvery, repaymentFrequencyType, interestRatePerPeriod (annual/12 if product is monthly), interestRateFrequencyType,
   amortizationType, interestType, interestCalculationPeriodType=1, expectedDisbursementDate, submittedOnDate, loanType=individual, transactionProcessingStrategyId,
   externalId, linkAccountId, createStandingInstructionAtDisbursement. Reads `loanId`.
5. `POST loans/{id}?command=approve` {approvedOnDate, approvedLoanAmount, expectedDisbursementDate, note} (`:766-798`).
6. `POST loans/{id}?command=disburse|disburseToSavings` {actualDisbursementDate, transactionAmount, note} (`:800-828`).
7. F3 read-back for accountNo/status.
- Resume: status 100 → approve+disburse, 200 → disburse, 300/600/601/602/700 → "already disbursed" (`:755,868-899`); others → hard failure.
- Callers / externalId: Corporate `CorporateLoanRestructureCommands.cs:408-420` (ApplicationNumber, DisburseToSavings=true); NAMP `NampWorkflowCommands.cs:1234-1247,1375-1388`
  (ApplicationNumber, disburse to vendor, SI=true); RH-SHF `BookRhshfDisbursementCommand.cs:75-87,111` (`{Reference}-C{cycle}`, disburse, SI default true).
- tpbackend:
  - List/probe: `LoansApiResource.java:766-793` (`externalId` query param), `FIN/portfolio/loanaccount/service/LoanReadPlatformServiceImpl.java:296-332` —
    `l.external_id = ?` **AND** `o.hierarchy like <caller office>%` (`:299-318`). Returns a Page `{totalFilteredRecords, pageItems}`.
  - Create/approve/disburse: `LoansApiResource.java:796-829, 884-935`; all go through `commandsSourceWritePlatformService.logCommandSource`.
    `stateTransitions` injects `originalMakerId` (`TP/commands/approvallimit/util/MakerIdentityJsonInjector.java`).
  - All CRMS create fields are in the supported-parameter list (`LoanApplicationCommandFromApiJsonHelper.java:82-93`); `createStandingInstructionAtDisbursement`
    requires linkAccountId (`:393-400`) — CRMS honours this.
  - externalId uniqueness: column-level `unique="true"` in `fineract-provider/src/main/resources/db/changelog/tenant/parts/0001_initial_schema.xml:2002-2004`
    and JPA `@UniqueConstraint(name="loan_externalid_UNIQUE")` (`FIN/portfolio/loanaccount/domain/Loan.java:158`). Translation to the friendly
    "Loan with externalId `x` already exists" happens only when the DB error text contains `loan_externalid_UNIQUE`
    (`FIN/portfolio/loanaccount/service/LoanApplicationWritePlatformServiceJpaRepositoryImpl.java:1260-1277`); otherwise
    "Unknown data integrity issue with resource." Both are HTTP **403** (`FIN/infrastructure/core/exceptionmapper/PlatformDataIntegrityExceptionMapper.java:52`).
  - Maker-checker: `FIN/commands/service/SynchronousCommandProcessingService.java:79,106-145` — when the task is maker-checker enabled and the
    user has no `markerCheckerOverride`, the command is rolled back and `logCommand` returns **200 `{commandId, resourceId}`** with status "awaiting approval"
    (`:153-161`, caught in `PortfolioCommandSourceWritePlatformServiceImpl.java:115-117`).

---

## 2. `ICoreBankingService` (tpgateway) — method by method

All in `CRMS.Infrastructure/ExternalServices/CoreBanking/CoreBankingService.cs`; DTOs in `CoreBankingDtos.cs`.

### C1. Account full details — `GET /core/account/fulldetailsbynuban/{nuban}` (`:47-82`)
- Cached per service instance in `_detailsCache` (`:28,49-50,70`). Response read: `clientType` (top-level, "BUSINESS"→Corporate), `clientDetails`
  {id, status (string), active, fullName, mobileNo, bvn, officeId/Name, clientType, incorporationNumber, kycLevel, activationDate|activatedOnDate ("dd-MM-yyyy"),
  dateOfBirth, addressLine1-3, city, state}, `directors[]`/`signatories[]` {id, firstname, lastname, mobileNo, email, bvn, dateOfBirth, address parts, savingsAccountId}.
- Backs five interface methods:
  - `GetCustomerByAccountNumberAsync` (`:84-112`) — Corporate initiation `InitiateCorporateLoanCommand.cs:74`, `GetCorporateAccountDataQuery.cs:25`,
    intranet `ApplicationService.cs:469`, RH-SHF NUBAN enquiry `Rhshf/Queries/LookupCoreBankingAccountQuery.cs:46`, RH-SHF BOA snapshot `GetRhshfBoaAccountSnapshotQuery.cs:51`.
  - `GetCorporateInfoAsync` (`:126-145`) — `InitiateCorporateLoanCommand.cs:83`, `GetCorporateAccountDataQuery.cs:34`, RH-SHF director pull/cross-check
    `PullRhshfDirectorsFromCbsCommand.cs:39`, `RunRhshfDirectorCrossCheckCommand.cs:88`.
  - `GetDirectorsAsync(corporateId)` (`:147-172`) — **ignores its argument and returns `_detailsCache.Values.FirstOrDefault()`'s directors, or an empty success list
    if nothing is cached**. Callers: `InitiateCorporateLoanCommand.cs:135`, `GetCorporateAccountDataQuery.cs:44`, `ApplicationService.cs:493`, RH-SHF `:42/:96`.
  - `GetSignatoriesAsync` (`:174-193`) — MandateType hard-coded "A".
  - `GetAccountInfoAsync` (`:195-217`) — balances hard-coded **0** (endpoint has none). Callers: RH-SHF NUBAN enquiry `LookupCoreBankingAccountQuery.cs:38`,
    BOA snapshot `GetRhshfBoaAccountSnapshotQuery.cs:43`, statement query `GetAccountStatementQuery.cs:66`, Health probe `HealthController.cs:158` (fixed NUBAN).
  - `GetAccountRawJsonAsync` (`:317-335`) — Admin diagnostics `ApplicationService.cs:556`.
- tpbackend: no such route. *Inferred* backing route (same composition — client + directors + signatories keyed by NUBAN):
  `GET /tp/savingsaccounts/fullclientdetails/{externalId}` (`TP/portfolio/savings/api/TPSavingsAccountsApiResource.java:393-403` →
  `TPSavingsAccountReadPlatformServiceImpl.java:973-982`, shape `TP/portfolio/client/data/TPClientFullDetailsData.java:30-32` = `{clientDetails, directors, signatories}` of
  `TPClientData`). The tpbackend shape is **not** what CRMS parses: `TPClientData` has `fullname` (not `fullName`), `status` as an object, `externalId` (BVN) not `bvn`,
  dates as arrays, `address` as a collection, and there is no top-level `clientType` (`TP/portfolio/client/data/TPClientData.java:54-150`). So tpgateway reshapes; the
  CRMS DTO is tpgateway's contract and cannot be verified here.

### C2. Statement — `GET /core/transactions/{nuban}?startDate=dd-MM-yyyy&endDate=dd-MM-yyyy` (`:219-281`)
- Reads array of {id, amount, note, transactionType, balance, createdDate (DateTime), transactionReference, sessionID}. Credit iff `transactionType == "Deposit"`
  (case-insensitive), everything else Debit (`:248-250`). Opening balance derived from the first row.
- Callers: Corporate initiation `InitiateCorporateLoanCommand.cs:196`, `GetCorporateAccountDataQuery.cs:57`, statement refetch `RefetchInternalBankStatementCommand.cs:50`,
  `GetAccountStatementQuery.cs:24`, Customer credit check `CustomerCheckService.cs:575`.
- tpbackend: no such route. *Inferred* backing route: `GET /tp/savingsaccounts/transactions/{externalId}?startDate&endDate` (`TPSavingsAccountsApiResource.java:298-309`
  → `TPSavingsAccountReadPlatformServiceImpl.java:412-452`), which parses `dd-MM-yyyy` (`:414`) — the same format CRMS sends — and on a parse error **logs and
  silently drops the filter** (`:427-430`, falls through to all history). Query: `MFSavingsAccountTransactionRepository.java:33-37` — no `is_reversed` filter, so reversed
  rows are included (`reversed` flag in `MFSavingsAccountTransactionData`); transaction types are `DEPOSIT`, `WITHDRAWAL`, `INTEREST_POSTING`, `DIVIDEND_PAYOUT`, ...
  (`MFSavingsAccountTransactionData.java:146-170`). If tpgateway passes these through, CRMS counts interest postings/dividends/reversal credits as **debits** and does
  not drop reversed rows (CRMS DTO has no `reversed` field). Needs tpgateway confirmation.

### C3. Not implemented (return Failure without any HTTP call)
`GetCustomerByIdAsync` (`:114-118`), `CreateCustomerAsync`, `GetAccountBalanceAsync` (`:283-287`), `CreateLoanAsync`/`ApproveLoanAsync`/`DisburseLoanAsync`/
`GetLoanInfoAsync`/`GetRepaymentScheduleAsync`/`GetLoanStatusAsync` (`:293-309`), `GetCustomerExposureAsync` (`:314-315`; planned `GET /core/account/exposure/{acct}`
per `ICoreBankingService.cs:215-218`). All real loan work goes through `IFineractDirectService`.

---

## 3. Mismatch / risk register

| # | Sev | Where | Finding |
|---|---|---|---|
| M1 | High | F1 | `calculateLoanSchedule` omits `clientId` + `submittedOnDate`; tpbackend validates as a create (`LoanScheduleCalculationPlatformServiceImpl.java:112-115`, `LoanApplicationCommandFromApiJsonHelper.java:136,368-371`) → 400 → CRMS silently uses its own schedule for offer letters, NAMP appraisal and the portal preview. |
| M2 | High | F7 | `GET /loanproducts` list never carries `charges` (`LoanProductReadPlatformServiceImpl.java:107`); CRMS reads fees from it → RH-SHF letter fees and double-financing guard always empty. *Resolved; see F7, including the 2026-11-23 finding that booking sent no charges.* |
| M3 | High (conditional) | F9 | Under Fineract maker-checker for CREATE/APPROVE/DISBURSE_LOAN, tpbackend answers **200** `{commandId}` while nothing happened (`SynchronousCommandProcessingService.java:133-145,153-161`). CRMS treats any 2xx on approve/disburse as done and returns `Disbursed=true` regardless of the read-back status (`FineractDirectService.cs:812-850`). On create it fails ("invalid booking response", `:729-732`) but each retry queues another pending command. Safe only if the CRMS service user has `markerCheckerOverride` / BATCHPOSTING_TPDIRECT (`:120-129`) or maker-checker is off for those tasks — verify on BOA. |
| M4 | Med | F9 | Duplicate-externalId detection relies on the text "external ... already exists" (`:979-985`), which tpbackend produces only if the DB index is literally named `loan_externalid_UNIQUE`. A Liquibase-created schema (`0001_initial_schema.xml:2002-2004`, unnamed inline unique) yields "Unknown data integrity issue" (403) → CRMS reports a plain booking failure instead of resuming. No double loan (DB still enforces), but no auto-resume; the next attempt's probe recovers it. |
| M5 | Med | F9 | Probe `GET /loans?externalId=` is scoped to the CRMS user's office hierarchy (`LoanReadPlatformServiceImpl.java:299-318`). If that user is not at head office, the probe misses loans in other branches → relies on M4 path. Resume also does not check that the found loan's client/product match the request (`:858-899`). |
| M6 | Med | pipeline | Polly retries POST create/approve/disburse on 5xx/408/network errors (`DependencyInjection.cs:1022-1027`); no idempotency header (tpbackend has none for loans). Server-side, Fineract itself retries deadlocks with 1–11 s sleeps (`PortfolioCommandSourceWritePlatformServiceImpl.java:96-114`), all inside CRMS's single 30 s budget. |
| M7 | Med | F9 | A timeout/exception during approve or disburse escapes to the outer catch (`:744-748`) → `Result.Failure` with **no loanId**, contradicting the documented "Success with Disbursed=false and loanId intact" contract (`:757-761`). The disburse may have committed. Recovery depends on the next attempt's externalId probe (see M5). |
| M8 | Med | C1 | `GetDirectorsAsync` ignores `corporateId` and returns the first cached NUBAN's directors, or an empty *success* when the cache is empty (`CoreBankingService.cs:153-155`). Callers cannot tell "no directors" from "never looked up". |
| M9 | Med (inferred) | C2 | Credit/debit by `"Deposit"` only; reversed rows not excluded; date-parse failure in tpbackend silently returns full history. Depends on tpgateway's pass-through. |
| M10 | Low | F3 | Per-period paid/outstanding fields returned by tpbackend are zeroed by CRMS; `PrincipalOutstanding` mapped from loan balance. |
| M11 | Low | F4 | "Overdue" derived from penalties, not `inArrears`; N+1 loan detail calls (tpbackend's `loanBalance` in F2 unused). |
| M12 | Low | F9 | `numberOfRepayments = TenorMonths` and `loanTermFrequency = TenorMonths × repaymentEvery` — correct only when `repaymentEvery = 1` month; a quarterly product would get 3× the tenor. RH-SHF passes `DefaultNumberOfRepayments` (1) so it is fine there; NAMP/Corporate pass months. |
| M13 | Low | F6 | BVN search also matches corporate entity BVN (`TPClientReadPlatformServiceImpl.java:661`); only active savings returned; `nin` supported but unused. |
| M14 | Low | F7 | Product "active" ignores tpbackend `status`; catalogue may be office-filtered for the CRMS user (`LoanProductReadPlatformServiceImpl.java:113-117`). |
| M15 | Low | auth | Fineract key cached 8 h with no invalidation on 401; both auth handlers send requests without Authorization when auth fails, producing a 401 that reads as an API error. |
| S1 | Security | CBS auth | `CoreBankingAuthHandler.cs:87,107` log the client-credentials payload including the client secret at Information level. |
| D1 | Doc | tpbackend | OpenAPI/CLAUDE.md base path `/fineract-provider/api/v1` ≠ actual `/core_banking/api` (`application.properties:101`, `JerseyConfig.java:36`). |
| D2 | Config | CRMS | `CRMS.API/appsettings.json` FineractDirect UseMock=true vs intranet false: on TEST the API host's NAMP webhook/loan-account/health calls hit the mock. |

### Status codes CRMS sees from tpbackend
400 validation (`PlatformApiDataValidationExceptionMapper.java:49`), 401 unauthenticated / tpgcore permission rejection (`UnAuthenticatedUserExceptionMapper.java:43`,
`TPGatewayCoreBankingFilter.java` setErrorResponse), 403 domain rule + data integrity (`PlatformDomainRuleExceptionMapper.java:50`, `PlatformDataIntegrityExceptionMapper.java:52`),
404 not found (`PlatformResourceNotFoundExceptionMapper.java:48`), 200 for maker-checker-queued commands. CRMS treats all non-2xx as failure (correct), 404 as empty only
in `FindClientsAsync` and the externalId probe (correct), and **all 2xx as success (wrong under maker-checker, M3)**.
