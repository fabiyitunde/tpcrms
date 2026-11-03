using System.Text.Json;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Application.Rhshf.Services;
using CRMS.Domain.Enums;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>
/// The CRMS's copy of the portal's v1 EOP snapshot hash must give the portal's own answer for the
/// portal's own worked example (CRMSQA#3, 29 Oct, §5.4): the §3 fixture with its Urea line, expected
/// SHA-256 52b1c95a…6f05. The lines here are deliberately out of order and one number is written as
/// 14688.0, both of which the rules say must not change the hash.
/// </summary>
public class RhshfEopSnapshotHashTests
{
    private const string Expected = "52b1c95a65eef1da1ec292f85ebba7e9429beaff61f0f2d53727ed26d1136f05";

    private const string Fixture = """
    {
      "currency": "NGN",
      "inputLines": [
        {
          "eopId": "3cc02579-d786-40fb-b87d-17d6195c5847", "crop": "Maize", "item": "Extension Services",
          "category": "Services", "specificInput": null, "supplier": null,
          "quantityPerFarmer": 1, "unit": "Per Farmer", "unitPrice": 9000, "basis": "perFarmer", "farmers": 30,
          "amount": 270000, "packagingExcess": 0, "inLoan": true,
          "lineId": "00000000-0000-0000-0000-0000000000a3",
          "supplierProductId": null, "supplierCatalogueReference": null, "priceVersionId": null, "priceVersion": null,
          "packSize": null, "packSizeUnit": null, "packUnit": null, "numberOfPacks": null, "pricePerPack": null,
          "procurementQuantity": null, "packagingAdjustmentQuantity": null, "packagingAdjustment": null,
          "providerStatus": "ASSIGNED",
          "serviceProvider": {
            "providerId": "73000000-0000-0000-0000-000000000001", "legalName": "Synthetic Agro Advisory Ltd",
            "registrationReference": "RC-SYN-073", "serviceType": "Extension Services",
            "rateId": "73000000-0000-0000-0000-0000000000a1", "rateVersion": 2, "rateBasis": "PerFarmer",
            "approvedRate": 9000, "quantity": 30,
            "assignmentReference": "73000000-0000-0000-0000-00000000c002", "assignmentStatus": "Assigned"
          }
        },
        {
          "eopId": "3cc02579-d786-40fb-b87d-17d6195c5847", "crop": "Maize", "item": "Urea Fertilizer",
          "category": "Fertilizer", "specificInput": "Urea 46%", "supplier": "Synthetic Agro Inputs Ltd",
          "quantityPerFarmer": 1, "unit": "Kg", "unitPrice": 1000, "basis": "perFarmer", "farmers": 30,
          "amount": 30000, "packagingExcess": 20000, "inLoan": true,
          "lineId": "00000000-0000-0000-0000-0000000000a2",
          "supplierProductId": "5b000000-0000-0000-0000-000000000002", "priceVersionId": "9e000000-0000-0000-0000-000000000002",
          "packSize": 50, "packSizeUnit": "Kg", "packUnit": "50 Kg Bag", "numberOfPacks": 1, "pricePerPack": 50000,
          "procurementQuantity": 50, "packagingAdjustmentQuantity": 20, "packagingAdjustment": 20000,
          "providerStatus": null, "serviceProvider": null
        },
        {
          "eopId": "3cc02579-d786-40fb-b87d-17d6195c5847", "crop": "Maize", "item": "NPK Fertilizer",
          "category": "Fertilizer", "specificInput": "NPK 15:15:15", "supplier": "Synthetic Agro Inputs Ltd",
          "quantityPerFarmer": 2, "unit": "Kg", "unitPrice": 1120.00, "basis": "perFarmer", "farmers": 30,
          "amount": 67200, "packagingExcess": 44800, "inLoan": true,
          "lineId": "00000000-0000-0000-0000-0000000000a1",
          "supplierProductId": "5b000000-0000-0000-0000-000000000001", "priceVersionId": "9e000000-0000-0000-0000-000000000001",
          "priceVersion": 3, "packSize": 50, "packSizeUnit": "Kg", "packUnit": "50 Kg Bag", "numberOfPacks": 2,
          "pricePerPack": 56000, "procurementQuantity": 100, "packagingAdjustmentQuantity": 40, "packagingAdjustment": 44800,
          "providerStatus": null, "serviceProvider": null
        }
      ],
      "charges": [],
      "eops": [
        {
          "eopId": "3cc02579-d786-40fb-b87d-17d6195c5847", "crop": "Maize", "state": "Yobe", "farmers": 30,
          "inputs": 367200, "packagingExcess": 64800, "insurance": 14688.0, "workingCapital": 18360,
          "techServicesFee": 120000, "approvedTotal": 585048.0, "loanAmount": 585048.0,
          "eopReference": "EOP-3CC02579", "revision": 1, "boaApprovalDate": "2026-10-20T09:15:00Z",
          "snapshotHash": "52b1c95a65eef1da1ec292f85ebba7e9429beaff61f0f2d53727ed26d1136f05",
          "snapshotHashScheme": "rhshf-eop-snapshot-v1"
        }
      ]
    }
    """;

