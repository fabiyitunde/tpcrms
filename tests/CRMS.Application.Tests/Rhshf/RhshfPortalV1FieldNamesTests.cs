using System.Text.Json;
using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.DTOs;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>
/// The portal's frozen v1 Slice B contract (CRMSQA#3, 29 Oct) names several fields differently from
/// the earlier assessment the CRMS first read, and sends rateVersion as a number. The JSON below is the
/// portal's own synthetic example, read with the same web defaults the API's model binder uses.
/// </summary>
public class RhshfPortalV1FieldNamesTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private const string V1Financing = """
    {
      "currency": "NGN",
      "inputLines": [
        {
          "eopId": "3cc02579-d786-40fb-b87d-17d6195c5847", "crop": "Maize", "item": "NPK Fertilizer",
          "category": "Fertilizer", "specificInput": "NPK 15:15:15", "supplier": "Synthetic Agro Inputs Ltd",
          "quantityPerFarmer": 2, "unit": "Kg", "unitPrice": 1120, "basis": "perFarmer", "farmers": 30,
          "amount": 67200, "packagingExcess": 44800, "inLoan": true,
          "lineId": "00000000-0000-0000-0000-0000000000a1",
          "supplierProductId": "5b000000-0000-0000-0000-000000000001",
          "supplierCatalogueReference": "SP-5B000000",
          "priceVersionId": "9e000000-0000-0000-0000-000000000001",
          "priceVersion": 3,
          "packSize": 50, "packSizeUnit": "Kg", "packUnit": "50 Kg Bag", "numberOfPacks": 2,
          "pricePerPack": 56000, "procurementQuantity": 100,
          "packagingAdjustmentQuantity": 40, "packagingAdjustment": 44800,
          "providerStatus": null, "serviceProvider": null
        },
        {
          "eopId": "3cc02579-d786-40fb-b87d-17d6195c5847", "crop": "Maize", "item": "Extension Services",
          "category": "Services", "specificInput": null, "supplier": null,
          "quantityPerFarmer": 1, "unit": "Per Farmer", "unitPrice": 9000, "basis": "perFarmer", "farmers": 30,
          "amount": 270000, "packagingExcess": 0, "inLoan": true,
          "lineId": "00000000-0000-0000-0000-0000000000a3",
          "providerStatus": "ASSIGNED",
          "serviceProvider": {
            "providerId": "73000000-0000-0000-0000-000000000001",
            "legalName": "Synthetic Agro Advisory Ltd",
            "registrationReference": "RC-SYN-073",
            "serviceType": "Extension Services",
            "rateId": "73000000-0000-0000-0000-0000000000a1",
            "rateVersion": 2,
            "rateBasis": "PerFarmer",
            "approvedRate": 9000,
            "quantity": 30,
            "assignmentReference": "73000000-0000-0000-0000-00000000c002",
            "assignmentStatus": "Assigned"
          }
        }
      ],
      "charges": [],
      "eops": [
        {
          "eopId": "3cc02579-d786-40fb-b87d-17d6195c5847", "crop": "Maize", "state": "Yobe", "farmers": 30,
          "inputs": 367200, "packagingExcess": 64800, "insurance": 14688.0, "workingCapital": 18360,
          "techServicesFee": 120000, "approvedTotal": 585048.0, "loanAmount": 585048.0,
          "eopReference": "EOP-3CC02579", "revision": 1,
          "boaApprovalDate": "2026-10-20T09:15:00Z",
          "snapshotHash": "52b1c95a65eef1da1ec292f85ebba7e9429beaff61f0f2d53727ed26d1136f05",
          "snapshotHashScheme": "rhshf-eop-snapshot-v1"
        }
      ]
    }
    """;

    private static RhshfFinancingDto Read(string json) => JsonSerializer.Deserialize<RhshfFinancingDto>(json, Web)!;

    [Fact]
    public void Pack_and_price_detail_is_read_from_the_v1_names()
    {
        var detail = SubmitConsolidatedEopHandler.MapFinancing(Read(V1Financing))!.InputLines[0].Detail!;

        Assert.Equal("Kg", detail.BaseUnit);
        Assert.Equal(50m, detail.PackSize);
        Assert.Equal("50 Kg Bag", detail.PackUnit);
        Assert.Equal(2m, detail.PackCount);
        Assert.Equal(56_000m, detail.PricePerPack);
        Assert.Equal(100m, detail.ProcurementQuantity);
        Assert.Equal(40m, detail.PackagingExcessQuantity);
        Assert.Equal("9e000000-0000-0000-0000-000000000001", detail.PriceVersionId);
        Assert.Equal(3, detail.PriceVersion);
    }

    [Fact]
    public void Service_provider_is_read_from_the_v1_names_and_a_numeric_rate_version_is_accepted()
    {
        var detail = SubmitConsolidatedEopHandler.MapFinancing(Read(V1Financing))!.InputLines[1].Detail!;

        Assert.Equal("Synthetic Agro Advisory Ltd", detail.ProviderName);
        Assert.Equal(9_000m, detail.ProviderRate);
        Assert.Equal("2", detail.ProviderRateVersion);
        Assert.Equal(30m, detail.ProviderBillableQuantity);
        Assert.Equal("73000000-0000-0000-0000-00000000c002", detail.ProviderAssignmentId);
        Assert.Equal("PerFarmer", detail.ProviderRateBasis);
    }

    [Fact]
    public void Eop_approval_date_is_read_from_boaApprovalDate()
    {
        var eop = SubmitConsolidatedEopHandler.MapFinancing(Read(V1Financing))!.Eops!.Single();

        Assert.Equal("EOP-3CC02579", eop.EopReference);
        Assert.Equal(new DateTime(2026, 10, 20, 9, 15, 0, DateTimeKind.Utc), eop.EopApprovedAt!.Value.ToUniversalTime());
        Assert.Equal(1, eop.EopRevision);
    }

    [Fact]
    public void The_earlier_names_are_still_read_when_v1_ones_are_absent()
    {
        const string earlier = """
        {
          "inputLines": [
            { "item": "Extension Services", "amount": 270000, "baseUnit": "Kg", "packCount": 6,
              "packagingExcessQuantity": 20,
              "serviceProvider": { "registeredName": "Kano Extension Co-op", "rate": 9000, "rateVersion": "v2",
                                   "billableQuantity": 30, "assignmentId": "a-1" } }
          ],
          "eops": [ { "eopId": "3cc02579-d786-40fb-b87d-17d6195c5847", "approvedAt": "2026-10-20T09:15:00Z" } ]
        }
        """;

        var mapped = SubmitConsolidatedEopHandler.MapFinancing(Read(earlier))!;
        var detail = mapped.InputLines[0].Detail!;

        Assert.Equal("Kg", detail.BaseUnit);
        Assert.Equal(6m, detail.PackCount);
        Assert.Equal(20m, detail.PackagingExcessQuantity);
        Assert.Equal("Kano Extension Co-op", detail.ProviderName);
        Assert.Equal(9_000m, detail.ProviderRate);
        Assert.Equal("v2", detail.ProviderRateVersion);
        Assert.Equal(30m, detail.ProviderBillableQuantity);
        Assert.Equal("a-1", detail.ProviderAssignmentId);
        Assert.NotNull(mapped.Eops!.Single().EopApprovedAt);
    }
}
