using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>
/// Perfection is the Legal Officer's assertion and is only open at their stage: once the case has
/// moved on, the committee has voted on whatever was asserted, so re-opening it would change the
/// basis of a decision already taken.
/// </summary>
public class AssertRhshfCollateralPerfectionHandlerTests
{
    private const decimal TotalEopValue = 51_500_000.00m;

    private static RhshfCreditProfile ProfileAtLegalClearance()
    {
        var profile = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: TotalEopValue, currency: "NGN", farmerCount: 1200,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;

        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments, RhshfProfilingStage.ReviewAndSubmit,
        })
        {
            profile.AdvanceStageForTest(stage);
        }

        profile.AppraiseWithFinancials(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);
        return profile; // InternalStage == LegalClearance
    }

    private static RhshfCollateral Instrument(RhshfCreditProfile profile, bool withDocument = true)
    {
        var collateral = RhshfCollateral.Create(
            profile.Id, profile.CurrentCycleNumber, RhshfCollateralType.BankGuarantee, Guid.NewGuid(), null,
            "BG-2026-01", null, null, "First Bank", 52_000_000m, true, null, null, null, null, null).Value;
        if (withDocument)
            collateral.AddDocument("guarantee.pdf", "application/pdf", "path/g.pdf", 1024);
        return collateral;
    }

    private static AssertRhshfCollateralPerfectionHandler Handler(RhshfCreditProfile profile, RhshfCollateral collateral)
        => new(new FakeProfileRepository(profile), new FakeCollateralRepository(collateral), new FakeUnitOfWork());

    [Fact]
    public async Task Assert_AtLegalClearance_RecordsPerfectionAgainstTheLegalOfficer()
    {
        var profile = ProfileAtLegalClearance();
        var collateral = Instrument(profile);
        var legalOfficerId = Guid.NewGuid();

        var result = await Handler(profile, collateral).Handle(new AssertRhshfCollateralPerfectionCommand(
            profile.Reference, collateral.Id, legalOfficerId, RhshfCollateralPerfectionStatus.Perfected));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCollateralPerfectionStatus.Perfected, collateral.PerfectionStatus);
        Assert.Equal(legalOfficerId, collateral.PerfectedByUserId);
    }

    [Fact]
    public async Task Assert_AfterTheCaseHasMovedOn_IsRefused()
    {
        var profile = ProfileAtLegalClearance();
        var collateral = Instrument(profile);
        profile.AdvanceToRiskReviewFromLegal(Guid.NewGuid()); // now at RiskReview

        var result = await Handler(profile, collateral).Handle(new AssertRhshfCollateralPerfectionCommand(
            profile.Reference, collateral.Id, Guid.NewGuid(), RhshfCollateralPerfectionStatus.Perfected));

        Assert.False(result.IsSuccess);
        Assert.Contains("legal clearance", result.Error);
        Assert.Null(collateral.PerfectionStatus);
    }

    [Fact]
    public async Task Assert_WithoutTheEvidenceOnFile_IsRefused()
    {
        var profile = ProfileAtLegalClearance();
        var collateral = Instrument(profile, withDocument: false);

        var result = await Handler(profile, collateral).Handle(new AssertRhshfCollateralPerfectionCommand(
            profile.Reference, collateral.Id, Guid.NewGuid(), RhshfCollateralPerfectionStatus.Perfected));

        Assert.False(result.IsSuccess);
        Assert.Contains("no document attached", result.Error);
    }

    [Fact]
    public async Task Assert_OnAnInstrumentFromAnotherCase_IsRefused()
    {
        var profile = ProfileAtLegalClearance();
        var other = ProfileAtLegalClearance();
        var collateral = Instrument(other);

        var result = await Handler(profile, collateral).Handle(new AssertRhshfCollateralPerfectionCommand(
            profile.Reference, collateral.Id, Guid.NewGuid(), RhshfCollateralPerfectionStatus.Perfected));

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    private class FakeProfileRepository(RhshfCreditProfile profile) : IRhshfCreditProfileRepository
    {
        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default)
            => Task.FromResult<RhshfCreditProfile?>(profile.Reference == reference ? profile : null);
        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RhshfCreditProfile?>(null);
        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default) => Task.FromResult<RhshfCreditProfile?>(null);
        public Task AddAsync(RhshfCreditProfile p, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<RhshfCreditProfile>> GetAllForListAsync(Guid? branchId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>([]);
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>([]);
        public Task<IReadOnlyList<RhshfReusableDocument>> GetReusableDocumentsForFacAsync(Guid facId, Guid excludeProfileId, RhshfDocumentCategory category, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeCollateralRepository(RhshfCollateral collateral) : IRhshfCollateralRepository
    {
        public Task<IReadOnlyList<RhshfCollateral>> GetByProfileAndCycleAsync(Guid profileId, int cycleNumber, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCollateral>>([collateral]);
        public Task<RhshfCollateral?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<RhshfCollateral?>(collateral.Id == id ? collateral : null);
        public Task AddAsync(RhshfCollateral c, CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(RhshfCollateral c) { }
        public Task<RhshfCollateralDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}
