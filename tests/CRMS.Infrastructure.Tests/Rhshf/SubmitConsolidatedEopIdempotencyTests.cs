using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Domain.Aggregates.Location;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories.Rhshf;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRMS.Infrastructure.Tests.Rhshf;

/// <summary>
/// Regression test for a live-reproduced bug: resubmitting the same submissionId threw
/// DbUpdateConcurrencyException ("0 rows affected") instead of idempotently returning the existing
/// case, because the second RhshfIssuedToken appended to an already-tracked profile was mis-tracked
/// by EF as Modified rather than Added. Fixed in CRMSDbContext.SaveChangesAsync by checking the
/// database directly instead of inferring from ChangeTracker property-modification state. Needs a
/// real CRMSDbContext (not hand-rolled fakes) since the bug is EF change-tracking behavior, not
/// application logic.
/// </summary>
public class SubmitConsolidatedEopIdempotencyTests
{
    private static (CRMSDbContext Db, SubmitConsolidatedEopHandler Handler) MakeHarness()
    {
        var options = new DbContextOptionsBuilder<CRMSDbContext>()
            .UseInMemoryDatabase("RhshfSubmitIdempotency_" + Guid.NewGuid())
            .Options;
        var db = new CRMSDbContext(options);
        var handler = new SubmitConsolidatedEopHandler(
            new RhshfCreditProfileRepository(db), new FakeTokenService(), new FakeFineractDirectService(),
            new FakeLocationRepository(), db);
        return (db, handler);
    }

    private static SubmitConsolidatedEopRequest MakeRequest(Guid submissionId) => new(
        SubmissionId: submissionId,
        Programme: new RhshfProgrammeDto("RH-SHF-DRY-2026", "Renewed Hope"),
        Session: new RhshfSessionDto("2026-DRY", "Dry Season 2026"),
        Fac: new RhshfFacDto(
            FacId: Guid.NewGuid(), CompanyName: "Idempotency Test Farms Ltd", RcNumber: "RC7654321",
            Tin: "10293847-0001", BoaAccountNumber: "0099887766",
            Contact: new RhshfFacContactDto("test@example.com", "+2348099998888"), State: "Kano", Lga: "Bunkure"),
        TotalEopValue: 5_000_000.00m,
        Currency: "NGN",
        FarmerCount: 100,
        EopLines: [new RhshfEopLineDto("Hybrid Maize Seed", 2000, 2500.00m, 5_000_000.00m)],
        CallbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
        Metadata: null);

    [Fact]
    public async Task Submit_SameSubmissionId_Twice_ReturnsSameReference_DoesNotThrow()
    {
        var (db, handler) = MakeHarness();
        var submissionId = Guid.NewGuid();
        var request = MakeRequest(submissionId);

        var first = await handler.Handle(new SubmitConsolidatedEopCommand(request, "{}"));
        var second = await handler.Handle(new SubmitConsolidatedEopCommand(request, "{}"));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Data!.Reference, second.Data!.Reference);
        Assert.NotEqual(first.Data.Token, second.Data.Token); // fresh token issued each time, same case
    }

    [Fact]
    public async Task Submit_SameSubmissionId_ThreeTimes_EachRecordsANewIssuedToken()
    {
        var (db, handler) = MakeHarness();
        var submissionId = Guid.NewGuid();
        var request = MakeRequest(submissionId);

        await handler.Handle(new SubmitConsolidatedEopCommand(request, "{}"));
        await handler.Handle(new SubmitConsolidatedEopCommand(request, "{}"));
        await handler.Handle(new SubmitConsolidatedEopCommand(request, "{}"));

        var profile = await db.RhshfCreditProfiles.Include(p => p.IssuedTokens).SingleAsync();
        Assert.Equal(3, profile.IssuedTokens.Count);
    }

    private class FakeTokenService : IRhshfTokenService
    {
        private int _counter;
        public RhshfIssuedTokenResult IssueToken(Guid rhshfCreditProfileId, string reference, Guid facId, string programmeCode)
        {
            var jti = $"jti-{++_counter}";
            return new RhshfIssuedTokenResult($"token-{jti}", jti, DateTime.UtcNow.AddMinutes(20), $"https://crms.example.com/rhshf/profiling/{reference}?token=token-{jti}");
        }
        public RhshfTokenValidationResult? ValidateToken(string token) => throw new NotSupportedException();
    }

    private class FakeFineractDirectService : IFineractDirectService
    {
        public Task<Result<NampBoaAccountInfo>> GetNampBoaAccountAsync(string boaAccountNumber, CancellationToken ct = default)
            => Task.FromResult(Result.Failure<NampBoaAccountInfo>("not resolvable in test"));
        public Task<Result<FineractClientInfo>> GetClientByIdAsync(long clientId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<ProposedRepaymentSchedule>> CalculateRepaymentScheduleAsync(ScheduleCalculationRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<ClientAccountSummary>> GetClientAccountsAsync(long clientId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<FineractLoanDetail>> GetLoanDetailAsync(long loanId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<CustomerExposure>> GetCustomerExposureAsync(long clientId, string accountNumber, string customerName, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<IReadOnlyList<FineractLoanProduct>>> GetLoanProductsAsync(bool activeOnly = true, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<FineractBookingResult>> BookApprovedLoanAsync(FineractLoanBookingRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeLocationRepository : ILocationRepository
    {
        public Task<Location?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Location?> GetByIdWithChildrenAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Location?> GetByCodeAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Location>> GetByTypeAsync(LocationType type, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Location>> GetChildrenAsync(Guid parentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Location>> GetAllActiveAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Location>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetDescendantBranchIdsAsync(Guid locationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetAncestorIdsAsync(Guid locationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Location?> GetHierarchyTreeAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Location?> GetBranchByNameAsync(string name, CancellationToken ct = default) => Task.FromResult<Location?>(null);
        public Task AddAsync(Location location, CancellationToken ct = default) => throw new NotSupportedException();
        public void Update(Location location) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
