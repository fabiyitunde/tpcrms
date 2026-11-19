# 04 — TransactionPoint custom layer in tpbackend (BOA core banking)

Read-only study of `C:\absdev\tpbackend` (branch `master`, HEAD `531c48b9c2`, 2026-11-12). Nothing was built or run,
and no live service was called. No credentials are reproduced here.

Path abbreviations:
- `TP/` = `fineract-provider/src/main/java/net/aggregatebusiness/transactionpoint/`
- `OAF/` = `fineract-provider/src/main/java/org/apache/fineract/`
- `CL/` = `fineract-provider/src/main/resources/db/changelog/tp-v2/`

A raw dump of every extracted route (with line numbers) is next to this file in `routes.txt`.

---

## 0. Shape of the system

- This is an Apache Fineract fork. The custom code is 1,142 Java files under `TP/`. Besides that, 22 files in `OAF/` were
  edited to call into it (listed in §1.13). `custom/foo` and `module/dummy` hold only the stock Fineract
  extension-module samples (`FooDummyServiceImpl`, `DummyService`). They contain no bank code (`settings.gradle:20-23`).
- **URL layout.** The servlet context is `/core_banking` (`application.properties:101`) and Jersey is mounted at `/api`
  (`OAF/infrastructure/core/config/JerseyConfig.java:36`). Fineract resources use their bare paths, e.g.
  `/core_banking/api/loans` (`LoansApiResource.java:151` `@Path("/loans")`), so there is **no `/v1`**. TP resources sit
  under `/core_banking/api/tp/...`.
- **Gateway aliases.** TPGateway reaches TP routes through `/core_banking/api/tpgcore/...`, which a per-client URL
  allow-list filter checks and then forwards (§4). Sub-ledger routes go through `/core_banking/api/tpgapi/tp/...`.
- **TPM Server.** This is an external .NET "payment server". It holds virtual accounts (sub-ledgers), EasyPay/NIP
  interbank transfers, name enquiry and the ChargeMaster. All of its URLs derive from the `TPM_SERVER_URL` secret
  (`TP/infrastructure/configuration/TPConfig.java:61-73`, e.g. `/api/Subledger`, `/api/EasyPayAPI/FundsTransfer`).
- **Per-bank configuration.** Each bank's settings come from AWS Secrets Manager (`TPConfig.java:118,132`), with keys
  enumerated at `TPConfig.java:160-200`. Runtime switches are Fineract `c_configuration` rows (global configuration).
- **Jobs.**
  - Most custom jobs are Quartz jobs that register their own triggers. They are not entries in Fineract's `job` table.
  - Three jobs are surfaced as Fineract scheduler jobs through thin adapters in `OAF/`, because `@CronTarget` scanning
    only covers `org.apache.fineract.*`. They are "Internal Fraud Detection", "Generate Credit Bureau Snapshot" and
    "Publish RD Instalment Missed Events" (`OAF/infrastructure/jobs/service/JobName.java:22-49`).

---

## 1. Module map

Each entry gives the purpose, the routes (verb, path, source line), the main tables and the jobs. Routes are relative
to `/core_banking/api`.

### 1.1 accounting
- **chequeclearing** (`/tp/chequeclearing`, `TP/accounting/chequeclearing/api/ChequeClearingApiResource.java`)
  - Purpose: cheque lodgement with a hold, then a background release.
  - Routes: GET `/` aggregate (69); POST `/` new cheque (78); GET `/list` (92); GET `/{id}` (102);
    POST `/cancel/validate` (112); POST `/cancel` (124).
  - Job: `ChequeClearingBackgroundJob`, every 60 s (`job/ChequeClearingBackgroundJob.java:105`).
- **journalentry** (`/tp/journalentries`, `TP/accounting/journalentry/api/TPJournalEntriesApiResource.java`)
  - Route: POST `/acc_mapping` (71–88). This creates a balanced GL journal in which one leg is the GL account mapped to
    *the savings product of the account whose NUBAN is `externalId`*, for the given `paymentTypeId`. The other leg is the
    caller's `glAccountId`.
  - How it works: it resolves the mapping from the `MProductToGLAccountMapping` table, builds a standard
    `createJournalEntry` command and logs it through the command pipeline
    (`service/TPJournalEntryWritePlatformServiceJpaRepositoryImpl.java:68-143`).
  - Limits:
    - The date is always today and the currency is hard-coded NGN (`:91,155-157`).
    - The savings account balance is **not** moved. Only GL rows are written.
    - An unknown `externalId` causes an NPE (`:76-78`).
- **producttoaccountmapping**: the domain entity used above (`MProductToGLAccountMapping(Repository).java`). It has no
  routes.

### 1.2 batch
- `/tp/batches` POST `/` (`TP/batch/api/TPBatchApiResource.java:90`). This is a TP variant of Fineract's batch API,
  with TP command strategies under `batch/command/`.
- The internal findings note that `enclosingTransaction=true` is not truly all-or-nothing, because each strategy
  catches its own exceptions.

### 1.3 commands (maker-checker extensions)
- `/tp/makercheckers` (`TP/commands/api/TPMakercheckersApiResource.java`)
  - GET `/` with teller context (140); GET `/{auditId}` (181); GET `/{auditId}/validate` (202).
  - POST `/basic/{auditId}` approve (247); POST `/{auditId}` approve or reject with reason (310);
    DELETE `/{auditId}` (455).
- `/tp/makercheckers/restrictions`: CRUD for per-user and per-action amount ceilings
  (`TPMakerCheckersRestrictionApiResource.java:61-110`).
- `/tp/makerchecker/notifications`: GET `/`, `/all`, `/count`; POST `/{id}/read`, `/{id}/dismiss`, `/read-all`
  (`TPMakerCheckerNotificationsApiResource.java:69-143`).
