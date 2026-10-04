-- ============================================================================
-- Fast-forward a RH-SHF case to the RATIFICATION stage, so a Final Approver can
-- generate the offer with one click (which produces the real, downloadable PDF).
--
-- TARGET: RHSHF-2026-450362
--
-- WHY NOT GENERATE THE OFFER IN SQL
--   The offer letter is a PDF rendered by the application at ratification and
--   uploaded to file storage. SQL can set the case state but cannot produce that
--   file. A SQL-inserted offer row would point at a PDF that does not exist, and
--   "Download" would fail. So this script takes the case to the point of
--   ratification; you then click "Ratify" in the UI and the app generates the
--   genuine, downloadable / re-uploadable offer.
--
-- WHAT THIS DOES
--   Sets the case to Status=UnderReview, InternalStage=Ratification, clears the
--   profiling stage, and ensures it is on cycle 1. That is all the Ratify action
--   requires:
--     * the offer PDF needs only profile fields (company, RC, programme, amount,
--       currency) -- not the farm plan, appraisal, or Fineract tenor;
--     * with no appraisal/risk/committee records on the cycle, the rule that the
--       ratifier must differ from the appraiser/risk/committee actor has nobody
--       to exclude, so any Final Approver (or SystemAdmin) can ratify.
--
-- SAFETY
--   * Runs in a transaction -- nothing commits until you say so.
--   * Scoped strictly to the one reference. Shows before/after and the row count.
--   * Does NOT touch any other case or any configuration.
--
-- AFTER RUNNING (and COMMIT):
--   1. Log in as a FinalApprover (or SystemAdmin).
--   2. Open the case; the "Ratify" button appears in the header.
--   3. Ratify with the approved amount EQUAL to the EOP total (shown below) --
--      partial approval is rejected. The app generates the offer PDF.
--   4. Case moves to "Awaiting Offer Acceptance"; the Offer tab now offers the
--      download, and the signed-copy re-upload.
--
-- HOW TO RUN IN DBEAVER
--   1. Set the SQL editor to MANUAL commit (toolbar auto-commit toggle off).
--   2. Run the whole file with Alt+X (Execute SQL Script) -- not Ctrl+Enter.
--   3. Check rows_updated = 1 and the AFTER grid reads Ratification.
--   4. Click the toolbar Commit button (or Rollback if wrong).
-- ============================================================================

START TRANSACTION;

-- STEP 1: current state (note TotalEopValue -- you enter it at ratify)
SELECT 'BEFORE' AS Notice;

SELECT Reference, CompanyName, Status, InternalStage, CurrentStage,
       CurrentCycleNumber, TotalEopValue, Currency
FROM RhshfCreditProfiles
WHERE Reference = 'RHSHF-2026-450362';

-- STEP 2: advance to the ratification stage
UPDATE RhshfCreditProfiles
SET Status             = 'UnderReview',
    InternalStage      = 'Ratification',
    CurrentStage       = NULL,
    CurrentCycleNumber = GREATEST(CurrentCycleNumber, 1),
    UpdatedAt          = UTC_TIMESTAMP(6)
WHERE Reference = 'RHSHF-2026-450362';

-- Must be exactly 1. If 0, the reference does not exist -- ROLLBACK and check it.
SELECT ROW_COUNT() AS rows_updated;

-- STEP 3: confirm
SELECT 'AFTER' AS Notice;

SELECT Reference, Status, InternalStage, CurrentStage, CurrentCycleNumber, TotalEopValue, Currency
FROM RhshfCreditProfiles
WHERE Reference = 'RHSHF-2026-450362';

-- ============================================================================
--   COMMIT;    -- rows_updated = 1 and the AFTER state reads Ratification
--   ROLLBACK;  -- anything looked wrong (e.g. rows_updated = 0)
-- ============================================================================
