using CRMS.Application.Advisory.Interfaces;
using CRMS.Domain.Configuration;

namespace CRMS.Infrastructure.ExternalServices.AIServices;

/// <summary>
/// Deterministic scoring of one individual for the customer check (docs/Customer_Check.md,
/// aiAdvisory). Its own engine, not a mode of <see cref="RuleBasedScoringEngine"/>: that one is
/// built for a company with accounts and collateral, and its numbers for the three loan tracks
/// must not move. Four categories; one with no data is reported as not assessed and carries no
/// score, and the overall score is weighted over the rest.
/// </summary>
public class CustomerCheckScoringEngine
{
    public const string CreditHistory = "CreditHistory";
    public const string RepaymentCapacity = "RepaymentCapacity";
    public const string CashflowStability = "CashflowStability";
    public const string IdentityAndFraud = "IdentityAndFraud";

    public record Result(
        List<RiskScoreOutput> Scores,
        List<CustomerAdvisoryGap> NotAssessed,
        decimal OverallScore,
        string OverallRating,
        string Recommendation,
        List<string> RedFlags,
        List<string> Conditions,
        decimal? RecommendedAmount,
        int? RecommendedTenorMonths);

    public Result CalculateScores(CustomerAdvisoryRequest r, ScoringConfiguration config)
    {
        var cfg = config.CustomerCheck;
        var scores = new List<RiskScoreOutput>();
        var gaps = new List<CustomerAdvisoryGap>();
        var redFlags = new List<string>();

        Place(CreditHistoryScore(r, config, redFlags), cfg.CreditHistoryWeight, scores, gaps);
        Place(RepaymentCapacityScore(r, config, redFlags), cfg.RepaymentCapacityWeight, scores, gaps);
        Place(CashflowScore(r, config, redFlags), cfg.CashflowStabilityWeight, scores, gaps);
        Place(IdentityScore(r, config, redFlags), cfg.IdentityAndFraudWeight, scores, gaps);

        var totalWeight = scores.Sum(s => s.Weight);
        var overall = totalWeight > 0 ? Math.Round(scores.Sum(s => s.Score * s.Weight) / totalWeight, 2) : 0m;
        var rating = RuleBasedScoringEngine.DetermineRating(overall);
        var recommendation = scores.Count == 0 ? "Refer" : Recommend(overall, redFlags.Count, config.Recommendations);
        var (amount, tenor) = Lend(r, overall, recommendation, config.LoanAdjustments);
        var conditions = Conditions(r, scores, gaps, redFlags, overall);

        return new Result(scores, gaps, overall, rating, recommendation, redFlags, conditions, amount, tenor);
    }

    private static void Place((RiskScoreOutput? Score, string? Gap) outcome, decimal weight, List<RiskScoreOutput> scores, List<CustomerAdvisoryGap> gaps)
    {
        if (outcome.Score is { } s)
            scores.Add(s with { Weight = weight });
        else
            gaps.Add(new CustomerAdvisoryGap(outcome.Score?.Category ?? CategoryOf(outcome), weight, outcome.Gap ?? "No data."));
    }

    private static string CategoryOf((RiskScoreOutput? Score, string? Gap) outcome) => outcome.Gap?.Split('|')[0] ?? "Unknown";

    private static (RiskScoreOutput? Score, string? Gap) Gap(string category, string reason) => (null, $"{category}|{reason}");

    // ── Credit history: the bureau and the person's own loans with the bank ──────────────────────