- `/tp/roleapprovallimits`: CRUD for role approval bands (`TP/commands/approvallimit/api/RoleApprovalLimitsApiResource.java:66-182`).
  - Bands are per `entity_name` and action, and **include `LOAN` APPROVE / DISBURSE / DISBURSETOSAVINGS / REPAYMENT**
    (`docs/ROLE_BASED_APPROVAL_LIMITS.md:433-480`).
  - They are gated by the global configuration `enable-role-approval-limits`.
- `MakerIdentityJsonInjector` (`TP/commands/approvallimit/util/MakerIdentityJsonInjector.java`) adds `originalMakerId`
  to the command JSON. The approved transaction and its journals are then attributed to the maker, not the checker.
  - The edited Fineract loan resources call it: `OAF/portfolio/loanaccount/api/LoanTransactionsApiResource.java:247`
    and `LoansApiResource.java:24`.

### 1.4 infrastructure
- **bulkimport** (`/tp/bulkimport`, `TP/infrastructure/bulkimport/api/TPBulkImportApiResource.java:63-147`)
  - Routes: template, upload, list, details, download, item logs, approve, delete.
  - Job: `TPBulkImportProcessImportsJob`, every 1 min.
- **cbnreports** (`/tp/cbn-report`, `CBNReportApiResource.java:57-99`)
  - Purpose: maps GLs to CBN return lines, then generates and exports the return (it uses a trial-balance stored
    procedure).
- **documentmanagement** (`tp/{entityType}/{entityId}/documents`, `TPDocumentManagementApiResource.java:103-232`)
  - Routes: list, create, set verified, update, get, delete.
- **security** (`/tp/authentication`, `TPAuthenticationApiResource.java:109,200`): POST `/` login and
  GET `/authtoken/{token}` (MFA verification). See §4.
- **hooks**: Fineract hooks get two extra templates, "SNS Topic" and "SQS" (`TP/infrastructure/hooks/api/HookApiConstants.java`).
  - The processor is `processor/SNSTopicProcessor.java:90-149`. It publishes to the configured Topic ARN.
  - Credentials have been optional since `cf712770fa` (2026-11-09, changelog `CL/v15/0017_sns_hook_optional_credentials.xml`).
- **queue**: SQS dispatch of interbank transfers (`TP/infrastructure/queue/*`, `application.properties:30-59`).
- **configuration**: `TPConfig` (Secrets Manager), `DynamoDBConfig` (the sub-ledger access log table
  `SubLedgerAccessLogDb`) and `TPFeatureSwitches` (`TPFeatureSwitches.java:33-35`).
- **jobs**: `PurgeJobRunHistoryJob` (cron).

### 1.5 organisation
- **broadcast**: `/tp/broadcast` POST and GET (`BroadcastApiResource.java:61,79`). Job: `ProcessBroadcastJob`, every 5 min.
- **teller**: teller extensions for inter-branch work. It has no routes of its own.

### 1.6 portfolio: client
- **TP clients** (`TP/portfolio/client/api/TPClientsApiResource.java`)
  - POST `/tp/clients` (90); GET `/{clientId}` (111); GET `/bysavingsId/{savingsAccountId}` (140).
  - POST `/activatesubledger/{clientId}` (169); POST `/kyc/{clientId}` (184); PUT `/{clientId}` (196).
  - GET and PUT `/{id}/indemnity` (214, 227); PUT `/{id}/notifications` (247).
  - **GET `/lookup?q=&legalForm=&limit=`** (270). A typeahead that matches account_no, mobile, **BVN (`external_id`)**,
    NIN, id or name prefix, scoped to the caller's office hierarchy (`service/ClientLookupService.java:55-79`). BVN and
    NIN are masked in the result.
  - **POST `/find`** (280) and **POST `/findsimple`** (293). The body keys are `emailAddress`, `mobileNo`,
    `externalId` (= BVN), `nin` and `nuban`. The options are `?transactions=` and `?accounts=`
    (`service/TPClientReadPlatformServiceImpl.java:612-700`).
  - GET `/summary/{clientId}` (306); GET `/accounts/{clientId}` (315); GET `/fulldetails/{clientId}` (323).
- **Directors**: `/tp/directors/{clientId}` with POST, GET and PUT; DELETE `/{clientId}/{directorId}`; POST
  `/{clientId}/{directorId}` to change status (`DirectorsApiResource.java:59-121`).
  - A director is a link between an entity client and a person client (`data/DirectorData.java`: `entityAccountClient`,
    `directorClient`, `managingDirector`, `active`).
  - BVN consistency is enforced (`exception/DirectorBVNInconsistencyException.java`).
- **Signatories**: `/tp/signatories/{clientId}`, same shape (`SignatoriesApiResource.java:57-119`).
- **Shareholders**: `/tp/shareholders/{clientId}` GET and POST; PUT and DELETE `/{clientId}/{shareholderId}`
  (`ShareholdersApiResource.java:56-84`).
- **Merchants** (`/tp/merchants`, `MerchantsApiResource.java:60-157`)
  - Purpose: merchant and sub-ledger group views.
  - Routes: GET `/`; GET `{clientId}/transfers`; GET `{clientId}/transfers/{nuban}`; GET `{clientId}/subledgers` (92);
    GET and POST `{clientId}/collection-accounts` (100, 108); GET `{clientId}/collection-account/{groupCode}` (117);
    GET and POST `callback-url/{clientId}`; GET `getaccount/{nuban}`; GET `groupcodes/{clientId}`; POST `groupcodes`.
- **Data model.** BVN is stored on `m_client.external_id`, which is unique (`domain/MClient.java:139`). An entity's
  BVN is in `entity_bvn` (`:148`). NIN is in `m_client.nin`.

### 1.7 portfolio: client merge, office, office migration
- `/tp/clients/merge`: GET list, POST merge, GET `/validate` (`ClientMergeApiResource.java:78-139`).
- `/tp/clients/offices` GET (`TPClientOfficeApiResource.java:61`).
- `/tp/offices`: GET, and PUT `/{id}/visibility` (`TPOfficeApiResource.java:64,75`).
- `/tp/officemigrations`: list, get, items, events, validate, create, approve, cancel, retry, republish
  (`TPOfficeMigrationApiResource.java:65-144`).
  - It publishes events to SNS (`service/OfficeMigrationSnsPublisher.java`).
  - Job: `ProcessOfficeMigrationJob`, every 1 min.

