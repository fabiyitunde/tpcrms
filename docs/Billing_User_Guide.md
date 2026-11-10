# About this guide

This guide is for the Billing Managers at Freewave Consult Ltd who look after what the Bank of Agriculture is charged for using the CRMS. It covers every page under the **Billing** section of the CRMS menu: the dashboard, the price list, the platform fees, the statements, the alerts, and the access and data page.

## What billing records

The CRMS keeps a ledger of three kinds of billable event:

| Kind | What is recorded | How it is priced |
|---|---|---|
| Third-party calls | Every identity check, bureau report, fraud check and AI advisory narrative the CRMS requests on the bank's behalf. Failed calls are recorded and billed like successful ones. | Per call, from the Price List |
| Partner checks | Every customer credit check a partner submits through the bank's API. The third-party calls made to answer it are billed as well. | Per check, from the Price List |
| Loan milestones | Each application reaching a milestone: received, booked, declined, or cancelled. Recorded separately for the Corporate, NAMP and RH-SHF loan flows. | Per milestone per flow, from Platform Fees |

Ledger rows hold no money. The amount shown anywhere is always the count of events multiplied by the price in force for that month, so changing a price changes what the dashboard shows for every month that price applies to. A statement is the exception: once a month is issued, its amounts are frozen.

## Who sees what

| | Billing Manager (Freewave) | System Administrator (bank) |
|---|---|---|
| Billing Dashboard, Price List, Platform Fees, Statements | Full access | Read only |
| "Our cost" and margin figures | Shown | Never shown |
| Set prices and platform fees | Yes | No |
| Issue, mark paid, void statements | Yes | No |
| Snooze, dismiss alerts | Yes | No |
| Access & Data page | Yes | Not visible |
| Add or remove Billing Managers | Yes | No |

The bank's administrators cannot grant the Billing Manager role, and cannot edit or deactivate a Billing Manager's account from their user administration page. Only a Billing Manager can add another one, from Access & Data.

# Signing in

## The first Billing Manager

The first Billing Manager account is created by the CRMS itself when the system starts, from the email address the technical team configured. It has no usable password yet, so the first sign-in goes through the password reset flow:

1. Open the CRMS address you were given. You will see the sign-in page.
2. Click **Forgot password?** under the sign-in button.
3. Enter your email address and send. A message with a link arrives from the CRMS. Check your spam folder the first time.
4. Open the link, choose a password, and save.
5. Sign in with your email address and the new password.

## Everyone after that

A Billing Manager adds further managers from **Access & Data** (see that chapter). A brand-new person receives an email with a link to set their password. Someone who already has a CRMS account simply gains the role and keeps the password they already use.

## After signing in

An account that holds only the Billing Manager role lands on the Billing Dashboard. An account that also holds other CRMS roles lands on the normal dashboard, and the Billing section is in the left-hand menu:

- **Billing Dashboard**
- **Price List**
- **Platform Fees**
- **Statements**
- **Access & Data** (Billing Managers only)

To change your password later, open **My Profile** at the bottom of the menu and use **Change Password**.

# The Billing Dashboard

The dashboard answers one question: what is due from the bank for a given month, and where does it come from.

## Choosing what to look at

Two controls at the top of the page apply to everything below them:

- **Month.** The dashboard opens on the current month. Pick an earlier month to see what was due then, at the prices that applied then.
- **Flow.** "All flows" by default. Pick Corporate, NAMP, RH-SHF, Partner API, Diagnostics, Other lookups or Unattributed to narrow every figure to that flow.

Buttons on the right take you to the Price List, Platform Fees and Statements.

## Notices at the top

Three notices may appear above the figures:

- **Alerts.** Anything the billing checks have found and nobody has dealt with yet. See the Alerts chapter.
- **No prices have been set yet.** Shown until at least one price exists. Everything is still counted, but worth zero.
- **This month is before billing went live.** Shown for months older than the ledger. Third-party calls for those months were rebuilt from the records they left behind, so they are a lower bound: BVN, NIN, TIN and fraud checks from that time left no record and are not included. Loan milestones for those months are exact.

## The tiles

| Tile | What it shows |
|---|---|
| Due this month | The total for the chosen month and flow, before VAT. |
| Third-party calls | The part of the total that comes from calls and partner checks, with the number of calls and how many of them failed. Failed calls are billed all the same. |
| Platform fees | The part that comes from loan milestones, with how many applications were received, booked and declined. |
| Our cost | Billing Managers only. What the calls cost Freewave at the cost figures entered on the Price List, with the margin against the amount due. The bank never sees this tile. |
| Unattributed | Calls this month that no application claimed. They are still billed. Green when it is zero. |