    private static (RiskScoreOutput?, string?) CreditHistoryScore(CustomerAdvisoryRequest r, ScoringConfiguration config, List<string> redFlags)
    {
        var ch = config.CreditHistory;
        var cc = config.CustomerCheck;
        var b = r.Bureau;
        var e = r.Exposure;
        var bankHistory = e.BankDataAvailable && (e.BankRunningLoans + e.BankOverdueLoans + e.BankPaidLoans + e.BankWrittenOffLoans) > 0;
        if (b is null && !bankHistory)
            return Gap(CreditHistory, e.BankDataAvailable
                ? "No bureau record and no loan history with the bank."
                : "No bureau record, and core banking could not be read.");

        var positives = new List<string>();
        var concerns = new List<string>();
        decimal score = ch.BaseScore;

        if (b is null)
        {
            score -= cc.NoBureauRecordPenalty;
            concerns.Add("No credit bureau holds a record for this person; only the bank's own history is assessed.");
        }
        else
        {
            if (b.CreditScore is { } cs)
            {
                if (cs >= ch.ExcellentCreditScoreThreshold) { score += ch.ExcellentCreditScoreBonus; positives.Add($"Strong bureau credit score of {cs}."); }
                else if (cs >= ch.GoodCreditScoreThreshold) { score += ch.GoodCreditScoreBonus; positives.Add($"Good bureau credit score of {cs}."); }
                else if (cs < ch.PoorCreditScoreThreshold) { score -= ch.PoorCreditScorePenalty; concerns.Add($"Low bureau credit score of {cs}."); redFlags.Add($"Low bureau credit score ({cs})."); }
            }
            if (b.WrittenOffLoans > 0)
            {
                score -= ch.DefaultPenalty;
                concerns.Add($"{b.WrittenOffLoans} bureau loan{(b.WrittenOffLoans == 1 ? "" : "s")} written off.");
                redFlags.Add($"{b.WrittenOffLoans} bureau loan{(b.WrittenOffLoans == 1 ? "" : "s")} written off.");
            }
            if (b.DelinquentLoans > 0)
            {
                score -= ch.DelinquencyPenalty;
                concerns.Add($"{b.DelinquentLoans} bureau loan{(b.DelinquentLoans == 1 ? " is" : "s are")} delinquent (NGN {b.TotalOverdue:N0} overdue).");
                redFlags.Add($"{b.DelinquentLoans} delinquent bureau facilit{(b.DelinquentLoans == 1 ? "y" : "ies")}.");
            }
            if (b.MaxDaysOverdue >= ch.SevereDelinquencyDaysThreshold) { score -= ch.SevereDelinquencyPenalty; concerns.Add($"Up to {b.MaxDaysOverdue} days overdue at a bureau."); }
            else if (b.MaxDaysOverdue >= ch.WatchListDaysThreshold) { score -= ch.WatchListPenalty; concerns.Add($"Up to {b.MaxDaysOverdue} days overdue at a bureau."); }
            if (b.PerformingLoans >= ch.MinPerformingLoansForBonus) { score += ch.PerformingLoansBonus; positives.Add($"{b.PerformingLoans} performing bureau facilities."); }
            if (b.ClosedLoans > 0 && b.DelinquentLoans == 0 && b.WrittenOffLoans == 0)
                positives.Add($"{b.ClosedLoans} bureau loan{(b.ClosedLoans == 1 ? "" : "s")} closed without a delinquency on record.");
            if (b.RecentEnquiries >= 3) concerns.Add($"{b.RecentEnquiries} recent credit enquiries.");
        }

        if (bankHistory)
        {
            if (e.BankWrittenOffLoans > 0) { score -= cc.BankWrittenOffPenalty; concerns.Add($"{e.BankWrittenOffLoans} loan{(e.BankWrittenOffLoans == 1 ? "" : "s")} with the bank written off."); redFlags.Add("A loan with the bank was written off."); }
            if (e.BankOverdueLoans > 0) { score -= cc.BankOverdueLoanPenalty; concerns.Add($"{e.BankOverdueLoans} loan{(e.BankOverdueLoans == 1 ? "" : "s")} with the bank past due, up to {e.BankMaxDaysPastDue} days (NGN {e.BankOverdue:N0})."); redFlags.Add($"A loan with the bank is {e.BankMaxDaysPastDue} days past due."); }
            if (e.BankPaidLoans >= cc.MinBankPaidLoansForBonus) { score += cc.BankPaidLoansBonus; positives.Add($"{e.BankPaidLoans} loans with the bank repaid in full."); }
            if (e.BankRunningLoans > 0 && e.BankOverdueLoans == 0) positives.Add($"{e.BankRunningLoans} running loan{(e.BankRunningLoans == 1 ? "" : "s")} with the bank, none past due.");
        }
        else if (e.BankDataAvailable)
            concerns.Add("No loan history with the bank.");

        score = Math.Clamp(score, 0, 100);
        var rationale = (b is null ? "No bureau record. " : $"{b.Bureau} report{(b.Reused ? " (stored copy)" : "")}: {b.TotalLoans} loans at {Math.Max(0, b.TotalLoans - b.ClosedLoans)} active, {b.DelinquentLoans} delinquent, {b.WrittenOffLoans} written off, NGN {b.TotalOutstanding:N0} outstanding. ")
            + (bankHistory ? $"With the bank: {e.BankRunningLoans} running, {e.BankOverdueLoans} past due, {e.BankPaidLoans} repaid, {e.BankWrittenOffLoans} written off." : "No loans with the bank.");
        return (new RiskScoreOutput(CreditHistory, score, 0, RuleBasedScoringEngine.DetermineRating(score), rationale.Trim(), concerns, positives), null);
    }