### 1.8 portfolio: savings
- **TP savings accounts** (`/tp/savingsaccounts`, `TPSavingsAccountsApiResource.java`)
  - GET `/{accountId}` (120).
  - **GET `/byexternalId/{externalId}`** (155). `externalId` is the **NUBAN**. The CRMS already calls this.
  - GET `/byclientexternalId/{externalId}` (205), which looks up by client BVN.
  - GET `/generatenuban` (246); GET `/transactions/{externalId}` (297).
  - POST `/account`, which creates and activates (315).
  - POST `/transactionlimit` (331).
  - **POST `/{savingsId}/arrearsrecoveryexemption`** (353), which exempts an account from loan-arrears sweeping.
  - POST `/existingaccount` (367); POST `/calculatecharges` (383); GET `/fullclientdetails/{externalId}` (392).
- `/tp/savingsaccounts/{savingsId}/transactions`
  - POST `?command=deposit|withdrawal|gsimDeposit|postInterestAsOn|holdAmount...` (234). These are the TP deposit and
    withdrawal commands, which take maker-checker and approval limits.
  - GET `/{txnId}` (286); GET `/{txnId}/receipt` as a PDF (306); GET `/statement` (364).
- `/tp/savingsaccounts/{nuban}/customtransactions` POST `?command=` (`TPSavingsAccountCustomTransactionsApiResource.java:80`).
  It posts by NUBAN, and the resource has no permission check.
- **`/tp/transactions/since?afterId=&limit=`** GET (`TPTransactionsApiResource.java:60-75`). It returns savings
  transactions in ascending id order, one page at a time. This is useful as a delta feed for reconciliation.
- `/tp/savingsproducts` CRUD (104-149); `/tp/fixeddepositaccounts` GET (96).
- `/tp/savingschequebook`: chequebook issue, validate, block and unblock (95-195).
- **Deposit and interest SNS events** (`savings/depositevents/*`, `TPSavingsInterestPostedEventPublisher`). These need
  an active hook for the entity and action. Some events are master-only; see §3.
- **Jobs**: `PurgeSavingsPostingLogJob` (cron) and `TPSavingsInterestBatchJobService`.
- **Fast posting.** "Fast posting" is a savings performance path behind `tp-fast-savings-posting`
  (`docs/fast-savings-posting.md`). Loan transfers (disbursement to savings, repayment from savings) stay on the full
  path (`fast-savings-posting.md:221,372`).

### 1.9 portfolio: transfers, interbranch and account transfers
- **`/tp/transfer`** (`TP/portfolio/transfer/api/InterbankTransferApiResource.java`)
  - Lists and logs: GET `/` (90); GET `/inward` (96); GET `/log/{logType}` (103).
  - Interbank (NIP via TPM EasyPay): POST `/` (111); POST `/mc` through maker-checker (121); POST `/authrequest` (150).
  - Sub-ledger transfers: POST `/subledger` (159), `/subledger/auth` (170), `/subledger/sync` (181).
  - Details: GET `/{transferId}` (192); GET `transactiondetails/{id}` (200); POST `/{transferId}` submit (209).
  - Enquiry: POST `/enquiry` (231).
  - **POST `/intrabank`** (241), savings to savings.
  - **POST `/intrabank/subledger`** (251), core account to virtual account.
  - **GET `/intrabank/status/{transactionReference}?sourceAccountNUBAN=`** (261), the idempotent status read.
  - Receipts and history: GET `/receipt/{id}` (269); GET `/history` (283).
  - Reverse and finalize: POST `/reverse` (297); POST `/finalize` (307).
  - Lookups: GET `transferdetails/{paymentReference}` (317); GET `tpgatewaytransferdetails/{ref}` (324);
    GET `tpgatewaytransactiondetails/{ref}` (332); GET `getallinwardtransfers` (340).
- `/tp/transfer/bulk/authrequest` (`BulkInterbankTransferApiResource.java:50`).
- `/tp/bulktransfer`: GET list and detail; POST `/intrabank/{clientId}` and `/intrabank` (`BulkTransferApiResource.java:64-98`).
- `/tp/interbank-dashboard` GET and `/export` (45, 56); `/tp/transferconfig` GET and POST (70, 80).
- `/tp/nameinquiry`: GET `/` lists institutions; POST `/` looks up an external account name (55, 63).
- **Idempotency.** The idempotency table is `tp_intrabank_transfer_reference`, with a unique key on
  (source savings id, reference) (`CL/v11/0011_intrabank_transfer_reference.xml`).
  - Enforcement is off by default and controlled by two configuration rows: `intrabank-transfer-idempotency-enforced`
    and `virtual-account-transfer-idempotency-enforced` (`docs/intrabank-transfer-idempotency.md`).
- **Jobs.**
  - `ProcessTransferBackgroundJob` (5 s), `ProcessBulkTransfersJob` (5 s) and `FinalizeBulkTransferBackgroundJob` (15 s).
  - `InstitutionListJob` (24 h) and `TransferReconciliationJob`.
  - There is also an SQS consumer (`queue/TransferDispatchConsumer.java:175`).
- **`/tp/accounttransfers`**: GET `/template`, POST `/`, GET `/`, GET `/{id}` (`TP/portfolio/account/api/TPAccountTransfersApiResource.java:82-149`).
  - Transfers are **savings to savings only** and inter-branch aware (`service/TPAccountTransferPostingService.java:83`).
    Savings-to-loan goes through Fineract's own `/accounttransfers`, whose edited files are
    `OAF/portfolio/account/service/AccountTransfersWritePlatformServiceImpl.java` and `AccountTransfersDataValidator.java`.