## The charts

- **Due by month.** The last twelve months, stacked by category, so a change in the bill is easy to spot.
- **Where the month's amount goes.** The chosen month's amount as a share by category.
- **Loans by flow.** Milestones reached in the month, per flow, with a table underneath: Received, Booked, Declined, Cancelled, Processing now, and the fees they add up to. "Processing now" is the number of applications received that have not yet reached a final milestone.
- **Calls by service.** Every third-party service used in the month with calls, failures, unit price and amount. Billing Managers also see the "Our cost" column.

## The Charges tab

A list of every billable call and partner check in the chosen month, newest first. Filter by category or outcome, or type part of a case reference or request id. Each row shows when the call was made, which flow and case it belonged to, the subject (for example the last four digits of a BVN, or a registration number), the item, whether it succeeded, which server made it, and its price. Click a case reference to open that application.

A row marked **rebuilt** was reconstructed from history rather than recorded live. Hover over a **Failed** badge to read the reason the provider gave.

## The Loans tab

One row per application, with what it has cost so far: the number of calls made for it, the amount those calls are due, the platform fees it has earned, and the total. Filter by outcome, tick **Only loans with activity this month** to leave out dormant cases, or search by reference or applicant name. Click a reference to open the application.

## The footer

The last line of the page says since when every call has been recorded live, and how many earlier rows were rebuilt from history.

# Price List

The Price List is what the bank pays per third-party call and per partner check. Open it from the menu or from the Prices button on the dashboard.

Items are grouped by category: Identity, Credit bureau, Business bureau, Fraud check, AI advisory and Partner API. Appendix A lists them all. For each item you see:

- **Price to the bank**, before VAT. "Not set, worth zero" until you set it.
- **Our cost** (Billing Managers only), what the call costs Freewave.
- **In force**, the month the current price has applied since.
- **History**, when a price has changed more than once.

## Setting or changing a price

1. Click **Set price** on the item.
2. Choose the month the price **applies from**. The current month is selected by default.
3. Enter the **price to the bank** and, if you want margin figures, **our cost per unit**.
4. Add a note if it helps, for example "agreed with the bank on 3 Nov".
5. Save.

Prices are dated, not overwritten. A price applies from the month you choose onwards, and earlier months keep whatever price they had. This has two useful consequences:

- To raise a price next month, set it with next month as the start. This month is untouched.
- To price history, for example when prices were agreed after the system had been running for a while, choose the first month the agreement covers. Every month from then on is re-priced on the dashboard. Months that already have an issued statement keep the statement's frozen figures.

Prices cannot be negative, and a price that has already been used on an issued statement is not changed on that statement.

# Platform Fees

Platform Fees is what the bank pays per application milestone, set separately for each loan flow. Open it from the menu or from the Platform fees button on the dashboard.

The page is a grid. Rows are the four milestones, columns are the flows:

| Milestone | Charged once when |
|---|---|
| Application received | An application arrives on the platform. |
| Loan booked | The loan is booked in core banking. |
| Application declined | The application is declined at any stage. |
| Application cancelled or withdrawn | The application is withdrawn, cancelled, or its offer lapses. |

Each cell shows the fee, the month it has applied since, and a **Set** button. A cell left at zero means that milestone is not charged for that flow, which is a normal state, not a gap. Setting a fee works exactly like setting a price: choose the month it applies from, enter the fee and optionally the cost and a note, and save. **History** on a cell shows the fees that applied before.

An application is counted once per milestone. One that is received, then declined, earns the received fee and the declined fee. One that is booked never also earns declined or cancelled.

# Statements

A statement closes a month. It gathers every billable event up to and including that month that has not been billed before, prices it, adds VAT, gives it a number, and freezes the figures. From then on the bank is invoiced from the statement, not from the dashboard, and the events on it are marked as billed.

## Preparing and issuing a statement

1. Open **Statements** and pick a month from the selector at the top. The page prepares a preview headed "Statement for [month], not yet issued".
2. Read the summary line: the number of billable events, the subtotal, the VAT, the total, and (Billing Managers only) our cost.
3. Read any warnings. The ones you will see:
   - **The month has not ended.** You can issue a statement for the current month, but calls made after you issue will roll into the next one. Normally wait for the month to end.
   - **N rows from earlier months were never billed.** Older unbilled events are swept in and shown as their own lines, with their own month. This happens the first time you issue, and after a statement is voided.
   - **No price set, so billed at zero.** Named items have events but no price. Set the prices first, then prepare the month again, or the statement will carry zero for them.
