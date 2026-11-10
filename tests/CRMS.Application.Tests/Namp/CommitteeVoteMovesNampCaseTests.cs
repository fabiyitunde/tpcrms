using CRMS.Application.Committee.Commands;
using CRMS.Application.Namp.Commands;
using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Namp;

/// <summary>
/// A NAMP committee round can be voted on from two pages: the NAMP case page (CastNampCommitteeVoteHandler)
/// and Committee &gt; My Votes, which lists NAMP reviews next to corporate ones and votes through the
/// shared CastVoteHandler. Whichever page casts the final vote, the case has to leave Committee
/// Circulation. A production case was stranded in November 2026 when the last vote came through
/// My Votes and nothing moved it.
/// </summary>
public class CommitteeVoteMovesNampCaseTests
{
    private static readonly Guid MemberA = Guid.NewGuid();
    private static readonly Guid MemberB = Guid.NewGuid();

    [Fact]
    public async Task FinalVote_ThroughSharedHandler_MovesNampCaseToRatification()
    {
        var (app, review) = CaseAtBranchCirculation();
        var handler = new CastVoteHandler(new FakeReviews(review), new FakeNampApps(app), new FakeUnitOfWork());

        var first = await handler.Handle(new CastVoteCommand(review.Id, MemberA, CommitteeVote.Approve, null));
        Assert.True(first.IsSuccess);
        Assert.Equal(NampApplicationStatus.BranchCommitteeCirculation, app.Status);

        var last = await handler.Handle(new CastVoteCommand(review.Id, MemberB, CommitteeVote.Approve, null));
        Assert.True(last.IsSuccess);
        Assert.Equal(CommitteeReviewStatus.VotingComplete, review.Status);
        Assert.Equal(NampApplicationStatus.Ratification, app.Status);
        Assert.Equal(MemberB, app.CommitteeDecisionByUserId);
    }

    [Fact]
    public async Task FinalVote_ThroughSharedHandler_RejectedMajority_DeclinesAtTheTier()
    {
        var (app, review) = CaseAtBranchCirculation();
        var handler = new CastVoteHandler(new FakeReviews(review), new FakeNampApps(app), new FakeUnitOfWork());

        await handler.Handle(new CastVoteCommand(review.Id, MemberA, CommitteeVote.Reject, "no"));
        var last = await handler.Handle(new CastVoteCommand(review.Id, MemberB, CommitteeVote.Reject, "no"));

        Assert.True(last.IsSuccess);
        Assert.Equal(NampApplicationStatus.BranchCommitteeDeclined, app.Status);
    }

    [Fact]
    public async Task FinalVote_ThroughNampHandler_StillMovesTheCase()
    {
        var (app, review) = CaseAtBranchCirculation();
        var handler = new CastNampCommitteeVoteHandler(new FakeReviews(review), new FakeNampApps(app), new FakeUnitOfWork());

        await handler.Handle(new CastNampCommitteeVoteCommand(review.Id, MemberA, "Approve", null));
        var last = await handler.Handle(new CastNampCommitteeVoteCommand(review.Id, MemberB, "Approve", null));

        Assert.True(last.IsSuccess);
        Assert.Equal(NampApplicationStatus.Ratification, app.Status);
    }

    [Fact]
    public async Task FinalVote_WhenTheCaseCannotBeFound_FailsLoudly()
    {
        var (_, review) = CaseAtBranchCirculation();
        var handler = new CastVoteHandler(new FakeReviews(review), new FakeNampApps(null), new FakeUnitOfWork());

        await handler.Handle(new CastVoteCommand(review.Id, MemberA, CommitteeVote.Approve, null));
        var last = await handler.Handle(new CastVoteCommand(review.Id, MemberB, CommitteeVote.Approve, null));

        // Better a visible error than a silently stranded case.
        Assert.False(last.IsSuccess);
        Assert.Contains("could not be found", last.Error);
    }

    [Fact]
    public async Task CorporateReview_IsLeftForTheCreditOfficerToConfirm()
    {
        var loanApplicationId = Guid.NewGuid();
        var review = CommitteeReview.Create(loanApplicationId, "CORP-2026-0001", CommitteeType.BranchCredit,
            Guid.NewGuid(), requiredVotes: 2, minimumApprovalVotes: 2).Value;
        review.AddMember(MemberA, "A", "CommitteeMember");
        review.AddMember(MemberB, "B", "CommitteeMember");
        review.StartVoting();
        var nampApps = new FakeNampApps(null);
        var handler = new CastVoteHandler(new FakeReviews(review), nampApps, new FakeUnitOfWork());

        await handler.Handle(new CastVoteCommand(review.Id, MemberA, CommitteeVote.Approve, null));
        var last = await handler.Handle(new CastVoteCommand(review.Id, MemberB, CommitteeVote.Approve, null));

        Assert.True(last.IsSuccess);
        Assert.Equal(CommitteeReviewStatus.VotingComplete, review.Status);
        Assert.False(nampApps.Looked, "a corporate review must not be looked up as a NAMP case");
    }