- **`/tp/interbranch`** and **`/tp/cashiers`**
  - Routes: positions, pending transactions, settle (`InterBranchApiResource.java:88-171`); cashier balance and
    reconcile (`CashierBalanceApiResource.java:74,97`).
  - There is a nightly Spring `@Scheduled` cashier reconciliation at 02:00 (`service/CashierBalanceReconciliationServiceImpl.java:71`).

### 1.10 portfolio: sub-ledgers (virtual accounts) and sub-ledger overdraft
- **`/tp/subledger`** (`TP/portfolio/subledger/api/SubLedgerApiResource.java`)
  - Group codes: GET `/groupcodes/{clientId}` (69); POST `/groupcodes` (83).
  - POST `/collectionaccount` (99), which sets the collection account.
  - Create and read: POST `/` (115); GET `/{id}` (130); GET `/list/{externalId}` (141).
  - Classifications: GET `/classifications/{externalId}` (154); POST `/classification` (164).
  - **Postings**: POST `/credit` (174), `/debit` (184), `/credit/transactional` (194), `/debit/transactional` (204).
  - Maintenance: POST `/rename` (214), `/block` (224), `/unblock` (235).
  - **Invoices**: POST `/invoice` (246); GET `/invoice/{ref}` (256); GET `/invoicelist/{nuban}` (266);
    GET `/invoicelist/byexternalid/{externalId}` (276).
  - History and bulk: GET `transactions/{id}` (289); POST `/bulk/create` (300).
  - POST `/merchanttransfer` (316); GET `transactiondata/{ref}` (326).
  - The sub-ledgers live in TPM (`service/SubLedgerServiceImpl.java:212,365,419`). Local mirror tables are
    `SubLedgerDb`, `SubLedgerTransactionDb` and `SubLedgerInvoice`.
  - Access is gated by `SubLedgerSecurityFilter`, which needs the `clientID` and `nuban` headers (§4).
  - Jobs: `ExpandBulkSubLedgerCreateJobs` and `SubmitBulkSubLedgerCreateJob` (15 s), `FinalizeBulkSubLedgerCreateJob`
    (10 s) and `SubLedgerClassificationsJob` (60 s).
- **`/tp/subledgeroverdraft`**: settings, accounts, limit, transactions, draw, settle, settle-by-settlement-sub-ledger
  (`SubLedgerOverdraftApiResource.java:71-177`). Job: `SubLedgerOverdraftEnsureJob`, every 60 s.

### 1.11 portfolio: charges, charge master, payment types, VAS
- `/tp/charges`: charge types, configuration, batches with preview and action, details (`TPChargesApiResource.java:68-166`).
  - Jobs: activation, maintenance, KYC, SMS and partial-settlement charge processors, running every 2 min to 24 h,
    some on cron (`portfolio/charges/job/*`).
- **`/tp/chargemaster`** relays TPM ChargeMaster (`TPChargeMasterApiResource.java:70-185`).
  - Routes: GET `/products`, `/products/{code}`, `/accounts`, `/accounts/{code}`; POST `/accounts`,
    `/accounts/activate/{code}`; DELETE `/accounts/{code}`; GET `/splits/{masterCode}`;
    POST `/splits/customer-account`, `/splits/gl`; DELETE `/splits/{id}`; GET `/history`;
    GET `/settlements/accrued`; POST `/settlements`.
  - Account charges are created inactive and activated separately. They are percentage or fixed, by amount range, per
    account, payment type and transaction type, with customer-account and GL splits.
  - Permissions are `*_TPCHARGEMASTER` (`docs/tp-charge-master-relay.md`). The caller chain is AgencyBanking, then
    TPGateway `/core/charges/**`, then `tpgcore/tp/chargemaster/**`.
- `/tp/paymenttypes` CRUD (`TPPaymentTypeApiResource.java:82-126`).
- `/tp/vas`: airtime, electricity, data, TV, status, refund, charges, settings, settlement (`VASApiResource.java:76-274`).
  Jobs run VAS processing, reconciliation, the activity report and settlement (02:00).

### 1.12 portfolio: KYC, KYB, fraud, search, loan, arrears recovery
- **`/tp/kyc`** (`KYCApiResource.java:71-365`)
  - Routes: KYC lookup (POST `/`, `/byclient`); `/verify`; logs; KYC level; BVN request limit; providers; request
    types; custom limits; balances; `/autoverify`; settings; `/liveness/session`; `/facecomparison` (AWS Rekognition).
  - Providers are SmileID, Monnify, UVerify and a TransactionPoint provider (`kyc/service/provider/*`).
  - Jobs: `KYCActivationJob` and `KYCRevalidationJob` (60 min), `AutoKYCRepairJob` (10 min) and `KYCBillingJob`.
- **`/tp/kyb`** handles CBN entity know-your-business (`KybApiResource.java:110-568`).
  - Routes: settings, document requirements, applications (internal and `/external`), invoice, CAC lookup and accept
    (`/{id}/cac`, a billed call that is cached by `service/KybCacService.java:67`), parties (directors, shareholders,
    passport), party search, documents, KYC lookup of parties, review, validate, results,
    `/clients/{clientId}/verification`, and fee configuration, runs and retry.
  - Jobs: `KybFeeDistributionJob` (cron `0 5 * * * ?`), plus an SQS invoice-notification consumer.
- **`/tp/frauddetection`** (`FraudDetectionApiResource.java:104-309`)
  - Routes: settings, cases with actions, NIBSS report, `gateway-decision`, `client-report`, team, rules
    (run and preview).
  - The sources are Smartcomply "Adhere" real-time screening of transactions, client reports, and internal rules
    (`docs/FRAUD_DETECTION_STATUS.md`).
  - Jobs: "Internal Fraud Detection" (Fineract job, `OAF/portfolio/frauddetection/jobs/InternalFraudDetectionJob.java:54`)
    and `FraudEscalationReminderJob` (5 min).
