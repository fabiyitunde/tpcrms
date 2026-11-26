using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

/// <summary>BOA's additional profiling requirements (2026-11-25): directors' NIN, ID and phone,
/// management staff, the Chief Promoter, per-person documents, and verification before Proceed.</summary>
public class RhshfProfilingReadinessTests
{
    private const string Bvn = "22212345678";
    private const string Nin = "12345678901";

    private static RhshfCreditProfile CreateProfile() =>
        RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: 51_500_000.00m, currency: "NGN", farmerCount: 1200,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;

    private static RhshfDirector AddDirector(RhshfCreditProfile profile, string name = "Ada Obi")
    {
        var director = RhshfDirector.CreateManual(profile.Id, name, Bvn, 50m, false, null, null, declaredByFac: true).Value;
        Assert.True(profile.AddDeclaredDirector(director).IsSuccess);
        return director;
    }

    private static List<RhshfProfilingFieldRequirement> AllFieldsMandatory() =>
        Enum.GetValues<RhshfProfilingFieldKey>()
            .Select((k, i) => RhshfProfilingFieldRequirement.Create(k, k.ToString(), null, isMandatory: true, i).Value)
            .ToList();

    private static RhshfDocumentRequirement Requirement(RhshfDocumentCategory category, bool mandatory = true) =>
        RhshfDocumentRequirement.Create(category, category.ToString(), null, mandatory, 10).Value;

    /// <summary>Walks to the documents stage, adding a farm plan on the way.</summary>
    private static void WalkToDocuments(RhshfCreditProfile profile)
    {
        profile.AdvanceStageForTest(RhshfProfilingStage.CompanyVerification);
        profile.AdvanceStageForTest(RhshfProfilingStage.CreditBureauCheck);
        profile.AdvanceStageForTest(RhshfProfilingStage.EopReview);
    }

    private static RhshfSupportingDocument Upload(RhshfCreditProfile profile, RhshfDocumentCategory category,
        Guid? directorId = null, Guid? managementStaffId = null) =>
        profile.AddSupportingDocument(category, $"{category}.pdf", "application/pdf", "path", 2048,
            directorId, managementStaffId).Value;

    // ── Director identity ────────────────────────────────────────────────

    [Theory]
    [InlineData("1234567890")]
    [InlineData("1234567890A")]
    public void UpdateNin_NotElevenDigits_Fails(string nin)
    {
        var director = AddDirector(CreateProfile());
        Assert.True(director.UpdateNin(nin).IsFailure);
    }

    [Fact]
    public void UpdateNin_StripsSeparators()
    {
        var director = AddDirector(CreateProfile());
        Assert.True(director.UpdateNin("123-4567-8901").IsSuccess);
        Assert.Equal(Nin, director.Nin);
    }

    [Fact]
    public void UpdateMeansOfId_PassportWithoutExpiry_Fails()
    {
        var director = AddDirector(CreateProfile());
        Assert.True(director.UpdateMeansOfId(RhshfMeansOfIdType.InternationalPassport, "A123", null).IsFailure);
        Assert.True(director.UpdateMeansOfId(RhshfMeansOfIdType.VotersCard, "VC123", null).IsSuccess);
    }

    [Fact]
    public void ExpiredId_IsInvalid_AndBlocksLeavingTheBureauStage()
    {
        var profile = CreateProfile();
        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);
        var director = AddDirector(profile);
        director.UpdateNin(Nin);
        director.UpdateContact("08012345678", null);
        director.UpdateMeansOfId(RhshfMeansOfIdType.InternationalPassport, "A123", DateTime.UtcNow.AddDays(-1));
        profile.AddManagementStaff("Bola Ade", "General Manager", "08000000000", null);
        profile.SetChiefPromoter(director.Id);

        var result = profile.AdvanceStage(RhshfProfilingStage.CreditBureauCheck, fieldRequirements: AllFieldsMandatory());

