using System.Text;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Application.Rhshf.Queries;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Documents;
using Xunit;

namespace CRMS.Infrastructure.Tests.Rhshf;

/// <summary>
/// Renders the RH-SHF Loan Pack generator end-to-end — a fully-populated dossier (every section present)
/// and a sparse case (only the workspace, all optional sections empty), proving the layout holds and
/// stays null/empty-safe at any stage.
/// </summary>
public class RhshfLoanPackPdfTests
{
    private static bool IsPdf(byte[] bytes) =>
        bytes.Length > 1000 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-";

    private static RhshfCaseWorkspaceDto Workspace(bool withLines) => new(
        Reference: "RHSHF-2026-000777",
        SubmissionId: Guid.NewGuid(),
        Status: RhshfCaseStatus.UnderReview,
        InternalStage: RhshfInternalStage.CommitteeVoting,
        CurrentProfilingStage: null,
        CurrentCycleNumber: 1,
        CompanyName: "Green Harvest Aggregators Ltd",
        RcNumber: "RC1234567",
        Tin: "TIN-99887766",
        BoaAccountNumber: "0000000029",
        State: "Kaduna",
        Lga: "Chikun",
        TotalEopValue: 15_000_000m,
        Currency: "NGN",
        FarmerCount: 450,
        EopLines: withLines
            ? new List<RhshfEopLineDto>
            {
                new("Maize seed", 2_000m, 1_500m, 3_000_000m),
                new("NPK fertiliser", 10_000m, 1_200m, 12_000_000m),
            }
            : new List<RhshfEopLineDto>(),
        BureauCheckOutcome: RhshfBureauOutcome.Cleared,
        BureauTotalLoans: 3,
        BureauActiveLoans: 1,
        BureauDelinquentFacilities: 0,
        BureauTotalOutstanding: 500_000m,
        BureauTotalOverdue: 0m,
        BureauRawJson: null,
        SupportingDocuments: new List<RhshfSupportingDocumentDto>(),
        Appraisals: new List<RhshfAppraisalDto>(),
        RiskReviews: new List<RhshfRiskReviewDto>(),
        Ratifications: new List<RhshfRatificationDto>
        {
            new(1, Guid.NewGuid(), "Grace Approver", DateTime.UtcNow, RhshfRatificationOutcome.Ratified, 15_000_000m, "Approved in full."),
        },
        DecisionOutcome: RhshfDecisionOutcome.Approved,
        ApprovedAmount: 15_000_000m,
        DecidedAt: DateTime.UtcNow,
        DecidedBy: "Grace Approver",
        DecisionNotes: "Strong crop economics.",
        BranchResolutionNote: "Routed to Kaduna branch via BOA account.",
        ReceivedAt: DateTime.UtcNow.AddDays(-10),
        UpdatedAt: DateTime.UtcNow,
        CommitteeTier: "Regional",
        CommitteeTierBand: "NGN 10m–50m");