- `/tp/search`: template, columns, search, `/advance` (85-148). The findings note that the `loanStatus` filter is broken.
- **`portfolio/loanaccount`** has only `service/LoansTransactionHookMessenger.java`.
  - On `LOAN_DISBURSAL` it builds journal-entry JSON for today's loan transactions. The actual `publishEvent` call is
    **commented out** (`:139, 145-228`), so nothing is sent. This is effectively dead code.
- **`/tp/loanarrearsrecoveries`**: see §2.4.
  - GET `/?savingsAccountId=|loanId=` (78).
  - POST `/{recoveryId}?command=reverse` (100). It reverses both legs.

### 1.13 regulatory, reporting, template, user administration
- **`/tp/bureau`** (`TP/regulatory/bureau/api/CreditBureauConfigApiResource.java:62-163`)
  - Routes: mappings (GET, PUT, DELETE), classification bands, classification overrides, previous references.
- **`/tp/bureau/bulk/{type}`**: GET `/template` and POST upload (`CreditBureauBulkUploadApiResource.java:61,70`).
  The types are `individuals`, `businesses`, `loans`, `previousreferences` and `overdraftfacilities`
  (`service/CreditBureauBulkUploadService.java:65-69`).
- **`/tp/overdraftfacilities`**: GET and PUT `/settings`; GET and POST `/{savingsAccountId}`
  (`OverdraftFacilityApiResource.java:75-122`).
  - Actions are GRANT, RENEW, CHANGE, CANCEL and WRITE_OFF, with optional posting to the books
    (`service/OverdraftFacilityService.java:45-77`, `OverdraftWriteOffPoster`).
- **`/tp/reports`** and **`/tp/reports/merchants`**: POST create, GET list, types, parameters, details
  (`TPReportsApiResource.java:61-104`). Job: `ProcessReportsJob`. The bureau return is produced as one of these
  report types (§2.5).
- `/tp/templates/byname/{name}` GET (92). `/tp/users`: POST, and DELETE `/{id}` to disable (67, 105).
- **Edited Fineract files that reference TP code** (22 in total). Among them:
  - `SynchronousCommandProcessingService`, `CashBasedAccountingProcessorForSavings`, `HikariCpConfig`, `JPAConfig`.
  - The hook processor provider, the teller services and `OfficeRepositoryWrapper`.
  - The account-transfer API, validator and service.
  - The loan and loan-transaction APIs.
  - The deposit and savings write services, and the savings scheduler.

---

## 2. What the CRMS might use

### 2.1 Customer and client lookup
| Need | Route | Notes |
|---|---|---|
| By BVN | `POST /tp/clients/find` `{externalId: <BVN>}`; `GET /tp/clients/lookup?q=<BVN>`; `GET /tp/savingsaccounts/byclientexternalId/{BVN}` | BVN is `m_client.external_id` (`MClient.java:139`); entity BVN is in `entity_bvn` (`:148`) |
| By NIN | `POST /tp/clients/find` `{nin}` → `findByNinOrEntityNIN` (`TPClientReadPlatformServiceImpl.java:669-670`); `lookup?q=` | |
| By NUBAN | `POST /tp/clients/find` `{nuban}` (`:678-679`, savings `external_id` = NUBAN); `GET /tp/savingsaccounts/byexternalId/{nuban}`; `GET /tp/merchants/getaccount/{nuban}` | The CRMS uses the first two today |
| Full profile | `GET /tp/clients/fulldetails/{id}`, `/summary/{id}`, `/accounts/{id}` | `/summary` and `/accounts` check no permission and no office (findings) |
| Directors / signatories / shareholders | `GET /tp/directors/{clientId}`, `/tp/signatories/{clientId}`, `/tp/shareholders/{clientId}` | Directors and signatories are linked person clients, so their BVN is the linked client's `external_id` |
| KYB (CAC) | `/tp/kyb/applications/{id}/cac`, `/parties`, `/clients/{clientId}/verification` | The CAC lookup is billed and cached once (`KybCacService.java:67`) |
| KYC | `/tp/kyc` (BVN and NIN through SmileID, Monnify, UVerify), `/facecomparison` | Each client has a BVN request quota (`MClient.java:303`, `/tp/kyc/{id}/requestlimit`) |
| Name enquiry (other banks) | `POST /tp/nameinquiry` | Goes through TPM EasyPay |

### 2.2 Loan booking
- **There is no TP loan-booking shortcut.** Booking, approval, disbursement, schedules and repayments all use stock
  Fineract `/loans` and `/loans/{id}/transactions`. These are what the CRMS already calls: `loans`,
  `loans?command=calculateLoanSchedule`, `loans/{id}?command=approve|disburse...`,
  `loans/{id}?associations=repaymentSchedule` and `loanproducts`.
- TP adds two things on top:
  1. **`originalMakerId` injection** on loan approve, disburse, disburse-to-savings, repayment and charge
     (`LoanTransactionsApiResource.java:247`).
  2. **Role approval limits for LOAN** actions, when `enable-role-approval-limits` is on. A CRMS service user that
     posts loan commands may therefore land in the maker-checker queue (`/tp/makercheckers`) instead of executing
     directly. This depends on the bank's `m_permission.can_maker_checker` settings and the configured bands.

### 2.3 Loan repayment posting, GL posting and reconciliation
- **Repayment**: use Fineract `POST /loans/{id}/transactions?command=repayment`. Fineract's
  `/accounttransfers` with `transferType=LOAN_REPAYMENT` gives savings-to-loan. That is exactly what arrears
  recovery uses internally (`TP/portfolio/loanarrearsrecovery/service/LoanArrearsRecoveryTransactionServiceImpl.java:104-109`).
- **Fraud and screening.** TP's Smartcomply screening is attached to transfers, not to loan repayments.
- **GL posting**
  - Fineract `POST /journalentries` handles free-form entries.
  - `POST /tp/journalentries/acc_mapping` takes a savings NUBAN plus a payment type and writes GL-only entries, dated
    today, in NGN.
  - Neither moves a customer balance.
