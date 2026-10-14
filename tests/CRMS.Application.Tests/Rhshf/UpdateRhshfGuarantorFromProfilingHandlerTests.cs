using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>
/// Editing a guarantor from the FAC's profiling form, which previously could only add and remove one.
///
/// Two things are load-bearing and neither is obvious from the happy path: the guarantor id is posted
/// from the browser and the staff-side update command carries no profile scoping, so this handler has
/// to prove the guarantor belongs to the case in the token; and the FAC's form has no inputs for the
/// contact fields, so a full-replace update would wipe what staff recorded.
/// </summary>
public class UpdateRhshfGuarantorFromProfilingHandlerTests
{
    [Fact]
    public async Task UpdatesTheFieldsTheFormShows()
    {
        var profile = MakeProfile();
        var guarantor = Guarantor(profile.Id);

        var result = await Handler(profile, guarantor).Handle(new UpdateRhshfGuarantorFromProfilingCommand(
            profile.Reference, guarantor.Id, RhshfGuarantorType.Individual, "Ada Surety-Bello",
            null, null, RhshfGuarantorRelationship.Shareholder, null, 2_500_000m));

        Assert.True(result.IsSuccess);
        Assert.Equal("Ada Surety-Bello", guarantor.FullName);
        Assert.Equal(RhshfGuarantorRelationship.Shareholder, guarantor.RelationshipCategory);
        Assert.Equal(2_500_000m, guarantor.GuaranteeAmount);
    }

    [Fact]
    public async Task LeavesTheStaffRecordedContactFieldsAlone()
    {
        // The FAC's form has no phone/email/address/notes inputs. A full-replace update would post
        // nulls for all four and silently erase an officer's work.
        var profile = MakeProfile();
        var guarantor = Guarantor(profile.Id);
        guarantor.Update(
            RhshfGuarantorType.Individual, "Ada Surety", "12345678901", null,
            RhshfGuarantorRelationship.Director, null,
            "+2348031112222", "ada@surety.test", "14 Market Rd, Kano", 1_000_000m, "Spoke to her on 12 Oct.");

        await Handler(profile, guarantor).Handle(new UpdateRhshfGuarantorFromProfilingCommand(
            profile.Reference, guarantor.Id, RhshfGuarantorType.Individual, "Ada Surety",
            null, null, RhshfGuarantorRelationship.Director, null, 1_000_000m));

        Assert.Equal("+2348031112222", guarantor.PhoneNumber);
        Assert.Equal("ada@surety.test", guarantor.Email);
        Assert.Equal("14 Market Rd, Kano", guarantor.Address);
        Assert.Equal("Spoke to her on 12 Oct.", guarantor.Notes);
    }

    [Fact]
    public async Task ABlankBvn_KeepsTheOneOnFile()
    {
        // The number is never sent to the browser (presence only), so the form has nothing to send
        // back — and posting null would wipe the key the bureau check runs on.
        var profile = MakeProfile();
        var guarantor = Guarantor(profile.Id);

        await Handler(profile, guarantor).Handle(new UpdateRhshfGuarantorFromProfilingCommand(
            profile.Reference, guarantor.Id, RhshfGuarantorType.Individual, "Ada Surety",
            null, null, RhshfGuarantorRelationship.Director, null, 1_000_000m));

        Assert.Equal("12345678901", guarantor.Bvn);
    }

    [Fact]
    public async Task ANewlyTypedBvn_Replaces()
    {
        var profile = MakeProfile();
        var guarantor = Guarantor(profile.Id);

        await Handler(profile, guarantor).Handle(new UpdateRhshfGuarantorFromProfilingCommand(
            profile.Reference, guarantor.Id, RhshfGuarantorType.Individual, "Ada Surety",
            "99999999999", null, RhshfGuarantorRelationship.Director, null, 1_000_000m));

        Assert.Equal("99999999999", guarantor.Bvn);
    }