    // ── Repayment capacity: obligations against inflow ───────────────────────────────────────────

    private static (RiskScoreOutput?, string?) RepaymentCapacityScore(CustomerAdvisoryRequest r, ScoringConfiguration config, List<string> redFlags)
    {
        var cc = config.CustomerCheck;
        var cf = r.Cashflow;
        if (cf is null || cf.AccountsRead == 0)
            return Gap(RepaymentCapacity, "No statement was read, so monthly inflow is not known.");

        var instalment = Instalment(r.ProposedAmount, r.ProposedTenorMonths, config.LoanAdjustments.BaseInterestRate);
        var obligations = instalment + Math.Max(0, r.Exposure.BankMonthlyInstalments);
        var positives = new List<string>();
        var concerns = new List<string>();
        decimal score;
        decimal? ratio = cf.AverageMonthlyInflow > 0 ? Math.Round(obligations / cf.AverageMonthlyInflow, 2) : null;

        if (ratio is null)
        {
            score = cc.BeyondCriticalScore;
            concerns.Add("No inflow in the period, so no repayment capacity can be shown.");
            redFlags.Add("No inflow on the accounts read.");
        }
        else if (ratio <= cc.ComfortableObligationRatio) { score = cc.ComfortableScore; positives.Add($"Obligations would take {ratio:P0} of monthly inflow."); }
        else if (ratio <= cc.ManageableObligationRatio) { score = cc.ManageableScore; positives.Add($"Obligations would take {ratio:P0} of monthly inflow."); }
        else if (ratio <= cc.StretchedObligationRatio) { score = cc.StretchedScore; concerns.Add($"Obligations would take {ratio:P0} of monthly inflow."); }
        else if (ratio <= cc.CriticalObligationRatio) { score = cc.CriticalScore; concerns.Add($"Obligations would take {ratio:P0} of monthly inflow."); redFlags.Add($"Repayments would take {ratio:P0} of monthly inflow."); }
        else { score = cc.BeyondCriticalScore; concerns.Add($"Obligations would exceed {cc.CriticalObligationRatio:P0} of monthly inflow ({ratio:P0})."); redFlags.Add($"Repayments would take {ratio:P0} of monthly inflow."); }

        if (!r.ProposalSupplied)
        {
            score -= cc.AssumedProposalPenalty;
            concerns.Add($"No loan amount and tenor were supplied; NGN {r.ProposedAmount:N0} over {r.ProposedTenorMonths} months was assumed.");
        }
        if (r.Exposure.BankMonthlyInstalments > 0)
            concerns.Add($"Existing instalments with the bank of NGN {r.Exposure.BankMonthlyInstalments:N0} a month are counted.");

        score = Math.Clamp(score, 0, 100);
        var rationale = $"Estimated instalment NGN {instalment:N0} a month on NGN {r.ProposedAmount:N0} over {r.ProposedTenorMonths} months" +
            (r.Exposure.BankMonthlyInstalments > 0 ? $", plus NGN {r.Exposure.BankMonthlyInstalments:N0} already due monthly with the bank" : "") +
            $", against average monthly inflow of NGN {cf.AverageMonthlyInflow:N0}" + (ratio is null ? "." : $" ({ratio:P0}).");
        return (new RiskScoreOutput(RepaymentCapacity, score, 0, RuleBasedScoringEngine.DetermineRating(score), rationale, concerns, positives), null);
    }