- **Reconciliation feeds**
  - `GET /tp/transactions/since?afterId=&limit=` is a savings-transaction delta cursor (`TPTransactionsApiResource.java:60-75`).
  - `GET /tp/transfer/intrabank/status/{reference}` is the idempotent status of a transfer.
  - Hooks (SNS or SQS) can be configured on any Fineract entity and action, including LOAN DISBURSE and REPAYMENT,
    through standard Fineract `/hooks` with the "SNS Topic" template (`SNSTopicProcessor.java:90-149`).
  - **There is no TP-specific loan event publisher.** `LoansTransactionHookMessenger` does not publish anything.

### 2.4 Arrears recovery: sweeping savings to clear loan arrears
This is on master and on BOA. The note fix `63171b8a85` from `fix/arrears-recovery-note` is merged into master.
- **Trigger.** A listener runs after the commit of every `SAVINGS_DEPOSIT`
  (`LoanArrearsRecoveryDomainServiceImpl.java:46-118`). It skips these deposits:
  - account transfers, including a loan disbursed into savings and overpayment refunds;
  - fixed and recurring deposit accounts, and group or GSIM accounts;
  - reversed deposits.
- **Eligibility** (`LoanArrearsRecoveryExecutorImpl.java:61-131`).
  - The account must be active, allow withdrawals, and carry neither a block nor a debit block.
  - It must not be exempt. The exemption flag is on `m_savings_account` and `m_savings_product`
    (`CL/v5/0005_arrears_recovery_exemption.xml`) and is set with `POST /tp/savingsaccounts/{id}/arrearsrecoveryexemption`.
- **Rule.** All of the client's loans in the same currency that are overdue beyond the grace days are each settled in
  their own transaction, as a savings-to-loan transfer of type `LOAN_REPAYMENT`, until the funds run out (`:133-155`).
- **Configuration.**
  - `c_configuration` `loan-arrears-recovery-from-savings`: value = grace days (default 1), seeded **disabled**
    (`CL/v4/0004_loan_arrears_recovery.xml:89-99`).
  - Switch `loan-arrears-recovery-excludes-overdraft-limit` (`TPFeatureSwitches.java:33`): when on, recovery takes
    only the customer's own funds.
- **Audit and narration.** Every attempt is recorded in `tp_loan_arrears_recovery` (`LoanArrearsRecovery.java:41-79`)
  with the trigger transaction, loan, `account_transfer_details_id`, amount, status, failure reason and date. A
  narration goes on the recovery withdrawal (`TransactionServiceImpl.java:156-165`).
- **Reversal.** `POST /tp/loanarrearsrecoveries/{id}?command=reverse` unwinds both legs.
- **CRMS impact.** Repayments can appear on a loan with no CRMS action. They are visible as Fineract loan transactions
  and in `GET /tp/loanarrearsrecoveries?loanId=`.

### 2.5 Credit bureau returns
`feature/bureau-returns` is fully merged into master (0 commits ahead). Its top commits are `5920512736` "Add credit
bureau Uniform Data Template return" and `22ad44a138` "Fix per-loan last payment lookup and stale dates after undo
write-off", followed by charge-master, fast-posting and tenant commits. The bureau code is present on
`origin/bankofagriculture`.
- **Snapshot.** `CreditBureauSnapshotService` freezes each reportable loan's servicing figures into
  `tp_bureau_loan_snapshot` (`:35-90`).
  - Reportable statuses are 300, 303, 304, 600, 601, 602 and 700.
  - Figures are reconstructed back to the as-at date.
  - The Fineract job "Generate Credit Bureau Snapshot" runs on cron `0 30 0 1 * ?`, the first of the month
    (`OAF/portfolio/bureaureturns/jobs/CreditBureauSnapshotJob.java:51-54`, `CL/v9/0009_credit_bureau_returns.xml:304-306`).
- **Return.** `CreditBureauReturnReadService` builds the CBN/bureau "Uniform Data Template" with a borrower sheet and
  a credit-information sheet (`:38-44`).
  - It reads `m_client`, addresses, identifiers, family members, collateral (both models), overdraft facilities and
    previous references.
  - It maps values through `tp_bureau_mapping`.
- **Delivery.** The return runs as a `/tp/reports` report type with parameters "AS AT DATE" and "BORROWER TYPE"
  (`CreditBureauReturnReportProcessor.java:51-52`). It is uploaded privately with an unguessable link
  (`CreditBureauReturnUploadPipe.java:49`).
- **Bureau fields captured in core banking**, which the CRMS could fill at booking:
  - The loan datatable "Credit Bureau Information", created as `dt_bureau_loan_info` on `m_loan`. Its columns are
    legal-challenge status, litigation date and consent status (`CL/v9/...:183-198`, `CreditBureauReturnReadService.java:63`).
  - The client datatables "Credit Bureau Individual Details" (PERSON: title, nationality, marital status...) and
    "Credit Bureau Business Details" (ENTITY: business category, tax id) (`CL/v9/...:3810-3884`, `:64-65`).
  - Classification bands per product and day range (`tp_bureau_classification_band`) and per-facility overrides.
- **Bulk back-fill** goes through `/tp/bureau/bulk/{type}`.
- **Write path for the CRMS.** These are Fineract datatables, so the write route is standard
  `/datatables/{name}/{loanId}`. Maker-checker can apply (a `CREATE_dt_bureau_loan_info_CHECKER` permission exists).

### 2.6 Charges
- Fineract loan and savings charges are unchanged.
- TP adds batch charge processing (`/tp/charges`) and the TPM **ChargeMaster** relay (§1.11). ChargeMaster handles
  transaction-triggered account charges with splits and settlement, aimed at agency banking. It has nothing
  loan-specific.

