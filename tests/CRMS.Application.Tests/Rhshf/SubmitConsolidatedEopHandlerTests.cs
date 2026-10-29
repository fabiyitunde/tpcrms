using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Domain.Aggregates.Location;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

public class SubmitConsolidatedEopHandlerTests
{
    private static SubmitConsolidatedEopRequest ValidRequest(Guid submissionId) => new(
        SubmissionId: submissionId,
        Programme: new RhshfProgrammeDto("RH-SHF-DRY-2026", "Renewed Hope - Dry Season 2026"),
        Session: new RhshfSessionDto("2026-DRY", "Dry Season 2026"),
        Fac: new RhshfFacDto(
            FacId: Guid.NewGuid(),
            CompanyName: "Alliedsoft Limited",
            RcNumber: "RC123456",
            Tin: "01234567-0001",
            BoaAccountNumber: "0123456789",
            Contact: new RhshfFacContactDto("fac@company.com", "+2348012345678"),
            State: "Kano",
            Lga: "Nassarawa"),
        TotalEopValue: 51_500_000.00m,
        Currency: "NGN",
        FarmerCount: 1200,
        EopLines: null,
        CallbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
        Metadata: null);

    [Fact]
    public async Task Handle_HappyPath_ReturnsExpectedShape()
    {
        var repo = new FakeRepository();
        var handler = new SubmitConsolidatedEopHandler(repo, new FakeTokenService(), new FakeFineractDirectService(), new FakeLocationRepository(), new FakeUnitOfWork(), new FakeScheduleRepository());

        var result = await handler.Handle(new SubmitConsolidatedEopCommand(ValidRequest(Guid.NewGuid()), "{}"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.StartsWith("RHSHF-", result.Data!.Reference);
        Assert.Equal("fake-token", result.Data.Token);
        Assert.Contains(result.Data.Reference, result.Data.ProfilingUrl);
        Assert.Equal("PROFILING_PENDING", result.Data.Status);
        Assert.Single(repo.Added);
    }

    [Fact]
    public async Task Handle_ResubmittingSameSubmissionId_ReturnsSameCase_NotADuplicate()
    {
        var repo = new FakeRepository();
        var handler = new SubmitConsolidatedEopHandler(repo, new FakeTokenService(), new FakeFineractDirectService(), new FakeLocationRepository(), new FakeUnitOfWork(), new FakeScheduleRepository());
        var submissionId = Guid.NewGuid();
        var request = ValidRequest(submissionId);

        var first = await handler.Handle(new SubmitConsolidatedEopCommand(request, "{}"));
        var second = await handler.Handle(new SubmitConsolidatedEopCommand(request, "{}"));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Data!.Reference, second.Data!.Reference);
        Assert.Single(repo.Added); // never a duplicate case
    }

    [Fact]
    public async Task Handle_MissingCompanyName_ReturnsFailure()
    {
        var repo = new FakeRepository();
        var handler = new SubmitConsolidatedEopHandler(repo, new FakeTokenService(), new FakeFineractDirectService(), new FakeLocationRepository(), new FakeUnitOfWork(), new FakeScheduleRepository());
        var request = ValidRequest(Guid.NewGuid()) with
        {
            Fac = ValidRequest(Guid.NewGuid()).Fac with { CompanyName = "" }
        };

        var result = await handler.Handle(new SubmitConsolidatedEopCommand(request, "{}"));

        Assert.False(result.IsSuccess);
        Assert.Empty(repo.Added);
    }

    /// <summary>
    /// The portal's Slice B fields (CRMSQA#3 items 1 to 3), sent as JSON under the names the portal
    /// proposed in its assessment, are kept on the case. A name that drifts shows up here as null.
    /// </summary>
    [Fact]
    public async Task Handle_KeepsSliceBDetail_UnderThePortalsFieldNames()
    {
        var eopId = Guid.NewGuid();
        var json = System.Text.Json.Nodes.JsonNode.Parse(
            System.Text.Json.JsonSerializer.Serialize(ValidRequest(Guid.NewGuid()) with { TotalEopValue = 254_400m },
                System.Text.Json.JsonSerializerOptions.Web))!;
        json["financing"] = System.Text.Json.Nodes.JsonNode.Parse($$"""
        {
          "currency": "NGN",
          "inputLines": [
            { "eopId": "{{eopId}}", "crop": "Maize", "item": "NPK Fertilizer", "category": "Fertilizer",
              "supplier": "Agro Supplies Ltd", "quantityPerFarmer": 10, "unit": "Kg", "unitPrice": 448,
              "basis": "perFarmer", "farmers": 30, "amount": 134400, "packagingExcess": 0, "inLoan": true,
              "baseUnit": "Kg", "packSize": 50, "packUnit": "Bag", "packCount": 6, "pricePerPack": 22400,
              "procurementQuantity": 300, "packagingExcessQuantity": 0,
              "priceVersionId": "pv-123", "supplierProductId": "sp-9" },
            { "eopId": "{{eopId}}", "crop": "Maize", "item": "Extension Services", "category": "Services",
              "quantityPerFarmer": 1, "unit": "Per Farmer", "unitPrice": 4000, "basis": "perFarmer",
              "farmers": 30, "amount": 120000, "packagingExcess": 0, "inLoan": true,
              "serviceProvider": { "providerId": "p-1", "registeredName": "Kano Extension Co-op",
                "serviceType": "Extension", "rate": 4000, "rateBasis": "per farmer", "rateVersion": "rv-2",
                "billableQuantity": 30, "assignmentId": "as-9" } }
          ],
          "charges": [],
          "inputsTotal": 254400, "packagingExcessTotal": 0, "chargesTotal": 0,
          "approvedEopTotal": 254400, "loanAmount": 254400, "excludedFromLoan": 0,
          "eops": [
            { "eopId": "{{eopId}}", "crop": "Maize", "state": "Kano", "farmers": 30, "inputs": 254400,
              "packagingExcess": 0, "insurance": 0, "workingCapital": 0, "techServicesFee": 0,
              "approvedTotal": 254400, "loanAmount": 254400,
              "eopReference": "EOP-1A2B3C4D", "approvedAt": "2026-10-12T09:00:00Z", "revision": 2,
              "snapshotHash": "sha256:abc" }
          ]
        }
        """);
        var request = System.Text.Json.JsonSerializer.Deserialize<SubmitConsolidatedEopRequest>(json, System.Text.Json.JsonSerializerOptions.Web)!;
        var repo = new FakeRepository();
        var handler = new SubmitConsolidatedEopHandler(repo, new FakeTokenService(), new FakeFineractDirectService(), new FakeLocationRepository(), new FakeUnitOfWork(), new FakeScheduleRepository());

        var result = await handler.Handle(new SubmitConsolidatedEopCommand(request, json.ToJsonString()));

        Assert.True(result.IsSuccess, result.Error);
        var profile = Assert.Single(repo.Added);
        var npk = profile.FinancingLines.Single(l => l.Item == "NPK Fertilizer");
        Assert.Equal("6 Bags of 50 Kg at NGN 22,400.00 each", npk.PackDisplay("NGN"));
        Assert.Equal("pv-123", npk.PriceVersionId);
        Assert.Equal("sp-9", npk.SupplierProductId);
        Assert.Equal(300m, npk.ProcurementQuantity);

        var service = profile.FinancingLines.Single(l => l.Item == "Extension Services");
        Assert.Equal("Kano Extension Co-op", service.ResponsibleParty);
        Assert.Equal(4_000m, service.ProviderRate);
        Assert.Equal("rv-2", service.ProviderRateVersion);
        Assert.Equal("as-9", service.ProviderAssignmentId);
        Assert.Empty(profile.OfferDataGaps());

        var eop = Assert.Single(profile.FinancingEops);
        Assert.Equal("EOP-1A2B3C4D", eop.EopReference);
        Assert.Equal(2, eop.EopRevision);
        Assert.Equal("sha256:abc", eop.SnapshotHash);
        Assert.Equal(new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc), eop.EopApprovedAt?.ToUniversalTime());

        Assert.Equal("6 Bags of 50 Kg at NGN 22,400.00 each; price version PV-123",
            CRMS.Application.Rhshf.Services.RhshfOfferDocumentDataBuilder.LineDetail(npk, "NGN"));
        Assert.Equal("provider rate NGN 4,000.00 per farmer; rate version RV-2",
            CRMS.Application.Rhshf.Services.RhshfOfferDocumentDataBuilder.LineDetail(service, "NGN"));
    }