    // ── Fixtures ──────────────────────────────────────────────────────────────────────────────────

    private static (NampApplication App, CommitteeReview Review) CaseAtBranchCirculation()
    {
        var officer = Guid.NewGuid();
        var app = NampApplication.Create(
            applicationNumber: "NAMP-2026-VOTE0001",
            loanProductId: Guid.NewGuid(),
            applicationReference: "PORTAL-VOTE-1",
            applicantName: "Musa Danjuma",
            boaAccountNumber: "0123456789",
            applicantCategory: NampApplicantCategory.YouthAgripreneur,
            equipmentDescription: "Tractor",
            equipmentValue: 12_000_000m,
            branchId: Guid.NewGuid(),
            committeeTier: NampCommitteeTier.Branch,
            recalledByUserId: officer).Value;

        Assert.True(app.Submit(officer).IsSuccess);
        Assert.True(app.SubmitFinancialAppraisal(officer, isApproved: true, null).IsSuccess);
        Assert.True(app.ApproveRiskReview(officer, null).IsSuccess);

        // NAMP reviews carry no LoanApplicationId; the number is the only link back.
        var review = CommitteeReview.Create(Guid.Empty, app.ApplicationNumber, CommitteeType.BranchCredit,
            officer, requiredVotes: 2, minimumApprovalVotes: 2).Value;
        review.AddMember(MemberA, "A", "BranchCommitteeMember");
        review.AddMember(MemberB, "B", "BranchCommitteeMember");
        Assert.True(review.StartVoting().IsSuccess);
        Assert.True(app.CirculateToCommittee(review.Id, officer).IsSuccess);

        return (app, review);
    }

    private class FakeReviews : ICommitteeReviewRepository
    {
        private readonly CommitteeReview _review;
        public FakeReviews(CommitteeReview review) => _review = review;

        public Task<CommitteeReview?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(id == _review.Id ? _review : null);
        public void Update(CommitteeReview review) { }

        public Task<CommitteeReview?> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CommitteeReview?> GetActiveByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CommitteeReview>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CommitteeReview>> GetByStatusAsync(CommitteeReviewStatus status, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CommitteeReview>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CommitteeReview>> GetPendingVotesByUserIdAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CommitteeReview>> GetOverdueAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CommitteeReview>> GetByCommitteeTypeAsync(CommitteeType type, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CommitteeReview>> GetAllByApplicationNumberAsync(string applicationNumber, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(CommitteeReview review, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeNampApps : INampApplicationRepository
    {
        private readonly NampApplication? _app;
        public bool Looked { get; private set; }
        public FakeNampApps(NampApplication? app) => _app = app;

        public Task<NampApplication?> GetByCommitteeReviewIdAsync(Guid committeeReviewId, CancellationToken ct = default)
        {
            Looked = true;
            return Task.FromResult(_app?.CurrentCommitteeReviewId == committeeReviewId ? _app : null);
        }
        public void Update(NampApplication application) { }

        public Task<NampApplication?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NampApplication?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NampApplication?> GetByApplicationReferenceAsync(string applicationReference, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NampApplication?> GetByApplicationReferenceWithHistoryAsync(string applicationReference, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NampApplication?> GetByApplicationNumberWithHistoryAsync(string applicationNumber, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<NampApplication>> GetByApplicationNumbersAsync(IReadOnlyList<string> applicationNumbers, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<NampApplication>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<NampApplication>> GetByStatusAsync(NampApplicationStatus status, Guid? branchId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<NampApplication>> GetByStatusAndTierAsync(NampApplicationStatus status, NampCommitteeTier tier, Guid? branchId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<NampApplication>> GetByStatusScopedAsync(NampApplicationStatus status, NampCommitteeTier? tier, IReadOnlyList<Guid>? branchIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<NampApplication>> GetByBranchAsync(Guid branchId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<NampApplication>> GetByCommitteeMembershipAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<NampApplication>> GetByParticipationAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(NampApplication application, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddNampDocumentAsync(NampDocument document, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NampDocument?> GetNampDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddDirectorAsync(NampDirector director, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NampDirector?> GetDirectorByIdAsync(Guid directorId, CancellationToken ct = default) => throw new NotSupportedException();
        public void RemoveDirector(NampDirector director) => throw new NotSupportedException();
        public Task<NampFinancialAppraisalReport?> GetFinancialAppraisalReportAsync(Guid nampApplicationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddFinancialAppraisalReportAsync(NampFinancialAppraisalReport report, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddPreDeploymentChecklistItemsAsync(IEnumerable<NampPreDeploymentChecklistItem> items, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}
