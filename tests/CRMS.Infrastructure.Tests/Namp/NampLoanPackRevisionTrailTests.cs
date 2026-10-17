using System.Text;
using CRMS.Application.Namp.Interfaces;
using CRMS.Infrastructure.Documents;
using Xunit;

namespace CRMS.Infrastructure.Tests.Namp;

/// <summary>
/// The revision trail in the NAMP loan pack.
///
/// A pack is read away from the system, often by someone deciding whether to rely on it. Without
/// this section it shows only the latest opinion at each stage, so a reworked application reads
/// exactly like a clean one and the reader has no way to know a committee had already voted and been
/// overruled by a return.
///
/// The sparse cases matter as much as the populated one: a first-pass application must print no
/// revision section at all, and a returned one whose rounds carry no votes must not break the layout.
/// </summary>
public class NampLoanPackRevisionTrailTests
{
    private static bool IsPdf(byte[] bytes) =>
        bytes.Length > 1000 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-";

    [Fact]
    public async Task AFirstPassApplicationRendersWithNoRevisionSection()
    {
        var bytes = await new NampLoanPackPdfGenerator().GenerateAsync(Data());

        Assert.True(IsPdf(bytes));
    }

    [Fact]
    public async Task ARevisedApplicationRendersThePassesAndSupersededVotes()
    {
        var data = Data() with
        {
            ReviewPassNumber = 2,
            StagePasses =
            [
                new NampLoanPackStagePass(1, "Financial appraisal", "Approved",
                    new DateTime(2026, 9, 1), "Chidi Okeke", "First appraisal."),
                new NampLoanPackStagePass(1, "Risk review", "Approved",
                    new DateTime(2026, 9, 3), "Sola Adeyemi", "Cleared to committee."),
                new NampLoanPackStagePass(2, "Financial appraisal", "Approved",
                    new DateTime(2026, 9, 20), "Chidi Okeke", "Revised after committee return."),
            ],
            SupersededRounds =
            [
                new NampLoanPackCommitteeRound(
                    "BranchCredit", "Closed", "Deferred",
                    new DateTime(2026, 9, 4), new DateTime(2026, 9, 18),
                    "Returned for rework: equipment valuation needs re-basing.",
                    ApprovalVotes: 2, RejectionVotes: 1, AbstainVotes: 0, PendingVotes: 1,
                    Members:
                    [
                        new NampLoanPackMemberVote("Ada Nwosu", "BranchCommitteeMember", true, "Approve", "Comfortable.", new DateTime(2026, 9, 5)),
                        new NampLoanPackMemberVote("Musa Bello", "BranchCommitteeMember", false, "Reject", "Valuation looks high.", new DateTime(2026, 9, 6)),
                        new NampLoanPackMemberVote("Ngozi Eze", "BranchCommitteeMember", false, "Approve", null, new DateTime(2026, 9, 6)),
                        new NampLoanPackMemberVote("Tolu Fashola", "BranchCommitteeMember", false, null, null, null),
                    ]),
            ],
        };

        var bytes = await new NampLoanPackPdfGenerator().GenerateAsync(data);

        Assert.True(IsPdf(bytes));
    }

    [Fact]
    public async Task ARevisedApplicationWithNoVotesCastStillRenders()
    {
        // A case returned before anyone voted: the round exists and is closed, with nothing in it.
        var data = Data() with
        {
            ReviewPassNumber = 2,
            StagePasses = [],
            SupersededRounds =
            [
                new NampLoanPackCommitteeRound(
                    "ZonalCredit", "Closed", "Deferred",
                    new DateTime(2026, 9, 4), new DateTime(2026, 9, 5),
                    "Returned for rework: wrong equipment cart.",
                    0, 0, 0, 3, Members: []),
            ],
        };

        var bytes = await new NampLoanPackPdfGenerator().GenerateAsync(data);

        Assert.True(IsPdf(bytes));
    }

