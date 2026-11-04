using System.Security.Cryptography;
using System.Text;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Application.Rhshf.Services;
using CRMS.Domain.Enums;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>
/// The portal's v2 snapshot hash, checked against the worked example it posted on CRMSQA#3 on 4 Nov:
/// one Maize EOP for 30 farmers, NPK at 2 kg per farmer × ₦1,120 plus Extension Services at ₦9,000
/// per farmer from one provider on rate version 2. The hashes are the portal's, reproduced there by
/// its own test.
/// </summary>
public class RhshfEopSnapshotHashV2Tests
{
    private const string PortalCanonicalOriginal =
        """{"approvedTotal":"487548","crop":"Maize","eopId":"3cc02579-d786-40fb-b87d-17d6195c5847","farmers":30,"inputLines":[{"amount":"67200","approvedRate":null,"assignmentReference":null,"assignmentStatus":null,"basis":"perFarmer","category":"Fertilizer","inLoan":true,"item":"NPK Fertilizer","lineId":"00000000-0000-0000-0000-0000000000a1","numberOfPacks":null,"packSize":null,"packagingExcess":"0","pricePerPack":null,"priceVersionId":null,"providerId":null,"providerStatus":null,"quantityPerFarmer":"2","rateBasis":null,"rateId":null,"rateVersion":null,"serviceInputReference":null,"serviceQuantity":null,"serviceType":null,"supplier":"Synthetic Agro Inputs Ltd","supplierProductId":null,"unit":"Kg","unitPrice":"1120"},{"amount":"270000","approvedRate":"9000","assignmentReference":"73000000-0000-0000-0000-00000000c001","assignmentStatus":"Assigned","basis":"perFarmer","category":"Services","inLoan":true,"item":"Extension Services","lineId":"00000000-0000-0000-0000-0000000000a3","numberOfPacks":null,"packSize":null,"packagingExcess":"0","pricePerPack":null,"priceVersionId":null,"providerId":"73000000-0000-0000-0000-000000000001","providerStatus":"ASSIGNED","quantityPerFarmer":"1","rateBasis":"PerFarmer","rateId":"73000000-0000-0000-0000-0000000000a1","rateVersion":2,"serviceInputReference":null,"serviceQuantity":"30","serviceType":"Extension Services","supplier":null,"supplierProductId":null,"unit":"Per Farmer","unitPrice":"9000"}],"inputs":"337200","insurance":"13488","loanAmount":"487548","packagingExcess":"0","revision":1,"scheme":"rhshf-eop-snapshot-v2","state":"Yobe","techServicesFee":"120000","workingCapital":"16860"}""";

    private static readonly Guid EopId = Guid.Parse("3cc02579-d786-40fb-b87d-17d6195c5847");

    /// <summary>The example as the portal would send it, with the four variations it hashed.</summary>
    private static (RhshfFinancingDto Financing, RhshfFinancingEopDto Eop) Example(
        decimal npkKgPerFarmer = 2m, decimal extensionRate = 9_000m,
        string assignmentReference = "73000000-0000-0000-0000-00000000c001", string rateVersion = "2")
    {
        var npk = 30m * npkKgPerFarmer * 1_120m;
        var extension = 30m * extensionRate;
        var inputs = npk + extension;
        var insurance = inputs * 0.04m;
        var workingCapital = inputs * 0.05m;
        var total = inputs + insurance + workingCapital + 120_000m;

        var lines = new List<RhshfFinancingInputLineDto>
        {
            new(EopId: EopId, Crop: "Maize", Item: "NPK Fertilizer", Category: "Fertilizer",
                Supplier: "Synthetic Agro Inputs Ltd", QuantityPerFarmer: npkKgPerFarmer, Unit: "Kg", UnitPrice: 1_120m,
                Basis: "perFarmer", Farmers: 30, Amount: npk, PackagingExcess: 0m, InLoan: true,
                LineId: Guid.Parse("00000000-0000-0000-0000-0000000000a1")),
            new(EopId: EopId, Crop: "Maize", Item: "Extension Services", Category: "Services",
                QuantityPerFarmer: 1m, Unit: "Per Farmer", UnitPrice: extensionRate,
                Basis: "perFarmer", Farmers: 30, Amount: extension, PackagingExcess: 0m, InLoan: true,
                LineId: Guid.Parse("00000000-0000-0000-0000-0000000000a3"), ProviderStatus: "ASSIGNED",
                ServiceProvider: new RhshfServiceProviderDto(
                    ProviderId: "73000000-0000-0000-0000-000000000001", ServiceType: "Extension Services",
                    RateBasis: "PerFarmer", RateVersion: rateVersion, RateId: "73000000-0000-0000-0000-0000000000a1",
                    ApprovedRate: extensionRate, Quantity: 30m,
                    AssignmentReference: assignmentReference, AssignmentStatus: "Assigned")),
        };
        var eop = new RhshfFinancingEopDto(
            EopId: EopId, Crop: "Maize", State: "Yobe", Farmers: 30, Inputs: inputs, PackagingExcess: 0m,
            Insurance: insurance, WorkingCapital: workingCapital, TechServicesFee: 120_000m,
            ApprovedTotal: total, LoanAmount: total, Revision: 1,
            SnapshotHashScheme: RhshfEopSnapshotHash.SchemeV1, SnapshotHashV2Scheme: RhshfEopSnapshotHash.SchemeV2);
        return (new RhshfFinancingDto(InputLines: lines, Eops: [eop]), eop);
    }

    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    [Fact]
    public void The_portals_canonical_text_hashes_to_its_posted_v2_hash()
        => Assert.Equal("b425e31b1e6b41f1c1aa982674fd73662b97c3fa7725c0dab4f640ea2b51a0b4", Sha(PortalCanonicalOriginal));

