using System.Text.RegularExpressions;
using CRMS.Application.Advisory.Interfaces;
using CRMS.Domain.Configuration;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.AIServices;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>The customer credit check's advisory (docs/Customer_Credit_Check.md, aiAdvisory): the individual
/// scoring profile, the not-assessed handling, and the prompt that leaves the system.</summary>
public class CustomerCheckAdvisoryTests
{
    private static readonly ScoringConfiguration Config = new();
    private readonly CustomerCheckScoringEngine _engine = new();

    private static CustomerAdvisoryRequest Full(
        int? creditScore = 680, int delinquent = 0, int writtenOff = 0, int maxDays = 0,
        decimal inflow = 400_000m, decimal outflow = 300_000m, int bounced = 0, int negativeDays = 0, int activeMonths = 12,
        bool? bvnVerified = true, bool? watchListed = false, int? fraudScore = 20, bool? bankCustomer = true,
        int overdueLoans = 0, int paidLoans = 2, decimal instalments = 0m, bool supplied = true, decimal amount = 500_000m, int tenor = 12)
        => new(
            Guid.NewGuid(), amount, tenor, supplied,
            new CustomerBureauInput("FIRST_CENTRAL", false, creditScore, 5, 2, 3, 2, delinquent, writtenOff, 1_840_000m, delinquent > 0 ? 215_000m : 0m, maxDays, 1),
            new CustomerCashflowInput(12, 2, 87, activeMonths, inflow, outflow, inflow - outflow, 0.2m, bounced, negativeDays, 142_000m),
            new CustomerExposureInput(true, 1, overdueLoans, paidLoans, 0, 1_625_000m, 0m, 0, instalments, 2, 1, 1_625_000m),
            new CustomerIdentityInput(bvnVerified, watchListed, fraudScore, fraudScore is null ? null : fraudScore < 30 ? "LOW" : fraudScore < 70 ? "MEDIUM" : "HIGH", bankCustomer, "2", 92));

    [Fact]
    public void A_clean_customer_scores_all_four_categories_with_the_profile_weights()
    {
        var r = _engine.CalculateScores(Full(), Config);

        Assert.Equal(4, r.Scores.Count);
        Assert.Empty(r.NotAssessed);
        Assert.Equal(["CreditHistory", "RepaymentCapacity", "CashflowStability", "IdentityAndFraud"], r.Scores.Select(s => s.Category));
        Assert.Equal([0.40m, 0.25m, 0.20m, 0.15m], r.Scores.Select(s => s.Weight));
        Assert.All(r.Scores, s => Assert.InRange(s.Score, 0, 100));
        Assert.InRange(r.OverallScore, 65, 100);
        Assert.Contains(r.Recommendation, new[] { "Approve", "StrongApprove" });
        Assert.Empty(r.RedFlags);
        Assert.NotNull(r.RecommendedAmount);
        Assert.Equal(12, r.RecommendedTenorMonths);
        var expected = Math.Round(r.Scores.Sum(s => s.Score * s.Weight) / r.Scores.Sum(s => s.Weight), 2);
        Assert.Equal(expected, r.OverallScore);
    }

    [Fact]
    public void Without_statements_capacity_and_cashflow_are_not_assessed_and_the_rest_are_reweighted()
    {
        var r = _engine.CalculateScores(Full() with { Cashflow = null }, Config);

        Assert.Equal(2, r.Scores.Count);
        Assert.Equal(2, r.NotAssessed.Count);
        Assert.Contains(r.NotAssessed, g => g.Category == "RepaymentCapacity" && g.Weight == 0.25m && g.Reason.Contains("statement"));
        Assert.Contains(r.NotAssessed, g => g.Category == "CashflowStability" && g.Weight == 0.20m);
        var expected = Math.Round(r.Scores.Sum(s => s.Score * s.Weight) / 0.55m, 2);
        Assert.Equal(expected, r.OverallScore);
        Assert.Contains(r.Conditions, c => c.Contains("six months of bank statements"));
    }

