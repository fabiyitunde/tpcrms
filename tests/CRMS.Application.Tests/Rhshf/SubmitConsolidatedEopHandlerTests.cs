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
        var handler = new SubmitConsolidatedEopHandler(repo, new FakeTokenService(), new FakeFineractDirectService(), new FakeLocationRepository(), new FakeUnitOfWork());

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
        var handler = new SubmitConsolidatedEopHandler(repo, new FakeTokenService(), new FakeFineractDirectService(), new FakeLocationRepository(), new FakeUnitOfWork());
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
        var handler = new SubmitConsolidatedEopHandler(repo, new FakeTokenService(), new FakeFineractDirectService(), new FakeLocationRepository(), new FakeUnitOfWork());
        var request = ValidRequest(Guid.NewGuid()) with
        {
            Fac = ValidRequest(Guid.NewGuid()).Fac with { CompanyName = "" }
        };

        var result = await handler.Handle(new SubmitConsolidatedEopCommand(request, "{}"));

        Assert.False(result.IsSuccess);
        Assert.Empty(repo.Added);
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

        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>(
                Added.Where(x => x.InternalStage == stage && (branchId == null || x.ResolvedBranchId == branchId)).ToList());
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