    [Fact]
    public async Task Refuses_AGuarantorBelongingToAnotherCase()
    {
        // Profiling is token-authenticated against one case, and the id comes from the browser. The
        // staff-side command has no profile scoping at all, so this check is the only thing standing
        // between a valid token for one case and another case's guarantor.
        var profile = MakeProfile();
        var someoneElses = Guarantor(Guid.NewGuid());

        var result = await Handler(profile, someoneElses).Handle(new UpdateRhshfGuarantorFromProfilingCommand(
            profile.Reference, someoneElses.Id, RhshfGuarantorType.Individual, "Renamed By Attacker",
            null, null, RhshfGuarantorRelationship.Director, null, 999m));

        Assert.False(result.IsSuccess);
        Assert.Contains("not found on this case", result.Error!);
        Assert.Equal("Ada Surety", someoneElses.FullName);
    }

    [Fact]
    public async Task Refuses_WhenTheDomainRulesReject()
    {
        var profile = MakeProfile();
        var guarantor = Guarantor(profile.Id);

        var result = await Handler(profile, guarantor).Handle(new UpdateRhshfGuarantorFromProfilingCommand(
            profile.Reference, guarantor.Id, RhshfGuarantorType.Individual, "Ada Surety",
            null, null, RhshfGuarantorRelationship.Other, null, 1_000_000m));

        Assert.False(result.IsSuccess);
        Assert.Contains("State the relationship", result.Error!);
    }

    // ── Fixtures ──────────────────────────────────────────────────────────────────────────────────

    private static RhshfGuarantor Guarantor(Guid profileId) => RhshfGuarantor.Create(
        profileId, RhshfGuarantorType.Individual, "Ada Surety", "12345678901", null,
        RhshfGuarantorRelationship.Director, null, null, null, null, 1_000_000m, null).Value;

    private static RhshfCreditProfile MakeProfile() => RhshfCreditProfile.Create(
        submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
        sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
        companyName: "Bunkure Agro Aggregators Limited", rcNumber: "RC771204", tin: null,
        boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
        state: "Kano", lga: "Bunkure", totalEopValue: 18_750_000.00m, currency: "NGN", farmerCount: 480,
        callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
        certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
        eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;

    private static UpdateRhshfGuarantorFromProfilingHandler Handler(
        RhshfCreditProfile profile, RhshfGuarantor guarantor)
        => new(new FakeProfileRepo(profile), new FakeGuarantorRepo(guarantor), new FakeUow());

    private class FakeProfileRepo : IRhshfCreditProfileRepository
    {
        private readonly RhshfCreditProfile _profile;
        public FakeProfileRepo(RhshfCreditProfile profile) => _profile = profile;

        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default)
            => Task.FromResult<RhshfCreditProfile?>(_profile.Reference == reference ? _profile : null);
        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<RhshfCreditProfile?>(null);
        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default)
            => Task.FromResult<RhshfCreditProfile?>(null);
        public Task AddAsync(RhshfCreditProfile profile, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>([]);
        public Task<IReadOnlyList<RhshfCreditProfile>> GetAllForListAsync(Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>([]);
        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<RhshfReusableDocument>> GetReusableDocumentsForFacAsync(Guid facId, Guid excludeProfileId, RhshfDocumentCategory category, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private class FakeGuarantorRepo : IRhshfGuarantorRepository
    {
        private readonly RhshfGuarantor _guarantor;
        public FakeGuarantorRepo(RhshfGuarantor guarantor) => _guarantor = guarantor;

        public Task<RhshfGuarantor?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<RhshfGuarantor?>(_guarantor.Id == id ? _guarantor : null);
        public Task<IReadOnlyList<RhshfGuarantor>> GetByProfileIdAsync(Guid rhshfCreditProfileId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfGuarantor>>([_guarantor]);
        public Task AddAsync(RhshfGuarantor guarantor, CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(RhshfGuarantor guarantor) { }
    }

    private class FakeUow : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}