    [Fact]
    public void Nothing_to_assess_yields_no_scores()
    {
        var empty = new CustomerAdvisoryRequest(Guid.NewGuid(), 100_000m, 12, false, null, null,
            new CustomerExposureInput(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null), new CustomerIdentityInput(null, null, null, null, null, null, null));

        var r = _engine.CalculateScores(empty, Config);

        Assert.Empty(r.Scores);
        Assert.Equal(4, r.NotAssessed.Count);
        Assert.Equal("Refer", r.Recommendation);
    }

    [Fact]
    public void Watch_list_overdue_and_high_fraud_add_up_to_a_decline()
    {
        var r = _engine.CalculateScores(Full(delinquent: 1, maxDays: 94, watchListed: true, fraudScore: 85, overdueLoans: 1), Config);

        Assert.Equal("Decline", r.Recommendation);
        Assert.True(r.RedFlags.Count >= 3);
        Assert.Contains(r.RedFlags, f => f.Contains("watch-listed"));
        Assert.Contains(r.RedFlags, f => f.Contains("high risk"));
        Assert.Contains(r.Conditions, c => c.StartsWith("Clear all overdue"));
        Assert.Contains(r.Conditions, c => c.Contains("watch-list"));
        Assert.Null(r.RecommendedAmount);
    }

    [Fact]
    public void Repayment_capacity_follows_the_obligation_ratio_bands()
    {
        // Instalment on 500k over 12 months at 18% flat = (500k + 90k) / 12 = 49,166.67.
        Assert.Equal(49_166.67m, CustomerCheckScoringEngine.Instalment(500_000m, 12, 18m));

        var comfortable = _engine.CalculateScores(Full(inflow: 400_000m), Config).Scores.Single(s => s.Category == "RepaymentCapacity");
        Assert.Equal(Config.CustomerCheck.ComfortableScore, comfortable.Score);
        Assert.Contains(comfortable.PositiveIndicators, p => p.Contains("12%"));

        var stretched = _engine.CalculateScores(Full(inflow: 100_000m), Config).Scores.Single(s => s.Category == "RepaymentCapacity");
        Assert.Equal(Config.CustomerCheck.StretchedScore, stretched.Score);

        var beyond = _engine.CalculateScores(Full(inflow: 50_000m, instalments: 20_000m), Config);
        Assert.Equal(Config.CustomerCheck.BeyondCriticalScore, beyond.Scores.Single(s => s.Category == "RepaymentCapacity").Score);
        Assert.Contains(beyond.RedFlags, f => f.StartsWith("Repayments would take"));

        var assumed = _engine.CalculateScores(Full(inflow: 400_000m, supplied: false), Config);
        Assert.Equal(Config.CustomerCheck.ComfortableScore - Config.CustomerCheck.AssumedProposalPenalty,
            assumed.Scores.Single(s => s.Category == "RepaymentCapacity").Score);
        Assert.Null(assumed.RecommendedAmount);
        Assert.Contains(assumed.Conditions, c => c.Contains("actual loan amount"));
    }

    [Fact]
    public void Credit_history_covers_the_bank_relationship_when_there_is_no_bureau_record()
    {
        var r = _engine.CalculateScores(Full() with { Bureau = null }, Config);

        var credit = r.Scores.Single(s => s.Category == "CreditHistory");
        Assert.Contains(credit.RedFlags, c => c.Contains("No credit bureau holds a record"));
        Assert.Contains(credit.PositiveIndicators, p => p.Contains("repaid in full"));
        Assert.Contains(r.Conditions, c => c.Contains("another bureau"));

        var none = _engine.CalculateScores(Full() with { Bureau = null, Exposure = new CustomerExposureInput(true, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null) }, Config);
        Assert.Contains(none.NotAssessed, g => g.Category == "CreditHistory");
    }