        Assert.True(result.IsFailure);
        Assert.Contains("expired", result.Error);
    }

    [Fact]
    public void BureauStage_AllDetailsPresent_Advances()
    {
        var profile = CreateProfile();
        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);
        var director = AddDirector(profile);
        director.UpdateNin(Nin);
        director.UpdateContact("08012345678", null);
        director.UpdateMeansOfId(RhshfMeansOfIdType.InternationalPassport, "A123", DateTime.UtcNow.AddYears(2));
        profile.AddManagementStaff("Bola Ade", "General Manager", "08000000000", null);
        profile.SetChiefPromoter(director.Id);

        var result = profile.AdvanceStage(RhshfProfilingStage.CreditBureauCheck, fieldRequirements: AllFieldsMandatory());

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
    }

    [Fact]
    public void OptionalFields_DoNotBlock()
    {
        var profile = CreateProfile();
        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);
        AddDirector(profile);
        var optional = AllFieldsMandatory();
        optional.ForEach(f => f.SetMandatory(false));

        Assert.True(profile.AdvanceStage(RhshfProfilingStage.CreditBureauCheck, fieldRequirements: optional).IsSuccess);
    }

    // ── Chief Promoter ───────────────────────────────────────────────────

    [Fact]
    public void SetChiefPromoter_MovesTheFlag_SoOnlyOnePersonHoldsIt()
    {
        var profile = CreateProfile();
        var director = AddDirector(profile);
        var manager = profile.AddManagementStaff("Bola Ade", "General Manager", null, null).Value;

        profile.SetChiefPromoter(director.Id);
        profile.SetChiefPromoter(manager.Id);

        Assert.False(director.IsChiefPromoter);
        Assert.True(manager.IsChiefPromoter);
        Assert.Equal("Bola Ade", profile.ChiefPromoterName);
    }

    [Fact]
    public void SetChiefPromoter_PersonNotOnCase_Fails()
    {
        var profile = CreateProfile();
        Assert.True(profile.SetChiefPromoter(Guid.NewGuid()).IsFailure);
    }

    [Fact]
    public void ChiefPromoterCv_IsMetByThePromotersOwnCv()
    {
        var profile = CreateProfile();
        var director = AddDirector(profile);
        profile.SetChiefPromoter(director.Id);
        WalkToDocuments(profile);
        Upload(profile, RhshfDocumentCategory.DirectorCv, director.Id);

        var missing = profile.MissingMandatoryDocuments([Requirement(RhshfDocumentCategory.ChiefPromoterCv)]);

        Assert.Empty(missing);
    }

    // ── Per-person documents ─────────────────────────────────────────────

    [Fact]
    public void DirectorCv_IsRequiredOfEachDirector()
    {
        var profile = CreateProfile();
        var first = AddDirector(profile, "Ada Obi");
        AddDirector(profile, "Chidi Eze");
        WalkToDocuments(profile);
        Upload(profile, RhshfDocumentCategory.DirectorCv, first.Id);

        var missing = profile.MissingMandatoryDocuments([Requirement(RhshfDocumentCategory.DirectorCv)]);

        Assert.Single(missing);
        Assert.Equal("Chidi Eze", missing[0].PersonName);
    }

    [Fact]
    public void Upload_ReplacesOnlyThatPersonsFile()
    {
        var profile = CreateProfile();
        var first = AddDirector(profile, "Ada Obi");
        var second = AddDirector(profile, "Chidi Eze");
        WalkToDocuments(profile);
        Upload(profile, RhshfDocumentCategory.DirectorCv, first.Id);
        Upload(profile, RhshfDocumentCategory.DirectorCv, second.Id);
        Upload(profile, RhshfDocumentCategory.DirectorCv, first.Id);

        Assert.Equal(2, profile.SupportingDocuments.Count(d => d.Category == RhshfDocumentCategory.DirectorCv));
    }

    [Fact]
    public void Upload_PerPersonCategoryWithoutPerson_Fails()
    {
        var profile = CreateProfile();
        AddDirector(profile);
        WalkToDocuments(profile);

        var result = profile.AddSupportingDocument(RhshfDocumentCategory.DirectorCv, "cv.pdf", "application/pdf", "p", 2048);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void RemoveManagementStaff_TakesTheirCvWithThem()
    {
        var profile = CreateProfile();
        var manager = profile.AddManagementStaff("Bola Ade", "General Manager", null, null).Value;
        WalkToDocuments(profile);
        Upload(profile, RhshfDocumentCategory.ManagementCv, managementStaffId: manager.Id);

        Assert.True(profile.RemoveManagementStaff(manager.Id).IsSuccess);
        Assert.Empty(profile.SupportingDocuments);
    }

    // ── Verification and the Proceed gate ────────────────────────────────

    private static (RhshfCreditProfile Profile, RhshfSupportingDocument Doc) SubmittedCaseWithCompanyProfile()
    {
        var profile = CreateProfile();
        WalkToDocuments(profile);
        var doc = Upload(profile, RhshfDocumentCategory.CompanyProfile);
        profile.AdvanceStageForTest(RhshfProfilingStage.SupportingDocuments);
        profile.AdvanceStageForTest(RhshfProfilingStage.ReviewAndSubmit);
        Assert.Equal(RhshfInternalStage.Appraisal, profile.InternalStage);
        return (profile, doc);
    }

    [Fact]
    public void Proceed_WithMandatoryDocumentAwaitingVerification_IsRefused()
    {
        var (profile, _) = SubmittedCaseWithCompanyProfile();
        var officer = Guid.NewGuid();
        profile.SeedViableAppraisal(officer);

        var result = profile.Appraise(officer, RhshfAppraisalOutcome.Proceed, null,
            documentRequirements: [Requirement(RhshfDocumentCategory.CompanyProfile)]);

        Assert.True(result.IsFailure);
        Assert.Contains("verified", result.Error);
    }

    [Fact]
    public void Proceed_OnceVerified_Succeeds()
    {
        var (profile, doc) = SubmittedCaseWithCompanyProfile();
        var officer = Guid.NewGuid();
        profile.SeedViableAppraisal(officer);
        Assert.True(profile.VerifySupportingDocument(doc.Id, officer).IsSuccess);

        var result = profile.Appraise(officer, RhshfAppraisalOutcome.Proceed, null,
            documentRequirements: [Requirement(RhshfDocumentCategory.CompanyProfile)]);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
        Assert.Equal(RhshfInternalStage.LegalClearance, profile.InternalStage);
    }

    [Fact]
    public void Proceed_WithARejectedDocument_IsRefused_ButReturnToFacIsNot()
    {
        var (profile, doc) = SubmittedCaseWithCompanyProfile();
        var officer = Guid.NewGuid();
        profile.SeedViableAppraisal(officer);
        profile.RejectSupportingDocument(doc.Id, officer, "Out of date");

        Assert.True(profile.Appraise(officer, RhshfAppraisalOutcome.Proceed, null).IsFailure);
        Assert.True(profile.Appraise(officer, RhshfAppraisalOutcome.ReturnToFac, "Replace it",
            RhshfProfilingStage.SupportingDocuments).IsSuccess);
    }

    [Fact]
    public void ReplacingARejectedDocument_StartsPendingAgain()
    {
        var (profile, doc) = SubmittedCaseWithCompanyProfile();
        var officer = Guid.NewGuid();
        profile.RejectSupportingDocument(doc.Id, officer, "Out of date");
        profile.Appraise(officer, RhshfAppraisalOutcome.ReturnToFac, "Replace it", RhshfProfilingStage.SupportingDocuments);

        var replacement = Upload(profile, RhshfDocumentCategory.CompanyProfile);

        Assert.Equal(RhshfDocumentVerificationStatus.Pending, replacement.VerificationStatus);
        Assert.Single(profile.SupportingDocuments);
    }

    [Fact]
    public void RejectedMandatoryDocument_BlocksResubmission()
    {
        var (profile, doc) = SubmittedCaseWithCompanyProfile();
        var officer = Guid.NewGuid();
        profile.RejectSupportingDocument(doc.Id, officer, "Out of date");
        profile.Appraise(officer, RhshfAppraisalOutcome.ReturnToFac, "Replace it", RhshfProfilingStage.SupportingDocuments);

        var result = profile.AdvanceStage(RhshfProfilingStage.SupportingDocuments,
            [Requirement(RhshfDocumentCategory.CompanyProfile)]);

        Assert.True(result.IsFailure);
        Assert.Contains("rejected", result.Error);
    }

    [Fact]
    public void Reject_WithoutReason_Fails()
    {
        var (profile, doc) = SubmittedCaseWithCompanyProfile();
        Assert.True(profile.RejectSupportingDocument(doc.Id, Guid.NewGuid(), " ").IsFailure);
    }

    [Fact]
    public void Verify_OutsideAppraisal_Fails()
    {
        var profile = CreateProfile();
        WalkToDocuments(profile);
        var doc = Upload(profile, RhshfDocumentCategory.CompanyProfile);

        Assert.True(profile.VerifySupportingDocument(doc.Id, Guid.NewGuid()).IsFailure);
    }

    [Fact]
    public void LegacyCase_MissingANewRequirement_IsNotHeldAtAppraisal()
    {
        // Submitted before the business plan was asked for: nothing in that slot. The FAC's submit
        // gate is what enforces it on new cases; appraisal must not strand the old ones.
        var (profile, doc) = SubmittedCaseWithCompanyProfile();
        var officer = Guid.NewGuid();
        profile.SeedViableAppraisal(officer);
        profile.VerifySupportingDocument(doc.Id, officer);

        var result = profile.Appraise(officer, RhshfAppraisalOutcome.Proceed, null,
            documentRequirements: [Requirement(RhshfDocumentCategory.CompanyProfile), Requirement(RhshfDocumentCategory.BusinessPlan)]);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
    }

    // ── Carrying a document over from an earlier case ──────────────────────

    [Theory]
    [InlineData(RhshfDocumentCategory.DirectorCv)]
    [InlineData(RhshfDocumentCategory.DirectorMeansOfId)]
    [InlineData(RhshfDocumentCategory.ManagementCv)]
    [InlineData(RhshfDocumentCategory.ChiefPromoterCv)]
    public void AddReusedSupportingDocument_PersonalCategory_Fails(RhshfDocumentCategory category)
    {
        var profile = CreateProfile();
        WalkToDocuments(profile);

        var result = profile.AddReusedSupportingDocument(category, "cv.pdf", "application/pdf", "path", 2048,
            Guid.NewGuid(), "RHSHF-2026-000001", DateTime.UtcNow.AddDays(-30));

        Assert.True(result.IsFailure);
        Assert.Contains("carried over", result.Error);
        Assert.Empty(profile.SupportingDocuments);
    }

    [Fact]
    public void AddReusedSupportingDocument_CaseCategory_Succeeds()
    {
        var profile = CreateProfile();
        WalkToDocuments(profile);

        var result = profile.AddReusedSupportingDocument(RhshfDocumentCategory.CompanyProfile, "profile.pdf",
            "application/pdf", "path", 2048, Guid.NewGuid(), "RHSHF-2026-000001", DateTime.UtcNow.AddDays(-30));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
    }
}
