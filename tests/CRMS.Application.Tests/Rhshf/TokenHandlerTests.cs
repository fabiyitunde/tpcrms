using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

public class TokenHandlerTests
{
    private static RhshfCreditProfile MakeProfile()
    {
        var result = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(),
            programmeCode: "RH-SHF-DRY-2026",
            programmeName: "Renewed Hope - Dry Season 2026",
            sessionCode: "2026-DRY",
            sessionName: "Dry Season 2026",
            facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited",
            rcNumber: "RC123456",
            tin: "01234567-0001",
            boaAccountNumber: "0123456789",
            contactEmail: "fac@company.com",
            contactPhone: "+2348012345678",
            state: "Kano",
            lga: "Nassarawa",
            totalEopValue: 51_500_000.00m,
            currency: "NGN",
            farmerCount: 1200,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng",
            certifiedAt: DateTime.UtcNow,
            rawSubmissionPayload: "{}",
            eopLines: null,
            resolvedBranchId: null,
            resolvedOfficeId: null);
        return result.Value;
    }

    // ── Refresh ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_ExistingNonTerminalCase_IssuesFreshToken_WithoutChangingStatus()
    {
        var profile = MakeProfile();
        var repo = new FakeRepository(profile);
        var handler = new RefreshRhshfTokenHandler(repo, new FakeTokenService(), new FakeUnitOfWork());
        var statusBefore = profile.Status;

        var result = await handler.Handle(new RefreshRhshfTokenCommand(profile.Reference));

        Assert.True(result.IsSuccess);
        Assert.Equal("fake-token", result.Data!.Token);
        Assert.Equal(statusBefore, profile.Status);
        Assert.Single(profile.IssuedTokens);
    }

    [Fact]
    public async Task Refresh_UnknownReference_Fails()
    {
        var repo = new FakeRepository(null);
        var handler = new RefreshRhshfTokenHandler(repo, new FakeTokenService(), new FakeUnitOfWork());

        var result = await handler.Handle(new RefreshRhshfTokenCommand("RHSHF-2026-999999"));

        Assert.False(result.IsSuccess);
    }

    // Refresh-on-a-terminal-case is intentionally not covered here: nothing built through Phase 2
    // can drive a profile into a terminal status yet (Approve/Decline/Expire arrive in Phases 4-9).
    // RefreshRhshfTokenHandler's IsTerminal guard exists for when those methods land; testing it
    // now would require bypassing the aggregate's own encapsulation to force a fake terminal state.

    // ── Verify ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_ValidTokenForCorrectCase_ConsumesTokenAndSucceeds()
    {
        var profile = MakeProfile();
        var repo = new FakeRepository(profile);
        var tokenService = new FakeTokenService { ReferenceOverride = profile.Reference };
        var handler = new VerifyRhshfProfilingTokenHandler(repo, tokenService, new FakeUnitOfWork());
        profile.IssueToken(tokenService.NextJti, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(20));

        var result = await handler.Handle(new VerifyRhshfProfilingTokenCommand(profile.Reference, "any-token-string"));

        Assert.True(result.IsSuccess);
        Assert.True(profile.IssuedTokens.Single().Jti == tokenService.NextJti);
    }

    [Fact]
    public async Task Verify_TokenIssuedForDifferentCase_IsRejected()
    {
        var profile = MakeProfile();
        var repo = new FakeRepository(profile);
        var tokenService = new FakeTokenService { ReferenceOverride = "RHSHF-2026-OTHERCASE" };
        var handler = new VerifyRhshfProfilingTokenHandler(repo, tokenService, new FakeUnitOfWork());

        var result = await handler.Handle(new VerifyRhshfProfilingTokenCommand(profile.Reference, "any-token-string"));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Verify_TokenReusedAfterConsumption_IsRejected()
    {
        var profile = MakeProfile();
        var repo = new FakeRepository(profile);
        var tokenService = new FakeTokenService { ReferenceOverride = profile.Reference };
        var handler = new VerifyRhshfProfilingTokenHandler(repo, tokenService, new FakeUnitOfWork());
        profile.IssueToken(tokenService.NextJti, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(20));

        var first = await handler.Handle(new VerifyRhshfProfilingTokenCommand(profile.Reference, "any-token-string"));
        var second = await handler.Handle(new VerifyRhshfProfilingTokenCommand(profile.Reference, "any-token-string"));

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
    }

    [Fact]
    public async Task Verify_InvalidSignature_IsRejected()
    {
        var profile = MakeProfile();
        var repo = new FakeRepository(profile);
        var tokenService = new FakeTokenService { RejectAll = true };
        var handler = new VerifyRhshfProfilingTokenHandler(repo, tokenService, new FakeUnitOfWork());

        var result = await handler.Handle(new VerifyRhshfProfilingTokenCommand(profile.Reference, "garbage"));

        Assert.False(result.IsSuccess);
    }

    // ── Fakes ────────────────────────────────────────────────────────────────

    private class FakeRepository : IRhshfCreditProfileRepository
    {
        private readonly RhshfCreditProfile? _profile;
        public FakeRepository(RhshfCreditProfile? profile) => _profile = profile;

        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_profile?.Id == id ? _profile : null);

        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default)
            => Task.FromResult(_profile?.Reference == reference ? _profile : null);

        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default)
            => Task.FromResult(_profile?.SubmissionId == submissionId ? _profile : null);

        public Task AddAsync(RhshfCreditProfile profile, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>(
                _profile is not null && _profile.InternalStage == stage ? [_profile] : []);
    }

    private class FakeTokenService : IRhshfTokenService
    {
        public string NextJti { get; } = Guid.NewGuid().ToString();
        public string? ReferenceOverride { get; set; }
        public bool RejectAll { get; set; }

        public RhshfIssuedTokenResult IssueToken(Guid rhshfCreditProfileId, string reference, Guid facId, string programmeCode)
            => new("fake-token", NextJti, DateTime.UtcNow.AddMinutes(20), $"https://crms.example.com/rhshf/profiling/{reference}?token=fake-token");

        public RhshfTokenValidationResult? ValidateToken(string token)
            => RejectAll ? null : new RhshfTokenValidationResult(ReferenceOverride ?? "__use_repo_reference__", Guid.NewGuid(), "RH-SHF-DRY-2026", NextJti);
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}