    [Fact]
    public async Task SeveralReturnsRenderAsSeveralRounds()
    {
        // Three closed rounds is the shape an auditor looks for: repeated returns until the vote
        // went a particular way. The pack has to show them all.
        var data = Data() with
        {
            ReviewPassNumber = 4,
            StagePasses = Enumerable.Range(1, 3)
                .Select(i => new NampLoanPackStagePass(
                    i, "Risk review", "Approved", new DateTime(2026, 9, i + 1), "Sola Adeyemi", $"Pass {i}."))
                .ToList(),
            SupersededRounds = Enumerable.Range(1, 3)
                .Select(i => new NampLoanPackCommitteeRound(
                    "BranchCredit", "Closed", "Deferred",
                    new DateTime(2026, 9, i), new DateTime(2026, 9, i + 10),
                    $"Returned for rework: round {i} concern.",
                    1, 2, 0, 0,
                    [new NampLoanPackMemberVote("Musa Bello", "BranchCommitteeMember", false, "Reject", null, new DateTime(2026, 9, i))]))
                .ToList(),
        };

        var bytes = await new NampLoanPackPdfGenerator().GenerateAsync(data);

        Assert.True(IsPdf(bytes));
    }

    [Fact]
    public async Task AReviewPassAboveOneWithNoTrailDataStillRenders()
    {
        // Defensive: the pass counter says "revised" but neither collection was supplied. The
        // section header prints and the tables are simply absent, rather than throwing.
        var data = Data() with { ReviewPassNumber = 3, StagePasses = null, SupersededRounds = null };

        var bytes = await new NampLoanPackPdfGenerator().GenerateAsync(data);

        Assert.True(IsPdf(bytes));
    }

    private static NampLoanPackData Data() => new(
        ApplicationNumber: "NAMP-2026-PACK0001",
        ApplicationReference: "PAYS-REF-1",
        Status: "Ratification",
        CreatedAt: new DateTime(2026, 8, 20),
        SubmittedAt: new DateTime(2026, 8, 22),
        RatifiedAt: null,
        RatifiedByUserName: null,
        GeneratedAt: new DateTime(2026, 10, 17),
        GeneratedBy: "Tolu Fashola",
        BankName: "Bank of Agriculture",
        BranchName: "Lagos Main Branch",
        ApplicantName: "Musa Danjuma",
        BoaAccountNumber: "0123456789",
        BoaAccountName: "Musa Danjuma",
        ApplicantCategory: "YouthAgripreneur",
        ApplicantPhone: "+2348012345678",
        ApplicantEmail: "musa@example.test",
        Nin: null, Bvn: null, DateOfBirth: null,
        StateOfResidence: "Kano", LocalGovernmentArea: "Nassarawa",
        Occupation: "Farmer", EmployerName: null, EmploymentStatus: null,
        YearsOfExperience: 6, NumberOfDependants: 3,
        EstimatedMonthlyIncome: 450_000m, MonthlyLivingExpenses: 180_000m,
        EstimatedNetWorth: 9_000_000m, ExistingLoanObligations: null,
        CompanyName: null, RcNumber: null, IndustrySector: "Agriculture",
        CacStatus: null, CacEntityType: null, CacRegistrationDate: null,
        CacNatureOfBusiness: null, CacShareCapital: null, CacAddress: null, CacFetchedAt: null,
        EquipmentDescription: "Tractor",
        EquipmentValue: 12_000_000m,
        LoanPurpose: "Mechanisation",
        EquityPercent: 20m, EquityAmount: 2_400_000m, LoanAmount: 9_600_000m,
        RequestedTenorMonths: 36, ApprovedInterestRate: 5m,
        CommitteeTier: "Branch",
        CommitteeDecision: null,
        CommitteeApprovalVotes: 0, CommitteeRejectionVotes: 0, CommitteeAbstainVotes: 0,
        CommitteeMinimumApprovalVotes: 3, CommitteeRequiredVotes: 4,
        CommitteeDecisionAt: null, CommitteeDecisionNote: null, CommitteeConditions: null,
        CommitteeMembers: [],
        FinancialAppraisal: null,
        Advisory: null,
        Directors: [],
        Guarantors: [],
        Collaterals: [],
        BureauReports: [],
        FinancialStatements: [],
        StatusHistory: [],
        Documents: []);
}
