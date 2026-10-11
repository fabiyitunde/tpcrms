using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfCollateralTests
{
    private static Result<RhshfCollateral> CreateBankGuarantee(string? guarantorBankName = "First Merchant Bank", decimal? guaranteeAmount = 22_400_000m)
        => RhshfCollateral.Create(
            Guid.NewGuid(), 1, RhshfCollateralType.BankGuarantee, Guid.NewGuid(), notes: null,
            referenceNumber: "BG-2026-001", issuedDate: DateTime.UtcNow, expiryDate: DateTime.UtcNow.AddYears(1),
            guarantorBankName: guarantorBankName, guaranteeAmount: guaranteeAmount, isUnconditional: true,
            crgCoveragePercentage: null,
            propertyDescription: null, propertyValue: null, titleReferenceNumber: null,
            registrationAuthority: null);

    [Fact]
    public void Create_BankGuarantee_WithRequiredFields_Succeeds()
    {
        var result = CreateBankGuarantee();

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCollateralType.BankGuarantee, result.Value.Type);
        Assert.True(result.Value.IsUnconditional);
    }

    [Fact]
    public void Create_BankGuarantee_WithoutBankName_Fails()
    {
        var result = CreateBankGuarantee(guarantorBankName: null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_BankGuarantee_WithZeroAmount_Fails()
    {
        var result = CreateBankGuarantee(guaranteeAmount: 0);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_NirsalCrg_WithCoverage_Succeeds()
    {
        var result = RhshfCollateral.Create(
            Guid.NewGuid(), 1, RhshfCollateralType.NirsalCrg, Guid.NewGuid(), notes: null,
            referenceNumber: "CRG-2026-001", issuedDate: DateTime.UtcNow, expiryDate: null,
            guarantorBankName: null, guaranteeAmount: null, isUnconditional: null,
            crgCoveragePercentage: 50m,
            propertyDescription: null, propertyValue: null, titleReferenceNumber: null,
            registrationAuthority: null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_NirsalCrg_WithoutCoverage_Fails()
    {
        var result = RhshfCollateral.Create(
            Guid.NewGuid(), 1, RhshfCollateralType.NirsalCrg, Guid.NewGuid(), notes: null,
            referenceNumber: "CRG-2026-001", issuedDate: DateTime.UtcNow, expiryDate: null,
            guarantorBankName: null, guaranteeAmount: null, isUnconditional: null,
            crgCoveragePercentage: null,
            propertyDescription: null, propertyValue: null, titleReferenceNumber: null,
            registrationAuthority: null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_LegalMortgage_WithPropertyDetails_Succeeds()
    {
        var result = RhshfCollateral.Create(
            Guid.NewGuid(), 1, RhshfCollateralType.LegalMortgage, Guid.NewGuid(), notes: null,
            referenceNumber: null, issuedDate: null, expiryDate: null,
            guarantorBankName: null, guaranteeAmount: null, isUnconditional: null,
            crgCoveragePercentage: null,
            propertyDescription: "3-hectare warehouse plot, Bunkure, Kano", propertyValue: 30_000_000m,
            titleReferenceNumber: "KN/2026/00123", registrationAuthority: "Kano State Land Registry");

        Assert.True(result.IsSuccess);
        Assert.Equal("KN/2026/00123", result.Value.TitleReferenceNumber);
        // Nobody asserts perfection at declaration time — that is the Legal Officer's, at clearance.
        Assert.Null(result.Value.PerfectionStatus);
    }

    [Fact]
    public void Create_LegalMortgage_WithoutPropertyValue_Fails()
    {
        var result = RhshfCollateral.Create(
            Guid.NewGuid(), 1, RhshfCollateralType.LegalMortgage, Guid.NewGuid(), notes: null,
            referenceNumber: null, issuedDate: null, expiryDate: null,
            guarantorBankName: null, guaranteeAmount: null, isUnconditional: null,
            crgCoveragePercentage: null,
            propertyDescription: "3-hectare warehouse plot", propertyValue: null,
            titleReferenceNumber: null, registrationAuthority: null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void AddDocument_AppendsToCollection()
    {
        var collateral = CreateBankGuarantee().Value;

        var document = collateral.AddDocument("guarantee.pdf", "application/pdf", "path/guarantee.pdf", 2048);

        Assert.Single(collateral.Documents);
        Assert.Equal("guarantee.pdf", document.FileName);
    }

    [Fact]
    public void Create_WithoutRecordedBy_Fails()
    {
        var result = RhshfCollateral.Create(
            Guid.NewGuid(), 1, RhshfCollateralType.BankGuarantee, Guid.Empty, notes: null,
            referenceNumber: null, issuedDate: null, expiryDate: null,
            guarantorBankName: "First Merchant Bank", guaranteeAmount: 1_000_000m, isUnconditional: true,
            crgCoveragePercentage: null,
            propertyDescription: null, propertyValue: null, titleReferenceNumber: null,
            registrationAuthority: null);

        Assert.True(result.IsFailure);
    }

    // ── Perfection: the Legal Officer's assertion, not a field anyone can fill in ──────────────

    [Fact]
    public void AssertPerfection_Perfected_RecordsWhoAssertedItAndWhen()
    {
        var collateral = CreateBankGuarantee().Value;
        collateral.AddDocument("guarantee.pdf", "application/pdf", "path/g.pdf", 1024);
        var legalOfficerId = Guid.NewGuid();

        var result = collateral.AssertPerfection(legalOfficerId, RhshfCollateralPerfectionStatus.Perfected);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCollateralPerfectionStatus.Perfected, collateral.PerfectionStatus);
        Assert.Equal(legalOfficerId, collateral.PerfectedByUserId);
        Assert.NotNull(collateral.PerfectedAt);
    }

    [Fact]
    public void AssertPerfection_Perfected_RequiresTheEvidenceToBeOnFile()
    {
        var collateral = CreateBankGuarantee().Value; // no document attached

        var result = collateral.AssertPerfection(Guid.NewGuid(), RhshfCollateralPerfectionStatus.Perfected);

        Assert.True(result.IsFailure);
        Assert.Null(collateral.PerfectionStatus);
        Assert.Null(collateral.PerfectedAt);
    }

    [Fact]
    public void AssertPerfection_CapturesTitleDetailsEstablishedDuringPerfection()
    {
        var collateral = RhshfCollateral.Create(
            Guid.NewGuid(), 1, RhshfCollateralType.LegalMortgage, Guid.NewGuid(), notes: null,
            referenceNumber: null, issuedDate: null, expiryDate: null,
            guarantorBankName: null, guaranteeAmount: null, isUnconditional: null,
            crgCoveragePercentage: null,
            propertyDescription: "3-hectare warehouse plot", propertyValue: 30_000_000m,
            titleReferenceNumber: null, registrationAuthority: null).Value;
        collateral.AddDocument("deed.pdf", "application/pdf", "path/d.pdf", 4096);

        collateral.AssertPerfection(
            Guid.NewGuid(), RhshfCollateralPerfectionStatus.Perfected,
            titleReferenceNumber: "KN/2026/00123", registrationAuthority: "Kano State Land Registry");

        // Legal is usually the first to know these, so the assertion can carry them.
        Assert.Equal("KN/2026/00123", collateral.TitleReferenceNumber);
        Assert.Equal("Kano State Land Registry", collateral.RegistrationAuthority);
    }

    [Fact]
    public void AssertPerfection_Pending_WalksTheAssertionBack_AndClearsItsAudit()
    {
        var collateral = CreateBankGuarantee().Value;
        collateral.AddDocument("guarantee.pdf", "application/pdf", "path/g.pdf", 1024);
        collateral.AssertPerfection(Guid.NewGuid(), RhshfCollateralPerfectionStatus.Perfected);

        var result = collateral.AssertPerfection(Guid.NewGuid(), RhshfCollateralPerfectionStatus.Pending);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCollateralPerfectionStatus.Pending, collateral.PerfectionStatus);
        // No stale "perfected by X" left behind on an instrument that is no longer perfected.
        Assert.Null(collateral.PerfectedByUserId);
        Assert.Null(collateral.PerfectedAt);
    }

    [Fact]
    public void AssertPerfection_WithoutALegalOfficer_Fails()
    {
        var collateral = CreateBankGuarantee().Value;
        collateral.AddDocument("guarantee.pdf", "application/pdf", "path/g.pdf", 1024);

        var result = collateral.AssertPerfection(Guid.Empty, RhshfCollateralPerfectionStatus.Perfected);

        Assert.True(result.IsFailure);
    }
}
