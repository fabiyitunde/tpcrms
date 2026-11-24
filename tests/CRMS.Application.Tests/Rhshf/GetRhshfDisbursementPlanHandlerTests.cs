using CRMS.Application.Rhshf.Queries;
using CRMS.Application.Rhshf.Services;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>The disbursement form shows the bank the plan it pays suppliers from, which must be the
/// offer letter's Schedule B exactly.</summary>
public class GetRhshfDisbursementPlanHandlerTests
{
    private static RhshfCreditProfile MakeProfile() => RhshfCreditProfile.Create(
        submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
        sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
        companyName: "Gote Agricultural Holdings", rcNumber: "RC123456", tin: "01234567-0001",
        boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
        state: "Kano", lga: "Nassarawa", totalEopValue: 5_000_000m, currency: "NGN", farmerCount: 50,
        callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
        certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
        eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;

    [Fact]
    public async Task The_plan_is_the_offer_letters_schedule_B()
    {
        var profile = MakeProfile();
        var handler = new GetRhshfDisbursementPlanHandler(new FakeProfileRepository(profile));

        var result = await handler.Handle(new GetRhshfDisbursementPlanQuery(profile.Reference));

        Assert.True(result.IsSuccess);
        var expected = RhshfOfferDocumentDataBuilder.BuildPlanRows(profile, profile.TotalEopValue);
        Assert.Equal(expected, result.Data);
        Assert.Equal(profile.TotalEopValue, result.Data!.Sum(r => r.Amount));
    }

    [Fact]
    public async Task An_unknown_case_fails()
    {
        var handler = new GetRhshfDisbursementPlanHandler(new FakeProfileRepository(MakeProfile()));

        var result = await handler.Handle(new GetRhshfDisbursementPlanQuery("RHSHF-NOPE"));

        Assert.False(result.IsSuccess);
    }

    private class FakeProfileRepository(RhshfCreditProfile profile) : IRhshfCreditProfileRepository
    {
        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RhshfCreditProfile?>(profile);
        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default)
            => Task.FromResult(profile.Reference == reference ? profile : null);
        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(RhshfCreditProfile p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RhshfCreditProfile>> GetAllForListAsync(Guid? branchId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RhshfReusableDocument>> GetReusableDocumentsForFacAsync(Guid facId, Guid excludeProfileId, RhshfDocumentCategory category, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