### 2.7 Virtual and collection accounts
- Sub-ledgers in TPM are the virtual accounts (§1.10).
  - A client must have the sub-ledger system activated (`POST /tp/clients/activatesubledger/{id}`). This returns a
    client secret in clear, per the findings.
  - Each client has group codes and a collection account (`/tp/subledger/collectionaccount`,
    `/tp/merchants/{clientId}/collection-accounts`).
  - Sub-ledgers support invoices and callback URLs.
  - `POST /tp/transfer/intrabank/subledger` moves money from a core account to a virtual account.
- This is a possible fit for repayment collection accounts per borrower. The money still has to be moved from the
  collection account into the loan through Fineract.

### 2.8 Transfers
- Intrabank transfers use `/tp/transfer/intrabank`. Send a `transactionReference` for idempotency; enforcement is off
  by default. Interbank transfers use `/tp/transfer/authrequest` or `/mc` through TPM EasyPay/NIP, with SQS dispatch.
- Bulk transfers are available. Disbursement to a borrower's account at another bank would be an interbank transfer
  after the loan is disbursed to savings.

### 2.9 Fraud detection
- Smartcomply "Adhere" screens transactions in real time (`/tp/frauddetection/gateway-decision`). Internal rules run
  as a job.
- `fraud_detection` branch: 2 commits not in master (`0cabf04e60` "Fraud Detection Implementation" and a merge).
  Master already has the `frauddetection` package.
- Not relevant to loan posting except that interbank disbursements may be screened.

---

## 3. Multi-tenancy, per-client branches and what is BOA-specific

- **One deployment per bank.** The tenant id is fixed by `fineract.tenant.identifier`, from the environment variable
  `FINERACT_DEFAULT_TENANTDB_IDENTIFIER` (`application.properties:71`). BOA uses `bankofagriculture`, which is also
  the test fixture value (`OfficeMigrationSnsPublisherTest.java:75`).
- **Tenant selection.** Each request carries header `Fineract-Platform-TenantId` or query `tenantIdentifier`
  (`OAF/infrastructure/security/filter/TenantAwareBasicAuthenticationFilter.java:83,105-113`).
- **Tenant use in code.** The TP code uses the tenant for SQS queue prefixes (e.g. `bankofagriculture_tp-interbank-transfer-processing`,
  `docs/interbank-transfer-sqs-dispatch.md:189`) and for Secrets Manager lookups (`TPConfig.java:79,118-132`).
  - An `alternate-tenant` global configuration overrides the tenant name used for interbank gateway calls
    (commit `124969b66c`).
- **Branches per bank.** `bankofagriculture` (and an older `boa`), `buypower`, `fullrange`, `madobi`, `minjibir`,
  `oneutilitymfb`, `valuepaymfb`, plus `staging`, `sandbox`, `dev` and `test`. Work lands on `fullrange` or
  `oneutilitymfb`, is merged to master, then to each bank's branch.
- **Buildspecs.** Each `buildspec_*.yml` is the same Gradle `bootJar` → Docker → ECR flow with a different AWS account
  and region. BOA is `434748569700` / eu-west-1, repository `tpbackend`. There is no per-bank code switch in them.
  - Every buildspec contains an **inline Docker Hub login with a plaintext password**, which should be rotated and
    moved to a secret. It is not repeated here.
- **BOA-specific code** is minimal.
  - The leftover default text "Bank of Agriculture" survives in `institution_details`.
  - The fraud frontend URL defaults to `https://core.bankofagriculture.com` (`FraudNotificationService.java:52`).
  - BOA's differences are data: tenant database, global configuration, `tp_tpgateway_cb_permissions` rows, hooks and
    secrets.

### What BOA (`origin/bankofagriculture`) does not have yet
`origin/bankofagriculture` is 7 commits behind master and has nothing that master lacks. Its tip is `cf712770fa`
"Allowing Hook Config" (2026-11-09). The diff is 51 files, +4,484 / −4,560 (`git diff --stat origin/bankofagriculture origin/master`).

1. **Fixed and recurring deposit and savings-interest SNS events** (`bb2a896dd3`, 2026-11-10).
   - Added: `savings/depositevents/TPDepositEvent*`, `TPDepositTypeResolver`, `TPRdInstalmentMissedService` and the
     Fineract job `RdInstalmentMissedEventsJob` (`JobName.RD_INSTALMENT_MISSED_EVENTS`).
   - Changelog `CL/v16/0018_rd_instalment_missed_events.xml`; edits to `DepositAccountDomainServiceJpa` and the
     deposit write service; docs `deposit-sns-events.md` and `deposit-events-change-summary.md`.
2. **Core-to-virtual-account transfer for invoices** (`029b56fb46`, `37ce2e13df`, `b92f84f9d0`, `88889e90b1`,
   `531c48b9c2`, 2026-11-12).
   - It funds a sub-ledger invoice through the TPM funding API: `TpmSubledgerFundingClient` and `TpmFundingResult`,
     with header `X-Subledger-Funding-Key` taken from the new `tp.tpm.subledger-funding-key` property.
   - New exceptions `InvoiceAmountMismatchException` and `InvoiceFundingPendingException`, with a mapper.
   - Idempotency extensions; about 115 changed lines in `InterbankTransferWriteServiceImpl`.
   - New tpgcore permission `INTRABANK_TRANSFER_SUBLEDGER` `/tp/transfer/intrabank/subledger` POST, plus the SQL
     scripts `docs/sql/tpgcore-intrabank-subledger-permission.sql` and `tpgcore-deposit-permissions.sql`.
3. **API docs consolidation.** The standalone `docs/api/*.openapi.yaml` files were deleted and
   `tpbackend-apidoc.json` was renamed to `tpbackend-apidog.json` (+957 lines) together with `tpbackend.openapi.json`.

**None of the 7 commits touches loans, arrears recovery, the bureau or client lookup.** Everything in §2 is already
on BOA.

---

## 4. Authentication for API callers

