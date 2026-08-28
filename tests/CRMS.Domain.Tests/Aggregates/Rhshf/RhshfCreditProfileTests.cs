using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfCreditProfileTests
{
    private static (Guid submissionId, string programmeCode, string programmeName, string sessionCode, string sessionName,
        Guid facId, string companyName, string rcNumber, string tin, string boaAccountNumber, string contactEmail,
        string contactPhone, string state, string lga, decimal totalEopValue, string currency, int? farmerCount,
        string callbackUrl, string? certifiedByAdmin, DateTime? certifiedAt, string rawSubmissionPayload,
        IEnumerable<(string Commodity, decimal QuantityKg, decimal UnitPricePerKg, decimal LineValue)>? eopLines,
        Guid? resolvedBranchId, Guid? resolvedOfficeId) ValidArgs() => (
        Guid.NewGuid(), "RH-SHF-DRY-2026", "Renewed Hope - Dry Season 2026", "2026-DRY", "Dry Season 2026",
        Guid.NewGuid(), "Alliedsoft Limited", "RC123456", "01234567-0001", "0123456789", "fac@company.com",
        "+2348012345678", "Kano", "Nassarawa", 51_500_000.00m, "NGN", 1200,
        "https://portal.example.gov.ng/api/integrations/crms/webhook", "admin@boa.gov.ng", DateTime.UtcNow, "{}",
        null, null, null);

    [Fact]
    public void Create_WithValidArgs_SucceedsAndSetsExpectedInitialState()
    {
        var a = ValidArgs();

        var result = RhshfCreditProfile.Create(
            a.submissionId, a.programmeCode, a.programmeName, a.sessionCode, a.sessionName, a.facId,
            a.companyName, a.rcNumber, a.tin, a.boaAccountNumber, a.contactEmail, a.contactPhone, a.state, a.lga,
            a.totalEopValue, a.currency, a.farmerCount, a.callbackUrl, a.certifiedByAdmin, a.certifiedAt,
            a.rawSubmissionPayload, a.eopLines, a.resolvedBranchId, a.resolvedOfficeId);

        Assert.True(result.IsSuccess);
        var profile = result.Value;
        Assert.Equal(RhshfCaseStatus.ProfilingPending, profile.Status);
        Assert.Equal(RhshfProfilingStage.CompanyVerification, profile.CurrentStage);
        Assert.StartsWith($"RHSHF-{DateTime.UtcNow:yyyy}-", profile.Reference);
        Assert.Equal(6, profile.Reference.Split('-')[2].Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingCompanyName_Fails(string companyName)
    {
        var a = ValidArgs();

        var result = RhshfCreditProfile.Create(
            a.submissionId, a.programmeCode, a.programmeName, a.sessionCode, a.sessionName, a.facId,
            companyName, a.rcNumber, a.tin, a.boaAccountNumber, a.contactEmail, a.contactPhone, a.state, a.lga,
            a.totalEopValue, a.currency, a.farmerCount, a.callbackUrl, a.certifiedByAdmin, a.certifiedAt,
            a.rawSubmissionPayload, a.eopLines, a.resolvedBranchId, a.resolvedOfficeId);

        Assert.True(result.IsFailure);
        Assert.Contains("companyName", result.Error);
    }

    [Fact]
    public void Create_WithZeroTotalEopValue_Fails()
    {
        var a = ValidArgs();

        var result = RhshfCreditProfile.Create(
            a.submissionId, a.programmeCode, a.programmeName, a.sessionCode, a.sessionName, a.facId,
            a.companyName, a.rcNumber, a.tin, a.boaAccountNumber, a.contactEmail, a.contactPhone, a.state, a.lga,
            0m, a.currency, a.farmerCount, a.callbackUrl, a.certifiedByAdmin, a.certifiedAt,
            a.rawSubmissionPayload, a.eopLines, a.resolvedBranchId, a.resolvedOfficeId);

        Assert.True(result.IsFailure);
        Assert.Contains("totalEopValue", result.Error);
    }

    [Fact]
    public void Create_WithInvalidCallbackUrl_Fails()
    {
        var a = ValidArgs();

        var result = RhshfCreditProfile.Create(
            a.submissionId, a.programmeCode, a.programmeName, a.sessionCode, a.sessionName, a.facId,
            a.companyName, a.rcNumber, a.tin, a.boaAccountNumber, a.contactEmail, a.contactPhone, a.state, a.lga,
            a.totalEopValue, a.currency, a.farmerCount, "not-a-url", a.certifiedByAdmin, a.certifiedAt,
            a.rawSubmissionPayload, a.eopLines, a.resolvedBranchId, a.resolvedOfficeId);

        Assert.True(result.IsFailure);
        Assert.Contains("callbackUrl", result.Error);
    }

    [Fact]
    public void IssueToken_ThenConsume_Succeeds()
    {
        var profile = CreateValidProfile();
        profile.IssueToken("jti-1", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(20));

        var result = profile.ConsumeToken("jti-1");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ConsumeToken_CalledTwiceForSameJti_SecondCallFails()
    {
        var profile = CreateValidProfile();
        profile.IssueToken("jti-1", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(20));

        var first = profile.ConsumeToken("jti-1");
        var second = profile.ConsumeToken("jti-1");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
    }

    [Fact]
    public void ConsumeToken_UnknownJti_Fails()
    {
        var profile = CreateValidProfile();

        var result = profile.ConsumeToken("never-issued");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void IssueToken_DoesNotChangeStatus()
    {
        var profile = CreateValidProfile();
        var statusBefore = profile.Status;

        profile.IssueToken("jti-1", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(20));

        Assert.Equal(statusBefore, profile.Status);
    }

    private static RhshfCreditProfile CreateValidProfile()
    {
        var a = ValidArgs();
        var result = RhshfCreditProfile.Create(
            a.submissionId, a.programmeCode, a.programmeName, a.sessionCode, a.sessionName, a.facId,
            a.companyName, a.rcNumber, a.tin, a.boaAccountNumber, a.contactEmail, a.contactPhone, a.state, a.lga,
            a.totalEopValue, a.currency, a.farmerCount, a.callbackUrl, a.certifiedByAdmin, a.certifiedAt,
            a.rawSubmissionPayload, a.eopLines, a.resolvedBranchId, a.resolvedOfficeId);
        return result.Value;
    }

    [Fact]
    public void Create_WithEopLines_AddsThemToTheCollection()
    {
        var a = ValidArgs();
        var lines = new[]
        {
            ("Maize", 100_000m, 340.00m, 34_000_000.00m),
            ("Sorghum", 50_000m, 330.00m, 16_500_000.00m),
        };

        var result = RhshfCreditProfile.Create(
            a.submissionId, a.programmeCode, a.programmeName, a.sessionCode, a.sessionName, a.facId,
            a.companyName, a.rcNumber, a.tin, a.boaAccountNumber, a.contactEmail, a.contactPhone, a.state, a.lga,
            a.totalEopValue, a.currency, a.farmerCount, a.callbackUrl, a.certifiedByAdmin, a.certifiedAt,
            a.rawSubmissionPayload, lines, a.resolvedBranchId, a.resolvedOfficeId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.EopLines.Count);
        Assert.Contains(result.Value.EopLines, l => l.Commodity == "Maize" && l.LineValue == 34_000_000.00m);
    }
}