    /// <summary>Flat-rate instalment: principal plus interest for the term, spread evenly.</summary>
    public static decimal Instalment(decimal amount, int tenorMonths, decimal annualRatePercent)
    {
        var months = Math.Max(1, tenorMonths);
        var interest = amount * (annualRatePercent / 100m) * months / 12m;
        return Math.Round((amount + interest) / months, 2);
    }

    // ── Cashflow stability ───────────────────────────────────────────────────────────────────────

    private static (RiskScoreOutput?, string?) CashflowScore(CustomerAdvisoryRequest r, ScoringConfiguration config, List<string> redFlags)
    {
        var cfg = config.Cashflow;
        var cc = config.CustomerCheck;
        var cf = r.Cashflow;
        if (cf is null || cf.AccountsRead == 0)
            return Gap(CashflowStability, "No statement was read.");

        var positives = new List<string>();
        var concerns = new List<string>();
        decimal score = cfg.BaseScore + cfg.InternalStatementBonus;
        positives.Add($"Statements from the bank's own records for {cf.AccountsRead} account{(cf.AccountsRead == 1 ? "" : "s")} over {cf.WindowMonths} months.");

        if (cf.NetMonthlyCashflow > 0) { score += cfg.PositiveCashflowBonus; positives.Add($"Positive net monthly cashflow of NGN {cf.NetMonthlyCashflow:N0}."); }
        else { score -= cfg.NegativeCashflowPenalty; concerns.Add($"Net monthly cashflow of NGN {cf.NetMonthlyCashflow:N0}."); redFlags.Add("Outflow exceeds inflow over the period."); }

        if (cf.AverageMonthlyInflow > 0)
        {
            if (cf.InflowVolatility < cfg.LowVolatilityThreshold) { score += cfg.LowVolatilityBonus; positives.Add("Steady monthly inflow."); }
            else if (cf.InflowVolatility > cfg.HighVolatilityThreshold) { score -= cfg.HighVolatilityPenalty; concerns.Add($"Monthly inflow varies widely ({cf.InflowVolatility:P0})."); }
        }

        var activeShare = cf.WindowMonths > 0 ? (decimal)cf.MonthsWithActivity / cf.WindowMonths : 0;
        if (activeShare < cc.MinActiveMonthsShare) { score -= cc.InactiveMonthsPenalty; concerns.Add($"Activity in only {cf.MonthsWithActivity} of {cf.WindowMonths} months."); }

        if (cf.BouncedTransactionCount > 0)
        {
            score -= cfg.BouncedTransactionPenalty;
            var word = cf.BouncedTransactionCount == 1 ? "transaction" : "transactions";
            concerns.Add($"{cf.BouncedTransactionCount} failed or reversed {word}.");
            // One reversal is ordinary; a pattern is the flag.
            if (cf.BouncedTransactionCount > 1) redFlags.Add($"{cf.BouncedTransactionCount} failed or reversed {word} on the accounts.");
        }

        if (cf.DaysWithNegativeBalance > cfg.HighNegativeBalanceDaysThreshold) { score -= cfg.HighNegativeBalancePenalty; concerns.Add($"Balance below zero on {cf.DaysWithNegativeBalance} days."); redFlags.Add($"Balance below zero on {cf.DaysWithNegativeBalance} days."); }
        else if (cf.DaysWithNegativeBalance > cfg.ModerateNegativeBalanceDaysThreshold) { score -= cfg.ModerateNegativeBalancePenalty; concerns.Add($"Balance below zero on {cf.DaysWithNegativeBalance} days."); }

        if (cf.WindowMonths < cfg.MinimumMonthsRequired) { score -= cfg.InsufficientCoveragePenalty; concerns.Add($"Only {cf.WindowMonths} months of statements."); }
        else if (cf.WindowMonths >= cfg.IdealMonthsCoverage) { score += cfg.IdealCoverageBonus; }

        if (cf.TransactionsAnalyzed < 10) concerns.Add($"Only {cf.TransactionsAnalyzed} transactions in the period.");

        score = Math.Clamp(score, 0, 100);
        var rationale = $"{cf.TransactionsAnalyzed} transactions over {cf.WindowMonths} months on {cf.AccountsRead} account{(cf.AccountsRead == 1 ? "" : "s")}. " +
            $"Average inflow NGN {cf.AverageMonthlyInflow:N0}, outflow NGN {cf.AverageMonthlyOutflow:N0} a month; balances total NGN {cf.TotalBalance:N0}.";
        return (new RiskScoreOutput(CashflowStability, score, 0, RuleBasedScoringEngine.DetermineRating(score), rationale, concerns, positives), null);
    }

