using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfDirectorTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();

    private static RhshfDirector CacDirector(long? cacId = 101, string name = "Ngozi Eze") =>
        RhshfDirector.FromCac(
            ProfileId, cacId, name, "Eze", "Ngozi", null, "F", "1980-01-01", "Nigerian", "Agronomist",
            "ngozi@fac.example", "+2348000000000", "12 Farm Rd", "Kano", "Kano",
            isChairman: false, "2020-01-01", "DIRECTOR", "ACTIVE", "Ordinary", 5000, null);

    [Fact]
    public void CreateManual_ValidInput_Succeeds()
    {
        var result = RhshfDirector.CreateManual(ProfileId, "Musa Bello", "12345678901", 40m, true, null, null);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SourcedFromCac);
        Assert.Equal("12345678901", result.Value.Bvn);
        Assert.True(result.Value.IsChairman);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateManual_MissingName_Fails(string name)
        => Assert.True(RhshfDirector.CreateManual(ProfileId, name, null, null, false, null, null).IsFailure);

    [Theory]
    [InlineData("1234567890")]    // 10 digits
    [InlineData("123456789012")]  // 12 digits
    [InlineData("1234567890X")]   // right length, not all digits
    public void CreateManual_InvalidBvn_Fails(string bvn)
        => Assert.True(RhshfDirector.CreateManual(ProfileId, "Musa Bello", bvn, null, false, null, null).IsFailure);

    [Fact]
    public void CreateManual_BlankBvn_IsAllowed()
    {
        // BVN is optional at capture time — it only gates inclusion in bureau checks.
        var result = RhshfDirector.CreateManual(ProfileId, "Musa Bello", null, null, false, null, null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Bvn);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void CreateManual_ShareholdingOutOfRange_Fails(decimal pct)
        => Assert.True(RhshfDirector.CreateManual(ProfileId, "Musa Bello", null, pct, false, null, null).IsFailure);

    [Fact]
    public void RefreshCacFields_PreservesManuallyEnteredBvnAndShareholding()
    {
        // This is the behaviour that makes per-director bureau checks survive a CAC refresh: CAC
        // never returns BVN, so a refresh that cleared it would silently break the next check run.
        var director = CacDirector();
        director.UpdateBvn("12345678901");
        director.UpdateShareholding(35m);

        director.RefreshCacFields(
            "Ngozi Eze-Okafor", "Eze-Okafor", "Ngozi", null, "F", "1980-01-01", "Nigerian", "Agronomist",
            "new@fac.example", "+2348111111111", "99 New Rd", "Abuja", "FCT",
            isChairman: true, "2021-06-01", "DIRECTOR", "ACTIVE", "Ordinary", 9000, null);

        Assert.Equal("12345678901", director.Bvn);
        Assert.Equal(35m, director.ShareholdingPercent);
        // ...while CAC-owned fields do update.
        Assert.Equal("Ngozi Eze-Okafor", director.FullName);
        Assert.True(director.IsChairman);
        Assert.Equal(9000, director.NumSharesAllotted);
    }

    [Fact]
    public void UpdateBvn_InvalidValue_Fails_AndLeavesPreviousValueIntact()
    {
        var director = CacDirector();
        director.UpdateBvn("12345678901");

        var result = director.UpdateBvn("bad");

        Assert.True(result.IsFailure);
        Assert.Equal("12345678901", director.Bvn);
    }
}

public class RhshfCreditProfileDirectorTests
{
    private static RhshfCreditProfile MakeProfile()
        => RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: 51_500_000m, currency: "NGN", farmerCount: 1200,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;

    private static RhshfDirector Cac(Guid profileId, long cacId, string name) =>
        RhshfDirector.FromCac(profileId, cacId, name, null, null, null, null, null, null, null,
            null, null, null, null, null, false, null, null, null, null, null, null);

    [Fact]
    public void GetDirectorsWithBvn_ReturnsOnlyThoseCarryingOne()
    {
        var profile = MakeProfile();
        var withBvn = Cac(profile.Id, 1, "Has BVN");
        withBvn.UpdateBvn("12345678901");
        profile.AddDirector(withBvn);
        profile.AddDirector(Cac(profile.Id, 2, "No BVN"));

        var subjects = profile.GetDirectorsWithBvn();

        Assert.Single(subjects);
        Assert.Equal("Has BVN", subjects[0].FullName);
    }

    [Fact]
    public void RemoveDirector_CacSourced_IsRejected()
    {
        var profile = MakeProfile();
        var cac = Cac(profile.Id, 1, "From CAC");
        profile.AddDirector(cac);

        var result = profile.RemoveDirector(cac.Id);

        Assert.True(result.IsFailure);
        Assert.Single(profile.Directors);
    }

    [Fact]
    public void RemoveDirector_ManuallyAdded_Succeeds()
    {
        var profile = MakeProfile();
        var manual = RhshfDirector.CreateManual(profile.Id, "Manual Entry", null, null, false, null, null).Value;
        profile.AddDirector(manual);

        var result = profile.RemoveDirector(manual.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(profile.Directors);
    }

    [Fact]
    public void PruneCacDirectorsNotIn_DropsStaleCacRows_ButKeepsManualOnes()
    {
        var profile = MakeProfile();
        var kept = Cac(profile.Id, 1, "Still Listed");
        var stale = Cac(profile.Id, 2, "Departed");
        var manual = RhshfDirector.CreateManual(profile.Id, "Manual Entry", null, null, false, null, null).Value;
        profile.AddDirector(kept);
        profile.AddDirector(stale);
        profile.AddDirector(manual);

        profile.PruneCacDirectorsNotIn([kept.Id]);

        Assert.Equal(2, profile.Directors.Count);
        Assert.Contains(profile.Directors, d => d.FullName == "Still Listed");
        Assert.Contains(profile.Directors, d => d.FullName == "Manual Entry");
        Assert.DoesNotContain(profile.Directors, d => d.FullName == "Departed");
    }

    [Fact]
    public void FindCacDirector_NeverMatchesManuallyAddedRows()
    {
        // A CAC refresh must not absorb a hand-entered director just because the names collide.
        var profile = MakeProfile();
        var manual = RhshfDirector.CreateManual(profile.Id, "Ngozi Eze", null, null, false, null, null).Value;
        profile.AddDirector(manual);

        Assert.Null(profile.FindCacDirector(null, "Ngozi Eze"));
    }
}
