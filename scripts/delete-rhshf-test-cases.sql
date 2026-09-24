-- ============================================================================
-- Remove RH-SHF test cases created during portal integration testing.
--
-- Raised by the RH-SHF portal team (CrmsIntegrationNoteToCrmsTeam.md §6): they
-- created test cases in the CRMS sandbox with fictitious FAC identifiers and
-- asked that these not be mistaken for genuine applications sitting in a staff
-- queue.
--
-- NOTE ON THE COUNT: their note lists "three credit cases", but one was an
-- idempotent repeat of the first — which returned the same reference rather
-- than creating a second case. The third reference below (RHSHF-2026-697886)
-- is a config-probe case CRMS created while verifying the sandbox, also for
-- removal. Three rows total.
--
-- WHY EXPLICIT DELETES RATHER THAN RELYING ON CASCADE
-- Most child tables are configured ON DELETE CASCADE, but not all of them are,
-- and BureauReports.RhshfCreditProfileId is only an INDEX — there is no foreign
-- key behind it, so deleting the profile would silently orphan its bureau rows.
-- Deleting explicitly, child-first, is correct regardless of what constraints
-- the target database actually has.
--
-- SAFETY
--   * Runs inside a transaction. Nothing commits until you say so.
--   * STEP 1 shows you exactly what will be removed. Read it before proceeding.
--   * Scoped strictly to the three references below — no wildcards, no date
--     ranges, nothing that could widen if run twice or pasted incompletely.
--   * Configuration tables (RhshfDocumentRequirements, RhshfRoutingConfigs,
--     RhshfPreDeploymentChecklistTemplates, RhshfAppraisalThresholds) are NOT
--     touched: they are programme setup, not case data.
--
-- USAGE
--   mysql -h <host> -u <user> -p <database> < scripts/delete-rhshf-test-cases.sql
--   ...then review STEP 1's output and run COMMIT (or ROLLBACK) yourself.
-- ============================================================================

START TRANSACTION;

-- Resolve the references to ids once, so every statement below targets exactly
-- the same rows even if something else is writing concurrently.
DROP TEMPORARY TABLE IF EXISTS tmp_rhshf_doomed;
CREATE TEMPORARY TABLE tmp_rhshf_doomed AS
SELECT Id, Reference, CompanyName, Status, InternalStage, TotalEopValue, ReceivedAt
FROM RhshfCreditProfiles
WHERE Reference IN ('RHSHF-2026-920295', 'RHSHF-2026-570338', 'RHSHF-2026-697886');

-- ── STEP 1: confirm the blast radius before committing ──────────────────────
SELECT '>>> CASES TO BE DELETED — verify these are the test cases <<<' AS Notice;
SELECT Reference, CompanyName, Status, InternalStage, TotalEopValue, ReceivedAt
FROM tmp_rhshf_doomed;

SELECT '>>> CHILD ROWS THAT WILL GO WITH THEM <<<' AS Notice;
SELECT 'EopLines'               AS ChildTable, COUNT(*) AS Rows FROM RhshfEopLines               WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed)
UNION ALL SELECT 'IssuedTokens',               COUNT(*) FROM RhshfIssuedTokens                   WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed)
UNION ALL SELECT 'SupportingDocuments',        COUNT(*) FROM RhshfSupportingDocuments            WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed)
UNION ALL SELECT 'StageConfirmations',         COUNT(*) FROM RhshfStageConfirmations             WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed)
UNION ALL SELECT 'StatusHistory',              COUNT(*) FROM RhshfStatusHistory                  WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed)
UNION ALL SELECT 'FarmPlans',                  COUNT(*) FROM RhshfFarmPlans                      WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed)
UNION ALL SELECT 'Directors',                  COUNT(*) FROM RhshfDirectors                      WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed)
UNION ALL SELECT 'BureauReports',              COUNT(*) FROM BureauReports                       WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed)
UNION ALL SELECT 'CallbackAttempts',           COUNT(*) FROM RhshfCallbackAttempts               WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed)
UNION ALL SELECT 'Appraisals',                 COUNT(*) FROM RhshfAppraisals                     WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed)
UNION ALL SELECT 'Offers',                     COUNT(*) FROM RhshfOffers                         WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);

-- ── STEP 2: grandchildren first (their parents are removed below) ───────────
DELETE FROM BureauAccounts
WHERE BureauReportId IN (SELECT Id FROM BureauReports WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed));

DELETE FROM BureauScoreFactors
WHERE BureauReportId IN (SELECT Id FROM BureauReports WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed));

DELETE FROM RhshfCollateralDocuments
WHERE RhshfCollateralId IN (SELECT Id FROM RhshfCollaterals WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed));

DELETE FROM RhshfCommitteeVotes
WHERE RhshfCommitteeReviewId IN (SELECT Id FROM RhshfCommitteeReviews WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed));

DELETE FROM RhshfOfferDocuments
WHERE RhshfOfferId IN (SELECT Id FROM RhshfOffers WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed));

-- ── STEP 3: direct children of the case ─────────────────────────────────────
DELETE FROM BureauReports                    WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfCollaterals                 WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfCommitteeReviews            WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfOffers                      WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfAdvisories                  WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfEligibilityChecks           WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfLegalClearances             WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfCallbackAttempts            WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfPreDeploymentChecklistItems WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfFinancialAppraisalReports   WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfStageConfirmations          WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfStatusHistory               WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfFarmPlans                   WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfDirectors                   WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfDisbursements               WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfRatifications               WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfRiskReviews                 WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfAppraisals                  WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfSupportingDocuments         WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfIssuedTokens                WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);
DELETE FROM RhshfEopLines                    WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);

-- ── STEP 4: the cases themselves ────────────────────────────────────────────
DELETE FROM RhshfCreditProfiles WHERE Id IN (SELECT Id FROM tmp_rhshf_doomed);

-- ── STEP 5: verify — every count below must be 0 ────────────────────────────
SELECT '>>> POST-DELETE VERIFICATION — all counts must be 0 <<<' AS Notice;
SELECT 'CreditProfiles remaining' AS Check_, COUNT(*) AS Rows FROM RhshfCreditProfiles WHERE Reference IN ('RHSHF-2026-920295', 'RHSHF-2026-570338', 'RHSHF-2026-697886')
UNION ALL SELECT 'Orphaned EopLines',          COUNT(*) FROM RhshfEopLines            WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed)
UNION ALL SELECT 'Orphaned StatusHistory',     COUNT(*) FROM RhshfStatusHistory       WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed)
UNION ALL SELECT 'Orphaned BureauReports',     COUNT(*) FROM BureauReports            WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed)
UNION ALL SELECT 'Orphaned IssuedTokens',      COUNT(*) FROM RhshfIssuedTokens        WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed)
UNION ALL SELECT 'Orphaned CallbackAttempts',  COUNT(*) FROM RhshfCallbackAttempts    WHERE RhshfCreditProfileId IN (SELECT Id FROM tmp_rhshf_doomed);

DROP TEMPORARY TABLE IF EXISTS tmp_rhshf_doomed;

-- ============================================================================
-- Nothing above is permanent yet.
--   COMMIT;    -- STEP 1 showed the right two cases and STEP 5 is all zeros
--   ROLLBACK;  -- anything looked wrong
-- ============================================================================