    [Fact]
    public void Cashflow_penalises_bounces_negative_days_and_quiet_months()
    {
        var clean = _engine.CalculateScores(Full(), Config).Scores.Single(s => s.Category == "CashflowStability").Score;
        var rough = _engine.CalculateScores(Full(bounced: 2, negativeDays: 12, activeMonths: 4), Config);
        var score = rough.Scores.Single(s => s.Category == "CashflowStability").Score;

        Assert.True(score < clean);
        Assert.Contains(rough.RedFlags, f => f.Contains("failed or reversed"));
        Assert.Contains(rough.RedFlags, f => f.Contains("below zero"));
        Assert.Contains(rough.Scores.Single(s => s.Category == "CashflowStability").RedFlags, c => c.Contains("4 of 12 months"));
    }

    // ── Prompt ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_prompt_carries_figures_and_flags_only()
    {
        var request = Full();
        var result = _engine.CalculateScores(request, Config);

        var prompt = LLMNarrativeGenerator.BuildCustomerUserPrompt(request, result);

        Assert.DoesNotMatch(new Regex(@"\d{10,}"), prompt);
        Assert.DoesNotContain(request.ReferenceId.ToString(), prompt);
        Assert.Contains("CreditHistory", prompt);
        Assert.Contains("RepaymentCapacity", prompt);
        Assert.Contains("FINAL - DO NOT CHANGE", prompt);
        Assert.Contains("NGN 500,000 over 12 months", prompt);
        Assert.Contains("BVN verified: yes", prompt);
        Assert.Contains("Never refer to the person by name", LLMNarrativeGenerator.BuildCustomerSystemPrompt());
    }

    [Fact]
    public async Task The_hybrid_service_uses_the_llm_narrative_when_it_answers_and_the_template_when_it_does_not()
    {
        var llm = new Mock<ILLMService>();
        llm.Setup(l => l.CompleteAsJsonAsync<LLMNarrativeGenerator.NarrativeResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMNarrativeGenerator.NarrativeResult { ExecutiveSummary = "From the model.", StrengthsAnalysis = "S", WeaknessesAnalysis = "W", SuggestedConditions = ["Obtain payslips."] });
        var generator = new LLMNarrativeGenerator(llm.Object, NullLogger<LLMNarrativeGenerator>.Instance);
        var parameters = new Mock<IScoringParameterRepository>();
        parameters.Setup(p => p.GetAllActiveAsLookupAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, decimal>());
        var configService = new Domain.Services.ScoringConfigurationService(parameters.Object);

        var withLlm = new HybridAIAdvisoryService(configService, new RuleBasedScoringEngine(),
            Microsoft.Extensions.Options.Options.Create(new AIAdvisorySettings { UseLLMNarrative = true }),
            NullLogger<HybridAIAdvisoryService>.Instance, generator, _engine);
        var a = await withLlm.GenerateCustomerAdvisoryAsync(Full());
        Assert.True(a.Success);
        Assert.True(a.NarrativeFromLlm);
        Assert.Equal("From the model.", a.ExecutiveSummary);
        Assert.Contains("Obtain payslips.", a.Conditions);
        Assert.Equal(4, a.Scores.Count);

        var withoutLlm = new HybridAIAdvisoryService(configService, new RuleBasedScoringEngine(),
            Microsoft.Extensions.Options.Options.Create(new AIAdvisorySettings { UseLLMNarrative = false }),
            NullLogger<HybridAIAdvisoryService>.Instance, null, _engine);
        var b = await withoutLlm.GenerateCustomerAdvisoryAsync(Full());
        Assert.True(b.Success);
        Assert.False(b.NarrativeFromLlm);
        Assert.Contains("Overall score", b.ExecutiveSummary);
        Assert.Contains("a bureau report", b.ExecutiveSummary);

        llm.Setup(l => l.CompleteAsJsonAsync<LLMNarrativeGenerator.NarrativeResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        var failing = await withLlm.GenerateCustomerAdvisoryAsync(Full());
        Assert.True(failing.Success);
        Assert.False(failing.NarrativeFromLlm);
        Assert.Equal(a.OverallScore, failing.OverallScore);
    }
}
