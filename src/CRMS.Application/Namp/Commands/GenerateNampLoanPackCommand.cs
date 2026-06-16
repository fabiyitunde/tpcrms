using CRMS.Application.Common;
using CRMS.Application.Namp.DTOs;
using CRMS.Application.Namp.Interfaces;
using CRMS.Application.Namp.Queries;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Commands;

public record GenerateNampLoanPackCommand(
    Guid NampApplicationId,
    Guid GeneratedByUserId,
    string GeneratedByUserName,
    string BankName,
    string BranchName
) : IRequest<ApplicationResult<byte[]>>;

public class GenerateNampLoanPackHandler
    : IRequestHandler<GenerateNampLoanPackCommand, ApplicationResult<byte[]>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IBureauReportRepository _bureauRepo;
    private readonly ICommitteeReviewRepository _committeeRepo;
    private readonly IUserRepository _userRepo;
    private readonly INampLoanPackGenerator _generator;

    public GenerateNampLoanPackHandler(
        INampApplicationRepository repo,
        IBureauReportRepository bureauRepo,
        ICommitteeReviewRepository committeeRepo,
        IUserRepository userRepo,
        INampLoanPackGenerator generator)
    {
        _repo = repo;
        _bureauRepo = bureauRepo;
        _committeeRepo = committeeRepo;
        _userRepo = userRepo;
        _generator = generator;
    }

    public async Task<ApplicationResult<byte[]>> Handle(
        GenerateNampLoanPackCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null)
            return ApplicationResult<byte[]>.Failure("NAMP application not found.");

        var finReport  = await _repo.GetFinancialAppraisalReportAsync(request.NampApplicationId, ct);
        var bureauReports = await _bureauRepo.GetByNampApplicationIdAsync(request.NampApplicationId, ct);

        // Load committee review (most recent, if app has one)
        Domain.Aggregates.Committee.CommitteeReview? committeeReview = null;
        if (app.CurrentCommitteeReviewId.HasValue)
            committeeReview = await _committeeRepo.GetByIdAsync(app.CurrentCommitteeReviewId.Value, ct);

        // Resolve user names for status history
        var allUsers = await _userRepo.GetAllAsync(ct);
        var userNameLookup = allUsers.ToDictionary(u => u.Id, u => u.FullName);

        string ResolveName(Guid id) =>
            userNameLookup.TryGetValue(id, out var name) ? name : id.ToString()[..8] + "...";

        var statusHistory = app.StatusHistory
            .OrderBy(h => h.ChangedAt)
            .Select(h => new NampLoanPackStatusEntry(
                h.Status,
                h.ChangedAt,
                ResolveName(h.ChangedByUserId),
                h.Note))
            .ToList();

        var committeeMembers = committeeReview?.Members
            .OrderByDescending(m => m.IsChairperson)
            .ThenBy(m => m.UserName)
            .Select(m => new NampLoanPackMemberVote(
                m.UserName,
                m.Role,
                m.IsChairperson,
                m.Vote?.ToString(),
                m.VoteComment,
                m.VotedAt))
            .ToList() ?? new List<NampLoanPackMemberVote>();

        string? ratifiedByName = app.RatifiedByUserId.HasValue
            ? ResolveName(app.RatifiedByUserId.Value) : null;

        // Resolve the committee decision. NAMP records the outcome on the application (auto-transition
        // on the last vote), so committeeReview.FinalDecision is usually null — derive from the recorded
        // outcome and the majority-approval tally instead of relying on FinalDecision.
        string? committeeDecision = null;
        if (committeeReview is not null)
        {
            if (app.CommitteeDecisionAt.HasValue)
                committeeDecision = committeeReview.HasMajorityApproval ? "Approved" : "Declined";
            else if (committeeReview.FinalDecision.HasValue)
                committeeDecision = committeeReview.FinalDecision.Value.ToString();
            else if (committeeReview.Status == Domain.Enums.CommitteeReviewStatus.VotingComplete)
                committeeDecision = committeeReview.HasMajorityApproval
                    ? "Approved (awaiting confirmation)" : "Declined (awaiting confirmation)";
            // otherwise null → "Pending" (voting still in progress)
        }

        var dto = GetNampApplicationByIdHandler.MapToDto(app, finReport, bureauReports);

        var data = new NampLoanPackData(
            ApplicationNumber:    app.ApplicationNumber,
            ApplicationReference: app.ApplicationReference,
            Status:               app.Status.ToString(),
            CreatedAt:            app.CreatedAt,
            SubmittedAt:          app.SubmittedAt,
            RatifiedAt:           app.RatifiedAt,
            RatifiedByUserName:   ratifiedByName,
            GeneratedAt:          DateTime.UtcNow,
            GeneratedBy:          request.GeneratedByUserName,
            BankName:             request.BankName,
            BranchName:           request.BranchName,
            ApplicantName:        app.ApplicantName,
            BoaAccountNumber:     app.BoaAccountNumber,
            BoaAccountName:       app.BoaAccountName,
            ApplicantCategory:    app.ApplicantCategory.ToString(),
            ApplicantPhone:       app.ApplicantPhone,
            ApplicantEmail:       app.ApplicantEmail,
            Nin:                  app.Nin,
            Bvn:                  app.Bvn,
            DateOfBirth:          app.DateOfBirth,
            StateOfResidence:     app.StateOfResidence,
            LocalGovernmentArea:  app.LocalGovernmentArea,
            Occupation:           app.Occupation,
            EmployerName:         app.EmployerName,
            EmploymentStatus:     app.EmploymentStatus,
            YearsOfExperience:    app.YearsOfExperience,
            NumberOfDependants:   app.NumberOfDependants,
            EstimatedMonthlyIncome:  app.EstimatedMonthlyIncome,
            MonthlyLivingExpenses:   app.MonthlyLivingExpenses,
            EstimatedNetWorth:       app.EstimatedNetWorth,
            ExistingLoanObligations: app.ExistingLoanObligations,
            CompanyName:          app.CompanyName,
            RcNumber:             app.RcNumber,
            IndustrySector:       app.IndustrySector,
            CacStatus:            app.CacStatus,
            CacEntityType:        app.CacEntityType,
            CacRegistrationDate:  app.CacRegistrationDate,
            CacNatureOfBusiness:  app.CacNatureOfBusiness,
            CacShareCapital:      app.CacShareCapital,
            CacAddress:           app.CacAddress,
            CacFetchedAt:         app.CacFetchedAt,
            EquipmentDescription: app.EquipmentDescription,
            EquipmentValue:       app.EquipmentValue,
            LoanPurpose:          app.LoanPurpose,
            EquityPercent:        app.EquityPercent,
            EquityAmount:         app.EquityAmount,
            LoanAmount:           app.LoanAmount,
            RequestedTenorMonths: app.RequestedTenorMonths,
            ApprovedInterestRate: app.ApprovedInterestRate,
            CommitteeTier:        app.CommitteeTier.ToString(),
            CommitteeDecision:    committeeDecision,
            CommitteeApprovalVotes:       committeeReview?.ApprovalVotes ?? 0,
            CommitteeRejectionVotes:      committeeReview?.RejectionVotes ?? 0,
            CommitteeAbstainVotes:        committeeReview?.AbstainVotes ?? 0,
            CommitteeMinimumApprovalVotes: committeeReview?.MinimumApprovalVotes ?? 0,
            CommitteeRequiredVotes:       committeeReview?.RequiredVotes ?? 0,
            CommitteeDecisionAt:  app.CommitteeDecisionAt,
            CommitteeDecisionNote: app.CommitteeDecisionNote,
            CommitteeConditions:  committeeReview?.ApprovalConditions,
            CommitteeMembers:     committeeMembers,
            FinancialAppraisal:   dto.FinancialAppraisalReport,
            Directors:            dto.Directors,
            Guarantors:           dto.Guarantors,
            Collaterals:          dto.Collaterals,
            BureauReports:        dto.BureauReports,
            FinancialStatements:  dto.FinancialStatements,
            StatusHistory:        statusHistory,
            Documents:            dto.Documents
        );

        var bytes = await _generator.GenerateAsync(data, ct);
        return ApplicationResult<byte[]>.Success(bytes);
    }
}
