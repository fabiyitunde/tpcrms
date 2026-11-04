using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.Services;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>
/// Reason codes for returns and declines (CRMSQA#1): an administrator's list with defaults, not a
/// list hard-coded or left waiting on BOA. The code is checked against the list as configured now.
/// </summary>
public class RhshfReasonCodeTests
{
    private static RhshfReasonCode Code(string code, RhshfReasonCodeType type, bool active = true)
    {
        var created = RhshfReasonCode.Create(code, type, code.ToLowerInvariant(), 10).Value;
        if (!active)
            created.Update(created.Label, 10, isActive: false);
        return created;
    }

    private static FakeReasonCodes Repo() => new(
        Code("DOCUMENT_MISSING", RhshfReasonCodeType.Correction),
        Code("ADVERSE_CREDIT_HISTORY", RhshfReasonCodeType.Decline),
        Code("RETIRED_REASON", RhshfReasonCodeType.Decline, active: false),
        Code(RhshfReasonCode.Other, RhshfReasonCodeType.Correction));

    [Fact]
    public async Task A_configured_code_of_the_right_type_is_recorded_normalised()
    {
        var result = await RhshfReasonCodeRules.ResolveAsync(Repo(), " document_missing ", RhshfReasonCodeType.Correction);

        Assert.Equal("DOCUMENT_MISSING", result.Value);
    }

    [Fact]
    public async Task No_code_means_OTHER()
        => Assert.Equal("OTHER", (await RhshfReasonCodeRules.ResolveAsync(Repo(), null, RhshfReasonCodeType.Decline)).Value);

    [Fact]
    public async Task OTHER_serves_declines_as_well_as_returns()
        => Assert.True((await RhshfReasonCodeRules.ResolveAsync(Repo(), "OTHER", RhshfReasonCodeType.Decline)).IsSuccess);

    [Theory]
    [InlineData("NOT_A_CODE", RhshfReasonCodeType.Correction, "not a configured reason")]
    [InlineData("RETIRED_REASON", RhshfReasonCodeType.Decline, "switched off")]
    [InlineData("DOCUMENT_MISSING", RhshfReasonCodeType.Decline, "not for declining")]
    [InlineData("ADVERSE_CREDIT_HISTORY", RhshfReasonCodeType.Correction, "not for sending it back")]
    public async Task Unknown_switched_off_or_wrong_type_codes_are_refused(string code, RhshfReasonCodeType type, string message)
    {
        var result = await RhshfReasonCodeRules.ResolveAsync(Repo(), code, type);

        Assert.True(result.IsFailure);
        Assert.Contains(message, result.Error);
    }

    [Fact]
    public void The_pick_list_is_the_active_codes_of_the_type_with_OTHER_last()
    {
        var all = new[]
        {
            Code(RhshfReasonCode.Other, RhshfReasonCodeType.Correction),
            Code("ADVERSE_CREDIT_HISTORY", RhshfReasonCodeType.Decline),
            Code("RETIRED_REASON", RhshfReasonCodeType.Decline, active: false),
            Code("DOCUMENT_MISSING", RhshfReasonCodeType.Correction),
        };

        Assert.Equal(["ADVERSE_CREDIT_HISTORY", "OTHER"],
            RhshfReasonCodeRules.PickList(all, RhshfReasonCodeType.Decline).Select(c => c.Code));
    }

    [Theory]
    [InlineData("document missing")]
    [InlineData("9_STARTS_WITH_A_DIGIT")]
    [InlineData("X")]
    public void A_code_must_be_capitals_digits_and_underscores(string code)
        => Assert.True(RhshfReasonCode.Create(code, RhshfReasonCodeType.Correction, "x", 1).IsFailure);

    [Fact]
    public void OTHER_cannot_be_switched_off()
        => Assert.True(Code(RhshfReasonCode.Other, RhshfReasonCodeType.Correction).Update("Other", 1, isActive: false).IsFailure);