    // ── Farmer schedule (CRMSQA#4) ───────────────────────────────────────────────────────────────

    private static SubmitConsolidatedEopHandler Handler(FakeRepository repo, FakeScheduleRepository schedule)
        => new(repo, new FakeTokenService(), new FakeFineractDirectService(), new FakeLocationRepository(), new FakeUnitOfWork(), schedule);

    private static List<RhshfScheduleFarmerDto> Farmers(params (string? Bvn, string? Nuban)[] farmers)
        => farmers.Select((f, i) => new RhshfScheduleFarmerDto(null, $"F-{i}", f.Bvn, null, f.Nuban)).ToList();

    [Fact]
    public async Task Handle_StoresTheFarmerSchedule_DigitsOnly()
    {
        var repo = new FakeRepository();
        var schedule = new FakeScheduleRepository();
        var request = ValidRequest(Guid.NewGuid()) with { FarmerSchedule = Farmers(("222 1234 5678", null), (null, "0123-456-789")) };

        var result = await Handler(repo, schedule).Handle(new SubmitConsolidatedEopCommand(request, "{}"));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, schedule.Rows.Count);
        Assert.Equal("22212345678", schedule.Rows[0].Bvn);
        Assert.Equal("0123456789", schedule.Rows[1].Nuban);
        Assert.All(schedule.Rows, r => Assert.Equal(repo.Added.Single().Id, r.RhshfCreditProfileId));
    }

    [Theory]
    [InlineData("1234", null, "farmerSchedule[0] has a BVN that is not 11 digits.")]
    [InlineData(null, null, "farmerSchedule[0] has no BVN, NIN or account number.")]
    public async Task Handle_RefusesABadScheduleWhole_WithoutEchoingIdentifiers(string? bvn, string? nuban, string expected)
    {
        var repo = new FakeRepository();
        var schedule = new FakeScheduleRepository();
        var request = ValidRequest(Guid.NewGuid()) with { FarmerSchedule = Farmers((bvn, nuban), ("22212345678", null)) };

        var result = await Handler(repo, schedule).Handle(new SubmitConsolidatedEopCommand(request, "{}"));

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error);
        Assert.Empty(repo.Added);
        Assert.Empty(schedule.Rows);
    }

    [Fact]
    public async Task Handle_RefusesTheSameFarmerTwiceUnderOneEop()
    {
        var schedule = new FakeScheduleRepository();
        var request = ValidRequest(Guid.NewGuid()) with { FarmerSchedule = Farmers(("22212345678", null), ("22212345678", null)) };

        var result = await Handler(new FakeRepository(), schedule).Handle(new SubmitConsolidatedEopCommand(request, "{}"));

        Assert.Equal("farmerSchedule[1] repeats a farmer already listed under the same EOP.", result.Error);
    }

    [Fact]
    public async Task Handle_ARepeatAddsTheScheduleToACaseWithNone_AndNeverReplacesOne()
    {
        var repo = new FakeRepository();
        var schedule = new FakeScheduleRepository();
        var handler = Handler(repo, schedule);
        var submissionId = Guid.NewGuid();

        await handler.Handle(new SubmitConsolidatedEopCommand(ValidRequest(submissionId), "{}"));
        Assert.Empty(schedule.Rows);

        var late = ValidRequest(submissionId) with { FarmerSchedule = Farmers(("22212345678", null)) };
        Assert.True((await handler.Handle(new SubmitConsolidatedEopCommand(late, "{}"))).IsSuccess);
        Assert.Single(schedule.Rows);

        var again = ValidRequest(submissionId) with { FarmerSchedule = Farmers(("33312345678", null), ("44412345678", null)) };
        Assert.True((await handler.Handle(new SubmitConsolidatedEopCommand(again, "{}"))).IsSuccess);
        Assert.Equal("22212345678", Assert.Single(schedule.Rows).Bvn);
        Assert.Single(repo.Added);
    }

    private class FakeScheduleRepository : IRhshfScheduleFarmerRepository
    {
        public List<RhshfScheduleFarmer> Rows { get; } = [];

        public Task<int> CountForProfileAsync(Guid rhshfCreditProfileId, CancellationToken ct = default)
            => Task.FromResult(Rows.Count(r => r.RhshfCreditProfileId == rhshfCreditProfileId));

        public Task AddRangeAsync(IEnumerable<RhshfScheduleFarmer> farmers, CancellationToken ct = default)
        {
            Rows.AddRange(farmers);
            return Task.CompletedTask;
        }
    }

    private class FakeRepository : IRhshfCreditProfileRepository
    {
        public List<RhshfCreditProfile> Added { get; } = [];

        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Added.FirstOrDefault(x => x.Id == id));

        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default)
            => Task.FromResult(Added.FirstOrDefault(x => x.Reference == reference));

        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default)
            => Task.FromResult(Added.FirstOrDefault(x => x.SubmissionId == submissionId));

        public Task AddAsync(RhshfCreditProfile profile, CancellationToken ct = default)
        {
            Added.Add(profile);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RhshfCreditProfile>> GetAllForListAsync(Guid? branchId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>([]);
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>(
                Added.Where(x => x.InternalStage == stage && (branchId == null || x.ResolvedBranchId == branchId)).ToList());
        public Task<IReadOnlyList<RhshfReusableDocument>> GetReusableDocumentsForFacAsync(Guid facId, Guid excludeProfileId, RhshfDocumentCategory category, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeTokenService : IRhshfTokenService
    {
        public RhshfIssuedTokenResult IssueToken(Guid rhshfCreditProfileId, string reference, Guid facId, string programmeCode)
            => new("fake-token", Guid.NewGuid().ToString(), DateTime.UtcNow.AddMinutes(20), $"https://crms.example.com/rhshf/profiling/{reference}?token=fake-token");

        public RhshfTokenValidationResult? ValidateToken(string token) => null;
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }

    /// <summary>Only GetNampBoaAccountAsync/GetClientByIdAsync are exercised by this handler's
    /// (best-effort) branch resolution — everything else on this interface is unused here.</summary>
    private class FakeFineractDirectService : IFineractDirectService
    {
        public Task<Result<NampBoaAccountInfo>> GetNampBoaAccountAsync(string boaAccountNumber, CancellationToken ct = default)
            => Task.FromResult(Result.Failure<NampBoaAccountInfo>("not resolved in this test"));

        public Task<Result<FineractClientInfo>> GetClientByIdAsync(long clientId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<ProposedRepaymentSchedule>> CalculateRepaymentScheduleAsync(
            ScheduleCalculationRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<ClientAccountSummary>> GetClientAccountsAsync(long clientId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<FineractLoanDetail>> GetLoanDetailAsync(long loanId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<CustomerExposure>> GetCustomerExposureAsync(long clientId, string accountNumber, string customerName, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<IReadOnlyList<FineractLoanProduct>>> GetLoanProductsAsync(bool activeOnly = true, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<FineractBookingResult>> BookApprovedLoanAsync(FineractLoanBookingRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    /// <summary>Only GetBranchByNameAsync is exercised — everything else is unused here.</summary>
    private class FakeLocationRepository : ILocationRepository
    {
        public Task<CRMS.Domain.Aggregates.Location.Location?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CRMS.Domain.Aggregates.Location.Location?> GetByIdWithChildrenAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CRMS.Domain.Aggregates.Location.Location?> GetByCodeAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CRMS.Domain.Aggregates.Location.Location>> GetByTypeAsync(LocationType type, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CRMS.Domain.Aggregates.Location.Location>> GetChildrenAsync(Guid parentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CRMS.Domain.Aggregates.Location.Location>> GetAllActiveAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CRMS.Domain.Aggregates.Location.Location>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetDescendantBranchIdsAsync(Guid locationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetAncestorIdsAsync(Guid locationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CRMS.Domain.Aggregates.Location.Location?> GetHierarchyTreeAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CRMS.Domain.Aggregates.Location.Location?> GetBranchByNameAsync(string name, CancellationToken ct = default) => Task.FromResult<CRMS.Domain.Aggregates.Location.Location?>(null);
        public Task AddAsync(CRMS.Domain.Aggregates.Location.Location location, CancellationToken ct = default) => throw new NotSupportedException();
        public void Update(CRMS.Domain.Aggregates.Location.Location location) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