4. Check the lines. One line per month, flow, category and item, with quantity, unit price and amount.
5. Type a note for the bank if you want one to appear on the statement.
6. Click **Issue statement for [month]**.

The preview tells you when it cannot issue: the month has not started, nothing is unbilled, or a statement already covers the month. In the last case the existing statement must be voided before the month can be reissued.

## Statement numbers and dates

Statements are numbered **BILL-yyyyMM-NN**, for example BILL-202611-01 for the first statement issued for November 2026. A second statement for the same month, after a void, becomes BILL-202611-02. Numbers are never reused.

A statement is due a fixed number of days after it is issued. Thirty days is the configured default. The due date is printed on the statement and shown in the list.

## The list

Every statement, newest first: number, period, status, issue date, due date, number of events and total. A statement that is past its due date and not paid is marked overdue. Click a row to open it.

## Opening a statement

The detail shows who issued it and when, the due date, any note, and the lines with a totals footer. Billing Managers also see the unit cost per line. From here:

- **PDF** downloads the statement as a document the bank can be sent. It carries Freewave's details and the bank's, the lines, the subtotal, VAT and total, and the payment instructions.
- **Excel** downloads the same figures as a workbook.
- **Mark paid** records the date the bank paid, the payment reference, and an optional note. The date cannot be in the future.
- **Void** cancels an issued statement. A reason is required. Every event on it returns to unbilled and will appear on the next statement issued. The number is not reused. A paid statement cannot be voided.

Neither export ever contains cost or margin figures.

# Alerts

Six checks run on their own, every six hours by default, on both CRMS servers. Each time they run, anything new they find becomes an alert that is emailed to every active Billing Manager and shown at the top of the Billing Dashboard. An alert stays until the condition goes away, at which point it resolves itself.

| Alert | What it means | Severity |
|---|---|---|
| Spend above norm | A service's calls this month are running well above its average over the previous months. Look for a retry loop or a flow calling it more than it should. | Warning, or Critical when far above |
| Failing calls | Most of a service's calls over the last couple of days failed. Every failed call is billed, so the bank is paying for nothing. The provider's answer can be seen under Admin > Diagnostics by an administrator. | Critical |
| Unattributed calls | Calls this month that no application claimed. They are billed, but nobody can say which case they were for. The Charges tab shows them under the Unattributed flow. | Warning |
| Unpriced item | An item has events this month and no price, so it is billed at zero. Set the price. | Warning |
| Overdue statement | An issued statement is past its due date and not marked paid. | Warning, Critical after 30 days |
| Month not closed | The previous month has ended with unbilled events and no statement. | Info, Warning after the grace period |

Appendix B lists the thresholds behind each check.

## Acting on an alert

On the dashboard each alert has two buttons for Billing Managers:

- **Snooze** hides it for a week. If the condition is still there after that, it comes back.
- **Dismiss** hides it until the condition resolves. Use this for something you have looked into and accepted.

A snoozed or dismissed alert can be brought back with **Reopen** in the alert history on the Access & Data page. The dashboard also shows how many checks in a row have found the same condition, so a long-running problem is obvious.

# Access & Data

This page is visible to Billing Managers only. It has two halves: who manages billing, and the state of the ledger behind the dashboard.

## Billing Managers

A table of the managers with name, email, status and last sign-in. To add one, enter the email address and, for someone new to the CRMS, their first and last name, then click **Add**.

- A person who already has a CRMS account gains the role and keeps their password. Their account is activated if it was inactive.
- A new person gets an account and an email with a link to set a password.

To remove a manager, click **Remove** and confirm. The account loses the role, and is deactivated if it had no other role. The last active manager cannot be removed, so the vendor can never lock itself out.

## Ledger

Three figures describe where the dashboard's rows come from:

- **Live rows**: calls and partner checks recorded at the moment they happened, and since when.
- **Loan milestones**: rows read off the loan books. They are refreshed every hour and on demand.
- **Rebuilt rows**: rows reconstructed from records that existed before billing went live.

Three actions sit beneath them:

- **Refresh loan milestones.** Reads every application and adds any received, booked, declined or cancelled row it has earned and not yet been given. The hourly refresh does the same thing. Safe to run at any time.
- **Rebuild history from inception.** Reconstructs third-party usage from the bureau reports, CAC lookups and partner check logs made before billing went live. Click **Preview** first: it reports how many rows it found and how many are already in the ledger, grouped by item, without writing anything. Click **Write** to add them. Rows are marked as rebuilt and are never added twice, so this is also safe to repeat. The preview also lists what it cannot reconstruct.
- **Billing checks.** Runs the six alert checks now instead of waiting for the next scheduled run, and reports what was found, what resolved and how many managers were emailed. Underneath is the alert history with a **Reopen** button for snoozed and dismissed alerts.