    [Fact]
    public async Task An_administrator_can_reword_and_switch_off_but_not_retype_a_code()
    {
        var repo = Repo();
        var handler = new SaveRhshfReasonCodeHandler(repo, new FakeUnitOfWork());

        var reworded = await handler.Handle(new SaveRhshfReasonCodeCommand("DOCUMENT_MISSING", RhshfReasonCodeType.Correction, "A document is missing", 5, false, "admin"));
        var retyped = await handler.Handle(new SaveRhshfReasonCodeCommand("DOCUMENT_MISSING", RhshfReasonCodeType.Decline, "x", 5, true, "admin"));
        var added = await handler.Handle(new SaveRhshfReasonCodeCommand("site_visit_failed", RhshfReasonCodeType.Decline, "Site visit failed", 90, true, "admin"));

        Assert.True(reworded.IsSuccess);
        var doc = await repo.GetByCodeAsync("DOCUMENT_MISSING");
        Assert.Equal("A document is missing", doc!.Label);
        Assert.False(doc.IsActive);
        Assert.False(retyped.IsSuccess);
        Assert.True(added.IsSuccess);
        Assert.NotNull(await repo.GetByCodeAsync("SITE_VISIT_FAILED"));
    }

    [Fact]
    public async Task Appraisal_records_the_picked_reason_on_the_return()
    {
        var profile = AtAppraisal();
        var handler = new AppraiseRhshfCaseHandler(new SingleProfileRepository(profile), new FakeUnitOfWork(), Repo());

        var result = await handler.Handle(new AppraiseRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfAppraisalOutcome.ReturnToFac, "Upload the CAC certificate", RhshfProfilingStage.SupportingDocuments,
            "DOCUMENT_MISSING"));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("DOCUMENT_MISSING", profile.GetOutstandingReturn()!.ReasonCode);
    }

    [Fact]
    public async Task Appraisal_refuses_a_correction_reason_for_a_decline()
    {
        var profile = AtAppraisal();
        var handler = new AppraiseRhshfCaseHandler(new SingleProfileRepository(profile), new FakeUnitOfWork(), Repo());

        var result = await handler.Handle(new AppraiseRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfAppraisalOutcome.Decline, "No", null, "DOCUMENT_MISSING"));

        Assert.False(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.UnderReview, profile.Status);
    }

    private static RhshfCreditProfile AtAppraisal()
    {
        var profile = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: 1_000_000m, currency: "NGN", farmerCount: 10,
            callbackUrl: "https://portal.example.gov.ng/cb", certifiedByAdmin: null, certifiedAt: null,
            rawSubmissionPayload: "{}", eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;
        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments, RhshfProfilingStage.ReviewAndSubmit,
        })
            profile.AdvanceStageForTest(stage);
        return profile;
    }

    private class FakeReasonCodes : IRhshfReasonCodeRepository
    {
        private readonly List<RhshfReasonCode> _codes;
        public FakeReasonCodes(params RhshfReasonCode[] codes) => _codes = [.. codes];
        public Task<IReadOnlyList<RhshfReasonCode>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfReasonCode>>(_codes);
        public Task<RhshfReasonCode?> GetByCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult(_codes.FirstOrDefault(c => c.Code == code.Trim().ToUpperInvariant()));
        public Task AddAsync(RhshfReasonCode reasonCode, CancellationToken ct = default)
        {
            _codes.Add(reasonCode);
            return Task.CompletedTask;
        }
    }

    private class SingleProfileRepository : IRhshfCreditProfileRepository
    {
        private readonly RhshfCreditProfile _profile;
        public SingleProfileRepository(RhshfCreditProfile profile) => _profile = profile;
        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RhshfCreditProfile?>(_profile);
        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default) => Task.FromResult<RhshfCreditProfile?>(_profile);
        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default) => Task.FromResult<RhshfCreditProfile?>(_profile);
        public Task AddAsync(RhshfCreditProfile profile, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<RhshfCreditProfile>> GetAllForListAsync(Guid? branchId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>([]);
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>([]);
        public Task<IReadOnlyList<RhshfReusableDocument>> GetReusableDocumentsForFacAsync(Guid facId, Guid excludeProfileId, RhshfDocumentCategory category, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}