    private static RhshfFinancingDto Read(string json = Fixture)
        => JsonSerializer.Deserialize<RhshfFinancingDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void Gives_the_portals_hash_for_the_portals_worked_example()
    {
        var financing = Read();

        Assert.Equal(Expected, RhshfEopSnapshotHash.ComputeV1(financing, financing.Eops!.Single()));
        Assert.Equal(RhshfSnapshotHashCheck.Verified, RhshfEopSnapshotHash.Check(financing, financing.Eops!.Single()));
    }

    [Fact]
    public void A_repriced_line_no_longer_matches()
    {
        var financing = Read(Fixture.Replace("\"unitPrice\": 1000,", "\"unitPrice\": 1050,"));

        Assert.Equal(RhshfSnapshotHashCheck.Mismatch, RhshfEopSnapshotHash.Check(financing, financing.Eops!.Single()));
    }

    [Fact]
    public void A_different_provider_no_longer_matches()
    {
        var financing = Read(Fixture.Replace("\"providerId\": \"73000000-0000-0000-0000-000000000001\"",
            "\"providerId\": \"73000000-0000-0000-0000-000000000009\""));

        Assert.Equal(RhshfSnapshotHashCheck.Mismatch, RhshfEopSnapshotHash.Check(financing, financing.Eops!.Single()));
    }

    [Fact]
    public void Fields_outside_the_hash_do_not_change_it()
    {
        // The approval date, revision, provider name and rate id are not hashed under v1.
        var financing = Read(Fixture
            .Replace("\"boaApprovalDate\": \"2026-10-20T09:15:00Z\"", "\"boaApprovalDate\": \"2026-10-21T10:00:00Z\"")
            .Replace("\"legalName\": \"Synthetic Agro Advisory Ltd\"", "\"legalName\": \"Renamed Ltd\""));

        Assert.Equal(RhshfSnapshotHashCheck.Verified, RhshfEopSnapshotHash.Check(financing, financing.Eops!.Single()));
    }

    [Fact]
    public void No_hash_and_an_unknown_scheme_are_reported_as_such()
    {
        var none = Read(Fixture.Replace("\"snapshotHash\": \"52b1c95a65eef1da1ec292f85ebba7e9429beaff61f0f2d53727ed26d1136f05\",", ""));
        var unknown = Read(Fixture.Replace("rhshf-eop-snapshot-v1", "rhshf-eop-snapshot-v9"));

        Assert.Equal(RhshfSnapshotHashCheck.NotSent, RhshfEopSnapshotHash.Check(none, none.Eops!.Single()));
        Assert.Equal(RhshfSnapshotHashCheck.UnsupportedScheme, RhshfEopSnapshotHash.Check(unknown, unknown.Eops!.Single()));
    }

    [Theory]
    [InlineData("14688.0", "14688")]
    [InlineData("1120.00", "1120")]
    [InlineData("0.50", "0.5")]
    [InlineData("0", "0")]
    [InlineData("0.0000004", "0")]
    [InlineData("2.0000005", "2.000001")]
    public void Numbers_are_written_the_portals_way(string input, string expected)
        => Assert.Equal(expected, RhshfEopSnapshotHash.Num(decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture)));
}