    // ── Identity and fraud ───────────────────────────────────────────────────────────────────────

    private static (RiskScoreOutput?, string?) IdentityScore(CustomerAdvisoryRequest r, ScoringConfiguration config, List<string> redFlags)
    {
        var cc = config.CustomerCheck;
        var ch = config.CreditHistory;
        var id = r.Identity;
        if (id.BvnVerified is null && id.FraudRiskScore is null && id.BankCustomerFound is null)
            return Gap(IdentityAndFraud, "Neither the identity check, the fraud check nor core banking answered.");

        var positives = new List<string>();
        var concerns = new List<string>();
        decimal score = cc.IdentityBaseScore;

        if (id.BvnVerified == true) { score += cc.BvnVerifiedBonus; positives.Add("BVN verified with the identity provider."); }
        else if (id.BvnVerified == false) { score -= cc.BvnUnverifiedPenalty; concerns.Add("The BVN could not be verified."); redFlags.Add("The BVN could not be verified."); }
        else concerns.Add("Identity was not checked.");

        if (id.WatchListed == true) { score -= cc.WatchListedPenalty; concerns.Add("The BVN is watch-listed."); redFlags.Add("The BVN is watch-listed."); }

        if (id.FraudRiskScore is { } fs)
        {
            if (fs >= ch.HighFraudRiskScoreThreshold) { score -= cc.HighFraudRiskPenalty; concerns.Add($"Fraud risk score {fs} (high)."); redFlags.Add("The fraud check rates this person high risk."); }
            else if (fs >= ch.ElevatedFraudRiskScoreThreshold) { score -= cc.ElevatedFraudRiskPenalty; concerns.Add($"Fraud risk score {fs} (medium)."); }
            else positives.Add($"Fraud risk score {fs} (low).");
        }
        else concerns.Add("No fraud check was run.");

        if (id.BankCustomerFound == true)
        {
            score += cc.KnownCustomerBonus;
            positives.Add(id.MonthsAsCustomer is { } m ? $"A customer of the bank for {m} months." : "A customer of the bank.");
            if (id.MonthsAsCustomer >= cc.EstablishedCustomerMonths) score += cc.EstablishedCustomerBonus;
            if (!string.IsNullOrWhiteSpace(id.KycLevel)) positives.Add($"KYC level {id.KycLevel}.");
        }
        else if (id.BankCustomerFound == false) concerns.Add("Not a customer of the bank.");

        score = Math.Clamp(score, 0, 100);
        var rationale = $"BVN {(id.BvnVerified == true ? "verified" : id.BvnVerified == false ? "not verified" : "not checked")}; " +
            $"watch-list {(id.WatchListed == true ? "hit" : id.WatchListed == false ? "clear" : "unknown")}; " +
            $"fraud risk {(id.FraudRiskScore is { } f ? $"{f} ({id.FraudRiskBand})" : "not checked")}; " +
            $"bank customer {(id.BankCustomerFound == true ? "yes" : id.BankCustomerFound == false ? "no" : "unknown")}.";
        return (new RiskScoreOutput(IdentityAndFraud, score, 0, RuleBasedScoringEngine.DetermineRating(score), rationale, concerns, positives), null);
    }