- **Direct Basic auth**, the default. `fineract.security.basicauth.enabled=true` and `oauth.enabled=false`
  (`application.properties:61-63`). Every call to `/api/**` needs `Authorization: Basic` plus the tenant header.
  - Fineract's standard `SecurityConfig` applies (`OAF/infrastructure/core/config/SecurityConfig.java:69-79`).
    `POST /api/tp/authentication` is open (`:73`).
  - The application-user permissions (`m_permission` codes such as `READ_TPCHARGEMASTER`) apply per role.
  - The 2FA flag is off by default.
  - **`CachingPasswordEncoder`** caches successful bcrypt matches for 5 minutes, keyed by an HMAC. The user is still
    loaded on every request, so disable and lock take effect at once (commit `ca06251865`, present on BOA).
- **OAuth**: a Keycloak JWT resource server is configured (`application.properties:122-123`, `OAuth2SecurityConfig.java:87-96`)
  but disabled.
- **TP login** (`POST /tp/authentication`)
  - It uses AWS Cognito with MFA software-token verification (`TPAuthenticationServiceImpl.java:128-191`).
  - **3 failed attempts lock the account for 5 minutes** (`:231-258`), and an IP tracker log is kept (`:300`).
  - This is for UIs. Service integrations use Basic auth.
- **Gateway path (`/api/tpgcore/*`)**: `TPGatewayCoreBankingFilter` (`TP/infrastructure/security/filter/TPGatewayCoreBankingFilter.java`,
  registered at `TPGatewayCoreBankingConfig.java`).
  - It requires a `clientID` header and matches method and path against `tp_tpgateway_cb_permissions`
    (`domain/TPGatewayCoreBankingPermission.java:38-50`).
  - A `*` wildcard matches a whole trailing segment, but a mid-path `*` matches `\w+` only, so hyphens fail.
  - On success it forwards to the real path, where Basic auth and the tenant header still apply.
  - Observation: on failure it writes a 401 body and then still calls `filterChain.doFilter` (`:139-146`). Not verified
    further.
- **Sub-ledger path** (`/api/tpgapi/tp/subledger/*`, `/tpgapi/tp/transfer/subledger/{auth,sync}`): `SubLedgerSecurityFilter`
  needs the `clientID` and `nuban` headers (`SubLedgerSecurityFilter.java:82-114`). It logs access to DynamoDB.
- **Rate limits.** There are **none** in tpbackend; a search found no rate-limit or throttle code. The only quotas are
  business ones: the BVN/KYC request limit per client and KYC tier transaction limits. Any rate limiting would sit in
  TPGateway or the load balancer, which were not studied here.
- **Many TP routes have no permission check.** They rely on authentication or the gateway filter only. Examples are
  `/tp/clients/summary`, `/tp/clients/accounts`, `/tp/merchants/*`, most `/tp/subledger` GETs, `/tp/subledger/merchanttransfer`,
  `customtransactions` and the CBN report routes (internal findings, §5). The CRMS should still use a least-privilege
  service user.

---

## 5. Design decisions from `docs/*.md` relevant to loans, repayments and postings
(Summaries only. `docs/api/tpbackend-findings.md` is internal and is only paraphrased here.)

- **`ROLE_BASED_APPROVAL_LIMITS.md` / `role-approval-limits-design.md`**
  - Approval bands per role, entity and action, with currency and minimum/maximum amounts.
  - Bands for LOAN APPROVE, DISBURSE, DISBURSETOSAVINGS and REPAYMENT, and for GLIM.
  - Amounts above every band are visible only to super admins.
  - The original maker is kept on `m_loan_transaction.appuser_id` and `acc_gl_journal_entry.createdby_id` after
    approval.
  - LOAN APPROVE is a status change only.
  - This is the main way loan commands from the CRMS could be held for a checker.
- **`intrabank-transfer-idempotency.md`**
  - Background: an incident on 2026-10-29 posted a transfer twice after a gateway timeout and retries.
  - Fix: an `INSERT IGNORE` claim keyed on (source account, reference), committed before posting, with attempt
    fencing.
  - Enforcement is per-tenant and off by default.
  - Status endpoint `GET /tp/transfer/intrabank/status/{reference}`.
  - Virtual-account credits can end as `UNKNOWN` or `CREDIT_FAILED`, with no automatic reversal.
  - Lesson for the CRMS: always send a stable reference and query status before retrying.
- **`newtransfer.md`**: interbank transfers through Fineract maker-checker. Interbank transfers create no GL entries
  until the background job debits the account.
- **`interbank-transfer-sqs-dispatch.md`**: SQS producer, consumer and reconciliation replace the 5-second sequential
  job. Queues are named per tenant.
- **`fast-savings-posting.md` (+ load test)**: an append-only fast path for zero-interest savings behind
  `tp-fast-savings-posting`, disabled by default. Loan-related transfers (disbursement to savings, repayment from
  savings, guarantor) stay on the full path.
- **`savings-interest-sns-events.md`, `deposit-sns-events.md`, `deposit-events-change-summary.md`**: events go through
  the hook pipeline (HookEvent → SNS) only if an active hook exists. Errors are swallowed. There is no equivalent for
  loans.
- **`tp-charge-master-relay.md`**: TPBackEnd is the only service that talks to TPM. Callers go through TPGateway
  `tpgcore`, with per-client permission rows.
- **`design/INTER_BRANCH_TRANSACTION_DESIGN.md`**: inter-branch accounting (positions and settlement GLs) and teller
  positions. It is relevant if a repayment is posted at a branch other than the loan's.
- **`KYB_STATUS.md`, `FRAUD_DETECTION_STATUS.md`**: status reports for the features in §1.12.
- **Internal findings, summarised by area** (about 180 items across 9 areas).
  - Many TP routes lack permission or office-hierarchy checks.
  - `acc_mapping` journals are forced to today and NGN, with no office check.
  - Bureau configuration is written without audit or maker-checker.
  - Reversing an already reversed arrears recovery reports success as a no-op.
  - The TP login leaks the request body on a missing field and trusts a client-supplied IP.
  - The batch API is not truly atomic.