    [Fact]
    public void The_CRMS_writes_the_same_canonical_text_as_the_portal()
    {
        var (financing, eop) = Example();
        Assert.Equal(PortalCanonicalOriginal, RhshfEopSnapshotHash.CanonicalText(financing, eop, v2: true));
    }

    [Theory]
    [InlineData("original", "fe2eb6b8faf08560ae2d34aeb6e3f137e0da225a23cbc2b242d50b08537b7ce0", "b425e31b1e6b41f1c1aa982674fd73662b97c3fa7725c0dab4f640ea2b51a0b4")]
    [InlineData("npk3kg", "bbdb0b9b5387b6d4ad8536ad79087c5bfb58915c33db6735b005bf15ae9ff0dd", "1dae7805b651967dccbebd957daa9c1653547228915b5d29644699c20edb06d9")]
    [InlineData("rate9500", "64999e101ba32e65a9859d2847e04b3e7d8e18aac6760ee51ec0a29e28b6c8f4", "4dd58a826bf9536ab74190fda3458f92d3f536e5de0632e2ab33283d9b59464d")]
    public void Each_worked_case_gives_the_portals_v1_and_v2_hashes(string variant, string v1, string v2)
    {
        var (financing, eop) = variant switch
        {
            "npk3kg" => Example(npkKgPerFarmer: 3m),
            "rate9500" => Example(extensionRate: 9_500m),
            _ => Example(),
        };

        Assert.Equal(v1, RhshfEopSnapshotHash.ComputeV1(financing, eop));
        Assert.Equal(v2, RhshfEopSnapshotHash.ComputeV2(financing, eop));
    }

    [Fact]
    public void A_reassignment_or_new_rate_version_changes_v2_only_to_the_portals_hashes()
    {
        const string v1 = "fe2eb6b8faf08560ae2d34aeb6e3f137e0da225a23cbc2b242d50b08537b7ce0";
        var (reassigned, reassignedEop) = Example(assignmentReference: "73000000-0000-0000-0000-00000000c002");
        var (reversioned, reversionedEop) = Example(rateVersion: "3");

        Assert.Equal(v1, RhshfEopSnapshotHash.ComputeV1(reassigned, reassignedEop));
        Assert.Equal(v1, RhshfEopSnapshotHash.ComputeV1(reversioned, reversionedEop));
        Assert.Equal("2f4bfbca448b0e64ea441fd13242091b432d431ec4f744132a680efee0a584c1", RhshfEopSnapshotHash.ComputeV2(reassigned, reassignedEop));
        Assert.Equal("8d9652ba21b474118e05d0c10708d7e32e4b25af57467342f261e8b394e42a8a", RhshfEopSnapshotHash.ComputeV2(reversioned, reversionedEop));
    }

    [Fact]
    public void Check_verifies_v2_and_reports_either_hash_not_matching_as_a_mismatch()
    {
        var (financing, eop) = Example();
        var both = eop with
        {
            SnapshotHash = "fe2eb6b8faf08560ae2d34aeb6e3f137e0da225a23cbc2b242d50b08537b7ce0",
            SnapshotHashV2 = "b425e31b1e6b41f1c1aa982674fd73662b97c3fa7725c0dab4f640ea2b51a0b4",
        };

        Assert.Equal(RhshfSnapshotHashCheck.Verified, RhshfEopSnapshotHash.Check(financing, both));
        Assert.Equal(RhshfSnapshotHashCheck.Verified, RhshfEopSnapshotHash.Check(financing, both with { SnapshotHash = null }));
        Assert.Equal(RhshfSnapshotHashCheck.Mismatch, RhshfEopSnapshotHash.Check(financing, both with { SnapshotHashV2 = new string('0', 64) }));
        Assert.Equal(RhshfSnapshotHashCheck.Mismatch, RhshfEopSnapshotHash.Check(financing, both with { SnapshotHash = new string('0', 64) }));
    }
}