    [Fact]
    public async Task LoanPack_FullDossier_RendersValidPdf()
    {
        var report = new RhshfAppraisalReportDto(
            Id: Guid.NewGuid(), CycleNumber: 1, PreparedByUserId: Guid.NewGuid(), PreparedByName: "Chidi Officer",
            SavedAt: DateTime.UtcNow,
            OwnProductionCost: 2_000_000m, HarvestAndLogisticsCost: 500_000m, CycleMonths: 6, InterestRatePercent: 12.5m,
            AssumptionBasisNote: "Based on 2025 yields.",
            GrossRevenue: 22_000_000m, FinancedInputCost: 15_000_000m, OwnCashCosts: 2_500_000m, InterestCharge: 1_875_000m,
            TotalCost: 19_375_000m, AmountDueAtHarvest: 16_875_000m, CashAvailableForDebtService: 20_000_000m,
            GrossMargin: 2_625_000m, GrossMarginPercent: 11.9m, Dscr: 1.3m, NetReturnToFarmer: 2_625_000m,
            Irr: 28.4m, NetPresentValue: 1_200_000m,
            TotalHectares: 100m, BlendedYieldKgPerHectare: 2_200m, BlendedPricePerKg: 100m,
            BreakEvenYieldKgPerHectare: 1_800m, BreakEvenPricePerKg: 82m,
            YieldHeadroomPercent: 18m, PriceHeadroomPercent: 18m,
            DscrPass: true, GrossMarginPass: true, IrrPass: true, YieldHeadroomPass: true, PriceHeadroomPass: true,
            AllGatesPass: true, GatesPassed: 5,
            RepaymentCapacityRating: RhshfRepaymentCapacityRating.Strong,
            CreditOfficerRecommendation: RhshfCreditRecommendation.Pass,
            SummaryNotes: "Recommend proceed.", OverrideJustification: null);

        var appraisal = new RhshfFinancialAppraisalDto(
            FarmPlans: new List<RhshfFarmPlanDto>
            {
                new(Guid.NewGuid(), "Maize", 100m, 2_200m, 100m, 220_000m, 22_000_000m, Guid.NewGuid(), "Chidi Officer", DateTime.UtcNow),
            },
            Report: report,
            Thresholds: new RhshfAppraisalThresholdsDto(1.2m, 10m, 15m, 10m, 10m),
            TotalEopValue: 15_000_000m,
            Currency: "NGN");

        var directors = new RhshfDirectorsDto(
            CacStatus: "Active", CacEntityType: "Private Company", CacRegistrationDate: "2018-05-01",
            CacNatureOfBusiness: "Agriculture", CacShareCapital: 10_000_000m, CacAddress: "Kaduna",
            CacFetchedAt: DateTime.UtcNow,
            Directors: new List<RhshfDirectorDto>
            {
                new(Guid.NewGuid(), "Ada Director", "Managing Director", true, "Director", 6000, "Ordinary", 60m, true, true, true, null, null),
                new(Guid.NewGuid(), "Bola Director", "Director", false, "Director", 4000, "Ordinary", 40m, false, false, true, null, null),
            });

        var bureau = new List<RhshfBureauReportDto>
        {
            new(Guid.NewGuid(), "Green Harvest Aggregators Ltd", "RhshfCompany", null, "Completed", 720, "A",
                0, 1, 3, 500_000m, 0m, 0, false, 15, "Low", null, null, DateTime.UtcNow),
            new(Guid.NewGuid(), "Ada Director", "RhshfDirector", null, "Completed", 680, "B",
                1, 2, 4, 1_200_000m, 50_000m, 30, false, 22, "Low", null, null, DateTime.UtcNow),
        };

        var collateral = new List<RhshfCollateralDto>
        {
            new(Guid.NewGuid(), RhshfCollateralType.BankGuarantee, "BG-2026-01", DateTime.UtcNow, DateTime.UtcNow.AddYears(1),
                Guid.NewGuid(), "Chidi Officer", DateTime.UtcNow, "Clean BG.",
                "First Bank", 15_000_000m, true, null, null, null, null, null,
                RhshfCollateralPerfectionStatus.Perfected, new List<RhshfCollateralDocumentDto>()),
        };

        var guarantors = new List<RhshfGuarantorDto>
        {
            new(Guid.NewGuid(), RhshfGuarantorType.Individual, "Emeka Surety", true, null, "Business associate", null, null, null, 5_000_000m, null),
        };

        var committee = new RhshfCommitteeReviewDto(
            CycleNumber: 1, RequiredVotes: 3, MinimumApprovalVotes: 2, Tier: CommitteeType.RegionalCredit,
            BranchId: Guid.NewGuid(), FinalDecision: RhshfCommitteeDecision.Approved,
            Votes: new List<RhshfCommitteeVoteDto>
            {
                new(Guid.NewGuid(), "Member One", RhshfCommitteeVoteChoice.Approve, DateTime.UtcNow, "Solid case."),
                new(Guid.NewGuid(), "Member Two", RhshfCommitteeVoteChoice.Approve, DateTime.UtcNow, null),
            });

        var offer = new RhshfOfferDto(
            CycleNumber: 1, GeneratedAt: DateTime.UtcNow, OfferDocumentPath: "offer.pdf", KfsDocumentPath: "kfs.pdf",
            Status: RhshfOfferStatus.Accepted, ApprovedAmount: 15_000_000m, Currency: "NGN",
            InterestRatePercent: 12.5m, CycleMonths: 6, AmountDueAtHarvest: 16_875_000m,
            FacRespondedAt: DateTime.UtcNow, FacResponseNotes: "Accepted.",
            SignedDocuments: new List<RhshfOfferDocumentDto>
            {
                new(Guid.NewGuid(), "signed-offer.pdf", 2048, DateTime.UtcNow, RhshfOfferDocumentKind.SignedOfferLetter),
            });

        var disbursements = new List<RhshfDisbursementDto>
        {
            new(1, Guid.NewGuid(), "Ops Officer", DateTime.UtcNow, 15_000_000m, "1234567890", "AgriInputs Ltd",
                RhshfDisbursementStatus.Booked, "FIN-0001", null),
        };

        var advisory = new RhshfAdvisoryDto(
            Id: Guid.NewGuid(), RhshfCreditProfileId: Guid.NewGuid(), Status: "Completed",
            OverallScore: 82.5m, OverallRating: "Strong", Recommendation: "Approve",
            RecommendedAmount: 15_000_000m, ExecutiveSummary: "Viable input-financing case with strong buffers.",
            StrengthsAnalysis: "Good yields.", WeaknessesAnalysis: "Price volatility.", MitigatingFactors: "BG in place.",
            KeyRisks: "Weather, price.", HasCriticalRedFlags: false, ModelVersion: "v1",
            GeneratedAt: DateTime.UtcNow, ErrorMessage: null,
            RiskScores: new List<RhshfAdvisoryRiskScoreDto>(),
            RedFlags: new List<string>(),
            Conditions: new List<string> { "Perfect the bank guarantee before disbursement." },
            Covenants: new List<string>());

        var history = new List<RhshfStatusHistoryDto>
        {
            new(1, RhshfCaseStatus.UnderReview, RhshfInternalStage.Appraisal, null, "Appraisal proceeded", Guid.NewGuid(), "Chidi Officer", false, null, DateTime.UtcNow.AddDays(-3)),
            new(1, RhshfCaseStatus.UnderReview, RhshfInternalStage.CommitteeVoting, null, "Advanced to committee", null, "System", true, null, DateTime.UtcNow.AddDays(-1)),
        };

        var data = new RhshfLoanPackData(
            BankName: "Bank of Agriculture", GeneratedBy: "Chidi Officer", GeneratedAt: DateTime.UtcNow,
            Workspace: Workspace(withLines: true), Directors: directors, BureauReports: bureau,
            FinancialAppraisal: appraisal, Collaterals: collateral, Guarantors: guarantors,
            CommitteeReview: committee, Offer: offer, Disbursements: disbursements, Advisory: advisory,
            StatusHistory: history);

        var bytes = await new RhshfLoanPackPdfGenerator().GenerateAsync(data);

        Assert.True(IsPdf(bytes), "full loan pack should be a valid PDF");
    }

    [Fact]
    public async Task LoanPack_SparseCase_StillRenders()
    {
        var data = new RhshfLoanPackData(
            BankName: "Bank of Agriculture", GeneratedBy: "Risk Officer", GeneratedAt: DateTime.UtcNow,
            Workspace: Workspace(withLines: false), Directors: null,
            BureauReports: Array.Empty<RhshfBureauReportDto>(),
            FinancialAppraisal: null,
            Collaterals: Array.Empty<RhshfCollateralDto>(),
            Guarantors: Array.Empty<RhshfGuarantorDto>(),
            CommitteeReview: null, Offer: null,
            Disbursements: Array.Empty<RhshfDisbursementDto>(),
            Advisory: null,
            StatusHistory: Array.Empty<RhshfStatusHistoryDto>());

        var bytes = await new RhshfLoanPackPdfGenerator().GenerateAsync(data);

        Assert.True(IsPdf(bytes));
    }
}
