using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

/// <summary>
/// The optional detail the portal adds in its Slice B (CRMSQA#3 items 1 to 3): packs and price
/// version, the approved service provider, and the EOP's own reference. Descriptive only — none of
/// it changes an amount.
/// </summary>
public class RhshfFinancingDetailTests
{
    private static readonly Guid Profile = Guid.NewGuid();

    private static RhshfFinancingLine Line(
        string item = "NPK Fertilizer", string? category = "Fertilizer", string? supplier = "Agro Supplies Ltd",
        RhshfFinancingLineDetail? detail = null)
        => new(Profile, Guid.NewGuid(), "Maize", item, category, null, supplier, 10m, "Kg", 448m,
            "perFarmer", 30, 134_400m, 0m, true, detail);

    [Fact]
    public void Pack_detail_is_kept_and_read_back_as_the_borrower_would_say_it()
    {
        var line = Line(detail: new RhshfFinancingLineDetail(
            BaseUnit: "Kg", PackSize: 50m, PackUnit: "Bag", PackCount: 6m, PricePerPack: 22_400m,
            ProcurementQuantity: 300m, PackagingExcessQuantity: 0m, PriceVersionId: "a1b2c3d4-0000"));

        Assert.Equal("6 Bags of 50 Kg at NGN 22,400.00 each", line.PackDisplay("NGN"));
        Assert.Equal(300m, line.ProcurementQuantity);
        Assert.Equal("a1b2c3d4-0000", line.PriceVersionId);
        Assert.Equal(134_400m, line.Amount);
    }

    [Fact]
    public void A_line_from_before_slice_b_has_no_detail_and_prints_as_before()
    {
        var line = Line();

        Assert.Null(line.PackDisplay("NGN"));
        Assert.Null(line.ProviderName);
        Assert.Equal("Agro Supplies Ltd", line.ResponsibleParty);
    }

    [Fact]
    public void A_service_line_names_its_approved_provider()
    {
        var line = Line("Extension Services", "Services", supplier: null,
            detail: new RhshfFinancingLineDetail(ProviderId: "p-1", ProviderName: "Kano Extension Co-op",
                ProviderRate: 4_000m, ProviderRateBasis: "per farmer", ProviderAssignmentId: "as-9"));

        Assert.Equal("Kano Extension Co-op", line.ResponsibleParty);
        Assert.Equal("as-9", line.ProviderAssignmentId);
    }

    [Fact]
    public void An_overlong_value_is_shortened_rather_than_failing_the_submission()
    {
        var line = Line(detail: new RhshfFinancingLineDetail(PriceVersionId: new string('x', 500)));
        Assert.Equal(200, line.PriceVersionId!.Length);

        var eop = new RhshfFinancingEop(Profile, new RhshfFinancingEopInput(
            Guid.NewGuid(), "Maize", "Kano", 30, 1m, 0m, 0m, 0m, 0m, 1m, 1m, EopReference: new string('E', 300)));
        Assert.Equal(200, eop.EopReference!.Length);
    }

    [Theory]
    [InlineData("Kano Extension Co-op", 0)]
    [InlineData(null, 1)]
    public void A_financed_service_is_an_offer_gap_only_while_no_provider_is_named(string? provider, int gaps)
    {
        var service = new RhshfFinancingLineInput(Guid.NewGuid(), "Maize", "Extension Services", "Services", null, null,
            1m, "Per Farmer", 4_000m, "perFarmer", 30, 120_000m, 0m, true,
            new RhshfFinancingLineDetail(ProviderName: provider));
        var profile = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Iveren Agro Limited", rcNumber: "RC771204", tin: null,
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Yobe", lga: "Damaturu", totalEopValue: 120_000m, currency: "NGN", farmerCount: 30,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null,
            financing: new RhshfFinancingInput([service], [], 120_000m, 0m, 0m, 120_000m, 120_000m, 0m));

        Assert.True(profile.IsSuccess, profile.IsSuccess ? null : profile.Error);
        Assert.Equal(gaps, profile.Value.OfferDataGaps().Count);
    }

    [Fact]
    public void The_eop_prints_the_portals_reference_when_it_sent_one()
    {
        var id = Guid.Parse("1a2b3c4d-0000-0000-0000-000000000000");
        var withRef = new RhshfFinancingEop(Profile, new RhshfFinancingEopInput(
            id, "Maize", "Kano", 30, 1m, 0m, 0m, 0m, 0m, 1m, 1m,
            EopReference: "EOP-1A2B3C4D", EopApprovedAt: new DateTime(2026, 10, 12), EopRevision: 2, SnapshotHash: "abc"));
        var without = new RhshfFinancingEop(Profile, new RhshfFinancingEopInput(
            id, "Maize", "Kano", 30, 1m, 0m, 0m, 0m, 0m, 1m, 1m));

        Assert.Equal("EOP-1A2B3C4D", withRef.DisplayReference);
        Assert.Equal(2, withRef.EopRevision);
        Assert.Equal("EOP 1A2B3C4D", without.DisplayReference);
    }
}
