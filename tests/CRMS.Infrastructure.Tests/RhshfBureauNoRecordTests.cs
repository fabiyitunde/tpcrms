using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Moq;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// The FAC company's automated bureau check at the CreditBureauCheck stage. A company neither
/// bureau holds a file on used to come back as a clean all-zero report and be marked Cleared; it is
/// now its own outcome, NoRecord, kept apart from a pull that actually failed.
/// </summary>
public class RhshfBureauNoRecordTests
{
    private const string Rc = "RC123456";

    private static RhshfCreditProfile ProfileOnBureauStage()
    {
        var profile = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: Rc, tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: 51_500_000m, currency: "NGN", farmerCount: 1200,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;
        // AdvanceStage names the stage being left: this moves CompanyVerification → CreditBureauCheck.
        Assert.True(profile.AdvanceStage(RhshfProfilingStage.CompanyVerification).IsSuccess);
        Assert.Equal(RhshfProfilingStage.CreditBureauCheck, profile.CurrentStage);
        return profile;
    }

    private static async Task<RhshfBureauOutcome> Run(string crcError, string firstCentralError)
    {
        var profile = ProfileOnBureauStage();
        var repo = new Mock<IRhshfCreditProfileRepository>();
        repo.Setup(r => r.GetByReferenceAsync(profile.Reference, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        var sc = new Mock<ISmartComplyProvider>();
        sc.Setup(s => s.GetCRCBusinessHistoryAsync(Rc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyBusinessCreditReport>(crcError));
        sc.Setup(s => s.GetFirstCentralBusinessAsync(Rc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyBusinessCreditReport>(firstCentralError));

        var result = await new EnsureRhshfBureauCheckHandler(repo.Object, sc.Object, Mock.Of<IUnitOfWork>())
            .Handle(new EnsureRhshfBureauCheckCommand(profile.Reference));

        Assert.True(result.IsSuccess);
        return profile.BureauCheckOutcome;
    }

    [Fact]
    public async Task No_file_at_either_bureau_is_recorded_as_no_record()
    {
        Assert.Equal(RhshfBureauOutcome.NoRecord, await Run(
            "Business not found in credit bureau: No credit history found",
            "Business not found in credit bureau: No credit history found"));
    }

    [Fact]
    public async Task A_bureau_that_failed_outright_is_still_a_failure()
    {
        Assert.Equal(RhshfBureauOutcome.Failed, await Run(
            "Business not found in credit bureau",
            "Credit check service is temporarily unavailable"));
    }
}
