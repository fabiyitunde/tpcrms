using CRMS.Application.Advisory.Interfaces;
using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>
/// These assert what the advisory is built *from*. The scoring itself lives in the shared
/// IAIAdvisoryService and is not RH-SHF's to test — but the request handed to it is, and it was
/// previously a bureau stub with no score plus an empty financial list, so every credit-history
/// and repayment-capacity score came out of nothing.
/// </summary>
public class GenerateRhshfAdvisoryHandlerTests
{
    private const decimal Eop = 5_000_000.00m;

    // ── Fixtures ───────────────────────────────────────────────────────────

    private static RhshfCreditProfile ProfileAtAppraisal()
    {
        var profile = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: Eop, currency: "NGN", farmerCount: 1200,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;

        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments, RhshfProfilingStage.ReviewAndSubmit,
        })
        {
            // The handler refuses to run until a bureau check has happened, and the domain only
            // accepts one while the case is on that stage.
            if (stage == RhshfProfilingStage.CreditBureauCheck)
                profile.RecordBureauCheck(RhshfBureauOutcome.Cleared, 2, 2, 0, 1_200_000m, 0m, null);

            profile.AdvanceStageForTest(stage);
        }

        return profile;
    }

    private static BureauReport CompletedReport(
        Guid profileId, string subjectName, string partyType, int? score,
        int activeLoans = 2, int delinquent = 0, int maxDelinquencyDays = 0,
        decimal outstanding = 1_200_000m, bool legalActions = false)
    {
        var report = BureauReport.Create(
            CreditBureauProvider.CRC,
            partyType == "RhshfCompany" ? SubjectType.Business : SubjectType.Individual,
            subjectName,
            bvn: partyType == "RhshfCompany" ? null : "12345678901",
            requestedByUserId: Guid.NewGuid(),
            partyType: partyType,
            rhshfCreditProfileId: profileId).Value;

        report.CompleteWithData(
            registryId: "REG-1", creditScore: score, scoreGrade: "AA", reportDate: DateTime.UtcNow,
            rawResponseJson: null, pdfReportBase64: null,
            totalAccounts: activeLoans, activeLoans: activeLoans,
            performingAccounts: activeLoans - delinquent, delinquentFacilities: delinquent,
            closedAccounts: 0, totalOutstandingBalance: outstanding, totalOverdue: delinquent > 0 ? 250_000m : 0,
            totalCreditLimit: 0, maxDelinquencyDays: maxDelinquencyDays, hasLegalActions: legalActions);

        return report;
    }

    private static (GenerateRhshfAdvisoryHandler Handler, CapturingAdvisoryService Ai) MakeHandler(
        RhshfCreditProfile profile, List<BureauReport>? reports = null)
    {
        var ai = new CapturingAdvisoryService();
        var handler = new GenerateRhshfAdvisoryHandler(
            new FakeProfileRepo(profile), new FakeAdvisoryRepo(), new FakeCollateralRepo(),
            new FakeFinancialStatementRepo(), new FakeBureauRepo(reports ?? []), ai, new FakeUow());
        return (handler, ai);
    }

    // ── Bureau mapping ─────────────────────────────────────────────────────

    [Fact]
    public async Task EveryCompletedBureauSubject_BecomesItsOwnInput()
    {
        var profile = ProfileAtAppraisal();
        var reports = new List<BureauReport>
        {
            CompletedReport(profile.Id, "Alliedsoft Limited", "RhshfCompany", 720),
            CompletedReport(profile.Id, "Ngozi Eze", "RhshfDirector", 655),
            CompletedReport(profile.Id, "Sadiq Bello", "RhshfDirector", 590),
        };
        var (handler, ai) = MakeHandler(profile, reports);

        await handler.Handle(new GenerateRhshfAdvisoryCommand(profile.Reference, Guid.NewGuid()));

        Assert.Equal(3, ai.Captured!.BureauReports.Count);
        Assert.Equal([720, 655, 590], ai.Captured.BureauReports.Select(b => b.CreditScore));
        Assert.All(ai.Captured.BureauReports, b => Assert.False(b.IsPlaceholder));
    }

    [Fact]
    public async Task IncompleteBureauReports_AreExcluded_NotPassedAsZeros()
    {
        var profile = ProfileAtAppraisal();
        var failed = BureauReport.Create(
            CreditBureauProvider.CRC, SubjectType.Individual, "Unreachable Director", "12345678901",
            Guid.NewGuid(), partyType: "RhshfDirector", rhshfCreditProfileId: profile.Id).Value;
        failed.MarkFailed("Provider timed out");

        var (handler, ai) = MakeHandler(profile,
            [CompletedReport(profile.Id, "Alliedsoft Limited", "RhshfCompany", 720), failed]);

        await handler.Handle(new GenerateRhshfAdvisoryCommand(profile.Reference, Guid.NewGuid()));

        var input = Assert.Single(ai.Captured!.BureauReports);
        Assert.Equal("Alliedsoft Limited", input.SubjectName);
    }

    [Fact]
    public async Task NoUsableBureauData_IsFlaggedAsAPlaceholder_NotACleanHistory()
    {
        // A zeroed input with IsPlaceholder=false reads to the scorer as "no debt, no delinquency"
        // — the most favourable possible credit history, from an absence of data.
        var profile = ProfileAtAppraisal();
        var (handler, ai) = MakeHandler(profile, []);

        await handler.Handle(new GenerateRhshfAdvisoryCommand(profile.Reference, Guid.NewGuid()));

        var input = Assert.Single(ai.Captured!.BureauReports);
        Assert.True(input.IsPlaceholder);
        Assert.Null(input.CreditScore);
    }

    [Fact]
    public async Task DelinquencyDepthAndFraudSignals_ReachTheScorer()
    {
        var profile = ProfileAtAppraisal();
        var report = CompletedReport(
            profile.Id, "Alliedsoft Limited", "RhshfCompany", 480,
            activeLoans: 4, delinquent: 2, maxDelinquencyDays: 120, legalActions: true);
        report.RecordFraudCheckResults(85, "DECLINE");
        var (handler, ai) = MakeHandler(profile, [report]);

        await handler.Handle(new GenerateRhshfAdvisoryCommand(profile.Reference, Guid.NewGuid()));

        var input = Assert.Single(ai.Captured!.BureauReports);
        Assert.Equal(120, input.MaxDelinquencyDays);
        Assert.True(input.HasLegalActions);
        Assert.Equal(85, input.FraudRiskScore);
        Assert.Equal("DECLINE", input.FraudRecommendation);
        Assert.Equal("Non-Performing", input.WorstStatus);
        Assert.Equal(2, input.DefaultedLoansCount); // past 90 days
    }

    [Fact]
    public async Task DelinquencyUnder90Days_IsNotCountedAsDefaulted()
    {
        var profile = ProfileAtAppraisal();
        var (handler, ai) = MakeHandler(profile,
            [CompletedReport(profile.Id, "Alliedsoft Limited", "RhshfCompany", 610,
                activeLoans: 3, delinquent: 1, maxDelinquencyDays: 45)]);

        await handler.Handle(new GenerateRhshfAdvisoryCommand(profile.Reference, Guid.NewGuid()));

        var input = Assert.Single(ai.Captured!.BureauReports);
        Assert.Equal(0, input.DefaultedLoansCount);
        Assert.Equal(1, input.DelinquentLoansCount);
        Assert.Equal("Watch", input.WorstStatus);
    }

    [Fact]
    public async Task ExistingExposure_IsSummedFromTheBureauReports()
    {
        var profile = ProfileAtAppraisal();
        var (handler, ai) = MakeHandler(profile,
        [
            CompletedReport(profile.Id, "Alliedsoft Limited", "RhshfCompany", 700, activeLoans: 2, outstanding: 3_000_000m),
            CompletedReport(profile.Id, "Ngozi Eze", "RhshfDirector", 640, activeLoans: 1, outstanding: 500_000m),
        ]);

        await handler.Handle(new GenerateRhshfAdvisoryCommand(profile.Reference, Guid.NewGuid()));

        Assert.Equal(3_500_000m, ai.Captured!.ExistingExposure);
        Assert.Equal(3, ai.Captured.ExistingFacilitiesCount);
    }

    // ── Financial mapping ──────────────────────────────────────────────────

    [Fact]
    public async Task SavedAppraisal_BecomesTheFinancialInput_ReadOffTheReportNotRecomputed()
    {
        var profile = ProfileAtAppraisal();
        var officer = Guid.NewGuid();
        profile.SeedViableAppraisal(officer);
        var report = profile.GetCurrentCycleFinancialAppraisal()!;
        var (handler, ai) = MakeHandler(profile);

        await handler.Handle(new GenerateRhshfAdvisoryCommand(profile.Reference, Guid.NewGuid()));

        var input = Assert.Single(ai.Captured!.FinancialStatements);
        Assert.Equal(report.GrossRevenue, input.Revenue);
        Assert.Equal(report.NetReturnToFarmer, input.NetProfit);
        Assert.Equal(report.Dscr, input.DebtServiceCoverageRatio);
        Assert.Equal("Crop cycle", input.YearType);
    }

    [Fact]
    public async Task NoSavedAppraisal_LeavesTheFinancialListEmpty_AndSaysSoInContext()
    {
        var profile = ProfileAtAppraisal();
        var (handler, ai) = MakeHandler(profile);

        await handler.Handle(new GenerateRhshfAdvisoryCommand(profile.Reference, Guid.NewGuid()));

        Assert.Empty(ai.Captured!.FinancialStatements);
        Assert.Contains("No financial appraisal has been saved", ai.Captured.AdditionalContext);
    }

    [Fact]
    public async Task Tenor_ComesFromTheCropCycle_NotZero()
    {
        // Zero told the model the facility had no term at all.
        var profile = ProfileAtAppraisal();
        profile.SeedViableAppraisal(Guid.NewGuid());
        var (handler, ai) = MakeHandler(profile);

        await handler.Handle(new GenerateRhshfAdvisoryCommand(profile.Reference, Guid.NewGuid()));

        Assert.Equal(profile.GetCurrentCycleFinancialAppraisal()!.CycleMonths, ai.Captured!.RequestedTenorMonths);
        Assert.True(ai.Captured.RequestedTenorMonths > 0);
    }

    // ── Narrative context ──────────────────────────────────────────────────

    [Fact]
    public async Task Context_CarriesTheFarmPlanAndViabilityFigures()
    {
        var profile = ProfileAtAppraisal();
        profile.SeedViableAppraisal(Guid.NewGuid());
        var (handler, ai) = MakeHandler(profile);

        await handler.Handle(new GenerateRhshfAdvisoryCommand(profile.Reference, Guid.NewGuid()));

        var context = ai.Captured!.AdditionalContext;
        Assert.Contains("Farm plan:", context);
        Assert.Contains("Maize", context);
        Assert.Contains("DSCR", context);
        Assert.Contains("Break-even sensitivity", context);
    }

    [Fact]
    public async Task Context_SurfacesAnOfficerOverride()
    {
        // The single most decision-relevant fact on an appraisal: a human recommended a facility
        // the numbers reject. Burying it would let the advisory read as an independent second
        // opinion when it is scoring an already-overridden case.
        var profile = ProfileAtAppraisal();
        // The fixture seeds a viable plan; this test needs one that fails the gates.
        profile.ClearFarmPlansForTest();
        profile.AddFarmPlan(RhshfFarmPlan.Create(
            profile.Id, profile.CurrentCycleNumber, "Maize", 10m, 500m, 100m, Guid.NewGuid()).Value);
        profile.SaveFinancialAppraisal(
            preparedByUserId: Guid.NewGuid(), ownProductionCost: 300_000m, harvestAndLogisticsCost: 200_000m,
            cycleMonths: 6, interestRatePercent: 12m, assumptionBasisNote: null,
            thresholds: RhshfAppraisalTestExtensions.DefaultThresholds(),
            recommendation: RhshfCreditRecommendation.Pass, summaryNotes: null,
            overrideJustification: "NIRSAL CRG covers 75% of exposure.");
        var (handler, ai) = MakeHandler(profile);

        await handler.Handle(new GenerateRhshfAdvisoryCommand(profile.Reference, Guid.NewGuid()));

        var context = ai.Captured!.AdditionalContext;
        Assert.Contains("overrode the failing gates", context);
        Assert.Contains("NIRSAL", context);
        Assert.Contains("Viability gates failed:", context);
    }

    [Fact]
    public async Task Context_FlagsDirectorsThatCouldNotBeCreditChecked()
    {
        var profile = ProfileAtAppraisal();
        profile.AddDirector(RhshfDirector.CreateManual(profile.Id, "Ngozi Eze", "12345678901", null, false, null, null).Value);
        profile.AddDirector(RhshfDirector.CreateManual(profile.Id, "Sadiq Bello", null, null, false, null, null).Value);
        var (handler, ai) = MakeHandler(profile);

        await handler.Handle(new GenerateRhshfAdvisoryCommand(profile.Reference, Guid.NewGuid()));

        Assert.Contains("1 have no BVN and were therefore not credit-checked", ai.Captured!.AdditionalContext);
    }

    [Fact]
    public async Task Context_SaysWhenNoFarmPlanExists_RatherThanOmittingIt()
    {
        var profile = ProfileAtAppraisal();
        profile.ClearFarmPlansForTest();
        var (handler, ai) = MakeHandler(profile);

        await handler.Handle(new GenerateRhshfAdvisoryCommand(profile.Reference, Guid.NewGuid()));

        Assert.Contains("No farm plan has been recorded", ai.Captured!.AdditionalContext);
    }

    [Fact]
    public async Task BureauCheckNeverRun_BlocksGenerationEntirely()
    {
        var profile = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "P", programmeName: "P", sessionCode: "S", sessionName: "S",
            facId: Guid.NewGuid(), companyName: "C", rcNumber: "RC1", tin: "T", boaAccountNumber: "0123456789",
            contactEmail: "a@b.com", contactPhone: "+2348012345678", state: "Kano", lga: "N",
            totalEopValue: Eop, currency: "NGN", farmerCount: 10,
            callbackUrl: "https://x.example/cb", certifiedByAdmin: "a@b.com", certifiedAt: DateTime.UtcNow,
            rawSubmissionPayload: "{}", eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;
        var (handler, ai) = MakeHandler(profile);

        var result = await handler.Handle(new GenerateRhshfAdvisoryCommand(profile.Reference, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Null(ai.Captured);
    }

    // ── Fakes ──────────────────────────────────────────────────────────────

    private class CapturingAdvisoryService : IAIAdvisoryService
    {
        public AIAdvisoryRequest? Captured { get; private set; }

        public Task<AIAdvisoryResponse> GenerateAdvisoryAsync(AIAdvisoryRequest request, CancellationToken ct = default)
        {
            Captured = request;
            return Task.FromResult(new AIAdvisoryResponse(
                Success: true, ErrorMessage: null,
                RiskScores: [new RiskScoreOutput("Credit History", 70m, 1m, "Low", "ok", [], [])],
                OverallScore: 70m, OverallRating: "Low", Recommendation: "Approve",
                RecommendedAmount: Eop, RecommendedTenorMonths: 6, RecommendedInterestRate: 12m, MaxExposure: Eop,
                Conditions: [], Covenants: [],
                ExecutiveSummary: "ok", StrengthsAnalysis: "ok", WeaknessesAnalysis: "ok",
                MitigatingFactors: null, KeyRisks: null, RedFlags: []));
        }

        public string GetModelVersion() => "test-model-v1";
    }

    private class FakeProfileRepo : IRhshfCreditProfileRepository
    {
        private readonly RhshfCreditProfile _profile;
        public FakeProfileRepo(RhshfCreditProfile profile) => _profile = profile;

        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<RhshfCreditProfile?>(_profile.Id == id ? _profile : null);
        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default)
            => Task.FromResult<RhshfCreditProfile?>(_profile.Reference == reference ? _profile : null);
        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default)
            => Task.FromResult<RhshfCreditProfile?>(null);
        public Task AddAsync(RhshfCreditProfile profile, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>([]);
        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private class FakeBureauRepo : IBureauReportRepository
    {
        private readonly List<BureauReport> _reports;
        public FakeBureauRepo(List<BureauReport> reports) => _reports = reports;

        public Task<IReadOnlyList<BureauReport>> GetByRhshfCreditProfileIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BureauReport>>(_reports);

        public Task<BureauReport?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<BureauReport?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<BureauReport>> GetByLoanApplicationIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<BureauReport>> GetByLoanApplicationIdWithDetailsAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<BureauReport>> GetByNampApplicationIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<BureauReport>> GetByNampApplicationIdWithDetailsAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<BureauReport>> GetByBVNAsync(string bvn, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<BureauReport?> GetLatestByBVNAsync(string bvn, CreditBureauProvider? provider = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(BureauReport report, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(BureauReport report) { }
        public void Delete(BureauReport report) { }
    }

    private class FakeAdvisoryRepo : IRhshfAdvisoryRepository
    {
        public Task<RhshfAdvisory?> GetByRhshfCreditProfileIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<RhshfAdvisory?>(null);
        public Task<RhshfAdvisory?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<RhshfAdvisory?>(null);
        public Task AddAsync(RhshfAdvisory advisory, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(RhshfAdvisory advisory) { }
    }

    private class FakeCollateralRepo : IRhshfCollateralRepository
    {
        public Task<IReadOnlyList<RhshfCollateral>> GetByProfileAndCycleAsync(Guid id, int cycle, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCollateral>>([]);
        public Task<RhshfCollateral?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(RhshfCollateral collateral, CancellationToken ct = default) => Task.CompletedTask;
        public Task<RhshfCollateralDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeFinancialStatementRepo : IRhshfFinancialStatementRepository
    {
        public Task<IReadOnlyList<RhshfFinancialStatement>> GetByProfileIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfFinancialStatement>>([]);
        public Task<RhshfFinancialStatement?> GetByProfileAndYearAsync(Guid id, int year, CancellationToken ct = default)
            => Task.FromResult<RhshfFinancialStatement?>(null);
        public Task<RhshfFinancialStatement?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<RhshfFinancialStatement?>(null);
        public Task AddAsync(RhshfFinancialStatement statement, CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(RhshfFinancialStatement statement) { }
    }


    private class FakeUow : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}