    // ── Recommendation, lending, conditions ──────────────────────────────────────────────────────

    private static string Recommend(decimal score, int redFlagCount, RecommendationThresholds cfg)
    {
        if (redFlagCount >= cfg.CriticalRedFlagsThreshold || score < cfg.ReferMinScore) return "Decline";
        if (score >= cfg.StrongApproveMinScore && redFlagCount <= cfg.StrongApproveMaxRedFlags) return "StrongApprove";
        if (score >= cfg.ApproveMinScore && redFlagCount <= cfg.ApproveMaxRedFlags) return "Approve";
        if (score >= cfg.ApproveWithConditionsMinScore) return "ApproveWithConditions";
        return "Refer";
    }

    private static (decimal? Amount, int? Tenor) Lend(CustomerAdvisoryRequest r, decimal score, string recommendation, LoanAdjustmentConfig cfg)
    {
        if (!r.ProposalSupplied || recommendation == "Decline") return (null, null);
        var multiplier = score switch
        {
            >= 80 => cfg.Score80PlusMultiplier,
            >= 70 => cfg.Score70PlusMultiplier,
            >= 60 => cfg.Score60PlusMultiplier,
            >= 50 => cfg.Score50PlusMultiplier,
            _ => cfg.BelowScore50Multiplier,
        };
        var amount = Math.Round(r.ProposedAmount * multiplier / 1000m, 0) * 1000m;
        var tenor = score >= cfg.LowScoreThresholdForTenorRestriction ? r.ProposedTenorMonths : Math.Min(r.ProposedTenorMonths, cfg.MaxTenorForLowScores);
        return (amount, tenor);
    }

    private static List<string> Conditions(CustomerAdvisoryRequest r, List<RiskScoreOutput> scores, List<CustomerAdvisoryGap> gaps, List<string> redFlags, decimal overall)
    {
        var c = new List<string>();
        if (r.Exposure.BankOverdueLoans > 0 || (r.Bureau?.DelinquentLoans ?? 0) > 0)
            c.Add("Clear all overdue facilities before disbursement.");
        if (r.Identity.WatchListed == true)
            c.Add("Resolve the watch-list entry with the identity provider before any lending.");
        if (r.Identity.BvnVerified != true)
            c.Add("Verify the customer's identity in person before disbursement.");
        if (gaps.Any(g => g.Category == RepaymentCapacity || g.Category == CashflowStability))
            c.Add("Obtain at least six months of bank statements to establish repayment capacity.");
        if (r.Cashflow is { } cf && cf.WindowMonths < 6)
            c.Add("Obtain at least six months of bank statements before relying on the cashflow assessment.");
        if (!r.ProposalSupplied)
            c.Add("Re-run the check with the actual loan amount and tenor before deciding.");
        if (overall < 65 && scores.Any(s => s.Category == RepaymentCapacity))
            c.Add("Require a guarantor or security for the facility.");
        if (r.Bureau is null)
            c.Add("Obtain a credit bureau report from another bureau before deciding.");
        return c;
    }
}
