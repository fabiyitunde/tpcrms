using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

/// <summary>
/// The declared shareholding total, capped at 100% across the whole case.
///
/// The old rule was per-director only (0-100 on one row), so two directors could declare 60% and
/// 50% and the case would total 110% — a single director cannot see its siblings' holdings, which is
/// why the check belongs on the aggregate.
///
/// Three behaviours are deliberate and asserted here: the edited director is excluded from the total
/// it is checked against; imports are not capped; and an under-allocated total is not an error,
/// because this screen captures directors rather than a complete ownership register.
/// </summary>
public class RhshfShareholdingTotalTests
{
    [Fact]
    public void AddDeclaredDirector_WouldExceed100_IsRefused()
    {
        // The portal team's example: 60% declared, then another at 50%.
        var profile = ProfileWithDirectors(60m);

        var result = profile.AddDeclaredDirector(Manual(profile.Id, "Second Director", 50m));

        Assert.True(result.IsFailure);
        Assert.Contains("110%", result.Error);
        Assert.Contains("cannot exceed 40%", result.Error);
        Assert.Single(profile.Directors);
    }

    [Fact]
    public void AddDeclaredDirector_ExactlyReaching100_IsAllowed()
    {
        var profile = ProfileWithDirectors(60m);

        var result = profile.AddDeclaredDirector(Manual(profile.Id, "Second Director", 40m));

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, profile.DeclaredShareholdingTotal);
    }

    [Fact]
    public void AddDeclaredDirector_WithNoDeclaredHolding_IsAllowedEvenAt100()
    {
        // A null holding is unknown, not zero — a director with no percentage on record must still be
        // addable, or a fully-allocated case could never record its remaining board members.
        var profile = ProfileWithDirectors(100m);

        var result = profile.AddDeclaredDirector(Manual(profile.Id, "Unstated Holding", null));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, profile.Directors.Count);
    }

    [Fact]
    public void UpdateDirectorShareholding_ExcludesTheDirectorBeingEdited()
    {
        // Lowering 60% to 55% must not be refused against its own current 60%. This is the bug a
        // naive "sum everything and compare" implementation produces.
        var profile = ProfileWithDirectors(60m, 40m);
        var first = profile.Directors[0];

        var result = profile.UpdateDirectorShareholding(first.Id, 55m);

        Assert.True(result.IsSuccess);
        Assert.Equal(55m, first.ShareholdingPercent);
        Assert.Equal(95m, profile.DeclaredShareholdingTotal);
    }

    [Fact]
    public void UpdateDirectorShareholding_RaisingPastTheRemainder_IsRefused()
    {
        var profile = ProfileWithDirectors(60m, 40m);
        var first = profile.Directors[0];

        var result = profile.UpdateDirectorShareholding(first.Id, 70m);

        Assert.True(result.IsFailure);
        Assert.Contains("cannot exceed 60%", result.Error);
        Assert.Equal(60m, first.ShareholdingPercent);
    }

    [Fact]
    public void UpdateDirectorShareholding_UnknownDirector_IsRefused()
    {
        var profile = ProfileWithDirectors(60m);

        var result = profile.UpdateDirectorShareholding(Guid.NewGuid(), 10m);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public void ImportDirector_IsNotCapped()
    {
        // The Bank of Agriculture register carries its own percentages. Refusing them at the door
        // would strand the import halfway through the director list with no way forward, so an
        // over-allocated total is allowed in and caught at submission instead.
        var profile = ProfileWithDirectors(60m);

        profile.ImportDirector(Manual(profile.Id, "From Core Banking", 50m));

        Assert.Equal(110m, profile.DeclaredShareholdingTotal);
        Assert.True(profile.IsShareholdingOverAllocated);
    }

    [Fact]
    public void RemainingShareholding_NeverGoesNegative_OnAnOverAllocatedCase()
    {
        var profile = ProfileWithDirectors(60m);
        profile.ImportDirector(Manual(profile.Id, "From Core Banking", 50m));

        Assert.Equal(0m, profile.RemainingShareholding());
    }

    [Fact]
    public void Submit_IsBlocked_WhenAnImportLeftTheTotalOver100()
    {
        var profile = ReadyToSubmit(60m);
        profile.ImportDirector(WithBvn(Manual(profile.Id, "From Core Banking", 50m)));

        var result = profile.AdvanceStage(RhshfProfilingStage.ReviewAndSubmit);

        Assert.True(result.IsFailure);
        Assert.Contains("110%", result.Error);
        Assert.Contains("more than 100%", result.Error);
    }

    [Fact]
    public void Submit_IsAllowed_WhenTheTotalIsUnder100()
    {
        // Not a gate: non-director shareholders need not be listed, and nothing attests that this
        // list is the complete register. Blocking here would push a FAC to inflate a director to
        // 100%, which is worse data than an honest 85%.
        var profile = ReadyToSubmit(85m);

        var result = profile.AdvanceStage(RhshfProfilingStage.ReviewAndSubmit);

        Assert.True(result.IsSuccess);
        Assert.Equal(85m, profile.DeclaredShareholdingTotal);
    }

    // ── Fixtures ──────────────────────────────────────────────────────────────────────────────────

    private static RhshfDirector Manual(Guid profileId, string name, decimal? percent) =>
        RhshfDirector.CreateManual(profileId, name, null, percent, isChairman: false, null, null).Value;

    private static RhshfDirector WithBvn(RhshfDirector director)
    {
        director.UpdateBvn("12345678901");
        return director;
    }

    private static RhshfCreditProfile MakeProfile() => RhshfCreditProfile.Create(
        submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
        sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
        companyName: "Bunkure Agro Aggregators Limited", rcNumber: "RC771204", tin: "20771204-0001",
        boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
        state: "Kano", lga: "Bunkure", totalEopValue: 18_750_000.00m, currency: "NGN", farmerCount: 480,
        callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
        certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
        eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;

    private static RhshfCreditProfile ProfileWithDirectors(params decimal?[] percentages)
    {
        var profile = MakeProfile();
        for (var i = 0; i < percentages.Length; i++)
            profile.ImportDirector(Manual(profile.Id, $"Director {i + 1}", percentages[i]));
        return profile;
    }

    /// <summary>A case sitting on ReviewAndSubmit with everything else the submit gate wants: a BVN on
    /// every director and at least one crop in the farm plan.</summary>
    private static RhshfCreditProfile ReadyToSubmit(decimal declaredPercent)
    {
        var profile = MakeProfile();
        profile.ImportDirector(WithBvn(Manual(profile.Id, "Director 1", declaredPercent)));

        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);
        profile.AdvanceStage(RhshfProfilingStage.CreditBureauCheck);
        // Stage-gated: the farm plan can only be recorded once the case is actually on EopReview.
        profile.AddFarmPlanDuringProfiling("Maize", 120m, 2_500m, 450m);
        profile.AdvanceStage(RhshfProfilingStage.EopReview);
        profile.AdvanceStage(RhshfProfilingStage.SupportingDocuments);
        return profile;
    }
}