# Month-end routine

A suggested routine for each month:

1. In the first days of the month, open the Billing Dashboard for the month just ended. Deal with any alerts, in particular unpriced items and unattributed calls.
2. Check the Calls by service table against your own provider invoices. The "Our cost" tile and the margin tell you whether the cost figures on the Price List are still right.
3. Open Statements, pick the month, and read the preview and its warnings. Fix anything that needs fixing on the Price List or Platform Fees and prepare the month again.
4. Issue the statement. Download the PDF and send it to the bank.
5. When the bank pays, open the statement and **Mark paid** with the reference.
6. If a statement turns out to be wrong, **Void** it with a reason, correct the prices, and issue again. The new statement picks up the released events.

# Questions and answers

**A price was agreed but the dashboard shows zero.** The price was probably set to apply from a later month than the one you are looking at. Open the Price List, click History on the item, and check the "from" month. Set the price again with an earlier starting month if needed.

**A failed call is on the bill.** That is by design. The bank pays for every call the CRMS makes on its behalf, whether the provider answered or not. Repeated failures raise a Failing calls alert so the cause can be found quickly.

**What is the Unattributed flow?** A call that was made outside any application, for example from a screen that does not belong to a case. It is still billed. If the number is large, tell the technical team which screen was used.

**The month shows fewer calls than I expected.** If the month is before billing went live, it was rebuilt from history and is a lower bound. BVN, NIN, TIN and fraud checks from that time cannot be recovered.

**I changed a price and the issued statement did not change.** Statements are frozen on issue. To rebill a month, void the statement and issue it again.

**Can the bank's administrator change prices or add managers?** No. Administrators see the billing pages read only, without the cost column, and the Access & Data page is not shown to them at all.

**I did not get the alert email.** Alerts are sent to active Billing Managers only. Check with an administrator that your account is active and that your email address on the Access & Data page is right. Administrators can see every email the CRMS sent, and whether it was delivered, under Admin > Outbound Email.

# Appendix A: Billable items

| Category | Item |
|---|---|
| Identity | BVN verification |
| Identity | BVN verification (advanced) |
| Identity | NIN verification |
| Identity | TIN verification |
| Identity | CAC lookup |
| Identity | CAC lookup (advanced, with directors) |
| Credit bureau | CRC credit score |
| Credit bureau | CRC credit history |
| Credit bureau | CRC full report |
| Credit bureau | First Central summary |
| Credit bureau | First Central full report |
| Credit bureau | First Central credit score |
| Credit bureau | Credit Registry summary |
| Credit bureau | Credit Registry full report |
| Credit bureau | Credit Registry advanced report |
| Credit bureau | Premium (all bureaus) report |
| Business bureau | CRC business report |
| Business bureau | First Central business report |
| Business bureau | Premium business report |
| Fraud check | Loan fraud check (individual) |
| Fraud check | Loan fraud check (business) |
| AI advisory | AI advisory narrative |
| Partner API | Partner customer credit check |
| Platform fees | Application received (per flow) |
| Platform fees | Loan booked (per flow) |
| Platform fees | Application declined (per flow) |
| Platform fees | Application cancelled or withdrawn (per flow) |

Flows: Corporate, NAMP, RH-SHF, Partner API, Diagnostics, Other lookups, Unattributed. Platform fees apply to the three loan flows only.

# Appendix B: Alert thresholds

These are the defaults. The technical team can change them in the system configuration.

| Check | Default rule |
|---|---|
| Spend above norm | A service is on course for at least twice its average monthly calls over the last three months, with at least ten calls so far this month. Critical at four times. |
| Failing calls | At least half of a service's calls over the last two days failed, with at least five calls in that window. |
| Unattributed calls | At least one call this month belongs to no case. |
| Unpriced item | Any item or platform fee with events this month and no price in force. |
| Overdue statement | An issued statement is past its due date, which is thirty days after issue. Critical when thirty or more days late. |
| Month not closed | The previous month has unbilled events and no statement. Information only for the first nine days of the month, a warning from the tenth. |
| Checks run | Every six hours on each server, and on demand from Access & Data. |
| Snooze | Seven days. |
| VAT | 7.5% on statements. |
